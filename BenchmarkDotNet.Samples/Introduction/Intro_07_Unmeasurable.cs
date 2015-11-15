namespace BenchmarkDotNet.Samples.Introduction
{
    public class Intro_07_Unmeasurable
    {
        private int x;

        // We can't measure such operation, it's too fast
        [Benchmark]
        public void Inc()
        {
            x++;
        }
    }
}