using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace VRComfortStudy
{
    public sealed class SessionLogger : MonoBehaviour
    {
        [SerializeField, Min(128)] private int maximumQueuedLines = 8192;
        private BlockingCollection<WriteRequest> queue;
        private Thread worker;
        private volatile string error;
        private bool accepting;
        public string OutputDirectory { get; private set; }
        public string Error => error ?? "";
        public bool HasFault => !string.IsNullOrEmpty(error);
        public bool IsOpen => accepting;
        private struct WriteRequest { public string file, text; public bool json; }

        public bool BeginTrial(TrialMetadata metadata)
        {
            if (worker != null && worker.IsAlive) return false;
            if (queue != null) { queue.Dispose(); queue = null; }
            error = null;
            try
            {
                OutputDirectory = Path.Combine(Application.persistentDataPath, "VRComfortStudy",
                    CsvFormat.SafeSegment(metadata.participant_id), CsvFormat.SafeSegment(metadata.trial_id));
                Directory.CreateDirectory(OutputDirectory);
                if (File.Exists(Path.Combine(OutputDirectory, "metadata.json")))
                    throw new IOException("Trial path already exists; refusing to overwrite logs.");
                File.WriteAllText(Path.Combine(OutputDirectory, "metadata.json"), JsonUtility.ToJson(metadata, true), new UTF8Encoding(false));
                queue = new BlockingCollection<WriteRequest>(Math.Max(128, maximumQueuedLines));
                accepting = true;
                string directory = OutputDirectory;
                worker = new Thread(() => WriteLoop(directory)) { IsBackground = true, Name = "VRComfortStudy.LogWriter" };
                worker.Start();
                Csv("motion.csv", "time_s", "active_time_s", "dt_s", "virtual_speed_m_s", "virtual_accel_m_s2",
                    "virtual_yaw_abs_deg_s", "head_angular_speed_deg_s", "head_x_m", "head_y_m", "head_z_m",
                    "head_qx", "head_qy", "head_qz", "head_qw", "tracking_valid", "sample_valid", "reason", "input_source");
                Csv("features.csv", "snapshot_id", "time_s", "active_time_s", "window_start_s", "window_end_s", "valid_ratio",
                    "window_ready", "invalid_reason", "virtual_speed_mean", "virtual_accel_rms", "virtual_yaw_abs_mean",
                    "virtual_yaw_abs_max", "head_angular_speed_mean", "head_angular_speed_std", "moving_time_ratio", "vignette_command_strength");
                Csv("ratings.csv", "rating_id", "snapshot_id", "request_time_s", "confirm_time_s", "rating_0_10",
                    "response_delay_s", "valid_for_model_eval", "reason", "scale_id");
                Csv("events.csv", "time_s", "active_time_s", "event_type", "detail");
                Csv("performance.csv", "time_s", "app_frame_ms", "measurement_source");
                return !HasFault;
            }
            catch (Exception ex) { error = ex.Message; accepting = false; return false; }
        }

        public void Csv(string file, params object[] fields)
        {
            Enqueue(new WriteRequest { file = file, text = CsvFormat.Row(fields) });
        }

        public void Event(double wall, double active, string kind, string detail)
            => Csv("events.csv", wall, active, kind, detail);

        public void Features(FeatureSnapshot f)
        {
            Csv("features.csv", f.snapshot_id, f.request_time_s, f.active_time_s, f.window_start_s, f.window_end_s,
                f.valid_ratio, f.window_ready, f.invalid_reason, f.virtual_speed_mean, f.virtual_accel_rms,
                f.virtual_yaw_abs_mean, f.virtual_yaw_abs_max, f.head_angular_speed_mean, f.head_angular_speed_std,
                f.moving_time_ratio, f.vignette_command_strength);
        }

        private void Enqueue(WriteRequest request)
        {
            if (!accepting || queue == null) return;
            try
            {
                if (!queue.TryAdd(request)) error = "Log queue overflow; trial must stop.";
            }
            catch (InvalidOperationException) { error = "Log queue closed unexpectedly."; }
        }

        public bool FinishTrial(TrialSummary summary)
        {
            if (!accepting) return false;
            summary.logger_error = Error;
            summary.log_complete = !HasFault;
            Enqueue(new WriteRequest { file = "summary.json", text = JsonUtility.ToJson(summary, true), json = true });
            accepting = false;
            queue.CompleteAdding();
            if (!worker.Join(5000)) error = "Log writer timeout; check output before using this trial.";
            return !HasFault;
        }

        private void WriteLoop(string directory)
        {
            var writers = new Dictionary<string, StreamWriter>();
            string summary = null;
            try
            {
                DateTime lastFlush = DateTime.UtcNow;
                while (!queue.IsCompleted)
                {
                    if (queue.TryTake(out WriteRequest request, 250))
                    {
                        if (request.json) summary = request.text;
                        else
                        {
                            if (!writers.TryGetValue(request.file, out StreamWriter writer))
                            {
                                writer = new StreamWriter(Path.Combine(directory, request.file), false, new UTF8Encoding(false));
                                writers.Add(request.file, writer);
                            }
                            writer.WriteLine(request.text);
                        }
                    }
                    if ((DateTime.UtcNow - lastFlush).TotalSeconds >= 1)
                    {
                        foreach (StreamWriter value in writers.Values) value.Flush();
                        lastFlush = DateTime.UtcNow;
                    }
                }
                foreach (StreamWriter writer in writers.Values) writer.Flush();
                // A final summary is a completion receipt. It is withheld if any write/queue error occurred.
                if (summary != null && !HasFault)
                    File.WriteAllText(Path.Combine(directory, "summary.json"), summary, new UTF8Encoding(false));
            }
            catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
            finally
            {
                foreach (StreamWriter writer in writers.Values)
                    try { writer.Dispose(); } catch (Exception ex) { error = ex.Message; }
                if (HasFault)
                    try { File.WriteAllText(Path.Combine(directory, "LOG_INCOMPLETE.txt"), Error); } catch { }
            }
        }

        private void OnDestroy()
        {
            if (!accepting) return;
            accepting = false;
            error = "Logger destroyed without normal trial finalization.";
            queue.CompleteAdding();
            worker.Join(2000);
        }
    }
}
