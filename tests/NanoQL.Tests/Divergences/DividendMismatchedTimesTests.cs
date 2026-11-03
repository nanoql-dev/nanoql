// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Cash dividends on structures that measure time differently. QuantLib's analytic dividend engine counts dividends
/// from the risk-free curve's reference date (<c>analyticdividendeuropeanengine.cpp:43-56</c>), and its
/// finite-difference engine places them on the risk-free curve's times, where this one's is negative, so a dividend
/// paid between the evaluation date and a risk-free curve settling later is left out: on a one-year put, a 2.0 dividend
/// paid the day after the evaluation date doesn't move the analytic price. On the market restated from the evaluation
/// date, the risk-free curve discounting nothing before its own date, both engines count it. nanoql's engines run from
/// the earliest reference date of the structures, each discounting nothing before its own, so they count it, and price
/// the market as QuantLib's engines price it restated: the analytic engine's value, delta, gamma and vega, and the
/// finite-difference engine's value under both cash-dividend models. The analytic engine's rho keeps QuantLib's times,
/// the risk-free curve's own, so a dividend before its reference date adds nothing to it: QuantLib's rho on the market
/// as given, with that dividend's value taken off the spot. Its theta runs each structure over its own time
/// (<c>AnalyticThetaMismatchedTimesTests</c>), QuantLib's <c>blackScholesTheta</c> at the spot less the dividends'
/// value plus QuantLib's own dividend term. QuantLib's analytic engine also reads the dividend curve's rate per its own
/// day counter's year in the theta, and the curve at the risk-free curve's time in the rho (<c>:93-105</c>); here each
/// is read per the risk-free curve's time and by date, as on the curve restated on that day counter, QuantLib's read
/// where the two curves share their times. Where every structure shares the risk-free curve's day counter and reference
/// date, the settlement date, the times and the reads are QuantLib's.
/// </summary>
public class DividendMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void ADividendInTheGapIsTheOptions()
    {
        // QuantLib on the market restated from today: the analytic engine's value, delta, gamma and vega; its finite-
        // difference engine (400 x 400), 8.261833935531243 and 8.261205730947648 under the spot and the escrowed
        // models. QuantLib's analytic engine at the spot less the dividend's value (98) and no dividend: the same four
        // and the rho, and the theta its blackScholesTheta reads on them (its own, over the risk-free curve's time,
        // -2.4040363532049938). On the market as given its analytic engine reads 7.4834738389519675, the price without
        // the dividend, and its finite-difference engine 7.431579950974667 under both
        BlackScholesMertonProcess process = Process(new FlatForward(Today, 0.0, A365));
        Analytic(
            process,
            Today + 1,
            [8.261063112333577, -0.404391092842614, 0.015813472393935633, -2.351448891678239, -47.36655305791349, 37.96814721783948]);
        FiniteDifference(process, Today + 1, 8.261833935531243, 8.261205730947648);
    }

    [Fact]
    public void ADividendAfterTheGapIsQuantLibs()
    {
        // the dividend on 15 December: QuantLib's analytic engine on the market as given in value, delta, gamma, rho
        // and vega, and the theta its blackScholesTheta reads at the spot less the dividend's value, 98.01555562844473,
        // plus its own dividend term (its theta -2.365164839481786); its finite-difference engine on the restated
        // market, on the market as given 8.233497894819253 and 8.202761354433404
        BlackScholesMertonProcess process = Process(new FlatForward(Today, 0.0, A365));
        Analytic(
            process,
            Today + 61,
            [8.254774467800562, -0.4041451427731601, 0.01580853054686332, -2.3125771213743533, -47.46795659345569, 37.96833246909108]);
        FiniteDifference(process, Today + 61, 8.287621554741566, 8.254916941843737);
    }

    [Fact]
    public void ADividendCurveSettlingWithTheRiskFreeCurveIsReadByDate()
    {
        // q 3% settling T+2 with the risk-free curve, the volatility today, so the earliest date is the volatility's:
        // QuantLib's analytic engine on the market as given for the 15 December dividend (theta -3.41110787761532), and
        // at the spot less the 16 October dividend's value with none (theta -3.428132215477116), the thetas
        // blackScholesTheta's as above; the rho reads the dividend curve by date, from its own reference date
        BlackScholesMertonProcess process = Process(FlatForward.Create(2, new TARGET(), 0.03, A365, Context));
        Analytic(
            process,
            Today + 61,
            [9.483038042539427, -0.4376489040528592, 0.015686090318387386, -3.358937383463702, -51.937647717225936, 37.667096749876926]);
        Analytic(
            process,
            Today + 1,
            [9.485767889689605, -0.4377467401910237, 0.015687581532221846, -3.375963402060966, -51.81086680179722, 37.665883258864646]);
    }

    [Fact]
    public void ADividendCurveOnAnotherDayCounterIsReadPerTheRiskFreeCurvesTime()
    {
        // a dividend curve on Act/360 beside a risk-free curve on Act/365, a 2.0 dividend on 15 December: QuantLib on
        // the curve restated on Act/365, the same discount at every date, reads the value 9.486181015174774, the rho
        // -52.52174422764317 and the theta -3.3740258645611516; on the curve as given, the same value, the rho
        // -52.52173407556717 and the theta -3.373662371570814
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.03, new Actual360()), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, null, 0.25, A365));
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));
        OneAssetOptionResults a = AnalyticDividendEuropeanEngine.Calculate(put, Context, process, [new FixedDividend(2.0, Today + 61)]);
        Assert.Equal(9.486181015174774, a.NPV!.Value, 1e-12);
        Assert.Equal(0.0, (a.Rho!.Value / -52.52174422764317) - 1.0, 1e-12);
        Assert.Equal(0.0, (a.Theta!.Value / -3.3740258645611516) - 1.0, 1e-12);
    }

    // The risk-free curve 5% settling T+2 (19 October), the volatility 25% dated today, beside a dividend curve
    private static BlackScholesMertonProcess Process(YieldTermStructure dividendCurve)
    {
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        Assert.Equal(Today + 4, settling.ReferenceDate);
        return new BlackScholesMertonProcess(100.0, dividendCurve, settling, new BlackConstantVol(Today, null, 0.25, A365));
    }

    private static VanillaOption Put() =>
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));

    // The analytic engine's value, delta, gamma, theta, rho and vega on a one-year put with a 2.0 dividend
    private static void Analytic(BlackScholesMertonProcess process, Date dividendDate, double[] expected)
    {
        OneAssetOptionResults a = AnalyticDividendEuropeanEngine.Calculate(Put(), Context, process, [new FixedDividend(2.0, dividendDate)]);
        double[] actual = [a.NPV!.Value, a.Delta!.Value, a.Gamma!.Value, a.Theta!.Value, a.Rho!.Value, a.Vega!.Value];
        for (int i = 0; i < expected.Length; ++i)
        {
            Assert.Equal(0.0, (actual[i] / expected[i]) - 1.0, 1e-10);
        }
    }

    // The finite-difference engine's value on 400 x 400 under the spot and the escrowed cash-dividend models
    private static void FiniteDifference(BlackScholesMertonProcess process, Date dividendDate, double spotModel, double escrowedModel)
    {
        var fd = new MakeFdBlackScholesVanillaEngine(process).WithTGrid(400).WithXGrid(400).WithCashDividends([dividendDate], [2.0]);
        Assert.Equal(spotModel, fd.ToEngine()(Put(), Context).NPV!.Value, 1e-11);
        Assert.Equal(
            escrowedModel,
            fd.WithCashDividendModel(FdBlackScholesVanillaEngineCashDividendModel.Escrowed).ToEngine()(Put(), Context).NPV!.Value,
            1e-11);
    }
}
