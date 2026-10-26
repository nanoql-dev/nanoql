// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The quanto engine's derived Greeks (C3's review, B3-2). QuantLib takes the rho, vega, qrho, qvega and qlambda from
/// the inner dividend rho by one factor at expiry each (<c>quantoengine.hpp:140-171</c>), assuming the input moves the
/// quanto zero rate in parallel. With the dividend curve on Act/360, whose clock the quanto curve keeps, a shift of the
/// risk-free rate moves it by the ratio of the two curves' times: rho −18.4754 where the price under the shift moves
/// −17.2347, the quanto Greeks 1.4% off. For a forward-start quanto, whose forward engine reads the quanto curve at the
/// reset and at expiry, an input whose contribution varies in time moves the two unevenly: on a volatility curve
/// (0.10, 0.20, 0.30 at one, two and three years) QuantLib's qvega and qlambda are 25.5% short of its own price bumped
/// in the FX volatility and the correlation, and under an FX volatility curve the vega 3.5% over. nanoql reads each
/// input's change to the quanto curve's log discount by date at the dates the inner engine reads (ER5 (xxiv), R8). The
/// checks: on Act/360 dividends, the market restated onto Act/365, where QuantLib's formula is exact; on the curves,
/// QuantLib's own prices bumped by 1e-5 each way in each input.
/// </summary>
public class QuantoGreeksTermStructureTests
{
    private static readonly Date[] Nodes = [Today, Today + 365, Today + 730, Today + 1095];
    private static readonly QuantoVanillaOption European = new(
        new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 730));

    private static readonly QuantoForwardVanillaOption Forward = new(
        1.0, Today + 365, new PlainVanillaPayoff(OptionType.Call, 0.0), new EuropeanExercise(Today + 730));

    [Fact]
    public void ADividendCurveOnItsOwnDaysMovesTheDriftByDate()
    {
        // r 5%, q 3% on Act/360, σ 20%, the foreign rate 2%, the FX volatility 10%, ρ 0.3; restated, q·365/360 on Act/365
        GeneralizedBlackScholesProcess onAct360 = Process(new FlatForward(Today, 0.03, new Actual360()), Flat(0.05), Vol(0.20));
        GeneralizedBlackScholesProcess restated = Process(Flat(0.03 * 365.0 / 360.0), Flat(0.05), Vol(0.20));
        foreach (Func<GeneralizedBlackScholesProcess, QuantoOptionResults> price in new Func<GeneralizedBlackScholesProcess, QuantoOptionResults>[]
        {
            p => QuantoEngine.Calculate(European, Context, p, Flat(0.02), Vol(0.10), 0.3, AnalyticEuropeanEngine.Calculate),
            p => QuantoEngine.Calculate(Forward, Context, p, Flat(0.02), Vol(0.10), 0.3, ForwardEngine),
        })
        {
            QuantoOptionResults expected = price(restated), actual = price(onAct360);
            Assert.Equal(expected.NPV!.Value, actual.NPV!.Value, 1e-12 * expected.NPV!.Value);
            foreach (Func<QuantoOptionResults, double?> greek in new Func<QuantoOptionResults, double?>[]
            {
                r => r.Rho, r => r.Vega, r => r.Qrho, r => r.Qvega, r => r.Qlambda,
            })
            {
                double e = greek(expected)!.Value;
                Assert.Equal(e, greek(actual)!.Value, 1e-10 * Math.Max(1.0, Math.Abs(e)));
            }
        }
    }

    [Fact]
    public void AForwardStartQuantoOnAVolatilityCurveMeetsQuantLibsBumps()
    {
        // rising zero curves (r 2% to 5%, q 0 to 3%, the foreign rate 1% to 2.5%) under a volatility curve; QuantLib's
        // value and its price bumped in each input
        var vol = new BlackVarianceCurve(Today, Nodes[1..], [0.10, 0.20, 0.30], A365);
        GeneralizedBlackScholesProcess p = Process(Zero(0.0, 0.01, 0.02, 0.03), Zero(0.02, 0.03, 0.04, 0.05), vol);
        QuantoOptionResults r = QuantoEngine.Calculate(
            Forward, Context, p, Zero(0.01, 0.015, 0.02, 0.025), Vol(0.10), 0.3, ForwardEngine);
        Assert.Equal(9.037437869690141, r.NPV!.Value, 1e-12 * 9.037437869690141);
        Assert.Equal(-18.07487573994848, r.Rho!.Value, 1e-6);
        Assert.Equal(39.404209055327044, r.Vega!.Value, 1e-6);
        Assert.Equal(57.52785222821188, r.Qrho!.Value, 1e-6);
        Assert.Equal(-4.635260426066878, r.Qvega!.Value, 1e-6);
        Assert.Equal(-1.545086808807383, r.Qlambda!.Value, 1e-6);
    }

    [Fact]
    public void AForwardStartQuantoOnAnExchangeRateVolatilityCurveMeetsQuantLibsBumps()
    {
        // r 5%, q 3%, σ 20%, the foreign rate 2%, the FX volatility 0.05 at a year and 0.15 at two
        var fx = new BlackVarianceCurve(Today, [Today + 365, Today + 730], [0.05, 0.15], A365);
        QuantoOptionResults r = QuantoEngine.Calculate(
            Forward, Context, Process(Flat(0.03), Flat(0.05), Vol(0.20)), Flat(0.02), fx, 0.3, ForwardEngine);
        Assert.Equal(5.978663261498028, r.NPV!.Value, 1e-12 * 5.978663261498028);
        Assert.Equal(-11.957326523148735, r.Rho!.Value, 1e-6);
        Assert.Equal(31.449804423822055, r.Vega!.Value, 1e-6);
        Assert.Equal(48.664893112926315, r.Qrho!.Value, 1e-6);
        Assert.Equal(-2.919893585451305, r.Qvega!.Value, 1e-6);
        Assert.Equal(-2.1940981243417923, r.Qlambda!.Value, 1e-6);
    }

    private static OneAssetOptionResults ForwardEngine(
        ForwardVanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process) =>
        ForwardVanillaEngine.Calculate(option, context, process, AnalyticEuropeanEngine.Calculate);

    private static FlatForward Flat(double rate) => new(Today, rate, A365);

    private static BlackConstantVol Vol(double v) => new(Today, new NullCalendar(), v, A365);

    private static ZeroCurve Zero(params double[] rates) => new(Nodes, rates, A365);

    private static GeneralizedBlackScholesProcess Process(
        YieldTermStructure dividends, YieldTermStructure riskFree, BlackVolTermStructure vol) =>
        new BlackScholesMertonProcess(100.0, dividends, riskFree, vol);
}
