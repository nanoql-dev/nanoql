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

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Vanna-Volga double-barrier engine's classes. QuantLib passes the foreign discount twice to the smile's
/// interpolation (<c>vannavolgadoublebarrierengine.hpp:144</c>), whose constructor takes the domestic one first, as
/// QuantLib's single-barrier engine passes it: the vanilla on the smile moves by 1.4e-4 at QuantLib's rates and 6.0e-4
/// at a foreign rate of 3% (QuantLib 1.44). nanoql passes the domestic discount first; its vanilla equals the
/// single-barrier engine's on the same quotes. nanoql also carries the single-barrier engine's fixes: each curve read
/// at the instant the quotes' time denotes on the volatility's clock, an out option on or beyond a barrier worth its
/// rebate, an in option's rebate the inner engine's, and the already-out and already-in tests inclusive at the
/// barriers. QuantLib's own case builds its curves on Actual360 beside the engine's Actual/365F volatility, where
/// QuantLib's inner engine accrues the variance over 365/360 risk-free years and its own discounts read each curve's
/// clock: these reads move 16 of its 40 values past its 1e-4, by up to 1.11e-3 (eight strikes, each knock-in and
/// knock-out: the calls at 1.13321 and 1.22687 and the puts at 1.38843 and 1.46047 over a year, the calls at 1.06145
/// and 1.19545 and the puts at 1.44298 and 1.56345 over two; QuantLib's cached 0.036217, 0.144130, 0.025532, 0.074560,
/// 0.036798, 0.060490, 0.047325, 0.111030, 0.059136, 0.199810, 0.042694, 0.103890, 0.061539, 0.093460, 0.081464,
/// 0.177040 against nanoql's 0.035420, 0.144927, 0.025147, 0.074945, 0.036576, 0.060712, 0.046837, 0.111519, 0.058023,
/// 0.200923, 0.042200, 0.104384, 0.061129, 0.093870, 0.080606, 0.177897), and QuantLib's own engine misses 20 of the 40
/// on the case restated to Actual/365F (worst 1.04e-3): the cache belongs to QuantLib's mixed clocks (QuantLib 1.44).
/// On the restated case nanoql is QuantLib's engine with the domestic discount, within 1e-12 on all 40.
/// </summary>
public class VannaVolgaDoubleBarrierTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    // QuantLib's 40 values on its case restated to Actual/365F, its engine with the domestic discount first and the
    // Suo-Wang inner engine
    [Theory]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.13321, 1.0, 0.035399683999394)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.13321, 1.0, 0.144947703744088)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.22687, 1.0, 0.025132039409934)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.22687, 1.0, 0.074960038748781)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.31179, 1.0, 0.016698852132543)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.31179, 1.0, 0.027205789632218)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.38843, 1.0, 0.010041760821381)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.38843, 1.0, 0.005700341670404)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.46047, 1.0, 0.004886769340638)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.46047, 1.0, 0.000125774665913)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.13321, 1.0, 0.007325795691073)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.13321, 1.0, 0.000210976181086)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.22687, 1.0, 0.017101482737919)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.22687, 1.0, 0.003523280412072)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.31179, 1.0, 0.026841258522640)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.31179, 1.0, 0.022228922271962)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.38843, 1.0, 0.036585202164448)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.38843, 1.0, 0.060703291141394)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.46047, 1.0, 0.046846841169705)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.46047, 1.0, 0.111508499738259)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.06145, 2.0, 0.057985024222267)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.06145, 2.0, 0.200961233020962)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.19545, 2.0, 0.042171762293312)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.19545, 2.0, 0.104412354426252)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.32238, 2.0, 0.028178840492968)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.32238, 2.0, 0.035664505695371)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.44298, 2.0, 0.017317721593624)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.44298, 2.0, 0.006337377782667)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockIn, 1.56345, 2.0, 0.007644212055859)]
    [InlineData(OptionType.Call, DoubleBarrierType.KnockOut, 1.56345, 2.0, 0.000000000000000)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.06145, 2.0, 0.011784543188434)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.06145, 2.0, 0.000000000000000)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.19545, 2.0, 0.028013489733120)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.19545, 2.0, 0.004346825967833)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.32238, 2.0, 0.043756282622055)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.32238, 2.0, 0.031787212560235)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.44298, 2.0, 0.061147958100775)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.44298, 2.0, 0.093851412002032)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockIn, 1.56345, 2.0, 0.080627490068949)]
    [InlineData(OptionType.Put, DoubleBarrierType.KnockOut, 1.56345, 2.0, 0.177876144828869)]
    public void OnTheRestatedCaseTheEngineIsQuantLibsWithTheDomesticDiscount(
        OptionType type, DoubleBarrierType barrierType, double strike, double t, double quantLib)
    {
        var today = new Date(5, March, 2013);
        var context = new EvaluationContext { EvaluationDate = today };
        bool oneYear = t == 1.0;
        double q = oneYear ? 0.0003541 : 0.0009418;
        double r = oneYear ? 0.0033871 : 0.0039788;
        double vol = StrikeVols[(t, strike)];
        var qTS = new FlatForward(today, q, A365);
        var rTS = new FlatForward(today, r, A365);
        var option = new DoubleBarrierOption(
            barrierType, oneYear ? 1.1 : 1.0, oneYear ? 1.5 : 1.6, 0.0, new PlainVanillaPayoff(type, strike),
            new EuropeanExercise(today + (int)Math.Round(t * 365, MidpointRounding.AwayFromZero)));
        (DeltaVolQuote atm, DeltaVolQuote put25, DeltaVolQuote call25) = oneYear
            ? Quotes(t, 0.10087, 0.08925, 0.08463)
            : Quotes(t, 0.10891, 0.09525, 0.09197);
        double bsVanillaPrice = BlackFormulaFunctions.BlackFormula(
            type, strike, 1.30265 * qTS.Discount(t) / rTS.Discount(t), vol * Math.Sqrt(t), rTS.Discount(t));

        double calculated = VannaVolgaDoubleBarrierEngine.Calculate(
            option, context, SuoWangDoubleBarrierEngine.Calculate, atm, put25, call25, 1.30265, rTS, qTS, true,
            bsVanillaPrice).NPV!.Value;
        Assert.Equal(quantLib, calculated, 1e-12);
    }

    [Theory]
    [InlineData(0.0003541, 1.13321)] // QuantLib's foreign discount twice: 0.1879159854, the domestic first 0.1877781008
    [InlineData(0.03, 1.13321)] // 0.1518713994 against 0.1524715683, 6.0e-4
    [InlineData(0.03, 1.31179)]
    public void TheSmilesVanillaIsTheSingleBarrierEngines(double q, double strike)
    {
        // the market: the smile 0.11638, 0.09979, 0.11545 over a year on Actual/365F
        var today = new Date(15, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var qTS = new FlatForward(today, q, A365);
        var rTS = new FlatForward(today, 0.0033871, A365);
        (DeltaVolQuote atm, DeltaVolQuote put25, DeltaVolQuote call25) = Quotes(1.0, 0.11638, 0.09979, 0.11545);
        var payoff = new PlainVanillaPayoff(OptionType.Call, strike);
        var exercise = new EuropeanExercise(today + 365);

        double doubleBarrier = VannaVolgaDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, payoff, exercise), context,
            SuoWangDoubleBarrierEngine.Calculate, atm, put25, call25, 1.30265, rTS, qTS).Result<double>("VanillaPrice");
        double singleBarrier = VannaVolgaBarrierEngine.Calculate(
            new BarrierOption(BarrierType.UpOut, 1.5, 0.0, payoff, exercise), context, atm, put25, call25, 1.30265, rTS,
            qTS).Result<double>("VanillaPrice");
        Assert.Equal(singleBarrier, doubleBarrier);
    }

    [Fact]
    public void OnAFlatSmileTheEngineIsItsInnerEngineWithARebate()
    {
        // the smile flat at 10%: no correction; a rebate of 0.01 in and out. The knock-out is the inner engine's; the
        // knock-in its vanilla less the knock-out, the vanilla the smile's, whose interpolation inverts its price to a
        // volatility within the implied-volatility solver's accuracy (QuantLib's VannaVolga interpolation), so it
        // reads the analytic knock-in plus the smile vanilla's offset from the European
        var today = new Date(15, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var qTS = new FlatForward(today, 0.01, A365);
        var rTS = new FlatForward(today, 0.02, A365);
        (DeltaVolQuote atm, DeltaVolQuote put25, DeltaVolQuote call25) = Quotes(1.0, 0.10, 0.10, 0.10);
        var process = new BlackScholesMertonProcess(
            1.30265, qTS, rTS, new BlackConstantVol(today, new NullCalendar(), 0.10, A365));
        foreach (DoubleBarrierType type in new[] { DoubleBarrierType.KnockOut, DoubleBarrierType.KnockIn })
        {
            var option = new DoubleBarrierOption(
                type, 1.1, 1.5, 0.01, new PlainVanillaPayoff(OptionType.Call, 1.3), new EuropeanExercise(today + 365));
            OneAssetOptionResults vannaVolga = VannaVolgaDoubleBarrierEngine.Calculate(
                option, context, AnalyticDoubleBarrierEngine.Calculate, atm, put25, call25, 1.30265, rTS, qTS);
            double analytic = AnalyticDoubleBarrierEngine.Calculate(option, context, process).NPV!.Value;
            double offset = type == DoubleBarrierType.KnockIn
                ? vannaVolga.Result<double>("VanillaPrice") - AnalyticEuropeanEngine.Calculate(
                    new VanillaOption((StrikedTypePayoff)option.Payoff!, option.Exercise), context, process).NPV!.Value
                : 0.0;
            Assert.Equal(analytic + offset, vannaVolga.NPV!.Value, 1e-14);
        }
    }

    [Theory]
    [InlineData(1.1)]
    [InlineData(1.5)]
    public void OnABarrierAnOutOptionIsItsRebateAndAnInOptionItsVanilla(double spot)
    {
        // QuantLib's strict tests send a spot on a barrier to the inner engine's "barrier touched"
        var today = new Date(15, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var qTS = new FlatForward(today, 0.01, A365);
        var rTS = new FlatForward(today, 0.02, A365);
        (DeltaVolQuote atm, DeltaVolQuote put25, DeltaVolQuote call25) = Quotes(1.0, 0.11638, 0.09979, 0.11545);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 1.3);
        var exercise = new EuropeanExercise(today + 365);

        OneAssetOptionResults knockOut = VannaVolgaDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(DoubleBarrierType.KnockOut, 1.1, 1.5, 0.02, payoff, exercise), context,
            SuoWangDoubleBarrierEngine.Calculate, atm, put25, call25, spot, rTS, qTS);
        OneAssetOptionResults knockIn = VannaVolgaDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(DoubleBarrierType.KnockIn, 1.1, 1.5, 0.02, payoff, exercise), context,
            SuoWangDoubleBarrierEngine.Calculate, atm, put25, call25, spot, rTS, qTS);
        Assert.Equal(0.02, knockOut.NPV!.Value);
        Assert.Equal(knockIn.Result<double>("VanillaPrice"), knockIn.NPV!.Value);
    }

    [Fact]
    public void EachCurveIsReadAtTheQuotesInstant()
    {
        // curves on Actual360 and on Actual/365F with the same discount at every date price alike; QuantLib reads
        // the quotes' time on each curve's own clock, a different instant on each
        var today = new Date(15, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter a360 = new Actual360();
        (DeltaVolQuote atm, DeltaVolQuote put25, DeltaVolQuote call25) = Quotes(1.0, 0.11638, 0.09979, 0.11545);
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 1.1, 1.5, 0.0, new PlainVanillaPayoff(OptionType.Call, 1.3),
            new EuropeanExercise(today + 365));

        double OnCurves(DayCounter dc, double scale) => VannaVolgaDoubleBarrierEngine.Calculate(
            option, context, AnalyticDoubleBarrierEngine.Calculate, atm, put25, call25, 1.30265,
            new FlatForward(today, 0.0033871 * scale, dc), new FlatForward(today, 0.0003541 * scale, dc)).NPV!.Value;

        Assert.Equal(OnCurves(A365, 365.0 / 360.0), OnCurves(a360, 1.0), 1e-14);
    }

    [Fact]
    public void TheSurvivalProbabilityIsItsConvergedSeries()
    {
        // On a narrow corridor over a year QuantLib's ten terms give a survival probability of 7.2e-4 where fifty give
        // 1.6e-15; QuantLib's one-year quotes and spot, the analytic inner engine
        var today = new Date(15, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        (DeltaVolQuote atm, DeltaVolQuote put25, DeltaVolQuote call25) = Quotes(1.0, 0.10087, 0.08925, 0.08463);
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 1.28, 1.32, 0.0, new PlainVanillaPayoff(OptionType.Call, 1.30),
            new EuropeanExercise(today + 365));

        double Lambda(int series) => VannaVolgaDoubleBarrierEngine.Calculate(
            option, context, AnalyticDoubleBarrierEngine.Calculate, atm, put25, call25, 1.30265,
            new FlatForward(today, 0.0033871 * 365.0 / 360.0, A365), new FlatForward(today, 0.0003541 * 365.0 / 360.0, A365),
            series: series).Result<double>("lambda");

        double ten = Lambda(5);
        Assert.True(Math.Abs(ten) < 1e-12, $"{ten}");
        Assert.Equal(Lambda(50), ten, 1e-14);
    }

    // the vols at the case's strikes, QuantLib's rows' last vol column, for the vanilla price the case gives
    private static readonly Dictionary<(double T, double Strike), double> StrikeVols = new()
    {
        [(1.0, 1.13321)] = 0.11638,
        [(1.0, 1.22687)] = 0.10088,
        [(1.0, 1.31179)] = 0.08925,
        [(1.0, 1.38843)] = 0.08463,
        [(1.0, 1.46047)] = 0.08412,
        [(2.0, 1.06145)] = 0.12511,
        [(2.0, 1.19545)] = 0.10890,
        [(2.0, 1.32238)] = 0.09444,
        [(2.0, 1.44298)] = 0.09197,
        [(2.0, 1.56345)] = 0.09261,
    };

    // QuantLib's quotes: the at-the-money delta-neutral forward quote and the 25-delta put and call
    private static (DeltaVolQuote Atm, DeltaVolQuote Put25, DeltaVolQuote Call25) Quotes(
        double t, double put25, double atm, double call25) =>
        (new DeltaVolQuote(atm, DeltaVolQuoteDeltaType.Fwd, t, DeltaVolQuoteAtmType.AtmDeltaNeutral),
         new DeltaVolQuote(-0.25, put25, t, DeltaVolQuoteDeltaType.Fwd),
         new DeltaVolQuote(0.25, call25, t, DeltaVolQuoteDeltaType.Fwd));
}
