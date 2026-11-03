// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The one-dimensional Monte Carlo loop allocates nothing per sample after its first batch:
/// <see cref="MonteCarloModel{TPath, TStats}.AddSamples"/> draws a path that is a view of the generator's buffer,
/// prices it and adds the price, its accumulator reserved once per batch. Measured on an Ornstein-Uhlenbeck path, plain
/// and antithetic: into an <see cref="IncrementalStatistics"/>, which keeps no sample, a batch allocates nothing (<see
/// cref="AllocationAssert"/>); into a <see cref="GeneralStatistics"/>, a batch allocates its reservation, one array of
/// the samples it then holds, and nothing per sample.
/// </summary>
public class McAllocationTests
{
    private const int First = 100;
    private const int Batch = 4000;

    [Fact]
    public void AddingSamplesAllocatesNothingPerSampleAfterTheFirstBatch()
    {
        foreach (bool antithetic in new[] { false, true })
        {
            AllocationAssert.AllocatesNothing(
                () =>
                {
                    MonteCarloModel<MonteCarloPath, IncrementalStatistics> model = Model(new IncrementalStatistics(), antithetic);
                    model.AddSamples(First);
                    return model;
                },
                model => model.AddSamples(Batch));

            // The batch's reservation is one array of First + Batch samples; up to five measurements, as
            // AllocationAssert takes, the runtime allocating on the thread beside the loop at times.
            long reservation = ArrayBytes(First + Batch);
            long[] allocated = new long[5];
            bool within = false;
            for (int i = 0; i < allocated.Length && !within; i++)
            {
                MonteCarloModel<MonteCarloPath, GeneralStatistics> model = Model(new GeneralStatistics(), antithetic);
                model.AddSamples(First);
                long before = GC.GetAllocatedBytesForCurrentThread();
                model.AddSamples(Batch);
                allocated[i] = GC.GetAllocatedBytesForCurrentThread() - before;
                within = allocated[i] == reservation;
                Assert.Equal(First + Batch, model.SampleAccumulator.Samples);
            }

            Assert.True(within, $"allocated {string.Join(", ", allocated)} bytes, the reservation {reservation}");
        }
    }

    private static long ArrayBytes(int length)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        var array = new (double Value, double Weight)[length];
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(array);
        return bytes;
    }

    private static MonteCarloModel<MonteCarloPath, TStats> Model<TStats>(TStats stats, bool antithetic)
        where TStats : IStatistics
    {
        var grid = new TimeGrid(1.0, 10);
        IPathGenerator<MonteCarloPath> generator = SingleVariate<PseudoRandom>.MakePathGenerator(
            new OrnsteinUhlenbeckProcess(0.1, 0.01, 0.03), grid, PseudoRandom.MakeSequenceGenerator(10, 42), false);
        return new MonteCarloModel<MonteCarloPath, TStats>(generator, new LastValuePricer(), stats, antithetic);
    }

    private sealed class LastValuePricer : PathPricer<MonteCarloPath>
    {
        public override double Evaluate(MonteCarloPath path) => path.Back();
    }
}
