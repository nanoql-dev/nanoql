// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Vanna-Volga engine's own reads on curves that measure time differently from its volatility (fixed). QuantLib
/// dates the volatility by the evaluation date on Actual/365 Fixed (<c>vannavolgabarrierengine.cpp:83-86</c>) and
/// prices its barrier through the analytic engine on it, but reads everything else at the quotes' time on each curve's
/// own clock (<c>:97-137</c>, <c>:194-231</c>, <c>:313</c>): on Actual/360 curves a one-year up-and-out call at 1.40,
/// strike 1.25, on a skewed smile, is 0.008044987558286639 against 0.007989598182293868 on the Actual/365 Fixed curves
/// with the same discount factors, and on curves dated at the FX spot date, two days later, the common set-up, it is
/// off its own engine on the curves restated from the evaluation date by 9.1e-5 to 2.8e-2, the down-and-in most
/// (QuantLib-Python 1.44). nanoql reads each curve at the instant the quotes' time denotes on the volatility's clock,
/// nothing accrued before the curve's own date (the playbook's row for structures on another clock): its discount
/// factors, and the touch probability's zero rates, the logarithms of those discounts over the time, as the analytic
/// engine's are read. Where the curves are dated today on Actual/365 Fixed the reads are QuantLib's, with its bits.
/// QuantLib's check: its own engine on the curves restated from the evaluation date on Actual/365 Fixed (a curve dated
/// later a discount curve at 1 up to its date), where the quotes' time is the exercise's and where it isn't.
/// </summary>
public class VannaVolgaMismatchedTimesTests
{
    // (type, barrier, strike, QuantLib on the Actual/365 Fixed curves; on the Actual/360 curves it gives
    // 0.008044987558286639, 0.09919150717823703, 0.04469676865643372 and 0.017908185469386995)
    private static readonly (BarrierType Type, double Barrier, double Strike, double QuantLib)[] Cases =
    [
        (BarrierType.UpOut, 1.40, 1.25, 0.007989598182293868),
        (BarrierType.UpIn, 1.40, 1.25, 0.09947006283561127),
        (BarrierType.DownOut, 1.20, 1.35, 0.0447030841634671),
        (BarrierType.DownIn, 1.20, 1.25, 0.018099703182839266),
    ];

    [Fact]
    public void OnAct360CurvesTheEngineIsQuantLibsOnTheEquivalentCurves()
    {
        foreach (var c in Cases)
        {
            Assert.Equal(c.QuantLib, VannaVolgaOnSkew(c.Type, c.Barrier, c.Strike, act360: true), 1e-15);
        }
    }

    [Fact]
    public void OnAct365FixedCurvesTheEngineIsQuantLibsOwn()
    {
        foreach (var c in Cases)
        {
            Assert.Equal(c.QuantLib, VannaVolgaOnSkew(c.Type, c.Barrier, c.Strike, act360: false), 1e-15);
        }
    }

    [Fact]
    public void OnCurvesDatedAtTheSpotDateTheEngineIsQuantLibsOnTheRestatedCurves()
    {
        // the quotes' time the exercise's, a year; on the curves as given QuantLib gives 0.007955714179353554,
        // 0.09928078055717013, 0.04446714751331642 and 0.006352535340297234
        CheckOnSpotDateCurves(
            new Date(15, October, 2027),
            [0.007999413622177456, 0.09906123713385305, 0.044462402048149774, 0.006233784136876662],
            1e-15);
    }

    [Fact]
    public void WhereTheQuotesTimeIsNotTheExercisesTheEngineIsQuantLibsOnTheRestatedCurves()
    {
        // the quotes' time a year, the exercise in 300 days; on the curves as given QuantLib gives
        // 0.010228331539650313, 0.09700816319687336, 0.03881607512453363 and 0.012003607729080025. The barrier prices
        // inside agree with QuantLib's to a few ulps, and the engine's greeks, second differences over shifts of 1e-4,
        // carry a last bit into the eleventh decimal: QuantLib's own value moves by up to 1.8e-11 with the spot one ulp
        // up or down
        CheckOnSpotDateCurves(
            new Date(11, August, 2027),
            [0.010283421567759268, 0.09677722918827124, 0.03881252624415442, 0.011883659940872017],
            2e-11);
    }

    // A year from 15 June 2026, spot 1.30; spot-delta quotes, the put 13%, the delta-neutral ATM 12%, the call 11.5%;
    // domestic 3% and foreign 1% on Actual/360 curves, or the same discount factors on Actual/365 Fixed ones.
    private static double VannaVolgaOnSkew(BarrierType type, double barrier, double strike, bool act360)
    {
        var today = new Date(15, June, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = act360 ? new Actual360() : new Actual365Fixed();
        double scale = act360 ? 1.0 : 365.0 / 360.0;
        return VannaVolgaBarrierEngine.Calculate(
            new BarrierOption(
                type, barrier, 0.0, new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(today + 365)),
            context,
            new DeltaVolQuote(0.12, DeltaVolQuoteDeltaType.Spot, 1.0, DeltaVolQuoteAtmType.AtmDeltaNeutral),
            new DeltaVolQuote(-0.25, 0.13, 1.0, DeltaVolQuoteDeltaType.Spot),
            new DeltaVolQuote(0.25, 0.115, 1.0, DeltaVolQuoteDeltaType.Spot),
            1.30,
            new FlatForward(today, 0.03 * scale, dc),
            new FlatForward(today, 0.01 * scale, dc)).NPV!.Value;
    }

    // From 15 October 2026, spot 1.30, the same quotes at a year; domestic 3% and foreign 1% on Actual/365 Fixed curves
    // dated 19 October: the up-and-out and up-and-in calls at 1.40, strike 1.25, the down-and-out and down-and-in at
    // 1.20, strike 1.35, against QuantLib's engine on the curves restated from 15 October, in that order.
    private static void CheckOnSpotDateCurves(Date exerciseDate, double[] quantLib, double tolerance)
    {
        var today = new Date(15, October, 2026);
        var spotDate = new Date(19, October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        (BarrierType Type, double Barrier, double Strike)[] options =
        [
            (BarrierType.UpOut, 1.40, 1.25),
            (BarrierType.UpIn, 1.40, 1.25),
            (BarrierType.DownOut, 1.20, 1.35),
            (BarrierType.DownIn, 1.20, 1.35),
        ];
        for (int i = 0; i < options.Length; i++)
        {
            double value = VannaVolgaBarrierEngine.Calculate(
                new BarrierOption(
                    options[i].Type,
                    options[i].Barrier,
                    0.0,
                    new PlainVanillaPayoff(OptionType.Call, options[i].Strike),
                    new EuropeanExercise(exerciseDate)),
                context,
                new DeltaVolQuote(0.12, DeltaVolQuoteDeltaType.Spot, 1.0, DeltaVolQuoteAtmType.AtmDeltaNeutral),
                new DeltaVolQuote(-0.25, 0.13, 1.0, DeltaVolQuoteDeltaType.Spot),
                new DeltaVolQuote(0.25, 0.115, 1.0, DeltaVolQuoteDeltaType.Spot),
                1.30,
                new FlatForward(spotDate, 0.03, new Actual365Fixed()),
                new FlatForward(spotDate, 0.01, new Actual365Fixed())).NPV!.Value;
            Assert.Equal(quantLib[i], value, tolerance);
        }
    }
}
