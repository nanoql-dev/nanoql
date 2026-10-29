// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The perturbative barrier engine's distribution helpers and second-order derivatives (the S-xeq plan's XER5 (i), (ii)
/// and XER3 (a), fixed under R8; the helpers internal, read through reflection). QuantLib's port of Genz's TVPACK has
/// three lines that aren't Genz's: Plackett's integrand takes √(BA − R·BB) where Genz squares it
/// (<c>perturbativebarrieroptionengine.cpp:1260</c>; upstream's own fix after v1.44, #2935, which re-caches QuantLib's
/// two order-2 cases at 0.8943754 where v1.44 cached its defective code's 0.8943769); the Student branch belongs to the
/// degrees-of-freedom test, where QuantLib's attaches it to the normal integrand's cut (<c>:1261-1268</c>), so a normal
/// integrand past the cut returns 0.5 or NaN, not 0; and the bivariate normal's |ρ| ≥ 0.925 branch takes (1 − x²)²
/// where Genz's <c>BVND</c> takes √(1 − x²) (<c>:1398</c>). QuantLib's trivariate normal misses an independent one by
/// up to 0.0129 at six points (0.247037493575 against 0.253775665773, 0.051555378193 against 0.049633455514, 0 against
/// 0.012895124168, 0 against 0.006207519614, 0.144908828458 against 0.144207845916, 0.001686899450 against
/// 0.001348181042) and by up to 0.92 at 1,500 random ones; QuantLib's bivariate normal misses QuantLib's own We04DP by
/// up to 5.1e-3 [executed: the planner's <c>xp_pert.cpp</c>, <c>xp_pert2935.cpp</c>, <c>xp_pert_nd2.cpp</c>,
/// <c>xp_tvtl_time.cpp</c>]. The second-order term's derivative helpers aren't their functions' derivatives in a: the F
/// helper's factor reads τ − p where its function reads τ − s, and the E helper's two normal limits carry a stray inner
/// square root and its x-derivative of Φ₂(x, y) − Φ(x) the wrong sign. nanoql takes Genz's three lines and derives the
/// helpers term by term from ∂Φ₂(h, k; ρ)/∂h = φ(h) Φ((k − ρh)/√(1 − ρ²)). Checks: We04DP; a trivariate normal
/// integrated on 50 panels of 64-point Gauss-Legendre; five-point differences; the finite-difference barrier engine;
/// S-rx's analytic barrier engine on a flat market.
/// </summary>
public class PerturbativeBarrierEngineTests
{
    private static readonly Type Engine = typeof(PerturbativeBarrierOptionEngine);
    private static readonly Func<double, double, double, double> Nd2 = Helper<Func<double, double, double, double>>("Nd2");
    private static readonly TvtlFunction Tvtl = Helper<TvtlFunction>("Tvtl");

    private delegate double TvtlFunction(int nu, ReadOnlySpan<double> limit, ReadOnlySpan<double> sigmarho, double epsi);

