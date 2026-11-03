// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Bjerksund-Stensland's three Greeks it never sets. QuantLib's engine assigns its results from a local
/// <c>OneAssetOption::results</c> it never resets (<c>bjerksundstenslandengine.cpp:123</c>, <c>:157</c>, <c>:183</c>),
/// so the forward delta, elasticity and in-the-money probability it reports are uninitialized memory: denormals such as
/// 7.756808686434e-312, 7.745284637023e-312 and 2.1219957905e-314 for a one-year put at 100, varying by run
/// (QuantLib-Python 1.44). nanoql reports none, as every engine that doesn't compute one, in each of the engine's
/// branches.
/// </summary>
public class BjerksundStenslandUnsetGreeksTests
{
    [Theory]
    [InlineData(OptionType.Put, 100.0, 0.05, 365)] // the American branch
    [InlineData(OptionType.Call, 100.0, 0.0, 365)] // the European branch, q 0 and r 0
    [InlineData(OptionType.Put, 150.0, 0.05, 365)] // immediate exercise
    public void TheForwardDeltaElasticityAndInTheMoneyProbabilityAreNone(OptionType type, double strike, double r, int days)
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(today, 0.0, new Actual365Fixed()),
            new FlatForward(today, r, new Actual365Fixed()),
            new BlackConstantVol(today, new NullCalendar(), 0.25, new Actual365Fixed()));
        OneAssetOptionResults results = BjerksundStenslandApproximationEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(type, strike), new AmericanExercise(today, today + days)), context, process);
        Assert.NotNull(results.NPV);
        Assert.Null(results.DeltaForward);
        Assert.Null(results.Elasticity);
        Assert.Null(results.ItmCashProbability);
    }
}
