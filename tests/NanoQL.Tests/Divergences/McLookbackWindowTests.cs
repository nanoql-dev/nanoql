// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo partial lookbacks' window (fixed). QuantLib's partial pricers snap the window's date to the closest
/// time of a regular grid that doesn't carry it, and the partial fixed pricer drops the window's first node: a partial
/// floating call (running minimum 100, λ 1) whose window ends 37 days into a year, on spot 100, r 5%, q 2%, σ 20%, 4
/// steps, 100,000 antithetic samples at seed 42, snaps its window to the first node and reads the spot a quarter on, at
/// 7.805558043540716. nanoql makes the window's date a time of the grid, its start node included, so on this grid the
/// window is {0, 37 days}. Check: the discrete payoff's exact value on that window, e^(−rT) E[(S_T − λ min(m, S_t₁))⁺],
/// a quadrature over S_t₁ of the Black call on S_T given it: 10.221335405994491, within three of the engine's error
/// estimates (the continuous window's analytic value, 11.617971695171292, lies above both, the discrete minimum over
/// two nodes missing the path's dips between them). And a partial fixed call struck at 100 whose window opens 300 days
/// into the year, on the same paths, snaps its window to the grid's node at three quarters and drops it, so it reads
/// the terminal spot alone, a vanilla call, at 9.179431603675994; nanoql's window is {300 days, a year}, its exact
/// value e^(−rT) E[(max(S_t₁, S_T) − K)⁺] 10.686101666470105, the engine within three of its error estimates.
/// </summary>
public class McLookbackWindowTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void APartialWindowsDateIsATimeOfTheGrid()
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));
        var option = new ContinuousPartialFloatingLookbackOption(
            100.0, 1.0, Today + 37, new FloatingTypePayoff(OptionType.Call), new EuropeanExercise(Today + 365));
        OneAssetOptionResults mc = MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, process, 4, null, false, true, 100000, null, null, 42);
        double reference = TwoNodeWindow(100.0, 0.05, 0.02, 0.20, 37 / 365.0, 1.0, 100.0, 1.0);
        Assert.Equal(10.221335405994491, reference, 1e-9);
        Assert.True(Math.Abs(mc.NPV!.Value - reference) <= 3.0 * mc.ErrorEstimate!.Value, $"{mc.NPV} against {reference}");
    }

    [Fact]
    public void APartialFixedWindowReadsItsFirstNode()
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));
        var option = new ContinuousPartialFixedLookbackOption(
            Today + 300, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        OneAssetOptionResults mc = MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, process, 4, null, false, true, 100000, null, null, 42);
        double reference = TwoNodeFixedWindow(100.0, 0.05, 0.02, 0.20, 300 / 365.0, 1.0, 100.0);
        Assert.Equal(10.686101666470105, reference, 1e-9);
        Assert.True(Math.Abs(mc.NPV!.Value - reference) <= 3.0 * mc.ErrorEstimate!.Value, $"{mc.NPV} against {reference}");
    }

    // e^(−rT) E[(max(S_t₁, S_T) − K)⁺] on the window {t₁, T}: given S_t₁ = x at or above K, x (1 + E[(G − 1)⁺]) − K
    // with G = S_T/x; below K, the Black call on S_T at K; integrated over S_t₁'s log-normal density, split where it
    // crosses K
    private static double TwoNodeFixedWindow(double s0, double r, double q, double sigma, double t1, double t, double k)
    {
        double b = r - q;
        double tau = t - t1;
        double up = BlackFormulaFunctions.BlackFormula(OptionType.Call, 1.0, Math.Exp(b * tau), sigma * Math.Sqrt(tau));
        double zk = (Math.Log(k / s0) - ((b - (0.5 * sigma * sigma)) * t1)) / (sigma * Math.Sqrt(t1));
        double F(double z)
        {
            double x = s0 * Math.Exp(((b - (0.5 * sigma * sigma)) * t1) + (sigma * Math.Sqrt(t1) * z));
            double value = x >= k
                ? (x * (1.0 + up)) - k
                : BlackFormulaFunctions.BlackFormula(OptionType.Call, k, x * Math.Exp(b * tau), sigma * Math.Sqrt(tau));
            return Math.Exp(-0.5 * z * z) / Math.Sqrt(2.0 * Math.PI) * value;
        }

        var integrator = new GaussKronrodAdaptive(1e-10, 1000000);
        return Math.Exp(-r * t) * (integrator.Integrate(F, -12.0, zk) + integrator.Integrate(F, zk, 12.0));
    }

    // e^(−rT) E[(S_T − λ min(m, S_t₁))⁺] on the window {0, t₁}: given S_t₁, the Black call on S_T at λ min(m, S_t₁),
    // integrated over S_t₁'s log-normal density, split where S_t₁ crosses m
    internal static double TwoNodeWindow(double s0, double r, double q, double sigma, double t1, double t, double m, double lambda)
    {
        double b = r - q;
        double tau = t - t1;
        double zm = (Math.Log(m / s0) - ((b - (0.5 * sigma * sigma)) * t1)) / (sigma * Math.Sqrt(t1));
        double F(double z)
        {
            double x = s0 * Math.Exp(((b - (0.5 * sigma * sigma)) * t1) + (sigma * Math.Sqrt(t1) * z));
            double call = BlackFormulaFunctions.BlackFormula(OptionType.Call, lambda * Math.Min(m, x), x * Math.Exp(b * tau), sigma * Math.Sqrt(tau));
            return Math.Exp(-0.5 * z * z) / Math.Sqrt(2.0 * Math.PI) * call;
        }

        var integrator = new GaussKronrodAdaptive(1e-10, 1000000);
        return Math.Exp(-r * t) * (integrator.Integrate(F, -12.0, zm) + integrator.Integrate(F, zm, 12.0));
    }
}
