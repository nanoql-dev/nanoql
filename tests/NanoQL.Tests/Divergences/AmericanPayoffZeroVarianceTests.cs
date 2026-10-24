// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The American digital payoffs below a variance of ε (the S-van plan's VR4 (ii), under R8). QuantLib divides by the
/// variance there: the at-hit value is NaN out of the money (<c>americanpayoffathit.cpp:87-102</c>), and the at-expiry
/// one takes the spot's side of the strike where the limit takes the forward's (<c>americanpayoffatexpiry.cpp:112-121</c>).
/// On a cash of 10, S 100, q 0, r 5% and one year, QuantLib gives at σ = 0: at the hit at 102, NaN; at expiry at 102, 0;
/// the knock-out at expiry at 102 (or 103), 9.512294245007139; and its own formula at σ = 1e-6 gives 9.80392156862745,
/// 9.512294245007139 and 0 [executed on the wheel: <c>oracle_c1.py</c>]. nanoql's underlying follows its forward:
/// paid at the hit when the forward path reaches the strike, at expiry if it did (knock-in) or didn't (knock-out).
/// QuantLib's check: its own formula at σ = 1e-6, which nanoql's engine there computes.
/// </summary>
public class AmericanPayoffZeroVarianceTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AtTheHitTheForwardPathIsPaidWhenItReachesTheStrike()
    {
        // QuantLib at σ = 0: NaN at 102 and 110 and for the put at 98
        Assert.Equal(9.80392156862745, Price(OptionType.Call, 102.0, false, true, 0.0).NPV!.Value, 1e-14);
        Assert.Equal(0.0, Price(OptionType.Call, 110.0, false, true, 0.0).NPV);
        Assert.Equal(0.0, Price(OptionType.Put, 98.0, false, true, 0.0).NPV);
        foreach (double strike in new[] { 101.0, 102.0, 104.0, 110.0 })
        {
            AssertLimit(Price(OptionType.Call, strike, false, true, 1e-6).NPV, Price(OptionType.Call, strike, false, true, 0.0).NPV);
        }
    }

    [Fact]
    public void AtExpiryTheForwardsSideOfTheStrikeCounts()
    {
        // QuantLib at σ = 0: 0, the spot's side
        Assert.Equal(9.512294245007139, Price(OptionType.Call, 102.0, true, true, 0.0).NPV!.Value, 1e-14);
        foreach (double strike in new[] { 101.0, 102.0, 104.0, 110.0 })
        {
            AssertLimit(Price(OptionType.Call, strike, true, true, 1e-6).NPV, Price(OptionType.Call, strike, true, true, 0.0).NPV);
        }
    }

    [Fact]
    public void TheKnockOutIsPaidWhereTheForwardNeverReachesTheStrike()
    {
        // QuantLib at σ = 0: 9.512294245007139 at 103, its formula at 1e-6 0 (the forward passes 103 at 0.59 years)
        Assert.Equal(0.0, Price(OptionType.Call, 103.0, true, false, 0.0).NPV);
        Assert.Equal(9.512294245007139, Price(OptionType.Call, 110.0, true, false, 0.0).NPV!.Value, 1e-14);
        foreach (double strike in new[] { 101.0, 103.0, 104.0, 110.0 })
        {
            AssertLimit(Price(OptionType.Call, strike, true, false, 1e-6).NPV, Price(OptionType.Call, strike, true, false, 0.0).NPV);
        }
    }

    [Fact]
    public void TheGreeksAtTheHitAreTheLimitsDerivatives()
    {
        // q 2%, r 5%: the forward reaches 102 at ln(1.02)/0.03 years; QuantLib's formula at 1e-6 against σ = 0
        foreach (double strike in new[] { 101.0, 102.0 })
        {
            OneAssetOptionResults limit = Price(OptionType.Call, strike, false, true, 0.0, 0.02);
            OneAssetOptionResults quantLib = Price(OptionType.Call, strike, false, true, 1e-6, 0.02);
            AssertLimit(quantLib.NPV, limit.NPV);
            AssertLimit(quantLib.Delta, limit.Delta);
            AssertLimit(quantLib.Gamma, limit.Gamma);
            AssertLimit(quantLib.Rho, limit.Rho);
        }
    }

    private static OneAssetOptionResults Price(
        OptionType type, double strike, bool atExpiry, bool knockIn, double vol, double q = 0.0)
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, q, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), vol, A365));
        var option = new VanillaOption(
            new CashOrNothingPayoff(type, strike, 10.0), new AmericanExercise(Today, Today + 365, atExpiry));
        return knockIn
            ? AnalyticDigitalAmericanEngine.Calculate(option, Context, process)
            : AnalyticDigitalAmericanKOEngine.Calculate(option, Context, process);
    }

    private static void AssertLimit(double? atTinyVol, double? atZeroVol) =>
        Assert.True(
            Math.Abs(atTinyVol!.Value - atZeroVol!.Value) <= 1e-6 * Math.Max(1.0, Math.Abs(atTinyVol.Value)),
            $"QuantLib's formula at 1e-6 {atTinyVol}, the limit {atZeroVol}");
}
