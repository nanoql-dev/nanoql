// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The closed-form barrier and lookback engines' exactness on a volatility of time alone. Each engine reads the
/// variance and the discounts to expiry as constants. By the time change τ(t) = ∫σ², a market whose (r − q)/σ² is
/// constant over the option's life is a constant one in τ, so what pays at expiry is priced exactly as on the flat
/// market of the same variance and discounts to expiry, a date at its image on that market's clock; a payment at the
/// hit, discounted from the hit, needs r/σ² and q/σ² each constant. The shaped market here has r = σ², 4% to day 146
/// and 10.67% after, and no dividend; the carry-shaped market the same (r − q)/σ² with r flat at 8%; the ratio-varying
/// market the same volatility under r flat at 8% and no dividend; the flat market r 8% and σ √0.08, day 146 at day 73.
/// The full-window engines read the four alike, and the finite-difference barrier engine, exact under term structures,
/// converges to their price where the condition holds and away from it where it fails. The partial-time engines split
/// the variance at a date in calendar time, so they miss the time-changed market's price on the shaped market, and are
/// exact where σ and r − q are flat, a start-window knock-out's rebate, paid at the hit, where r and q are also flat to
/// the cover date.
/// </summary>
public class ClosedFormExactnessTests
{
    private const double V1 = 0.04;

    // the variance rate after the step, which brings the year's variance to 0.08
    private const double V2 = (0.08 - (V1 * 146 / 365.0)) / (219 / 365.0);

    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date Step = Today + 146;
    private static readonly Date Expiry = Today + 365;
    private static readonly EuropeanExercise European = new(Expiry);
    private static readonly PlainVanillaPayoff Call = new(OptionType.Call, 100.0);

    // the step on the flat market's clock: the variance to it and the rate's integral, 0.016 each, accrue at 0.08 a
    // year by day 73
    private static readonly Date StepOnTheFlatClock = Today + 73;

    // r = σ², no dividend: (r − q)/σ² = 1, r/σ² = 1 and q/σ² = 0
    private static readonly GeneralizedBlackScholesProcess Shaped =
        Market(Forwards(0.0, 0.0), Forwards(V1, V2), ShapedVolatility(1.0));

    // (r − q)/σ² = 1 with r flat: r/σ² and q/σ² vary
    private static readonly GeneralizedBlackScholesProcess CarryShaped =
        Market(Forwards(0.08 - V1, 0.08 - V2), Forwards(0.08, 0.08), ShapedVolatility(1.0));

    // the shaped volatility under r flat and no dividend: (r − q)/σ² varies
    private static readonly GeneralizedBlackScholesProcess RatioVarying =
        Market(Forwards(0.0, 0.0), Forwards(0.08, 0.08), ShapedVolatility(1.0));

    // the time-changed constant market
    private static readonly GeneralizedBlackScholesProcess Flat =
        Market(Forwards(0.0, 0.0), Forwards(0.08, 0.08), FlatVolatility(1.0));

    // cash 10 above a strike of 100
    private static readonly CashOrNothingPayoff Cash = new(OptionType.Call, 100.0, 10.0);

