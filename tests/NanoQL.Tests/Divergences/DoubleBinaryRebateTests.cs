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
/// The double-barrier binary engine's rebate (the S-bar plan's BR5 (xlvi), its reach found by the S-bar code review's
/// B1-2). QuantLib's engine never reads <c>DoubleBarrierOption</c>'s rebate: a cash-or-nothing call, cash 10, strike
/// 100, knocked out on [80, 120], prices 1.574843057685 at rebates 0 and 5 at r 5%, q 1%, σ 20% over a year, and 0
/// already knocked out. The facts run on <c>DoubleBarrierRebateTests</c>' market (q 2%). nanoql prices it as the double
/// barrier engine does (<c>DoubleBarrierRebateTests</c>): a knock-out's paid at the first touch of either barrier, now
/// where one is already touched, R × (KIKO + KOKI) at unit cash; a knock-in's at expiry where neither is touched, R ×
/// the unit-cash knock-out at strike 0, and none once knocked in. A KIKO's or KOKI's, which no convention fixes (at the
/// knock-out's touch, or at expiry if the knock-in never comes), is refused. Checks: the survival route on QuantLib's
/// image series (the reviewer's r3a, 0.60784983008 per unit rebate on [80, 120] at 20% over a year), and the double
/// barrier engine's own rebate on the same option.
/// </summary>
public class DoubleBinaryRebateTests
{
    private static readonly Date Today = new(1, January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);
    private static readonly CashOrNothingPayoff Payoff = new(OptionType.Call, 100.0, 10.0);

    [Fact]
    public void AKnockOutsRebateIsPaidAtTheFirstTouch()
    {
        double perUnit = (Price(DoubleBarrierType.KnockOut, 5.0, 100.0) - Price(DoubleBarrierType.KnockOut, 0.0, 100.0)) / 5.0;

        // the survival route, and the double barrier engine's rebate on a vanilla knock-out
        Assert.Equal(0.60784983008, perUnit, 1e-10);
        Assert.Equal(VanillaRebatePart(DoubleBarrierType.KnockOut, OptionType.Call) / 5.0, perUnit, 1e-12);
    }

    [Fact]
    public void AKnockInsRebateIsPaidAtExpiryWhereNeitherBarrierIsTouched()
    {
        double rebatePart = Price(DoubleBarrierType.KnockIn, 5.0, 100.0) - Price(DoubleBarrierType.KnockIn, 0.0, 100.0);
        var noTouch = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0), AYear);
        Assert.Equal(5.0 * AnalyticDoubleBarrierBinaryEngine.Calculate(noTouch, Context, ProcessAt(100.0)).NPV!.Value, rebatePart, 1e-12);
        Assert.Equal(VanillaRebatePart(DoubleBarrierType.KnockIn, OptionType.Put), rebatePart, 1e-12);
    }

    [Theory]
    [InlineData(80.0)]
    [InlineData(79.0)]
    [InlineData(120.0)]
    [InlineData(121.0)]
    public void AKnockOutAlreadyTouchedIsWorthItsRebateAndAKnockInNone(double spot)
    {
        Assert.Equal(5.0, Price(DoubleBarrierType.KnockOut, 5.0, spot));
        Assert.Equal(0.0, Price(DoubleBarrierType.KnockOut, 0.0, spot));
        Assert.Equal(Price(DoubleBarrierType.KnockIn, 0.0, spot), Price(DoubleBarrierType.KnockIn, 5.0, spot));
    }

    [Theory]
    [InlineData(DoubleBarrierType.KIKO)]
    [InlineData(DoubleBarrierType.KOKI)]
    public void AKikoOrKokiRebateIsRefused(DoubleBarrierType type)
    {
        var atHit = new AmericanExercise(Today, Today + 365);
        var rebated = new DoubleBarrierOption(type, 80.0, 120.0, 5.0, Payoff, atHit);
        var e = Assert.Throws<NanoQLException>(() => AnalyticDoubleBarrierBinaryEngine.Calculate(rebated, Context, ProcessAt(100.0)));
        Assert.Equal("rebate not supported for KIKO/KOKI options", e.Message);

        var bare = new DoubleBarrierOption(type, 80.0, 120.0, 0.0, Payoff, atHit);
        Assert.True(AnalyticDoubleBarrierBinaryEngine.Calculate(bare, Context, ProcessAt(100.0)).NPV > 0.0);
    }

    private static double Price(DoubleBarrierType type, double rebate, double spot) =>
        AnalyticDoubleBarrierBinaryEngine.Calculate(
            new DoubleBarrierOption(type, 80.0, 120.0, rebate, Payoff, AYear), Context, ProcessAt(spot)).NPV!.Value;

    // the double barrier engine's rebate part on a vanilla option struck at the binary's strike, its variance read there
    private static double VanillaRebatePart(DoubleBarrierType type, OptionType optionType)
    {
        double Vanilla(double rebate) => AnalyticDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(type, 80.0, 120.0, rebate, new PlainVanillaPayoff(optionType, 100.0), AYear),
            Context,
            ProcessAt(100.0)).NPV!.Value;
        return Vanilla(5.0) - Vanilla(0.0);
    }

    private static GeneralizedBlackScholesProcess ProcessAt(double spot) => new BlackScholesMertonProcess(
        spot,
        new FlatForward(Today, 0.02, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));
}
