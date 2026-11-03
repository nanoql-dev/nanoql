// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Suo-Wang double-barrier engine's four classes (fixed). QuantLib clips one strike limit only
/// (<c>suowangdoublebarrierengine.cpp:54-55</c>, #2881's class), so a knock-out call struck above H prices
/// −0.1694572673, a put struck below L −0.3467932631, and the knock-in call 0.9992298671 against the analytic engine's
/// 0.8297725998; it sums ten terms from −5 to 4 (<c>:77</c>), 0.004975355507 on a narrow corridor over two years where
/// 50 terms give −3.5e-13 and the binomial engine 1e-70; it ignores the rebate, 1.454185879 with a rebate of 5 as
/// without where the binomial Derman-Kani engine gives 3.982 (knock-in 6.818712906 against 9.194); and its powers
/// overflow to NaN at a volatility of 1e-4 and of 0 (QuantLib 1.44). nanoql clips both limits to the corridor,
/// converges the series, prices the rebate as the analytic engine does and takes the analytic engine's knock-out where
/// a term overflows. Checks: the analytic engine, QuantLib's binomial Derman-Kani values, the deterministic limit.
/// </summary>
public class SuoWangDoubleBarrierTests
{
    private static readonly Date Today = new(15, May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(OptionType.Call, 130.0)] // QuantLib's -0.1694572673
    [InlineData(OptionType.Put, 70.0)] // QuantLib's -0.3467932631
    public void AKnockOutStruckOutsideTheCorridorPricesNothing(OptionType type, double strike)
    {
        DoubleBarrierOption option = Option(DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, type, strike, 183);
        GeneralizedBlackScholesProcess process = Process(0.25);

        double suoWang = SuoWangDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        double analytic = AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        Assert.Equal(0.0, analytic);

        // the clipped limits cancel each term pairwise, within the rounding of the terms' own values
        Assert.True(Math.Abs(suoWang) < 1e-13, $"{suoWang}");
    }

    [Fact]
    public void AKnockInStruckAboveTheCorridorIsTheAnalyticEngines()
    {
        // QuantLib's 0.9992298671; the analytic engine's 0.8297725998, the binomial Derman-Kani engine's 0.829393372
        DoubleBarrierOption option = Option(DoubleBarrierType.KnockIn, 80.0, 120.0, 0.0, OptionType.Call, 130.0, 183);
        GeneralizedBlackScholesProcess process = Process(0.25);

        double suoWang = SuoWangDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        double analytic = AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        Assert.Equal(0.8297725998, analytic, 1e-10);
        Assert.Equal(analytic, suoWang, 1e-12);
    }

    [Fact]
    public void ANarrowCorridorOverTwoYearsIsWorthItsConvergedSeries()
    {
        // QuantLib's ten terms: 0.004975355507; fifty: -3.487560274e-13; the binomial engine 9.86e-70
        DoubleBarrierOption option = Option(DoubleBarrierType.KnockOut, 95.0, 105.0, 0.0, OptionType.Call, 100.0, 730);
        GeneralizedBlackScholesProcess process = Process(0.50);

        double ten = SuoWangDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        double fifty = SuoWangDoubleBarrierEngine.Calculate(option, Context, process, 50).NPV!.Value;
        Assert.True(Math.Abs(ten) < 1e-12, $"{ten}");
        Assert.Equal(fifty, ten, 1e-12);
    }

    [Theory]
    [InlineData(DoubleBarrierType.KnockOut, 3.982374755)] // QuantLib's 1.454185879, its rebate unread
    [InlineData(DoubleBarrierType.KnockIn, 9.193693482)] // QuantLib's 6.818712906
    public void ARebateIsPaidAsTheAnalyticEnginePaysIt(DoubleBarrierType type, double binomial)
    {
        // the binomial Derman-Kani engine at 2,000 steps, QuantLib's value
        DoubleBarrierOption option = Option(type, 80.0, 120.0, 5.0, OptionType.Call, 100.0, 183);
        GeneralizedBlackScholesProcess process = Process(0.25);

        double suoWang = SuoWangDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        double analytic = AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        Assert.Equal(analytic, suoWang, 1e-12);
        Assert.Equal(binomial, suoWang, 0.01);
    }

