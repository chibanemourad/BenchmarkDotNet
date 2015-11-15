namespace BenchmarkDotNet
{
    public enum BenchmarkIterationMode
    {
        PreWarmup, WarmupIdle, TargetIdle, Warmup, Target
    }

    public static class BenchmarkIterationModeExtensions
    {
        public static bool IsWarmup(this BenchmarkIterationMode mode)
        {
            return
                mode == BenchmarkIterationMode.PreWarmup ||
                mode == BenchmarkIterationMode.WarmupIdle ||
                mode == BenchmarkIterationMode.Warmup;
        }

        public static bool IsTarget(this BenchmarkIterationMode mode)
        {
            return
                mode == BenchmarkIterationMode.TargetIdle ||
                mode == BenchmarkIterationMode.Target;
        }
    }
}