    // the other engines' products on a market: each engine reads the four markets alike, so by the time change its
    // price is exact on the shaped market, a payment at the hit included, and on the carry-shaped one for what pays at
    // expiry
    private static readonly Dictionary<string, Func<GeneralizedBlackScholesProcess, double>> OtherProducts = new()
    {
        ["double barrier, knock-out with a rebate"] = m => AnalyticDoubleBarrierEngine.Calculate(
            DoubleBarrier(DoubleBarrierType.KnockOut, 2.0), Context, m).NPV!.Value,
        ["double barrier, knock-in with a rebate"] = m => AnalyticDoubleBarrierEngine.Calculate(
            DoubleBarrier(DoubleBarrierType.KnockIn, 2.0), Context, m).NPV!.Value,
        ["Suo-Wang, knock-out with a rebate"] = m => SuoWangDoubleBarrierEngine.Calculate(
            DoubleBarrier(DoubleBarrierType.KnockOut, 2.0), Context, m).NPV!.Value,
        ["Suo-Wang, knock-in with a rebate"] = m => SuoWangDoubleBarrierEngine.Calculate(
            DoubleBarrier(DoubleBarrierType.KnockIn, 2.0), Context, m).NPV!.Value,
        ["double barrier binary, knock-out"] = m => AnalyticDoubleBarrierBinaryEngine.Calculate(
            DoubleBinary(DoubleBarrierType.KnockOut), Context, m).NPV!.Value,
        ["double barrier binary, knock-in"] = m => AnalyticDoubleBarrierBinaryEngine.Calculate(
            DoubleBinary(DoubleBarrierType.KnockIn), Context, m).NPV!.Value,
        ["double barrier binary, KIKO"] = m => AnalyticDoubleBarrierBinaryEngine.Calculate(
            DoubleBinary(DoubleBarrierType.KIKO), Context, m).NPV!.Value,
        ["double barrier binary, KOKI"] = m => AnalyticDoubleBarrierBinaryEngine.Calculate(
            DoubleBinary(DoubleBarrierType.KOKI), Context, m).NPV!.Value,
        ["binary barrier, knock-out with a rebate"] = m => AnalyticBinaryBarrierEngine.Calculate(
            new BarrierOption(BarrierType.DownOut, 85.0, 3.0, Cash, new AmericanExercise(Today, Expiry, true)),
            Context,
            m).NPV!.Value,
        ["fixed lookback"] = m => AnalyticContinuousFixedLookbackEngine.Calculate(
            new ContinuousFixedLookbackOption(100.0, Call, European), Context, m).NPV!.Value,
        ["floating lookback"] = m => AnalyticContinuousFloatingLookbackEngine.Calculate(
            new ContinuousFloatingLookbackOption(100.0, new FloatingTypePayoff(OptionType.Call), European),
            Context,
            m).NPV!.Value,
    };

    public static TheoryData<string> OtherProductNames() => [.. OtherProducts.Keys];

    [Fact]
    public void TheBarrierEngineIsExactWhereTheDriftOverTheVarianceIsConstant()
    {
        var option = new BarrierOption(BarrierType.DownOut, 85.0, 0.0, Call, European);
        double closedForm = AnalyticBarrierEngine.Calculate(option, Context, Shaped).NPV!.Value;
        foreach (GeneralizedBlackScholesProcess market in new[] { CarryShaped, RatioVarying, Flat })
        {
            AssertReadAlike(closedForm, AnalyticBarrierEngine.Calculate(option, Context, market).NPV!.Value);
        }

        // the finite differences converge to it on the shaped market, 3.2e-6 off at 400 points and 7.9e-7 at 800; where
        // the ratio varies, the closed form misses theirs by -2.37%
        double at400 = Miss(closedForm, Fd(option, Shaped, 400));
        double at800 = Miss(closedForm, Fd(option, Shaped, 800));
        Assert.True(Math.Abs(at800) < 1e-6 && Math.Abs(at800) < Math.Abs(at400) / 3.0, $"{at400:E3} and {at800:E3}");
        Assert.Equal(-0.0237, Miss(closedForm, Fd(option, RatioVarying, 400)), 1e-4);
        Assert.Equal(-0.0237, Miss(closedForm, Fd(option, RatioVarying, 800)), 1e-4);
    }

    [Fact]
    public void AKnockOutsRebateNeedsTheRateAndTheDividendOverTheVarianceEachConstant()
    {
        // a rebate of 3, paid at the hit
        var option = new BarrierOption(BarrierType.DownOut, 85.0, 3.0, Call, European);
        double closedForm = AnalyticBarrierEngine.Calculate(option, Context, Shaped).NPV!.Value;
        AssertReadAlike(closedForm, AnalyticBarrierEngine.Calculate(option, Context, CarryShaped).NPV!.Value);

        // on the shaped market the finite differences converge to it, 6.0e-5 off at 400 points and 2.9e-5 at 800; on
        // the carry-shaped market the hit's calendar time moves the rebate's discount, and the closed form misses
        // theirs by 0.11% at either grid
        double at400 = Miss(closedForm, Fd(option, Shaped, 400));
        double at800 = Miss(closedForm, Fd(option, Shaped, 800));
        Assert.True(Math.Abs(at800) < 4e-5 && Math.Abs(at800) < Math.Abs(at400) / 1.5, $"{at400:E3} and {at800:E3}");
        double carry400 = Miss(closedForm, Fd(option, CarryShaped, 400));
        double carry800 = Miss(closedForm, Fd(option, CarryShaped, 800));
        Assert.InRange(carry400, 1e-3, 1.2e-3);
        Assert.InRange(carry800, 1e-3, 1.2e-3);
        Assert.True(Math.Abs(carry400 - carry800) < 5e-5, $"{carry400:E3} and {carry800:E3}");
    }

