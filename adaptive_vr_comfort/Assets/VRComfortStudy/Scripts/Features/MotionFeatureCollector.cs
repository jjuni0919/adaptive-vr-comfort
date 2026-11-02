using UnityEngine;
using UnityEngine.XR;

namespace VRComfortStudy
{
    [DefaultExecutionOrder(200)]
    public sealed class MotionFeatureCollector : MonoBehaviour
    {
        [SerializeField] private ExperimentManager experiment;
        [Tooltip("XR Origin transform, not the tracked Main Camera.")]
        [SerializeField] private Transform artificialMotionRoot;
        [SerializeField] private Transform head;
        [SerializeField, Min(1)] private float sampleHz = 20;
        [SerializeField, Min(1)] private float windowSeconds = 10;
        [SerializeField, Range(0.1f, 1)] private float requiredValidRatio = 0.95f;
        [SerializeField, Min(0.05f)] private float maximumSampleGap = 0.25f;
        [SerializeField, Min(0.1f)] private float jumpDistanceMetres = 1;
        [SerializeField, Range(1, 180)] private float jumpAngleDegrees = 20;
        private FeatureWindow window;
        private Vector3 previousRootPosition, previousVelocity;
        private Quaternion previousRootRotation, previousHeadRotation;
        private double previousTime, nextSample, nextFeatures;
        private bool haveVelocity, previousTracking;
        private int snapshotIndex;
        public bool TrackingValid { get; private set; }
        public float SampleHz => sampleHz;
        public float WindowSeconds => windowSeconds;
        public Transform MotionRoot => artificialMotionRoot;
        public Transform Head => head;

        public bool ValidateSetup(out string reason)
        {
            reason = "";
            if (artificialMotionRoot == null || head == null) reason = "Assign XR Origin and Main Camera in MotionFeatureCollector.";
            else if (!head.IsChildOf(artificialMotionRoot)) reason = "Main Camera must be a child of the assigned XR Origin.";
            else if ((artificialMotionRoot.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
                reason = "XR Origin must have world scale (1,1,1) for metre-based logs.";
            return reason.Length == 0;
        }

        public void Begin(ExperimentManager owner)
        {
            experiment = owner;
            window = new FeatureWindow(windowSeconds);
            snapshotIndex = 0;
            nextFeatures = 1;
            ResetWindow("TRIAL_START", false);
        }

        public void ResetWindow(string reason, bool logEvent = true)
        {
            if (experiment == null || window == null || artificialMotionRoot == null || head == null) return;
            experiment.SyncClock();
            previousTime = experiment.WallTime;
            nextSample = previousTime + 1.0 / sampleHz;
            previousRootPosition = artificialMotionRoot.position;
            previousRootRotation = artificialMotionRoot.rotation;
            previousHeadRotation = Quaternion.Inverse(artificialMotionRoot.rotation) * head.rotation;
            previousVelocity = Vector3.zero;
            haveVelocity = false;
            TrackingValid = previousTracking = ReadTracking();
            window.Clear(previousTime);
            if (logEvent) experiment.LogEvent("FEATURE_WINDOW_RESET", reason);
        }

        public void MarkDiscontinuity() => ResetWindow("MANUAL_RECENTER_SNAP_OR_TELEPORT");
        public void MarkRecenter() => ResetWindow("RECENTER");
        public void MarkSnapTurn() => ResetWindow("SNAP_TURN");
        public void MarkTeleport() => ResetWindow("TELEPORT");

        private bool ReadTracking()
        {
            if (experiment.InputSource == StudyInputSource.Simulator) return true;
            InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        }

        private void LateUpdate()
        {
            if (experiment == null || experiment.State != StudyState.Running || window == null) return;
            experiment.SyncClock();
            double now = experiment.WallTime;
            experiment.Logger.Csv("performance.csv", now, Time.unscaledDeltaTime * 1000, "UNITY_UNSCALED_FRAME_DT");
            if (now >= nextSample) CollectNow();
            if (now >= nextFeatures)
            {
                var f = CaptureSnapshot("tick_" + (++snapshotIndex).ToString("D6"), false);
                experiment.Logger.Features(f);
                nextFeatures = now + 1;
            }
        }

        public void CollectNow()
        {
            if (experiment == null || experiment.State != StudyState.Running || window == null) return;
            experiment.SyncClock();
            double now = experiment.WallTime;
            double dt = now - previousTime;
            if (dt < 0.000001) return;
            Vector3 rootPosition = artificialMotionRoot.position;
            Quaternion rootRotation = artificialMotionRoot.rotation;
            Quaternion headRotation = Quaternion.Inverse(rootRotation) * head.rotation;
            Vector3 localHead = artificialMotionRoot.InverseTransformPoint(head.position);
            Vector3 delta = rootPosition - previousRootPosition;
            float rotationDelta = Quaternion.Angle(previousRootRotation, rootRotation);
            bool jump = delta.magnitude > jumpDistanceMetres || rotationDelta > jumpAngleDegrees;
            TrackingValid = ReadTracking();
            Vector3 velocity = new Vector3(delta.x, 0, delta.z) / (float)dt;
            double speed = velocity.magnitude;
            double acceleration = haveVelocity ? (velocity - previousVelocity).magnitude / dt : 0;
            double yaw = Mathf.Abs(Mathf.DeltaAngle(previousRootRotation.eulerAngles.y, rootRotation.eulerAngles.y)) / dt;
            double headRate = Quaternion.Angle(previousHeadRotation, headRotation) / dt;
            bool valid = TrackingValid && previousTracking && haveVelocity && dt <= maximumSampleGap && !jump;
            string reason = !TrackingValid || !previousTracking ? "TRACKING_INVALID" : jump ? "POSE_DISCONTINUITY" :
                dt > maximumSampleGap ? "SAMPLE_GAP" : !haveVelocity ? "DERIVATIVE_WARMUP" : "";
            var interval = new MotionInterval {
                end_time_s = now, duration_s = dt, speed = speed, acceleration = acceleration,
                yaw_abs_rate = yaw, head_angular_speed = headRate, valid = valid, moving = speed > 0.02 || yaw > 1
            };
            window.Add(interval);
            experiment.Logger.Csv("motion.csv", now, experiment.ActiveTime, dt, speed, acceleration, yaw, headRate,
                localHead.x, localHead.y, localHead.z, headRotation.x, headRotation.y, headRotation.z, headRotation.w,
                TrackingValid, valid, reason, experiment.InputSource);
            previousTime = now;
            previousRootPosition = rootPosition;
            previousRootRotation = rootRotation;
            previousHeadRotation = headRotation;
            previousVelocity = velocity;
            previousTracking = TrackingValid;
            haveVelocity = TrackingValid && dt <= maximumSampleGap && !jump;
            nextSample = now + 1.0 / sampleHz;
            if (jump) ResetWindow("AUTOMATIC_POSE_JUMP");
        }

        public FeatureSnapshot CaptureSnapshot(string id, bool collectFirst = true)
        {
            if (collectFirst) CollectNow();
            experiment.SyncClock();
            FeatureSnapshot f = window.Capture(experiment.WallTime, experiment.ActiveTime, requiredValidRatio);
            f.snapshot_id = id;
            f.vignette_command_strength = experiment.Vignette != null ? experiment.Vignette.CommandStrength : 0;
            return f;
        }
    }
}
