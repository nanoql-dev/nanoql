// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The FD Heston engine's equity grid at high carry and low volatility. QuantLib's engine builds its equity mesher on a
/// helper process whose curves its call binds in each other's places (<c>fdhestonvanillaengine.cpp:126-128</c>), so the
/// mesher walks the forward's mirror about the spot. Where the carry is large against the volatility the grid's far
/// edge falls inside the distribution and QuantLib's price stops converging: a five-year call at 1.25 times the
/// forward, carry 0.30 and volatility 0.1, prices 0 on every grid, where the analytic engine gives 1.5732
/// (QuantLib-Python 1.44); where cash dividends take the mirrored walk below zero, its bound isn't a number and
/// QuantLib's engine refuses ("end must be larger than start"). nanoql keeps QuantLib's mesher wherever its range holds
/// the true forwards' band, the walk's ends on the helper's curves in place widened by the volatility's quantile, half
/// the mesher's own margin, and widens it to that band through QuantLib's own constraints where it doesn't, a bound
/// that isn't a number falling short. The multi-strike solve, whose mesher takes no constraints, solves each strike
/// alone where its grid doesn't hold the band at the points the solve is read at, S·K/Kᵢ for the option's strike K,
/// their forwards quanto-adjusted where a quanto helper is given; the two paths run on different grids, so a strike's
/// value steps where the switch moves, by up to about 0.4% at carry 0.06. The band holds the strike too, widened by the
/// same half margin, where an option far from the forwards takes its value. The check: QuantLib's components given
/// those constraints through the harness (<c>tests/vectors/cxx/pricingengines/vanilla/fdhestonvanillaengine.json</c>),
/// and the analytic engine's price under refinement.
/// </summary>
public class FdHestonHighCarryTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AHighCarryCallConvergesToTheAnalyticEngine()
    {
        // QuantLib 1.44's engine prices 0.0 on x-grids of 100, 200 and 400 (QuantLib-Python 1.44)
        (HestonModel model, VanillaOption call) = Market(5, 0.31, 0.01, 1.25, OptionType.Call);
        const double Analytic = 1.5731592765498104;
        Assert.Equal(Analytic, AnalyticHestonEngine.Calculate(call, Context, model, 192).NPV!.Value);
        AssertConverges(model, call, Analytic, 2.5e-2);
    }

    [Fact]
    public void ANegativeCarryPutConvergesToTheAnalyticEngine()
    {
        // QuantLib 1.44's engine prices 0.43108461265905135, 0.4192698886268002 and 0.4140697500981558 on x-grids of
        // 100, 200 and 400, its error growing from -15% to -19% (QuantLib-Python 1.44)
        (HestonModel model, VanillaOption put) = Market(10, 0.01, 0.21, 0.8, OptionType.Put);
        const double Analytic = 0.5091548969691079;
        Assert.Equal(Analytic, AnalyticHestonEngine.Calculate(put, Context, model, 192).NPV!.Value);
        AssertConverges(model, put, Analytic, 3e-3);
    }

    [Fact]
    public void AStrikeBeyondTheForwardsBandKeepsItsTail()
    {
        // a year's put at 0.7 of the forward, carry -0.3: QuantLib's grid holds the forwards' band but the strike sits
        // near its edge, and QuantLib 1.44's engine stalls, 0.0066016341928117825, 0.006176625089392059 and
        // 0.006063108322089414 on grids of 100 x 100 x 50, 200 x 200 x 100 and 400 x 400 x 100, +7.2%, +0.27% and -1.6%
        // (QuantLib-Python 1.44); nanoql's band holds the strike too
        (HestonModel model, VanillaOption put) = Market(1, 0.01, 0.31, 0.7, OptionType.Put);
        const double Analytic = 0.006160366199509028;
        Assert.Equal(Analytic, AnalyticHestonEngine.Calculate(put, Context, model, 192).NPV!.Value);
        double[] errors =
        [
            .. new[] { (100, 100, 50), (200, 200, 100), (400, 400, 100) }.Select(g => Math.Abs(
                (FdHestonVanillaEngine.Calculate(put, Context, model, g.Item1, g.Item2, g.Item3).NPV!.Value / Analytic) - 1.0)),
        ];
        Assert.True(errors[1] < 0.5 * errors[0] && errors[2] < 0.5 * errors[1], string.Join(", ", errors));
        Assert.True(errors[2] < 1.5e-2, string.Join(", ", errors));
    }

    [Fact]
    public void AMarketInsideTheBandKeepsQuantLibsBits()
    {
        // carry 0.05 at volatility 0.1 over five years: QuantLib's grid holds the band (QuantLib-Python 1.44)
        (HestonModel model, VanillaOption call) = Market(5, 0.06, 0.01, 1.25, OptionType.Call);
        OneAssetOptionResults r = FdHestonVanillaEngine.Calculate(call, Context, model);
        Assert.Equal(1.5898858597412422, r.NPV);
        Assert.Equal(0.16868030597327788, r.Delta);
        Assert.Equal(0.01277798741770898, r.Gamma);
        Assert.Equal(-1.2871379865544734, r.Theta);
    }

    [Fact]
    public void TheMultiStrikeSolveTakesEachStrikeAloneWhereItsGridFallsShort()
    {
        (HestonModel model, VanillaOption reference) = Market(5, 0.31, 0.01, 1.0, OptionType.Call);
        double f = ((PlainVanillaPayoff)reference.Payoff!).Strike;
        double[] strikes = [0.8 * f, f, 1.25 * f];
        ImmutableArray<OneAssetOptionResults> results = FdHestonVanillaEngine.CalculateStrikes(reference, strikes, Context, model);
        for (int i = 0; i < strikes.Length; ++i)
        {
            var single = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strikes[i]), reference.Exercise);
            OneAssetOptionResults alone = FdHestonVanillaEngine.Calculate(single, Context, model);
            Assert.Equal(alone.NPV, results[i].NPV);
            Assert.Equal(alone.Delta, results[i].Delta);
            Assert.Equal(alone.Gamma, results[i].Gamma);
            Assert.Equal(alone.Theta, results[i].Theta);
        }
    }

    [Fact]
    public void AReferenceStrikeAwayFromTheSpotTakesEachStrikeAlone()
    {
        // QuantLib 1.44's multi-strike solve, its grid sized on S^2/K_i, prices 0 for every strike where the solve is
        // read at S K/K_i, the analytic engine giving 0.2507, 0.0067 and 5.6e-5
        var rts = new FlatForward(Today, 0.06, A365);
        var qts = new FlatForward(Today, 0.04, A365);
        var model = new HestonModel(new HestonProcess(rts, qts, 100.0, 0.01, 1.5, 0.01, 0.1, -0.5));
        var exercise = new EuropeanExercise(Today + new Period(5, TimeUnit.Years));
        var reference = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 200.0), exercise);
        double[] strikes = [160.0, 200.0, 250.0];
        ImmutableArray<OneAssetOptionResults> results = FdHestonVanillaEngine.CalculateStrikes(reference, strikes, Context, model);
        for (int i = 0; i < strikes.Length; ++i)
        {
            var single = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strikes[i]), exercise);
            Assert.Equal(FdHestonVanillaEngine.Calculate(single, Context, model).NPV, results[i].NPV);
        }

        var call160 = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 160.0), exercise);
        double analytic = AnalyticHestonEngine.Calculate(call160, Context, model, 192).NPV!.Value;
        Assert.InRange(results[0].NPV!.Value / analytic, 0.98, 1.02);
    }

    [Fact]
    public void AQuantoForwardFarFromTheDomesticTakesEachStrikeAlone()
    {
        // a foreign rate of 0.30 against a domestic 0.05 over five years: the multi-strike solve, its band read without
        // the quanto adjustment, misses by -10%, -5.0% and -1.7% on the default grid; at an equity-FX correlation of 0
        // the analytic engine on the dividend curve q + r - r_f is exact
        var rts = new FlatForward(Today, 0.05, A365);
        var qts = new FlatForward(Today, 0.01, A365);
        var fts = new FlatForward(Today, 0.30, A365);
        var model = new HestonModel(new HestonProcess(rts, qts, 100.0, 0.01, 1.5, 0.01, 0.1, -0.5));
        var quanto = new FdmQuantoHelper(rts, fts, new BlackConstantVol(Today, new NullCalendar(), 0.1, A365), 0.0, 1.0);
        var exercise = new EuropeanExercise(Today + new Period(5, TimeUnit.Years));
        var reference = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), exercise);
        double[] strikes = [80.0, 100.0, 125.0];
        ImmutableArray<OneAssetOptionResults> results =
            FdHestonVanillaEngine.CalculateStrikes(reference, strikes, Context, model, [], quanto);

        var adjusted = new HestonModel(new HestonProcess(rts, new FlatForward(Today, 0.01 + 0.05 - 0.30, A365), 100.0, 0.01, 1.5, 0.01, 0.1, -0.5));
        for (int i = 0; i < strikes.Length; ++i)
        {
            var single = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strikes[i]), exercise);
            Assert.Equal(FdHestonVanillaEngine.Calculate(single, Context, model, [], quanto).NPV, results[i].NPV);

            double analytic = AnalyticHestonEngine.Calculate(single, Context, adjusted, 192).NPV!.Value;
            Assert.InRange(results[i].NPV!.Value / analytic, 0.995, 1.005);
        }
    }

    [Fact]
    public void CashDividendsTakingTheMirroredWalkBelowZeroPrice()
    {
        // QuantLib 1.44's engine refuses every call and put at 0.8, 1 and 1.25 times the dividend forward: "end must be
        // larger than start"
        var rts = new FlatForward(Today, 0.31, A365);
        var qts = new FlatForward(Today, 0.01, A365);
        var model = new HestonModel(new HestonProcess(rts, qts, 100.0, 0.01, 1.5, 0.01, 0.1, -0.5));
        Date maturity = Today + new Period(6, TimeUnit.Years);
        ImmutableArray<Dividend> dividends =
            [.. Enumerable.Range(1, 5).Select(y => (Dividend)new FixedDividend(8.0, Today + new Period(y, TimeUnit.Years)))];
        // S e^{(r-q)T} - sum D_i e^{(r-q)(T-t_i)}
        double forward = (100.0 - dividends.Sum(d => 8.0 * rts.Discount(d.Date) / qts.Discount(d.Date)))
            * qts.Discount(maturity) / rts.Discount(maturity);
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, forward), new EuropeanExercise(maturity));
        double[] npv =
        [
            .. new[] { 100, 200, 400 }.Select(x => FdHestonVanillaEngine.Calculate(call, Context, model, dividends, 100, x, 50).NPV!.Value),
        ];
        Assert.All(npv, v => Assert.True(double.IsFinite(v) && v > 0.0, string.Join(", ", npv)));
        Assert.True(Math.Abs(npv[2] - npv[1]) < 0.5 * Math.Abs(npv[1] - npv[0]), string.Join(", ", npv));
    }

    // v0 = theta 0.01, kappa 1.5, sigma 0.1, rho -0.5, spot 100; the strike a multiple of the forward
    private static (HestonModel Model, VanillaOption Option) Market(int years, double r, double q, double k, OptionType type)
    {
        var rts = new FlatForward(Today, r, A365);
        var qts = new FlatForward(Today, q, A365);
        var model = new HestonModel(new HestonProcess(rts, qts, 100.0, 0.01, 1.5, 0.01, 0.1, -0.5));
        Date maturity = Today + new Period(years, TimeUnit.Years);
        double forward = 100.0 * qts.Discount(maturity) / rts.Discount(maturity);
        return (model, new VanillaOption(new PlainVanillaPayoff(type, k * forward), new EuropeanExercise(maturity)));
    }

    // the error against the analytic engine at least halves with each doubling of the x-grid, within the bound at 400
    private static void AssertConverges(HestonModel model, VanillaOption option, double analytic, double bound)
    {
        double[] errors =
        [
            .. new[] { 100, 200, 400 }.Select(x => Math.Abs(
                (FdHestonVanillaEngine.Calculate(option, Context, model, 100, x, 50).NPV!.Value / analytic) - 1.0)),
        ];
        Assert.True(errors[1] < 0.5 * errors[0] && errors[2] < 0.5 * errors[1], string.Join(", ", errors));
        Assert.True(errors[2] < bound, string.Join(", ", errors));
    }
}
