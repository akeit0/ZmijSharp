using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(FormatBenchmarks).Assembly).Run(args);
