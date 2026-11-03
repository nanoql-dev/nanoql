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
/// The forward-start family's vega off a flat volatility. QuantLib's forward engine returns the inner engine's vega on
/// the implied volatility from the reset (<c>forwardengine.hpp:160-161</c>) and the cliquet and performance engines
/// Black's per period (<c>analyticcliquetengine.cpp:106-107</c>, <c>analyticperformanceengine.cpp:106-107</c>): each
/// the derivative in the period's forward volatility σ_f, where a parallel shift ε of the volatilities moves it by
/// (σ₂t₂ − σ₁t₁)/(σ_f·Δt)·ε. On a variance curve with its nodes at the dates, rising (0.10, 0.30, 0.30 at one, two and
/// three years), QuantLib's forward vega misses its own price under that shift by −17.5%, the cliquet's by −9.6%, the
/// performance engine's by −9.8% and the quanto forward's by −18.4%; falling (0.30, 0.25, 0.25), by −6.5%, −3.3%,
/// −3.4% and −6.8%. nanoql scales each period's vega by that factor: exactly 1 where the period's two volatilities are
/// equal, zero among them, so QuantLib's bits stand there; at a zero forward variance between unequal volatilities the
/// one-sided limit, infinite at the forward and 0 off it. The checks: QuantLib's prices under a parallel shift of 1e-6
/// each way (its tests' bump), the forward-performance engine, which the wheel lacks, against its own; on a flat zero
/// volatility, QuantLib's bits; at equal total variances, the one-sided bumps.
/// </summary>
public class ForwardVegaTermStructureTests
{
    private static readonly Date D1 = Today + 365, D2 = Today + 730, D3 = Today + 1095;
    private static readonly double[] RisingVols = [0.10, 0.30, 0.30], FallingVols = [0.30, 0.25, 0.25];

    [Fact]
    public void TheForwardVegaIsThePriceUnderAParallelShift()
    {
        // QuantLib's value and its price under the shift: rising, then falling
        Check(RisingVols, Forward, 17.262648930016802, 45.61177168405095);
        Check(FallingVols, Forward, 8.73765681866499, 40.47826673225785);
    }

    [Fact]
    public void TheCliquetVegaIsThePriceUnderAParallelShift()
    {
        Check(RisingVols, Cliquet, 30.15337623548772, 83.1358063777543);
        Check(FallingVols, Cliquet, 19.75073546620427, 78.04876616290812);
    }

    [Fact]
    public void ThePerformanceVegasAreThePricesUnderAParallelShift()
    {
        Check(RisingVols, Performance, 0.2889249033974931, 0.7960254514161047);
        Check(FallingVols, Performance, 0.188511469091796, 0.7466451702464738);

        // the forward-performance engine against its own price under the shift
        foreach (double[] vols in new[] { RisingVols, FallingVols })
        {
            const double h = 1e-6;
            double bumped = (ForwardPerformance(Market(vols, h)).NPV!.Value
                - ForwardPerformance(Market(vols, -h)).NPV!.Value) / (2.0 * h);
            double vega = ForwardPerformance(Market(vols)).Vega!.Value;
            Assert.Equal(bumped, vega, 1e-6 * bumped);
        }
    }

    [Fact]
    public void TheQuantoForwardVegaIsThePriceUnderAParallelShift()
    {
        Check(RisingVols, QuantoForward, 14.914620354156636, 41.865932765006164);
        Check(FallingVols, QuantoForward, 7.087103601275305, 37.57510059809732);
    }

    [Fact]
    public void AFlatZeroVolatilityKeepsQuantLibsBits()
    {
        GeneralizedBlackScholesProcess p = Flat(100.0, 0.05, 0.02, 0.0);
        var forward = new ForwardVanillaOption(
            0.9, Today + 182, new PlainVanillaPayoff(OptionType.Call, 0.0), new EuropeanExercise(Today + 365));
        OneAssetOptionResults r = ForwardVanillaEngine.Calculate(forward, Context, p, AnalyticEuropeanEngine.Calculate);
        Assert.Equal(11.118951211403292, r.NPV);
        Assert.Equal(0.0, r.Vega);

        var cliquet = new CliquetOption(
            new PercentageStrikePayoff(OptionType.Call, 0.9), new EuropeanExercise(Today + 365),
            [Today + 91, Today + 182, Today + 273]);
        r = AnalyticCliquetEngine.Calculate(cliquet, Context, p);
        Assert.Equal(31.543503967941007, r.NPV);
        Assert.Equal(0.0, r.Vega);
        r = AnalyticPerformanceEngine.Calculate(cliquet, Context, p);
        Assert.Equal(0.31076462613660516, r.NPV);
        Assert.Equal(0.0, r.Vega);
    }

