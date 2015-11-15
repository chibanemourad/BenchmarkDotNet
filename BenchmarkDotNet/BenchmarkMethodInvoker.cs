using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Statistic;
using BenchmarkDotNet.Tasks;

namespace BenchmarkDotNet
{
    public class BenchmarkMethodInvoker
    {
        private const long MinInvokeTimoutMilliseconds = 1000;
        private const long MaxInvokeTimoutMilliseconds = 10000;

        private class Measurement
        {
            public long OperationCount { get; }
            public long Ticks { get; }
            public double Nanoseconds { get; }
            public double Milliseconds { get; }
            public double Seconds { get; }
            public double NanosecondsPerOperation { get; }
            public double OperationsPerSecond { get; }

            public Measurement(long operationCount, long ticks)
            {
                OperationCount = operationCount;
                Ticks = Math.Max(ticks, 1);
                Nanoseconds = (Ticks / (double)Stopwatch.Frequency) * 1000000000;
                Milliseconds = Nanoseconds / 1000000;
                Seconds = Nanoseconds / 1000000000;
                NanosecondsPerOperation = Nanoseconds / OperationCount;
                OperationsPerSecond = OperationCount / Seconds;
            }

            public string GetDisplayValue()
            {
                return string.Format(EnvironmentHelper.MainCultureInfo,
                    "{0:0.####} ns/op, {1} op, {2:0.#} ms, {3:0.#} ns, {4} ticks, {5:0.#} op/s",
                    NanosecondsPerOperation, OperationCount, Milliseconds, Nanoseconds, Ticks, OperationsPerSecond);
            }
        }

        private void Throughput(BenchmarkTask task, Action initial, Func<string, long, long, Measurement> idleInvoke, Func<string, long, long, Measurement> targetInvoke)
        {
            initial();

            long invokeCount = 4;
            int preWarmupCounter = 0;
            bool unmeasurable = false;
            while (true)
            {
                BenchmarkState.Instance.IterationMode = BenchmarkIterationMode.PreWarmup;
                BenchmarkState.Instance.Iteration = preWarmupCounter++;
                var preIdle = idleInvoke("// Pre-Warmup (Idle)", invokeCount, 0);
                var preTarget = targetInvoke("// Pre-Warmup (Target)", invokeCount, 0);
                if (preTarget.Milliseconds > MinInvokeTimoutMilliseconds)
                {
                    if (preIdle.Milliseconds < 0.01 * preTarget.Milliseconds)
                        break;
                    if (preIdle.Milliseconds < 0.10 * preTarget.Milliseconds && preTarget.Milliseconds > MinInvokeTimoutMilliseconds)
                        break;
                    if ((preTarget.Nanoseconds - preIdle.Nanoseconds) / invokeCount < 0.5)
                    {
                        unmeasurable = true;
                        break;
                    }
                    if (preTarget.Milliseconds > MaxInvokeTimoutMilliseconds)
                    {
                        if (preIdle.Milliseconds > 0.10 * preTarget.Milliseconds)
                            unmeasurable = true;
                        break;
                    }
                }
                if (preTarget.Milliseconds < 1)
                    invokeCount *= MinInvokeTimoutMilliseconds;
                else if (preTarget.Milliseconds < MinInvokeTimoutMilliseconds)
                    invokeCount *= (long)Math.Ceiling(MinInvokeTimoutMilliseconds / preTarget.Milliseconds);
                else
                    invokeCount *= 2;
            }
            if (unmeasurable)
            {
                Console.WriteLine("!! Unmeasurable !!");
                return;
            }
            Console.WriteLine("// IterationCount = " + invokeCount);

            RunIterations(idleInvoke, 3, BenchmarkIterationMode.WarmupIdle, invokeCount);
            var targetIdle = RunIterations(idleInvoke, 3, BenchmarkIterationMode.TargetIdle, invokeCount);
            long idleTicks = targetIdle.Sum(r => r.Ticks) / targetIdle.Count;

            RunIterations(targetInvoke, task.Configuration.WarmupIterationCount, BenchmarkIterationMode.Warmup, invokeCount, idleTicks);
            RunIterations(targetInvoke, task.Configuration.TargetIterationCount, BenchmarkIterationMode.Target, invokeCount, idleTicks);
        }

        private List<Measurement> RunIterations(Func<string, long, long, Measurement> invoke, int iterationCount, BenchmarkIterationMode mode, long invokeCount, long idleTicks = 0)
        {
            var measurements = new List<Measurement>();
            var name = (mode == BenchmarkIterationMode.Target ? string.Empty : "// ") + mode + " ";
            Func<int, bool> isEnough = iteration => iteration >= iterationCount;
            if (iterationCount < 0)
            {
                if (mode.IsWarmup())
                    isEnough = iteration =>
                            measurements.Count >= 3 &&
                            measurements.Last().Ticks >= measurements.Penult().Ticks;
                else
                    isEnough = iteration =>
                            measurements.Count >= 5 &&
                            StatSummary.AreSimilar(
                                new StatSummary(measurements.Select(m => m.NanosecondsPerOperation)),
                                new StatSummary(measurements.WithoutLast().Select(m => m.NanosecondsPerOperation)));
            }
            for (int i = 0; !isEnough(i); i++)
            {
                BenchmarkState.Instance.IterationMode = mode;
                BenchmarkState.Instance.Iteration = i;
                measurements.Add(invoke(name + (i + 1), invokeCount, idleTicks));
            }
            return measurements;
        }

