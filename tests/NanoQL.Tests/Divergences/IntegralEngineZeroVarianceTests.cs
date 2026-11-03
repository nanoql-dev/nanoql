// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The integral engine at zero variance. QuantLib integrates the payoff over ±10√variance against a density scaled by
/// 1/√(2π·variance) (<c>integralengine.cpp:78-83</c>), both 0 or ∞ at zero variance, so its value is NaN
/// (QuantLib-Python 1.44). nanoql takes the limit, the discounted payoff at the forward. QuantLib's check: its analytic
/// European engine at the same zero volatility.
/// </summary>
public class IntegralEngineZeroVarianceTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(OptionType.Call, 100.0, 4.87705754992861)] // QuantLib's integral engine: NaN
    [InlineData(OptionType.Put, 110.0, 4.635236695078529)] // NaN
    public void AtZeroVarianceTheValueIsTheDiscountedPayoffAtTheForward(OptionType type, double strike, double analytic)
    {
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.0, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.0, A365));
        var option = new VanillaOption(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Today + 365));
        double value = IntegralEngine.Calculate(option, Context, process).NPV!.Value;
        Assert.True(Math.Abs(value - analytic) <= 1e-14 * analytic, $"{value} against QuantLib's analytic {analytic}");
    }
}