    [Fact]
    public void AtEqualTotalVariancesTheVegaIsTheOneSidedLimit()
    {
        // 0.4 at a year and 0.2 at four, the total variances equal to the last bit; r = q, so moneyness 1 is the
        // forward. QuantLib's vega is 0 at every moneyness (Black's at a zero deviation) and its values 9.2312, 0, 0.
        foreach ((double moneyness, double value) in new[] { (0.9, 9.231163463866357), (1.0, 0.0), (1.1, 0.0) })
        {
            OneAssetOptionResults r = EqualVariances(moneyness, 0.0);
            Assert.Equal(value, r.NPV!.Value, 1e-14 * value);
            double[] bumps = [.. new[] { 1e-4, 1e-6, 1e-8 }.Select(h => (EqualVariances(moneyness, h).NPV!.Value - r.NPV!.Value) / h)];
            if (moneyness == 1.0)
            {
                // QuantLib's one-sided bumps 3,294.5, 32,939 and 329,391: growing as 1/√h, so the limit is infinite
                Assert.Equal(3294.5148352791134, bumps[0], 1e-6 * bumps[0]);
                Assert.Equal(32939.143375060325, bumps[1], 1e-6 * bumps[1]);
                Assert.Equal(329390.83320882276, bumps[2], 1e-6 * bumps[2]);
                Assert.Equal(double.PositiveInfinity, r.Vega);
            }
            else
            {
                // QuantLib's one-sided bumps 0 at 1e-6 and 1e-8
                Assert.Equal(0.0, bumps[1]);
                Assert.Equal(0.0, bumps[2]);
                Assert.Equal(0.0, r.Vega);
            }
        }
    }

    // nanoql's vega against QuantLib's value and its price under a parallel shift.
    private static void Check(
        double[] vols, Func<GeneralizedBlackScholesProcess, OneAssetOptionResults> price, double value, double shifted)
    {
        OneAssetOptionResults r = price(Market(vols));
        Assert.Equal(value, r.NPV!.Value, 1e-12 * value);
        Assert.Equal(shifted, r.Vega!.Value, 1e-6 * shifted);
    }

    private static GeneralizedBlackScholesProcess Market(double[] vols, double shift = 0.0) =>
        new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.01, A365),
            new FlatForward(Today, 0.04, A365),
            new BlackVarianceCurve(Today, [D1, D2, D3], vols.Select(v => v + shift), A365, false));

    private static OneAssetOptionResults Forward(GeneralizedBlackScholesProcess p) =>
        ForwardVanillaEngine.Calculate(ForwardCall(), Context, p, AnalyticEuropeanEngine.Calculate);

    private static OneAssetOptionResults ForwardPerformance(GeneralizedBlackScholesProcess p) =>
        ForwardPerformanceVanillaEngine.Calculate(ForwardCall(), Context, p, AnalyticEuropeanEngine.Calculate);

    private static OneAssetOptionResults Cliquet(GeneralizedBlackScholesProcess p) =>
        AnalyticCliquetEngine.Calculate(TwoPeriods(), Context, p);

    private static OneAssetOptionResults Performance(GeneralizedBlackScholesProcess p) =>
        AnalyticPerformanceEngine.Calculate(TwoPeriods(), Context, p);

    private static OneAssetOptionResults QuantoForward(GeneralizedBlackScholesProcess p) =>
        QuantoEngine.Calculate(
            new QuantoForwardVanillaOption(1.0, D1, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(D2)),
            Context,
            p,
            new FlatForward(Today, 0.02, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.10, A365),
            0.3,
            (o, c, q) => ForwardVanillaEngine.Calculate(o, c, q, AnalyticEuropeanEngine.Calculate));

    private static ForwardVanillaOption ForwardCall() =>
        new(1.0, D1, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(D2));

    private static CliquetOption TwoPeriods() =>
        new(new PercentageStrikePayoff(OptionType.Call, 1.0), new EuropeanExercise(D3), [D1, D2]);

    private static OneAssetOptionResults EqualVariances(double moneyness, double shift)
    {
        var p = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.02, A365),
            new FlatForward(Today, 0.02, A365),
            new BlackVarianceCurve(
                Today, [Today + 365, Today + 1460, Today + 1825], [0.4 + shift, 0.2 + shift, 0.2 + shift], A365, false));
        var option = new ForwardVanillaOption(
            moneyness, Today + 365, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 1460));
        return ForwardVanillaEngine.Calculate(option, Context, p, AnalyticEuropeanEngine.Calculate);
    }
}