    [Fact]
    public void TheBinaryBarrierEngineIsExactWhereTheDriftOverTheVarianceIsConstant()
    {
        // knocked out at 85: the engine's exercise American paying at expiry, the finite differences' European
        var option = new BarrierOption(BarrierType.DownOut, 85.0, 0.0, Cash, new AmericanExercise(Today, Expiry, true));
        double closedForm = AnalyticBinaryBarrierEngine.Calculate(option, Context, Shaped).NPV!.Value;
        foreach (GeneralizedBlackScholesProcess market in new[] { CarryShaped, RatioVarying, Flat })
        {
            AssertReadAlike(closedForm, AnalyticBinaryBarrierEngine.Calculate(option, Context, market).NPV!.Value);
        }

        // the finite differences, 1.8e-5 off at 800 points and 1.4e-6 at 1,600; where the ratio varies, the closed form
        // misses theirs by -3.57%
        var european = new BarrierOption(BarrierType.DownOut, 85.0, 0.0, Cash, European);
        double at800 = Miss(closedForm, Fd(european, Shaped, 800));
        double at1600 = Miss(closedForm, Fd(european, Shaped, 1600));
        Assert.True(Math.Abs(at800) < 3e-5 && Math.Abs(at1600) < 3e-6, $"{at800:E3} and {at1600:E3}");
        Assert.Equal(-0.0357, Miss(closedForm, Fd(european, RatioVarying, 1600)), 1e-4);
    }

    [Fact]
    public void TheSoftBarrierEngineIsExactWhereTheDriftOverTheVarianceIsConstant()
    {
        var option = new SoftBarrierOption(BarrierType.DownIn, 80.0, 90.0, Call, European);
        double closedForm = AnalyticSoftBarrierEngine.Calculate(option, Context, Shaped).NPV!.Value;
        foreach (GeneralizedBlackScholesProcess market in new[] { CarryShaped, RatioVarying, Flat })
        {
            AssertReadAlike(closedForm, AnalyticSoftBarrierEngine.Calculate(option, Context, market).NPV!.Value);
        }

        // the term-structure-exact route, the finite differences' knock-ins averaged over the band by Gauss-Legendre on
        // four barriers, converges to it: 3.0e-4 off at 400 points, 7.5e-5 at 800
        double at400 = Miss(closedForm, BandAverage(400));
        double at800 = Miss(closedForm, BandAverage(800));
        Assert.True(Math.Abs(at800) < 1e-4 && Math.Abs(at800) < Math.Abs(at400) / 3.0, $"{at400:E3} and {at800:E3}");

        static double BandAverage(int points)
        {
            var rule = new GaussLegendreIntegration(4);
            Vector x = rule.X(), w = rule.Weights();
            double sum = 0.0;
            for (int i = 0; i < rule.Order; ++i)
            {
                var knockIn = new BarrierOption(BarrierType.DownIn, 85.0 + (5.0 * x[i]), 0.0, Call, European);
                sum += 0.5 * w[i] * Fd(knockIn, Shaped, points);
            }

            return sum;
        }
    }

    [Theory]
    [MemberData(nameof(OtherProductNames))]
    public void TheOtherEnginesReadTheFourMarketsAlike(string product)
    {
        Func<GeneralizedBlackScholesProcess, double> price = OtherProducts[product];
        double shaped = price(Shaped);
        foreach (GeneralizedBlackScholesProcess market in new[] { CarryShaped, RatioVarying, Flat })
        {
            AssertReadAlike(shaped, price(market));
        }
    }

