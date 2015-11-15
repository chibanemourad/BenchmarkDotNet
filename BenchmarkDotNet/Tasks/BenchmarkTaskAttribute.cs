using System;

namespace BenchmarkDotNet.Tasks
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class BenchmarkTaskAttribute : Attribute
    {
        public BenchmarkTask Task { get; protected set; }

        public BenchmarkTaskAttribute(
            int processCount = -1,
            BenchmarkMode mode = BenchmarkMode.Throughput,
            BenchmarkPlatform platform = BenchmarkPlatform.HostPlatform,
            BenchmarkJitVersion jitVersion = BenchmarkJitVersion.HostJit,
            BenchmarkFramework framework = BenchmarkFramework.HostFramework,
            BenchmarkToolchain toolchain = BenchmarkToolchain.Classic,
            BenchmarkRuntime runtime = BenchmarkRuntime.Clr,
            int warmupIterationCount = -1,
            int targetIterationCount = -1
            )
        {
            Task = new BenchmarkTask(
                processCount,
                new BenchmarkConfiguration(mode, platform, jitVersion, framework, toolchain, runtime, warmupIterationCount, targetIterationCount));
        }
    }
}