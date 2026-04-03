using BenchmarkDotNet.Running;
using Muthur.Tools.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(TextAssemblyBenchmarks).Assembly).Run(args);
