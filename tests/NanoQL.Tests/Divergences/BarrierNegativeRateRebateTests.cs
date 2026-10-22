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
/// A knock-out's rebate under negative rates (the S-rx plan's SRXR3 (xv), fixed under R8). QuantLib's analytic engine
/// takes <c>λ = √(μ² + 2r/σ²)</c> (<c>analyticbarrierengine.cpp:243-264</c>), the square root of a negative number
/// where r &lt; 0 and μ² &lt; −2r/σ², so a knock-out with a rebate is NaN: at r −1%, q −1.5% and a volatility of 20%, a
/// one-year down-and-out call at 90, strike 100, with 5 paid at the hit, where QuantLib's finite-difference engine
/// gives 9.889092750723153 [executed on the wheel]. Where λ² &lt; 0, nanoql's rebate term is the rebate's present value
/// at the hit, <c>R ∫₀ᵀ e^(−rt) f(t) dt</c>, f the first-passage density of the log-price to the barrier, integrated by
/// <see cref="GaussLobattoIntegral"/>; with the spot on the barrier it is the rebate, paid now. Where λ² ≥ 0 QuantLib's
/// closed form stands, with its bits. QuantLib's checks: its closed form, the same quantity where it works, and its
/// finite-difference engine (400 × 800) where it doesn't, within that engine's own grid error.
/// </summary>
public class BarrierNegativeRateRebateTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    [Fact]
    public void WhereLambdaIsRealTheIntegralIsQuantLibsClosedForm()
    {
        // (r, q, σ, barrier type, barrier, QuantLib's knock-out with 5 at the hit)
        (double R, double Q, double Vol, BarrierType Type, double Barrier, double QuantLib)[] cases =
        [
            (0.05, 0.02, 0.25, BarrierType.DownOut, 90.0, 11.46651161918616),
            (0.05, 0.02, 0.25, BarrierType.UpOut, 110.0, 3.5261435057891286),
            (-0.005, -0.01, 0.30, BarrierType.DownOut, 90.0, 11.719483059555458),
            (-0.005, -0.01, 0.30, BarrierType.UpOut, 110.0, 3.6328439350966724),
        ];
        foreach (var c in cases)
        {
            GeneralizedBlackScholesProcess process = Process(100.0, c.R, c.Q, c.Vol);
            double withRebate = Npv(c.Type, c.Barrier, 5.0, process);
            Assert.Equal(c.QuantLib, withRebate);

            double closedForm = withRebate - Npv(c.Type, c.Barrier, 0.0, process);
            double integral = 5.0 * RebateIntegral(c.R, c.Q, c.Vol, c.Barrier, 1.0);
            Assert.Equal(closedForm, integral, Math.Abs(closedForm) * 1e-15);
        }
    }

    [Fact]
    public void WhereLambdaIsNotRealTheRebateIsQuantLibsFiniteDifferenceValue()
    {
        // (r, q, σ, barrier type, barrier, QuantLib's finite-difference engine, 400 × 800); QuantLib's analytic: NaN
        (double R, double Q, double Vol, BarrierType Type, double Barrier, double Fd)[] cases =
        [
            (-0.01, -0.015, 0.20, BarrierType.DownOut, 90.0, 9.889092750723153),
            (-0.01, -0.015, 0.20, BarrierType.UpOut, 110.0, 3.1852599313779018),
            (-0.0075, -0.004, 0.08, BarrierType.DownOut, 90.0, 4.075607958472754),
            (-0.0075, -0.004, 0.08, BarrierType.UpOut, 110.0, 1.982190184878071),
        ];
        foreach (var c in cases)
        {
            double value = Npv(c.Type, c.Barrier, 5.0, Process(100.0, c.R, c.Q, c.Vol));
            Assert.True(Math.Abs(value - c.Fd) < 1.6e-3, $"{c.Type} at r {c.R}: {value} against {c.Fd}");
        }
    }

    [Fact]
    public void WithTheSpotOnTheBarrierTheRebateIsPaidNow()
    {
        // QuantLib: NaN
        Assert.Equal(5.0, Npv(BarrierType.DownOut, 90.0, 5.0, Process(90.0, -0.01, -0.015, 0.20)));
    }

    private static BlackScholesMertonProcess Process(double spot, double r, double q, double vol) => new(
        spot, new FlatForward(
            Today, q, Dc), new FlatForward(Today, r, Dc), new BlackConstantVol(Today, new NullCalendar(), vol, Dc));

    private static double Npv(
        BarrierType type, double barrier, double rebate, GeneralizedBlackScholesProcess process) =>
        AnalyticBarrierEngine.Calculate(
            new BarrierOption(type, barrier, rebate, new PlainVanillaPayoff(OptionType.Call, 100.0), Exercise),
            Context,
            process).NPV!.Value;

    // ∫₀ᵀ e^(−rt) f(t) dt from a spot of 100, f the first-passage density of the log-price (drift r − q − σ²/2,
    // volatility σ) to the barrier: the rebate's present value per unit paid at the hit.
    private static double RebateIntegral(double r, double q, double vol, double barrier, double t)
    {
        double b = Math.Log(barrier / 100.0);
        double nu = r - q - (0.5 * vol * vol);
        return new GaussLobattoIntegral(100000, 1e-15).Integrate(
            s => s > 0.0
                ? Math.Exp(-r * s) * Math.Abs(b) / (vol * Math.Sqrt(2.0 * Math.PI * s * s * s))
                    * Math.Exp(-((b - (nu * s)) * (b - (nu * s))) / (2.0 * vol * vol * s))
                : 0.0,
            0.0,
            t);
    }
}