        public void Throughput(BenchmarkTask task, long operationsPerInvoke, Action setupAction, Action targetAction, Action idleAction)
        {
            Throughput(task,
                () =>
                {
                    setupAction();
                    targetAction();
                    idleAction();
                },
                (name, invokeCount, idleTicks) => MultiInvoke(name, setupAction, idleAction, invokeCount, operationsPerInvoke, idleTicks),
                (name, invokeCount, idleTicks) => MultiInvoke(name, setupAction, targetAction, invokeCount, operationsPerInvoke, idleTicks));
        }

        public void Throughput<T>(BenchmarkTask task, long operationsPerInvoke, Action setupAction, Func<T> targetAction, Func<T> idleAction)
        {
            Throughput(task,
                () =>
                {
                    setupAction();
                    targetAction();
                    idleAction();
                },
                (name, invokeCount, idleTicks) => MultiInvoke(name, setupAction, idleAction, invokeCount, operationsPerInvoke, idleTicks),
                (name, invokeCount, idleTicks) => MultiInvoke(name, setupAction, targetAction, invokeCount, operationsPerInvoke, idleTicks));
        }


        public void SingleRun(BenchmarkTask task, long operationsPerInvoke, Action setupAction, Action targetAction, Action idleAction)
        {
            var warmupIterationCount = Math.Max(task.Configuration.WarmupIterationCount, 0);
            var targetIterationCount = Math.Max(task.Configuration.TargetIterationCount, 1);
            for (int i = 0; i < warmupIterationCount; i++)
            {
                BenchmarkState.Instance.IterationMode = BenchmarkIterationMode.Warmup;
                BenchmarkState.Instance.Iteration = i;
                MultiInvoke("// Warmup " + (i + 1), setupAction, targetAction, 1, operationsPerInvoke);
            }
            for (int i = 0; i < targetIterationCount; i++)
            {
                BenchmarkState.Instance.IterationMode = BenchmarkIterationMode.Target;
                BenchmarkState.Instance.Iteration = i;
                MultiInvoke("Target " + (i + 1), setupAction, targetAction, 1, operationsPerInvoke);
            }
        }

        private Measurement MultiInvoke(string name, Action setupAction, Action targetAction, long invocationCount, long operationsPerInvoke, long idleTicks = 0)
        {
            var totalOperations = invocationCount * operationsPerInvoke;
            setupAction();
            var stopwatch = new Stopwatch();
            if (invocationCount == 1)
            {
                stopwatch.Start();
                targetAction();
                stopwatch.Stop();
            }
            else if (invocationCount < int.MaxValue)
            {
                int intInvocationCount = (int)invocationCount;
                stopwatch.Start();
                for (int i = 0; i < intInvocationCount; i++)
                    targetAction();
                stopwatch.Stop();
            }
            else
            {
                stopwatch.Start();
                for (long i = 0; i < invocationCount; i++)
                    targetAction();
                stopwatch.Stop();
            }
            var measurement = new Measurement(totalOperations, stopwatch.ElapsedTicks - idleTicks);
            Console.WriteLine($"{name}: {measurement.GetDisplayValue()}");
            GcCollect();
            return measurement;
        }

        private object multiInvokeReturnHolder;

        private Measurement MultiInvoke<T>(string name, Action setupAction, Func<T> targetAction, long invocationCount, long operationsPerInvoke, long idleTicks = 0, T returnHolder = default(T))
        {
            var totalOperations = invocationCount * operationsPerInvoke;
            setupAction();
            var stopwatch = new Stopwatch();
            if (invocationCount == 1)
            {
                stopwatch.Start();
                returnHolder = targetAction();
                stopwatch.Stop();
            }
            else if (invocationCount < int.MaxValue)
            {
                int intInvocationCount = (int)invocationCount;
                stopwatch.Start();
                for (int i = 0; i < intInvocationCount; i++)
                    returnHolder = targetAction();
                stopwatch.Stop();
            }
            else
            {
                stopwatch.Start();
                for (long i = 0; i < invocationCount; i++)
                    returnHolder = targetAction();
                stopwatch.Stop();
            }
            multiInvokeReturnHolder = returnHolder;
            var measurement = new Measurement(totalOperations, stopwatch.ElapsedTicks - idleTicks);
            Console.WriteLine($"{name}: {measurement.GetDisplayValue()}");
            GcCollect();
            return measurement;
        }

        private static void GcCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}