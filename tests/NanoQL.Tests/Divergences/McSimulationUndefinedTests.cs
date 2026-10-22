// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The tolerance loop of <see cref="McSimulation{TPath, TStats}.Value"/> refuses what QuantLib skips or leaves
/// undefined (mcsimulation.hpp:118-134): a NaN error estimate, which QuantLib's comparison with the tolerance skips,
/// returning the mean (the S-sr plan's SSD17, after playbook §2's NaN row for <c>std::max_element</c>); and a
/// next-batch estimate past <c>Size</c>'s range, which QuantLib converts to <c>Size</c>, undefined there (the
/// undefined-path row): a tolerance of 0 makes the estimate infinite.
/// </summary>
public class McSimulationUndefinedTests
{
    [Fact]
    public void ANaNErrorEstimateIsRefused()
    {
        // QuantLib: the loop skipped, the mean NaN returned.
        NanoQLException e = Assert.Throws<NanoQLException>(() => new Engine(double.NaN).Run(0.01));
        Assert.Equal("error estimate is NaN", e.Message);
    }

    [Fact]
    public void ANextBatchPastTheSamplesRangeIsRefused()
    {
        // QuantLib: Size(+inf), undefined.
        NanoQLException e = Assert.Throws<NanoQLException>(() => new Engine(1.0).Run(0.0));
        Assert.Equal("next batch's estimate (inf) out of range", e.Message);
    }

    // An engine on an Ornstein-Uhlenbeck process whose pricer adds a constant to the path's end.
    private sealed class Engine(double shift) : McSimulation<MonteCarloPath, GeneralStatistics>(false, false)
    {
        public void Run(double tolerance) => Calculate(tolerance, null, int.MaxValue);

        protected override NanoQL.TimeGrid TimeGrid() => new(1.0, 2);

        protected override IPathGenerator<MonteCarloPath> PathGenerator() =>
            SingleVariate<PseudoRandom>.MakePathGenerator(
                new OrnsteinUhlenbeckProcess(0.1, 0.2), TimeGrid(), PseudoRandom.MakeSequenceGenerator(2, 7), false);

        protected override PathPricer<MonteCarloPath> PathPricer() => new Shifted(shift);
    }

    private sealed class Shifted(double shift) : PathPricer<MonteCarloPath>
    {
        public override double Evaluate(MonteCarloPath path) => path.Back() + shift;
    }
}