    [Theory]
    [InlineData(1000, 100)] // full resolution: 0.8943757367 (this run's record)
    [InlineData(100, 10)] // fast: 0.8943756216
    public void OrderTwoIsQuantLibMastersCacheNotItsV144Cache(int npoint, int npoint2)
    {
        // barrieroption.cpp's market: an up-and-out put at 101 under a barrier at 101, rates 3% and 2% on Actual360, a
        // variance curve at 10.5% and 11%; QuantLib's master caches #2935's 0.8943754, v1.44 its defective 0.8943769
        var today = new Date(15, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.02, dc), new FlatForward(today, 0.03, dc),
            new BlackVarianceCurve(today, [today + 90, today + 180], [0.105, 0.11], dc));
        var option = new BarrierOption(
            BarrierType.UpOut, 101.0, 0.0, new PlainVanillaPayoff(OptionType.Put, 101.0), new EuropeanExercise(today + 180));
        double value = PerturbativeBarrierOptionEngine.Calculate(option, context, process, 2, false, npoint, npoint2).NPV!.Value;
        Assert.True(Math.Abs(value - 0.8943754) <= 1e-6, $"{value} against #2935's 0.8943754");
        Assert.True(Math.Abs(value - 0.8943769) > 1e-6, $"{value} against v1.44's 0.8943769");
    }

    [Fact]
    public void TheBivariateNormalIsWe04DPPastPointNineTwoFive()
    {
        // ND2(a, b, ρ) = P(X > a, Y > b) = We04DP(ρ)(−a, −b); QuantLib's (1 − x²)² misses by up to 5.1e-3 (0.435497
        // against 0.440097 at ρ 0.93, h = k = 0)
        double[] limits = [-4.0, -2.0, -0.5, 0.0, 0.5, 2.0, 4.0];
        double worst = 0.0;
        foreach (double rho in new[] { 0.925, 0.93, 0.95, 0.975, 0.99, 0.999, -0.925, -0.95, -0.99, -0.999 })
        {
            var we04 = new BivariateCumulativeNormalDistributionWe04DP(rho);
            foreach (double h in limits)
            {
                foreach (double k in limits)
                {
                    worst = Math.Max(worst, Math.Abs(Nd2(h, k, rho) - we04.Evaluate(-h, -k)));
                }
            }
        }

        Assert.True(worst <= 1e-15, $"{worst}");
    }

    [Theory]
    [InlineData(0.3, -0.2, 0.5, 0.4, 0.3, 0.2, 0.253775665773)]
    [InlineData(-1.0, 0.5, 1.2, -0.6, 0.5, 0.3, 0.049633455514)]
    [InlineData(1.5, -1.5, 0.2, 0.8, -0.7, -0.5, 0.012895124168)]
    [InlineData(-2.5, 2.0, -0.5, 0.9, 0.85, 0.8, 0.006207519614)]
    [InlineData(0.0, 0.0, 0.1, -0.3, -0.2, 0.6, 0.144207845916)]
    [InlineData(2.0, 1.0, -3.0, 0.5, 0.5, 0.5, 0.001348181042)]
    public void TheTrivariateNormalIsGenzsAtThePlannersPoints(
        double h1, double h2, double h3, double r21, double r31, double r32, double planner)
    {
        // the planner's independent values to twelve digits beside this file's own reference
        double tvtl = Tvtl(0, [0.0, h1, h2, h3], [0.0, r21, r31, r32], 1e-12);
        double reference = TrivariateNormal(h1, h2, h3, r21, r31, r32);
        Assert.Equal(reference, tvtl, 1e-12);
        Assert.Equal(planner, reference, 1e-12);
    }

    [Fact]
    public void TheTrivariateNormalIsGenzsAtFifteenHundredPoints()
    {
        // limits in [−4, 4] and correlations in [−0.95, 0.95] from a Mersenne twister seeded 20261028, a correlation
        // matrix kept where its determinant is at least 1e-3; at the engine's requested accuracy, 1e-12. #2935's line
        // alone leaves 100 of the points past it, up to 6.8e-2, and QuantLib's bivariate normal line alone 20, up to
        // 2.2e-4 [executed: the planner's xp_tvtl_time.cpp]
        var rng = new MersenneTwisterUniformRng(20261028);
        double Uniform(double lo, double hi) => lo + ((hi - lo) * rng.NextReal());
        int points = 0, over = 0;
        double worst = 0.0;
        for (int i = 0; i < 4000 && points < 1500; ++i)
        {
            double h1 = Uniform(-4, 4), h2 = Uniform(-4, 4), h3 = Uniform(-4, 4);
            double r21 = Uniform(-0.95, 0.95), r31 = Uniform(-0.95, 0.95), r32 = Uniform(-0.95, 0.95);
            if (1 - (r21 * r21) - (r31 * r31) - (r32 * r32) + (2 * r21 * r31 * r32) < 1e-3)
            {
                continue;
            }

            ++points;
            double error = Math.Abs(
                Tvtl(0, [0.0, h1, h2, h3], [0.0, r21, r31, r32], 1e-12) - TrivariateNormal(h1, h2, h3, r21, r31, r32));
            worst = Math.Max(worst, error);
            if (error > 1e-12)
            {
                ++over;
            }
        }

        Assert.Equal(1500, points);
        Assert.True(over == 0, $"{over} points past 1e-12, worst {worst}");
    }

    [Fact]
    public void TheSecondOrderHelpersAreTheirFunctionsDerivatives()
    {
        // five-point differences in a, their step 1e-2 × min(max(1, |a|), √(2(τ − p))), the width on which the
        // functions vary; within 1e-7 relative wherever the derivative exceeds 1e-3, where the stencil carries digits.
        // QuantLib's F helper misses by up to 1.4e-3 relative for b² ≠ (1 − γ)², its E helper by orders of magnitude
        // (the planner's xp_pert.cpp)
        var dvv = Helper<Func<double, double, double, double, double, double, double>>("Dvv");
        var ddvv = Helper<Func<double, double, double, double, double, double, double>>("Ddvv");
        var dff = Helper<Func<double, double, double, double, double, double, double>>("Dff");
        var ddff = Helper<Func<double, double, double, double, double, double, double>>("Ddff");
        var dll = Helper<Func<double, double, double, double, double, double, double, double>>("Dll");
        var ddll = Helper<Func<double, double, double, double, double, double, double, double>>("Ddll");
        int compared = 0;
        double worst = 0.0;
        foreach (double tt in new[] { 0.5 * 0.011, 0.02, 0.06 })
        {
            foreach (double gm in new[] { 0.3, -0.4, 1.5 })
            {
                foreach ((double sf, double pf) in new[] { (0.36, 0.73), (0.18, 0.55), (0.09, 0.91), (0.5, 0.6), (0.02, 0.98) })
                {
                    double s = sf * tt, p = pf * tt, width = Math.Sqrt(2 * (tt - p));
                    foreach (double a in new[] { -0.3, -0.05, 0.1, 0.4 })
                    {
                        foreach (double b in new[] { -1.0 - gm, -1.0 + gm, 1.0 + gm, 0.7 })
                        {
                            const double c = -0.01;
                            void Check(Func<double, double> f, double analytic)
                            {
                                double fd = FivePoint(f, a, width);
                                if (Math.Max(Math.Abs(analytic), Math.Abs(fd)) > 1e-3)
                                {
                                    ++compared;
                                    worst = Math.Max(worst, Math.Abs(analytic - fd) / Math.Max(Math.Abs(analytic), Math.Abs(fd)));
                                }
                            }

                            Check(x => dvv(s, p, tt, x, b, gm), ddvv(s, p, tt, a, b, gm));
                            Check(x => dff(s, p, tt, x, b, gm), ddff(s, p, tt, a, b, gm));
                            Check(x => dll(s, p, tt, x, b, c, gm), ddll(s, p, tt, a, b, c, gm));
                        }
                    }
                }
            }
        }

        Assert.True(compared > 500, $"{compared}");
        Assert.True(worst <= 1e-7, $"{worst}");
    }

    [Fact]
    public void OrderTwoApproachesTheFiniteDifferencesOnAStronglyTimeDependentMarket()
    {
        // vols 0.10 then 0.30, K 95, H 110: QuantLib's finite-difference barrier engine at 800², 1600² and 3200²,
        // 4.4111575994, 4.4111651161 and 4.4111669887, second order, its limit 4.4111676129; QuantLib's order 2 errs
        // 7.6e-5 (4.4112434138) [executed: the planner's xp_pert.cpp]; fast order 2 here within 5e-6
        Date today = new(15, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(today, 0.02, dc),
            new FlatForward(today, 0.03, dc),
            new BlackVarianceCurve(today, [today + 90, today + 180], [0.10, 0.30], dc));
        var option = new BarrierOption(
            BarrierType.UpOut, 110.0, 0.0, new PlainVanillaPayoff(OptionType.Put, 95.0), new EuropeanExercise(today + 180));

        double fd = 4.4111669887 + ((4.4111669887 - 4.4111651161) / 3);
        double order2 = PerturbativeBarrierOptionEngine.Calculate(option, context, process, 2, false, 100, 10).NPV!.Value;
        Assert.Equal(fd, order2, 5e-6);
        Assert.True(Math.Abs(4.4112434138 - fd) > 5e-5);
    }

    [Fact]
    public void OnAFlatMarketEachOrderIsTheAnalyticBarrierPrice()
    {
        // the perturbations vanish where the carry and variance are constant: order 0 is S-rx's analytic up-and-out put
        Date today = new(15, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.02, dc), new FlatForward(today, 0.03, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.2, dc));
        var option = new BarrierOption(
            BarrierType.UpOut, 110.0, 0.0, new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(today + 180));

        double analytic = AnalyticBarrierEngine.Calculate(option, context, process).NPV!.Value;
        Assert.Equal(analytic, PerturbativeBarrierOptionEngine.Calculate(option, context, process, 0).NPV!.Value, 1e-14);
        for (int order = 1; order <= 2; ++order)
        {
            Assert.Equal(
                analytic,
                PerturbativeBarrierOptionEngine.Calculate(option, context, process, order, false, 100, 10).NPV!.Value,
                1e-10);
        }
    }

    // P(X1 < h1, X2 < h2, X3 < h3), correlations r21, r31, r32: X1's density times the bivariate normal of the other two
    // given it (We04DP), on 50 panels of 64-point Gauss-Legendre over [−12, h1], within 1.4e-15 of 400 panels (the
    // planner's xp_tvtl_time.cpp)
    private static double TrivariateNormal(double h1, double h2, double h3, double r21, double r31, double r32)
    {
        double s2 = Math.Sqrt(1 - (r21 * r21)), s3 = Math.Sqrt(1 - (r31 * r31));
        var bvn = new BivariateCumulativeNormalDistributionWe04DP((r32 - (r21 * r31)) / (s2 * s3));
        var phi = new NormalDistribution();
        var gl = new GaussLegendreIntegration(64);
        const int panels = 50;
        double a = -12.0, w = (h1 - a) / panels, sum = 0.0;
        for (int k = 0; k < panels; ++k)
        {
            double lo = a + (k * w), hi = lo + w;
            sum += gl.Evaluate(u =>
            {
                double x = (0.5 * (hi - lo) * u) + (0.5 * (hi + lo));
                return phi.Evaluate(x) * bvn.Evaluate((h2 - (r21 * x)) / s2, (h3 - (r31 * x)) / s3);
            }) * 0.5 * (hi - lo);
        }

        return sum;
    }

    private static double FivePoint(Func<double, double> f, double a, double width)
    {
        double h = 1e-2 * Math.Min(Math.Max(1.0, Math.Abs(a)), width);
        return ((8 * (f(a + h) - f(a - h))) - (f(a + (2 * h)) - f(a - (2 * h)))) / (12 * h);
    }

    private static T Helper<T>(string name)
        where T : Delegate =>
        Engine.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<T>();
}