    [Fact]
    public void TheTwoAssetEnginePricesTheTimeChangedMarketOnOneClockForBothAssets()
    {
        // the second asset's volatility 1.5 times the first's and its dividend 0.1·σ₁², so (r − q₂)/σ₂² = 0.4 against
        // the first's 1; the correlation 0.5
        GeneralizedBlackScholesProcess second =
            Market(Forwards(0.1 * V1, 0.1 * V2), Forwards(V1, V2), ShapedVolatility(1.5));
        GeneralizedBlackScholesProcess secondFlat =
            Market(Forwards(0.008, 0.008), Forwards(0.08, 0.08), FlatVolatility(1.5));
        foreach ((BarrierType type, double barrier) in new[] { (BarrierType.DownOut, 95.0), (BarrierType.UpIn, 115.0) })
        {
            var option = new TwoAssetBarrierOption(type, barrier, new PlainVanillaPayoff(OptionType.Call, 90.0), European);
            AssertReadAlike(
                AnalyticTwoAssetBarrierEngine.Calculate(option, Context, Flat, secondFlat, 0.5).NPV!.Value,
                AnalyticTwoAssetBarrierEngine.Calculate(option, Context, Shaped, second, 0.5).NPV!.Value);
        }
    }

    [Fact]
    public void TheVannaVolgaEnginePricesShapedRatesOfAFlatDifferenceAsTheirAverages()
    {
        // its volatilities are quotes, flat in time, at the option's expiry; the domestic rate steps from 4% to 10.67%
        // as on the shaped market and the foreign stays 2% below it, against flat curves of the same discounts to
        // expiry
        var atm = new DeltaVolQuote(0.2, DeltaVolQuoteDeltaType.Fwd, 1.0, DeltaVolQuoteAtmType.AtmDeltaNeutral);
        var put25 = new DeltaVolQuote(-0.25, 0.21, 1.0, DeltaVolQuoteDeltaType.Fwd);
        var call25 = new DeltaVolQuote(0.25, 0.22, 1.0, DeltaVolQuoteDeltaType.Fwd);
        foreach (DoubleBarrierType type in new[] { DoubleBarrierType.KnockOut, DoubleBarrierType.KnockIn })
        {
            var option = new DoubleBarrierOption(type, 80.0, 130.0, 0.0, Call, European);
            double Price(YieldTermStructure domestic, YieldTermStructure foreign) => VannaVolgaDoubleBarrierEngine.Calculate(
                option, Context, AnalyticDoubleBarrierEngine.Calculate, atm, put25, call25, 100.0, domestic, foreign).NPV!.Value;
            AssertReadAlike(
                Price(Forwards(0.08, 0.08), Forwards(0.06, 0.06)), Price(Forwards(V1, V2), Forwards(V1 - 0.02, V2 - 0.02)));
        }
    }

    [Fact]
    public void ThePartialTimeBarrierEngineMissesTheTimeChangedMarket()
    {
        // the cover date at day 146 on the shaped market and at day 73 on the flat one, whose price is the exact one:
        // the engine's price on the shaped market misses it by -72% to +31%
        (PartialBarrierRange Range, BarrierType Type, double Barrier, double Miss)[] cases =
        [
            (PartialBarrierRange.Start, BarrierType.DownOut, 85.0, 0.02009),
            (PartialBarrierRange.Start, BarrierType.UpOut, 140.0, -0.00920),
            (PartialBarrierRange.Start, BarrierType.DownIn, 85.0, -0.26568),
            (PartialBarrierRange.Start, BarrierType.UpIn, 140.0, 0.31360),
            (PartialBarrierRange.EndB1, BarrierType.DownOut, 85.0, 0.04225),
            (PartialBarrierRange.EndB1, BarrierType.UpOut, 140.0, -0.66608),
            (PartialBarrierRange.EndB1, BarrierType.DownIn, 85.0, -0.33832),
            (PartialBarrierRange.EndB1, BarrierType.UpIn, 140.0, 0.21605),
            (PartialBarrierRange.EndB2, BarrierType.UpOut, 140.0, -0.72049),
            (PartialBarrierRange.EndB2, BarrierType.UpIn, 140.0, 0.22871),
        ];
        foreach ((PartialBarrierRange range, BarrierType type, double barrier, double miss) in cases)
        {
            double shaped = AnalyticPartialTimeBarrierOptionEngine.Calculate(
                new PartialTimeBarrierOption(type, range, barrier, 0.0, Step, Call, European), Context, Shaped).NPV!.Value;
            double timeChanged = AnalyticPartialTimeBarrierOptionEngine.Calculate(
                new PartialTimeBarrierOption(type, range, barrier, 0.0, StepOnTheFlatClock, Call, European),
                Context,
                Flat).NPV!.Value;
            Assert.Equal(miss, Miss(shaped, timeChanged), 1e-5);
        }
    }

