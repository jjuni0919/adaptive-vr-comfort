using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRComfortStudy
{
    [DefaultExecutionOrder(300)]
    [RequireComponent(typeof(SessionLogger), typeof(MotionFeatureCollector))]
    public sealed class ExperimentManager : MonoBehaviour
    {
        [Header("Trial identity")]
        [SerializeField] private string participantId = "DEBUG_P001";
        [SerializeField] private StudyInputSource inputSource = StudyInputSource.Simulator;
        [SerializeField] private bool developmentData = true;
        [Header("Timing")]
        [SerializeField, Min(10)] private float maximumExposureSeconds = 240;
        [SerializeField, Min(5)] private float ratingIntervalSeconds = 30;
        [SerializeField, Min(2)] private float maximumRatingDelaySeconds = 20;
        [Header("Links")]
        [SerializeField] private SessionLogger logger;
        [SerializeField] private MotionFeatureCollector features;
        [SerializeField] private VignetteAdapter vignette;
        [SerializeField] private RatingUI ratingUI;
        [SerializeField] private TargetManager targets;
        [Tooltip("Only artificial move/turn providers. NEVER Camera/TrackedPoseDriver/XR Origin.")]
        [SerializeField] private Behaviour[] movementProviders = new Behaviour[0];
        [Header("Manual prototype")]
        [SerializeField, Range(0, 1)] private float runningVignetteStrength = 0.25f;
        public StringEvent onStateChanged = new StringEvent();
        public StringEvent onStatus = new StringEvent();
        private readonly StudyClock clock = new StudyClock();
        private bool clockStarted, finishAfterRating, finalizing;
        private bool[] originalProviderStates;
        private FeatureSnapshot pending;
        private double nextRatingAt, trackingLostAt = -1;
        private int ratingIndex, accepted, skipped;
        private string trialId;
        public StudyState State { get; private set; } = StudyState.Ready;
        public StudyInputSource InputSource => inputSource;
        public double WallTime => clock.WallTime;
        public double ActiveTime => clock.ActiveTime;
        public SessionLogger Logger => logger;
        public VignetteAdapter Vignette => vignette;
        public RatingUI RatingPanel => ratingUI;
        public TargetManager Targets => targets;
        public FeatureSnapshot PendingRating => pending;
        public string LastStatus { get; private set; } = "Ready";

        private void Awake()
        {
            if (logger == null) logger = GetComponent<SessionLogger>();
            if (features == null) features = GetComponent<MotionFeatureCollector>();
            if (vignette == null) vignette = GetComponent<VignetteAdapter>();
            if (ratingUI == null) ratingUI = GetComponent<RatingUI>();
            originalProviderStates = new bool[movementProviders.Length];
            for (int i = 0; i < movementProviders.Length; i++)
                if (movementProviders[i] != null) originalProviderStates[i] = movementProviders[i].enabled;
            SetMovement(false);
        }

        public void SyncClock()
        {
            if (clockStarted) clock.Advance(Time.realtimeSinceStartupAsDouble, State == StudyState.Running);
        }

        private void Update()
        {
            SyncClock();
            if (State != StudyState.Running && State != StudyState.Rating) return;
            if (logger.HasFault) { Finish(StudyState.Faulted, "LOGGER_ERROR: " + logger.Error); return; }
            if (State == StudyState.Rating && pending != null && WallTime - pending.request_time_s >= maximumRatingDelaySeconds)
                SkipRating("RESPONSE_TIMEOUT");
        }

        private void LateUpdate()
        {
            SyncClock();
            if (State != StudyState.Running) return;
            if (inputSource == StudyInputSource.Headset && !features.TrackingValid)
            {
                if (trackingLostAt < 0) trackingLostAt = WallTime;
                if (WallTime - trackingLostAt >= 1) { Finish(StudyState.Faulted, "HEAD_TRACKING_LOST"); return; }
            }
            else trackingLostAt = -1;
            if (ActiveTime >= maximumExposureSeconds) RequestRatingInternal(true);
            else if (ActiveTime >= nextRatingAt) RequestRatingInternal(false);
        }

        [ContextMenu("Start Trial")]
        public void StartTrial()
        {
            if (State == StudyState.Running || State == StudyState.Rating || finalizing) return;
            string issue = "";
            if (logger == null || features == null || !features.ValidateSetup(out issue))
            {
                SetStatus(logger == null || features == null ? "Missing logger/features." : issue);
                return;
            }
            if (inputSource == StudyInputSource.Simulator && !developmentData)
            { SetStatus("Simulator data must be marked DEBUG."); return; }
            trialId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var metadata = new TrialMetadata {
                participant_id = participantId, trial_id = trialId, started_utc = DateTime.UtcNow.ToString("O"),
                input_source = inputSource.ToString(), data_purpose = developmentData ? "DEBUG" : "DEVELOPMENT",
                unity_version = Application.unityVersion, app_version = Application.version,
                scene_name = SceneManager.GetActiveScene().name, sample_hz = features.SampleHz,
                window_s = features.WindowSeconds, rating_interval_s = ratingIntervalSeconds,
                maximum_exposure_s = maximumExposureSeconds
            };
            if (!logger.BeginTrial(metadata)) { SetStatus("Cannot start log: " + logger.Error); return; }
            clock.Start(Time.realtimeSinceStartupAsDouble);
            clockStarted = true;
            accepted = skipped = ratingIndex = 0;
            pending = null;
            finishAfterRating = false;
            trackingLostAt = -1;
            nextRatingAt = ratingIntervalSeconds;
            ChangeState(StudyState.Running);
            features.Begin(this);
            if (ratingUI != null) ratingUI.Hide();
            if (targets != null) targets.Begin(this);
            if (vignette != null) vignette.SetTargetStrength(runningVignetteStrength);
            SetMovement(true);
            LogEvent("TRIAL_STARTED", trialId);
            SetStatus("Running: " + logger.OutputDirectory);
        }

        public void RequestRating() => RequestRatingInternal(false);

        private void RequestRatingInternal(bool isFinal)
        {
            if (State != StudyState.Running) return;
            SyncClock();
            string id = "rating_" + (++ratingIndex).ToString("D4");
            // Freeze features BEFORE showing UI, disabling locomotion, or changing vignette.
            pending = features.CaptureSnapshot(id);
            logger.Features(pending);
            finishAfterRating = isFinal;
            ChangeState(StudyState.Rating);
            SetMovement(false);
            if (vignette != null) vignette.Clear();
            if (ratingUI != null) ratingUI.Show(this, id);
            LogEvent("RATING_REQUESTED", id + (isFinal ? ":FINAL" : ""));
        }

        public void SubmitRating(string id, int score)
        {
            if (State != StudyState.Rating || pending == null || id != pending.snapshot_id || score < 0 || score > 10) return;
            SyncClock();
            double delay = WallTime - pending.request_time_s;
            bool valid = pending.window_ready && delay <= maximumRatingDelaySeconds;
            logger.Csv("ratings.csv", id, id, pending.request_time_s, WallTime, score, delay, valid,
                valid ? "" : !pending.window_ready ? pending.invalid_reason : "RESPONSE_DELAY",
                "MODIFIED_SELF_REPORT_0_10");
            accepted++;
            LogEvent("RATING_CONFIRMED", CsvFormat.Row(id, score));
            EndRating();
        }

        public void SkipRating(string reason)
        {
            if (State != StudyState.Rating || pending == null) return;
            SyncClock();
            WriteMissingRating(reason);
            EndRating();
        }

        private void WriteMissingRating(string reason)
        {
            logger.Csv("ratings.csv", pending.snapshot_id, pending.snapshot_id, pending.request_time_s,
                WallTime, null, WallTime - pending.request_time_s, false, reason, "MODIFIED_SELF_REPORT_0_10");
            skipped++;
            LogEvent("RATING_MISSING", reason);
        }

        private void EndRating()
        {
            pending = null;
            if (ratingUI != null) ratingUI.Hide();
            if (finishAfterRating) { Finish(StudyState.Completed, "MAXIMUM_EXPOSURE_REACHED"); return; }
            nextRatingAt = ActiveTime + ratingIntervalSeconds;
            ChangeState(StudyState.Running);
            features.ResetWindow("RESUME_AFTER_RATING");
            if (vignette != null) vignette.SetTargetStrength(runningVignetteStrength);
            SetMovement(true);
        }

        [ContextMenu("Stop Trial")]
        public void StopTrial() => Finish(StudyState.Stopped, "USER_OR_OPERATOR_STOP");

        public void ResetTrial()
        {
            if (State == StudyState.Running || State == StudyState.Rating) StopTrial();
            if (ratingUI != null) ratingUI.Hide();
            ChangeState(StudyState.Ready);
            SetMovement(false);
            if (vignette != null) vignette.Clear();
        }

        private void Finish(StudyState endState, string reason)
        {
            if ((State != StudyState.Running && State != StudyState.Rating) || finalizing) return;
            finalizing = true;
            SyncClock();
            if (pending != null) { WriteMissingRating("TRIAL_ENDED: " + reason); pending = null; }
            ChangeState(endState);
            SetMovement(false);
            if (ratingUI != null) ratingUI.Hide();
            if (vignette != null) vignette.Clear();
            if (targets != null) targets.End();
            LogEvent("TRIAL_ENDED", reason);
            bool saved = logger.FinishTrial(new TrialSummary {
                trial_id = trialId, state = endState.ToString(), end_reason = reason,
                ended_utc = DateTime.UtcNow.ToString("O"), active_time_s = ActiveTime, wall_time_s = WallTime,
                accepted_ratings = accepted, skipped_ratings = skipped,
                targets_selected = targets != null ? targets.SelectedCount : 0, debug_data = developmentData
            });
            if (!saved) ChangeState(StudyState.Faulted);
            SetStatus(saved ? reason + " — saved: " + logger.OutputDirectory : "LOG INCOMPLETE: " + logger.Error);
            finalizing = false;
        }

        public void SetManualVignetteStrength(float strength)
        {
            runningVignetteStrength = Mathf.Clamp01(strength);
            if (State == StudyState.Running && vignette != null) vignette.SetTargetStrength(runningVignetteStrength);
            LogEvent("MANUAL_VIGNETTE_COMMAND", runningVignetteStrength.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public void RequestLessVignette()
        {
            LogEvent("LESS_VIGNETTE_REQUESTED", "Preference event; not a zero sickness label.");
            SetManualVignetteStrength(Mathf.Max(0, runningVignetteStrength - 0.25f));
        }

        public void LogEvent(string kind, string detail)
        {
            if (logger != null && logger.IsOpen) logger.Event(WallTime, ActiveTime, kind, detail);
        }

        private void ChangeState(StudyState value) { State = value; onStateChanged.Invoke(value.ToString()); }
        private void SetStatus(string value) { LastStatus = value; onStatus.Invoke(value); }

        private void SetMovement(bool allow)
        {
            for (int i = 0; i < movementProviders.Length; i++)
            {
                Behaviour item = movementProviders[i];
                if (item == null || item == this || item is Camera ||
                    item.GetType().Name.IndexOf("TrackedPoseDriver", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                item.enabled = allow && originalProviderStates != null && i < originalProviderStates.Length && originalProviderStates[i];
            }
        }

        private void OnApplicationPause(bool paused) { if (paused) Finish(StudyState.Stopped, "APPLICATION_PAUSED"); }
        private void OnApplicationFocus(bool focused) { if (!focused) Finish(StudyState.Stopped, "APPLICATION_FOCUS_LOST"); }
        private void OnDisable() { Finish(StudyState.Stopped, "MANAGER_DISABLED_OR_PLAYMODE_ENDED"); }
        private void OnApplicationQuit() { Finish(StudyState.Stopped, "APPLICATION_QUIT"); }
    }
}
