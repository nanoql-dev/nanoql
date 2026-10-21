// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The replicating variance-swap engine's lowest put strike. The portfolio adds an end strike max(k − dk, 0) below the
/// lowest put strike k, and weighs the put at k by the log payoff's slope down to it
/// (<c>replicatingvarianceswapengine.hpp:94-142</c>): at or below the step dk the end strike is 0, whose log payoff is
/// infinite, and QuantLib's variance and NPV are NaN. nanoql refuses, "put strikes must be greater than dk"; a put
/// strike above the step prices as QuantLib's.
/// </summary>
public class ReplicatingVarianceSwapRefusalTests
{
    [Fact]
    public void APutStrikeAtOrBelowTheStepIsRefused()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.0, dc), new FlatForward(today, 0.05, dc), new BlackConstantVol(today, null, 0.2, dc));
        var swap = new VarianceSwap(PositionType.Long, 0.04, 50000, today, today + 90);
        double[] calls = [100.0, 105.0, 110.0, 115.0, 120.0, 125.0, 130.0, 135.0];

        // QuantLib's variance and NPV: NaN, at the lowest put strike equal to the step, and below it
        ArgumentException atStep = Assert.Throws<ArgumentException>(
            () => ReplicatingVarianceSwapEngine.Calculate(swap, context, process, 5.0, calls, [5.0, 10.0, 20.0, 50.0, 100.0]));
        Assert.StartsWith("put strikes must be greater than dk", atStep.Message);
        Assert.Throws<ArgumentException>(
            () => ReplicatingVarianceSwapEngine.Calculate(swap, context, process, 5.0, calls, [4.0, 50.0, 100.0]));

        // above the step, QuantLib's: 0.1302892934036491, NPV 4459.148576855683
        VarianceSwapResults above = ReplicatingVarianceSwapEngine.Calculate(swap, context, process, 5.0, calls, [5.5, 50.0, 100.0]);
        Assert.Equal(0.1302892934036491, above.Variance!.Value, 1e-14);
        Assert.Equal(4459.148576855683, above.NPV!.Value, 1e-8);
    }
}