    [Fact]
    public void ThePartialLookbacksMissTheTimeChangedMarket()
    {
        // the window's date at day 146 on the shaped market and at day 73 on the flat one
        double fixedShaped = AnalyticContinuousPartialFixedLookbackEngine.Calculate(
            new ContinuousPartialFixedLookbackOption(Step, Call, European), Context, Shaped).NPV!.Value;
        double fixedTimeChanged = AnalyticContinuousPartialFixedLookbackEngine.Calculate(
            new ContinuousPartialFixedLookbackOption(StepOnTheFlatClock, Call, European), Context, Flat).NPV!.Value;
        Assert.Equal(-0.04854, Miss(fixedShaped, fixedTimeChanged), 1e-5);
        var floating = new FloatingTypePayoff(OptionType.Call);
        double floatingShaped = AnalyticContinuousPartialFloatingLookbackEngine.Calculate(
            new ContinuousPartialFloatingLookbackOption(100.0, 1.0, Step, floating, European), Context, Shaped).NPV!.Value;
        double floatingTimeChanged = AnalyticContinuousPartialFloatingLookbackEngine.Calculate(
            new ContinuousPartialFloatingLookbackOption(100.0, 1.0, StepOnTheFlatClock, floating, European),
            Context,
            Flat).NPV!.Value;
        Assert.Equal(0.08118, Miss(floatingShaped, floatingTimeChanged), 1e-5);
    }

    [Fact]
    public void APartialTimeRebateNeedsTheRatesFlatToTheCoverDate()
    {
        // a start-window down-and-out at 85 with a rebate of 3, paid at the hit before the cover event at day 146,
        // under σ flat and r − q 3% throughout; its rebate the option less the same option with none, against the
        // finite differences' knock-out paying the rebate alone to the cover date (a call struck at 1e9)
        var rebateOnly = new BarrierOption(
            BarrierType.DownOut, 85.0, 3.0, new PlainVanillaPayoff(OptionType.Call, 1e9), new EuropeanExercise(Step));
        double Rebate(GeneralizedBlackScholesProcess market) =>
            AnalyticPartialTimeBarrierOptionEngine.Calculate(
                new PartialTimeBarrierOption(BarrierType.DownOut, PartialBarrierRange.Start, 85.0, 3.0, Step, Call, European),
                Context,
                market).NPV!.Value
            - AnalyticPartialTimeBarrierOptionEngine.Calculate(
                new PartialTimeBarrierOption(BarrierType.DownOut, PartialBarrierRange.Start, 85.0, 0.0, Step, Call, European),
                Context,
                market).NPV!.Value;

        // r and q flat to the cover date, stepping at it: the finite differences converge to the engine's rebate,
        // 8.2e-4 off at 400 points and 4.1e-4 at 800
        GeneralizedBlackScholesProcess flatToTheCover =
            Market(Forwards(V1 - 0.03, V2 - 0.03), Forwards(V1, V2), FlatVolatility(1.0));
        double rebate = Rebate(flatToTheCover);
        double at400 = Miss(rebate, Fd(rebateOnly, flatToTheCover, 400));
        double at800 = Miss(rebate, Fd(rebateOnly, flatToTheCover, 800));
        Assert.True(Math.Abs(at800) < 6e-4 && Math.Abs(at800) < Math.Abs(at400) / 1.5, $"{at400:E3} and {at800:E3}");

        // r stepping at day 73, before the cover date: they move away from it, -0.31% at 400 points and -0.35% at 800
        GeneralizedBlackScholesProcess steppingBefore = Market(
            Forwards(V1 - 0.03, V2 - 0.03, StepOnTheFlatClock), Forwards(V1, V2, StepOnTheFlatClock), FlatVolatility(1.0));
        rebate = Rebate(steppingBefore);
        double before400 = Miss(rebate, Fd(rebateOnly, steppingBefore, 400));
        double before800 = Miss(rebate, Fd(rebateOnly, steppingBefore, 800));
        Assert.True(before400 < -2.5e-3 && before800 < before400, $"{before400:E3} and {before800:E3}");
    }

