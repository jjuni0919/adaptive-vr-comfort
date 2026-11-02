using System;

namespace VRComfortStudy
{
    /// <summary>Clock driven explicitly by monotonic unscaled timestamps; no Unity dependency.</summary>
    public sealed class StudyClock
    {
        public double WallTime { get; private set; }
        public double ActiveTime { get; private set; }
        private double last;
        private bool started;

        public void Start(double now)
        {
            if (!Finite(now)) throw new ArgumentOutOfRangeException(nameof(now));
            WallTime = ActiveTime = 0;
            last = now;
            started = true;
        }

        public void Advance(double now, bool exposureActive)
        {
            if (!started) return;
            if (!Finite(now) || now < last) throw new ArgumentOutOfRangeException(nameof(now));
            double dt = now - last;
            WallTime += dt;
            if (exposureActive) ActiveTime += dt;
            last = now;
        }

        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
