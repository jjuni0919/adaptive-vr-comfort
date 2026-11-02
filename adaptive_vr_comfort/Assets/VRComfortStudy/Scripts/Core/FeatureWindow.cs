using System;
using System.Collections.Generic;

namespace VRComfortStudy
{
    /// <summary>Time-weighted statistics, clipped at the exact trailing-window boundary.</summary>
    public sealed class FeatureWindow
    {
        private readonly Queue<MotionInterval> intervals = new Queue<MotionInterval>();
        private readonly double seconds;
        private double lastEnd = double.NegativeInfinity;
        private double validSince;

        public FeatureWindow(double seconds)
        {
            if (!StudyClock.Finite(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            this.seconds = seconds;
        }

        public void Clear(double now)
        {
            intervals.Clear();
            lastEnd = double.NegativeInfinity;
            validSince = now;
        }

        public void Add(MotionInterval sample)
        {
            if (!StudyClock.Finite(sample.end_time_s) || !StudyClock.Finite(sample.duration_s) || sample.duration_s <= 0)
                throw new ArgumentOutOfRangeException(nameof(sample));
            double start = sample.end_time_s - sample.duration_s;
            if (start < lastEnd - 0.000001) throw new ArgumentException("Overlapping/out-of-order motion interval.");
            if (!StudyClock.Finite(sample.speed) || !StudyClock.Finite(sample.acceleration) ||
                !StudyClock.Finite(sample.yaw_abs_rate) || !StudyClock.Finite(sample.head_angular_speed))
                sample.valid = false;
            intervals.Enqueue(sample);
            lastEnd = sample.end_time_s;
            Prune(lastEnd);
        }

        private void Prune(double now)
        {
            while (intervals.Count > 0 && intervals.Peek().end_time_s <= now - seconds)
                intervals.Dequeue();
        }

        public FeatureSnapshot Capture(double now, double activeTime, double minimumValidRatio = 0.95)
        {
            Prune(now);
            var result = new FeatureSnapshot {
                request_time_s = now, active_time_s = activeTime,
                window_start_s = now - seconds, window_end_s = now
            };
            double speed = 0, accelSq = 0, yaw = 0, headMean = 0, headM2 = 0, moving = 0, duration = 0;
            foreach (MotionInterval item in intervals)
            {
                double weight = Math.Max(0, Math.Min(now, item.end_time_s) -
                    Math.Max(now - seconds, item.end_time_s - item.duration_s));
                if (!item.valid || weight <= 0) continue;
                duration += weight;
                speed += item.speed * weight;
                accelSq += item.acceleration * item.acceleration * weight;
                yaw += item.yaw_abs_rate * weight;
                result.virtual_yaw_abs_max = Math.Max(result.virtual_yaw_abs_max, item.yaw_abs_rate);
                // Weighted Welford avoids cancellation for nearly constant angular speeds.
                double headDelta = item.head_angular_speed - headMean;
                headMean += weight / duration * headDelta;
                headM2 += weight * headDelta * (item.head_angular_speed - headMean);
                if (item.moving) moving += weight;
            }
            result.valid_duration_s = duration;
            result.valid_ratio = Math.Min(1, duration / seconds);
            bool warm = now - validSince >= seconds - 0.000001;
            result.window_ready = warm && result.valid_ratio >= minimumValidRatio;
            result.invalid_reason = !warm ? "WARMUP_AFTER_START_OR_RESET" :
                !result.window_ready ? "INSUFFICIENT_VALID_MOTION" : "";
            if (duration > 0)
            {
                result.virtual_speed_mean = speed / duration;
                result.virtual_accel_rms = Math.Sqrt(accelSq / duration);
                result.virtual_yaw_abs_mean = yaw / duration;
                result.head_angular_speed_mean = headMean;
                result.head_angular_speed_std = Math.Sqrt(Math.Max(0, headM2 / duration));
                result.moving_time_ratio = moving / duration;
            }
            return result;
        }
    }
}