    [Fact]
    public void ThePartialEnginesAreExactWhereTheVolatilityAndTheCarryAreFlat()
    {
        // r stepping from 4% to 10.67% as on the shaped market, q 3% below it and σ flat, against r and q at their
        // averages to expiry: the spot's law at each date and the discount to expiry are the same
        GeneralizedBlackScholesProcess carryFlat =
            Market(Forwards(V1 - 0.03, V2 - 0.03), Forwards(V1, V2), FlatVolatility(1.0));
        GeneralizedBlackScholesProcess averaged = Market(Forwards(0.05, 0.05), Forwards(0.08, 0.08), FlatVolatility(1.0));
        foreach (PartialBarrierRange range in new[] { PartialBarrierRange.Start, PartialBarrierRange.EndB1 })
        {
            foreach ((BarrierType type, double barrier) in new[]
            {
                (BarrierType.DownOut, 85.0), (BarrierType.UpOut, 140.0), (BarrierType.DownIn, 85.0), (BarrierType.UpIn, 140.0),
            })
            {
                var option = new PartialTimeBarrierOption(type, range, barrier, 0.0, Step, Call, European);
                AssertReadAlike(
                    AnalyticPartialTimeBarrierOptionEngine.Calculate(option, Context, averaged).NPV!.Value,
                    AnalyticPartialTimeBarrierOptionEngine.Calculate(option, Context, carryFlat).NPV!.Value);
            }
        }

        var fixedOption = new ContinuousPartialFixedLookbackOption(Step, Call, European);
        AssertReadAlike(
            AnalyticContinuousPartialFixedLookbackEngine.Calculate(fixedOption, Context, averaged).NPV!.Value,
            AnalyticContinuousPartialFixedLookbackEngine.Calculate(fixedOption, Context, carryFlat).NPV!.Value);
        var floatingOption = new ContinuousPartialFloatingLookbackOption(
            100.0, 1.0, Step, new FloatingTypePayoff(OptionType.Call), European);
        AssertReadAlike(
            AnalyticContinuousPartialFloatingLookbackEngine.Calculate(floatingOption, Context, averaged).NPV!.Value,
            AnalyticContinuousPartialFloatingLookbackEngine.Calculate(floatingOption, Context, carryFlat).NPV!.Value);
    }

    private static GeneralizedBlackScholesProcess Market(
        YieldTermStructure dividends, YieldTermStructure riskFree, BlackVolTermStructure volatility) =>
        new BlackScholesMertonProcess(100.0, dividends, riskFree, volatility);

    // the forward rate f1 to the step, or to another date, and f2 after it
    private static ForwardCurve Forwards(double f1, double f2, Date? step = null) =>
        new([Today, step ?? Step, Expiry], [f1, f1, f2], A365);

    // σ² at V1 to the step and V2 after it, or flat at the year's 0.08, each volatility scaled
    private static BlackVarianceCurve ShapedVolatility(double scale) =>
        new(Today, [Step, Expiry], [scale * Math.Sqrt(V1), scale * Math.Sqrt(0.08)], A365);

    private static BlackConstantVol FlatVolatility(double scale) =>
        new(Today, new NullCalendar(), scale * Math.Sqrt(0.08), A365);

    private static DoubleBarrierOption DoubleBarrier(DoubleBarrierType type, double rebate) =>
        new(type, 80.0, 130.0, rebate, Call, European);

    // cash 10 for a call struck at 0; KIKO and KOKI paid at the hit
    private static DoubleBarrierOption DoubleBinary(DoubleBarrierType type) =>
        new(
            type,
            80.0,
            130.0,
            0.0,
            new CashOrNothingPayoff(OptionType.Call, 0.0, 10.0),
            type is DoubleBarrierType.KIKO or DoubleBarrierType.KOKI ? new AmericanExercise(Today, Expiry) : European);

    private static double Fd(BarrierOption option, GeneralizedBlackScholesProcess market, int points) =>
        FdBlackScholesBarrierEngine.Calculate(option, Context, market, tGrid: points, xGrid: points).NPV!.Value;

    private static double Miss(double value, double reference) => (value / reference) - 1.0;

    // the same price to rounding: the markets' variance and discounts to expiry agree to their last bits
    private static void AssertReadAlike(double expected, double actual) =>
        Assert.Equal(expected, actual, 1e-14 * Math.Abs(expected));
}