    [Theory]
    [InlineData(1e-4)]
    [InlineData(0.0)]
    public void AtLowVolatilityTheKnockOutIsTheAnalyticEngines(double volatility)
    {
        // QuantLib's NaN; the forward, 100 e^(0.05 × 183/365), stays inside the corridor, so the call pays it less the
        // strike at expiry
        DoubleBarrierOption option = Option(DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, OptionType.Call, 100.0, 183);
        GeneralizedBlackScholesProcess process = Process(volatility);

        double suoWang = SuoWangDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        double analytic = AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        double t = 183 / 365.0;
        double deterministic = Math.Exp(-0.05 * t) * ((100.0 * Math.Exp(0.05 * t)) - 100.0);
        Assert.Equal(analytic, suoWang);
        Assert.Equal(deterministic, suoWang, 1e-12);
    }

    [Fact]
    public void AtAVolatilityOfAFewPercentCancellingSumsGiveWayToTheAnalyticEngine()
    {
        // QuantLib's Suo-Wang and analytic engines both give 14.2213591215, its CRR binomial engine 13.6845, 13.6745
        // and 13.6674 at 2,000, 8,000 and 32,000 steps, converging on the analytic value
        DoubleBarrierOption option = Option(DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, OptionType.Call, 95.0, 1095);
        GeneralizedBlackScholesProcess process = Process(0.02);

        double suoWang = SuoWangDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        double analytic = AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        double binomial = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDoubleBarrierOption>(
            option, Context, process, 8000).NPV!.Value;
        Assert.Equal(analytic, suoWang, 1e-12 * Math.Max(Math.Abs(analytic), 1.0));
        Assert.True(Math.Abs(binomial - suoWang) < 0.05, $"{binomial} against {suoWang}");
        Assert.True(Math.Abs(14.2213591215 - binomial) > 0.5, $"{binomial} against QuantLib's 14.2213591215");
    }

    [Theory]
    [InlineData(0.01, OptionType.Call, 95.0)]
    [InlineData(0.01, OptionType.Put, 105.0)]
    [InlineData(0.015, OptionType.Call, 95.0)]
    [InlineData(0.02, OptionType.Put, 105.0)]
    [InlineData(0.025, OptionType.Call, 95.0)]
    [InlineData(0.03, OptionType.Put, 105.0)]
    public void TheKnockOutIsTheAnalyticEnginesWithinTheSumsAgreement(double volatility, OptionType type, double strike)
    {
        // a sweep band, σ 1% to 3%, three years on [80, 120]: knock-outs and knock-ins
        GeneralizedBlackScholesProcess process = Process(volatility);
        foreach (DoubleBarrierType barrierType in new[] { DoubleBarrierType.KnockOut, DoubleBarrierType.KnockIn })
        {
            DoubleBarrierOption option = Option(barrierType, 80.0, 120.0, 0.0, type, strike, 1095);
            double suoWang = SuoWangDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
            double analytic = AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
            Assert.Equal(analytic, suoWang, 1e-12 * Math.Max(Math.Abs(analytic), 1.0));
        }
    }

    [Fact]
    public void QuantLibsValueStandsWhereItsTermsHaveConverged()
    {
        // QuantLib's 1.454185879 and, at a zero drift, 2.06212491
        Assert.Equal(
            1.454185879,
            SuoWangDoubleBarrierEngine.Calculate(
                Option(DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, OptionType.Call, 100.0, 183), Context, Process(0.25)).NPV!.Value,
            1e-9);
        var zeroDrift = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.05 - (0.5 * 0.04), A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));
        Assert.Equal(
            2.06212491,
            SuoWangDoubleBarrierEngine.Calculate(
                Option(DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, OptionType.Call, 100.0, 183), Context, zeroDrift).NPV!.Value,
            1e-8);
    }

    private static DoubleBarrierOption Option(
        DoubleBarrierType type, double lo, double hi, double rebate, OptionType optionType, double strike, int days) =>
        new(type, lo, hi, rebate, new PlainVanillaPayoff(optionType, strike), new EuropeanExercise(Today + days));

    // the market: S 100, q 0, r 0.05 on Actual/365F
    private static GeneralizedBlackScholesProcess Process(double volatility) =>
        new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.0, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), volatility, A365));
}
