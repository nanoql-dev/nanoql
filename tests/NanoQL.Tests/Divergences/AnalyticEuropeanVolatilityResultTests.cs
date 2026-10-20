// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="AnalyticEuropeanEngine"/>'s "volatility" additional result for an option expiring on the evaluation
/// date, priced when the reference date's events are included. QuantLib reports √(variance / time to expiry)
/// (<c>analyticeuropeanengine.cpp:113</c>), 0/0 there, NaN. nanoql reports the Black volatility at the exercise date
/// and strike, QuantLib's own value for that date; every other result is QuantLib's.
/// </summary>
public class AnalyticEuropeanVolatilityResultTests
{
    [Fact]
    public void TheVolatilityAtExpiryIsTheStructures()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today, IncludeReferenceDateEvents = true };
        var dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.02, dc), new FlatForward(today, 0.04, dc), new BlackConstantVol(today, null, 0.25, dc));
        var option = new EuropeanOption(new PlainVanillaPayoff(OptionType.Call, 95.0), new EuropeanExercise(today));

        OneAssetOptionResults results = AnalyticEuropeanEngine.Calculate(option, context, process);

        Assert.False(option.IsExpired(context));
        Assert.Equal(0.0, results.Result<double>("timeToExpiry"));
        Assert.Equal(0.25, results.Result<double>("volatility"));
        Assert.Equal(5.0, results.NPV);
    }
}
