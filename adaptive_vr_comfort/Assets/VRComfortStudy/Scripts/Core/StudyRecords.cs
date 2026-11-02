using System;
using UnityEngine;
using UnityEngine.Events;

namespace VRComfortStudy
{
    public enum StudyState { Ready, Running, Rating, Completed, Stopped, Faulted }
    public enum StudyInputSource { Simulator, Headset }
    [Serializable] public sealed class StringEvent : UnityEvent<string> { }
    [Serializable] public sealed class FloatEvent : UnityEvent<float> { }
    [Serializable] public sealed class BoolEvent : UnityEvent<bool> { }

    [Serializable]
    public sealed class FeatureSnapshot
    {
        public string snapshot_id;
        public double request_time_s, active_time_s, window_start_s, window_end_s;
        public double valid_duration_s, valid_ratio;
        public bool window_ready;
        public string invalid_reason;
        public double virtual_speed_mean, virtual_accel_rms;
        public double virtual_yaw_abs_mean, virtual_yaw_abs_max;
        public double head_angular_speed_mean, head_angular_speed_std, moving_time_ratio;
        public float vignette_command_strength;
    }

    [Serializable]
    public sealed class TrialMetadata
    {
        public string schema_version = "steps5-10-v1";
        public string participant_id, trial_id, started_utc, input_source, data_purpose;
        public string unity_version, app_version, scene_name;
        public string condition = "MANUAL_PROTOTYPE";
        public string motion_source = "XR_ORIGIN_DELTA_WITH_DISCONTINUITY_MASK";
        public string rating_scale = "MODIFIED_SELF_REPORT_0_10";
        public string status = "OPEN";
        public double sample_hz, window_s, rating_interval_s, maximum_exposure_s;
    }

    [Serializable]
    public sealed class TrialSummary
    {
        public string trial_id, state, end_reason, ended_utc, logger_error;
        public double active_time_s, wall_time_s;
        public int accepted_ratings, skipped_ratings, targets_selected;
        public bool debug_data, log_complete;
    }

    // Pure numeric interval: one sample describes (end_time_s - duration_s, end_time_s].
    public struct MotionInterval
    {
        public double end_time_s, duration_s;
        public double speed, acceleration, yaw_abs_rate, head_angular_speed;
        public bool valid, moving;
    }
}
