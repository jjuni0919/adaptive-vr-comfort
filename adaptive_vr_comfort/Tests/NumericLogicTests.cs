using System;
using VRComfortStudy;

// Standalone executable tests for the production numeric classes. No fake Unity API/stubs.
internal static class NumericLogicTests
{
    private static int checks;
    private static void Equal(double actual, double expected, string name, double tolerance = 1e-8)
    {
        checks++;
        if (Math.Abs(actual - expected) > tolerance || double.IsNaN(actual))
            throw new Exception(name + ": expected " + expected + ", got " + actual);
    }
    private static void True(bool condition, string name)
    { checks++; if (!condition) throw new Exception(name); }

    private static MotionInterval Interval(double end, double dt, double speed = 2, double accel = 3,
        double yaw = 4, double head = 5, bool valid = true, bool moving = true)
        => new MotionInterval { end_time_s = end, duration_s = dt, speed = speed, acceleration = accel,
            yaw_abs_rate = yaw, head_angular_speed = head, valid = valid, moving = moving };

    public static int Main()
    {
        var clock = new StudyClock();
        clock.Start(100);
        clock.Advance(110, true);
        clock.Advance(117, false);
        clock.Advance(120, true);
        Equal(clock.ActiveTime, 13, "rating pause excluded");
        Equal(clock.WallTime, 20, "wall time includes pause");
        bool backwardsRejected = false;
        try { clock.Advance(119, true); } catch (ArgumentOutOfRangeException) { backwardsRejected = true; }
        True(backwardsRejected, "clock rejects backwards timestamps");
        clock.Start(200);
        Equal(clock.ActiveTime, 0, "new trial resets clock");

        var window = new FeatureWindow(10);
        window.Clear(0);
        for (int i = 1; i <= 200; i++) window.Add(Interval(i * 0.05, 0.05));
        FeatureSnapshot f = window.Capture(10, 10);
        True(f.window_ready, "full valid 10-second window");
        Equal(f.virtual_speed_mean, 2, "speed mean");
        Equal(f.virtual_accel_rms, 3, "acceleration RMS");
        Equal(f.virtual_yaw_abs_mean, 4, "yaw mean");
        Equal(f.virtual_yaw_abs_max, 4, "yaw max");
        Equal(f.head_angular_speed_std, 0, "constant head rate variance");
        Equal(f.moving_time_ratio, 1, "moving ratio");
        True(!window.Capture(12, 12).window_ready, "stale tail not counted as valid data");

        var boundary = new FeatureWindow(10);
        boundary.Clear(0);
        boundary.Add(Interval(6, 6, speed: 1, head: 0, moving: false));
        boundary.Add(Interval(12, 6, speed: 3, head: 10));
        f = boundary.Capture(12, 12);
        Equal(f.valid_duration_s, 10, "interval clipped at window boundary");
        Equal(f.virtual_speed_mean, 2.2, "time weighted mean");
        Equal(f.moving_time_ratio, 0.6, "time weighted moving ratio");
        Equal(f.head_angular_speed_std, Math.Sqrt(24), "population time weighted variance");

        var missing = new FeatureWindow(10);
        missing.Clear(0);
        missing.Add(Interval(5, 5));
        missing.Add(Interval(10, 5, valid: false));
        f = missing.Capture(10, 10);
        Equal(f.valid_ratio, 0.5, "invalid samples excluded");
        True(!f.window_ready, "missing window cannot become a valid label");
        missing.Clear(10);
        missing.Add(Interval(11, 1));
        True(!missing.Capture(11, 11).window_ready, "reset after pause requires warmup");

        bool overlapRejected = false;
        try { missing.Add(Interval(11.5, 1)); } catch (ArgumentException) { overlapRejected = true; }
        True(overlapRejected, "overlapping intervals rejected");
        var badNumber = new FeatureWindow(10);
        badNumber.Clear(0);
        badNumber.Add(Interval(10, 10, speed: double.NaN));
        True(!badNumber.Capture(10, 10).window_ready, "NaN is invalid, never a zero label");

        True(CsvFormat.Row("a,b", "x\"y", "a\nb", null, 1.25, true) ==
            "\"a,b\",\"x\"\"y\",\"a\nb\",,1.25,true", "CSV quoting/culture/missing");
        True(CsvFormat.Row(double.NaN, double.PositiveInfinity, 0) == ",,0", "non-finite values do not become zero");
        True(!CsvFormat.SafeSegment("../../P001").Contains("."), "identifier cannot escape output directory");
        True(CsvFormat.SafeSegment("") == "anonymous", "empty ID is safe");
        Console.WriteLine("PASS: " + checks + " assertions against production numeric classes.");
        return 0;
    }
}
