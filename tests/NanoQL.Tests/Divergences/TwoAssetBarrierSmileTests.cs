// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The two-asset barrier's read of asset 2's volatility (the S-bar plan's BR5 (ix), fixed under R8; the S-exo plan's
/// ER5 class). QuantLib reads it at asset 1's strike, a level on another price scale: asset 2 at 5,000 on a smile, the
/// barrier at 4,500, a down-and-out call at strikes 90, 100 and 110 prices 6.054081, 4.647889 and 2.965222 on volatilities
/// read at 90 to 110, where the smile's volatility at the barrier is 34.37% (the planner's P2). nanoql reads it at the
/// barrier, asset 2's own level; the read is a choice inside a constant-volatility formula, so its check is that it reads
/// one level consistently (Revision 69). Checks: QuantLib's values on flat volatilities, 5.893116254296, 4.038304931649
/// and 2.557384709654 on QuantLib's Dr78 (the S-bar run's <c>c1_ta_bb.py</c>), to its six decimal places on the
/// formula's terms of weight 100 (BR4 (e)); and on the smile, the engine equals itself with asset 2's volatility flat at
/// its barrier level.
/// </summary>
public class TwoAssetBarrierSmileTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    private static readonly GeneralizedBlackScholesProcess Process1 = Process(100.0, 0.0, new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));

    private static readonly BlackVarianceSurface Smile = SmileSurface();

    [Fact]
    public void OnFlatVolatilitiesTheEngineIsQuantLibs()
    {
        double atBarrier = Smile.BlackVol(1.0, 4500.0);
        GeneralizedBlackScholesProcess flat = Process(5000.0, 0.0, new BlackConstantVol(Today, new NullCalendar(), atBarrier, A365));
        foreach ((double strike, double quantLib) in new[]
        {
            (90.0, 5.893116254296118),
            (100.0, 4.038304931648586),
            (110.0, 2.557384709653633),
        })
        {
            Assert.Equal(quantLib, Price(strike, flat), 1e-4);
        }
    }

    [Fact]
    public void OnASmileAssetTwoIsReadAtItsBarrier()
    {
        double atBarrier = Smile.BlackVol(1.0, 4500.0);
        GeneralizedBlackScholesProcess smile = Process(5000.0, 0.0, Smile);
        GeneralizedBlackScholesProcess flat = Process(5000.0, 0.0, new BlackConstantVol(Today, new NullCalendar(), atBarrier, A365));
        foreach (double strike in new[] { 90.0, 100.0, 110.0 })
        {
            Assert.Equal(Price(strike, flat), Price(strike, smile), 1e-13);
        }
    }

    private static double Price(double strike, GeneralizedBlackScholesProcess process2) =>
        AnalyticTwoAssetBarrierEngine.Calculate(
            new TwoAssetBarrierOption(BarrierType.DownOut, 4500.0, new PlainVanillaPayoff(OptionType.Call, strike), Exercise),
            Context,
            Process1,
            process2,
            0.5).NPV!.Value;

    private static GeneralizedBlackScholesProcess Process(double spot, double q, BlackVolTermStructure volatility) =>
        new BlackScholesMertonProcess(spot, new FlatForward(Today, q, A365), new FlatForward(Today, 0.05, A365), volatility);

    // asset 2's smile: 45%, 30%, 25% at 50, 100, 200 and again at 3,000, 5,000, 7,000
    private static BlackVarianceSurface SmileSurface()
    {
        double[] strikes = [50.0, 100.0, 200.0, 3000.0, 5000.0, 7000.0];
        double[] volatilities = [0.45, 0.30, 0.25, 0.45, 0.30, 0.25];
        var matrix = new Matrix(strikes.Length, 1);
        for (int i = 0; i < strikes.Length; i++)
        {
            matrix[i, 0] = volatilities[i];
        }

        return new BlackVarianceSurface(Today, new NullCalendar(), [Today + 365], strikes, matrix, A365);
    }
}
