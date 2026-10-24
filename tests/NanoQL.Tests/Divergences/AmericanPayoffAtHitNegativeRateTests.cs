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
/// The American payoff at the hit under negative rates (the S-van plan's VR4 (i), master Revision 57, S-rx's (xv)).
/// QuantLib's closed form takes λ = √(μ² − 2 ln D / σ²T) (<c>americanpayoffathit.cpp:75</c>, <c>:90</c>), the square
/// root of a negative number where r &lt; 0 and μ² &lt; −2r/σ², so the value is NaN: at r −1%, q −1.5% and 20%, and at
/// r −0.75%, q −0.4% and 8%, a down-and-in put at 90 and an up-and-in call at 110 paying 5 [executed on the wheel:
/// <c>oracle_c1.py</c>]. nanoql's value there is the payoff's present value at the hit integrated over the
/// first-passage density, which is QuantLib's closed form where λ is real; its Greeks are that integral's derivatives.
/// QuantLib's checks: its closed form, the same quantity where it works (the integral here computed by the test); and
/// its finite-difference barrier engine (400 × 800) where it doesn't: the cash at the hit is a knock-out's rebate on a
/// payoff that pays nothing, within that engine's grid error. Near the spot, where the Greeks' integrals grow as 1/b
/// and 1/b², the value and Greeks are checked against the integrals computed to 40 digits, independently of the
/// quadrature.
/// </summary>
public class AmericanPayoffAtHitNegativeRateTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void WhereLambdaIsRealTheFirstPassageIntegralIsQuantLibsClosedForm()
    {
        // (r, q, σ, type, strike, QuantLib's closed form for 5 at the hit) [executed on the wheel: oracle_c1d.py]
        (double R, double Q, double Vol, OptionType Type, double Strike, double QuantLib)[] cases =
        [
            (0.05, 0.02, 0.25, OptionType.Put, 90.0, 3.3277010715615774),
            (0.05, 0.02, 0.25, OptionType.Call, 110.0, 3.463861145516318),
            (-0.005, -0.01, 0.30, OptionType.Put, 90.0, 3.7975325320485935),
            (-0.005, -0.01, 0.30, OptionType.Call, 110.0, 3.594802717511667),
        ];
        foreach (var c in cases)
        {
            double value = Price(c.R, c.Q, c.Vol, c.Type, c.Strike).NPV!.Value;
            Assert.Equal(c.QuantLib, value);
            double integral = 5.0 * FirstPassageIntegral(c.R, c.Q, c.Vol, c.Strike);
            Assert.Equal(value, integral, Math.Abs(value) * 1e-13);
        }
    }

    [Fact]
    public void WhereLambdaIsNotRealTheValueIsQuantLibsFiniteDifferenceRebate()
    {
        // (r, q, σ, type, strike, QuantLib's FD knock-out rebate, 400 × 800); QuantLib's closed form: NaN
        (double R, double Q, double Vol, OptionType Type, double Strike, double Fd)[] cases =
        [
            (-0.01, -0.015, 0.20, OptionType.Put, 90.0, 3.118502464624829),
            (-0.01, -0.015, 0.20, OptionType.Call, 110.0, 3.0629238070583225),
            (-0.0075, -0.004, 0.08, OptionType.Put, 90.0, 1.049741905857591),
            (-0.0075, -0.004, 0.08, OptionType.Call, 110.0, 1.0577930704429974),
        ];
        foreach (var c in cases)
        {
            OneAssetOptionResults results = Price(c.R, c.Q, c.Vol, c.Type, c.Strike);
            double value = results.NPV!.Value;
            Assert.True(Math.Abs(value - c.Fd) < 1.6e-3, $"{c.Type} at r {c.R}: {value} against {c.Fd}");

            // the Greeks, the integral's derivatives, against its bumped values (QuantLib's own bumps in its
            // low-volatility case)
            double ds = 1e-3, dr = 1e-6;
            OneAssetOptionResults up = Price(c.R, c.Q, c.Vol, c.Type, c.Strike, 100.0 + ds);
            OneAssetOptionResults down = Price(c.R, c.Q, c.Vol, c.Type, c.Strike, 100.0 - ds);
            AssertNear((up.NPV!.Value - down.NPV!.Value) / (2.0 * ds), results.Delta!.Value, "delta");
            AssertNear((up.Delta!.Value - down.Delta!.Value) / (2.0 * ds), results.Gamma!.Value, "gamma");
            double rhoUp = Price(c.R + dr, c.Q, c.Vol, c.Type, c.Strike).NPV!.Value;
            double rhoDown = Price(c.R - dr, c.Q, c.Vol, c.Type, c.Strike).NPV!.Value;
            AssertNear((rhoUp - rhoDown) / (2.0 * dr), results.Rho!.Value, "rho");
        }
    }

    [Fact]
    public void NearTheSpotTheValueAndGreeksAreTheFirstPassageIntegralsToTheirPrecision()
    {
        // a strike 1% from the spot, where QuantLib's value is NaN: (r, q, σ, type, strike, and the 40-digit
        // integrals of the density and of its first and second derivatives by b = ln(H/S), the last two of order 1/b
        // and 1/b²) [executed: fp_reference.py, composite 20-point Gauss-Legendre in ln s, decimal arithmetic]
        (double R, double Q, double Vol, OptionType Type, double Strike, double I0, double I1, double I2)[] cases =
        [
            (-0.02, -0.015, 0.10, OptionType.Put, 99.0, 0.9303081798010805, 6.995974537773846, -9.532180319354639),
            (-0.005, -0.005, 0.05, OptionType.Call, 101.0, 0.8386486585322428, -15.944740871285909, 74.84064259813677),
        ];
        foreach (var c in cases)
        {
            OneAssetOptionResults results = Price(c.R, c.Q, c.Vol, c.Type, c.Strike);
            double value = results.NPV!.Value;
            Assert.Equal(5.0 * c.I0, value, 5.0 * c.I0 * 1e-14);
            Assert.Equal(value, 5.0 * FirstPassageIntegral(c.R, c.Q, c.Vol, c.Strike), value * 1e-13);

            // b = ln(H/S): ∂/∂S = −(1/S) ∂/∂b, ∂²/∂S² = (∂/∂b + ∂²/∂b²)/S²
            double delta = -5.0 * c.I1 / 100.0;
            double gamma = 5.0 * (c.I1 + c.I2) / (100.0 * 100.0);
            Assert.Equal(delta, results.Delta!.Value, Math.Abs(delta) * 1e-14);
            Assert.Equal(gamma, results.Gamma!.Value, Math.Abs(gamma) * 1e-12);
        }
    }

    private static OneAssetOptionResults Price(double r, double q, double vol, OptionType type, double strike, double spot = 100.0)
    {
        var process = new BlackScholesMertonProcess(
            spot, new FlatForward(Today, q, A365), new FlatForward(Today, r, A365),
            new BlackConstantVol(Today, new NullCalendar(), vol, A365));
        var option = new VanillaOption(new CashOrNothingPayoff(type, strike, 5.0), new AmericanExercise(Today, Today + 365));
        return AnalyticDigitalAmericanEngine.Calculate(option, Context, process);
    }

    // ∫₀ᵀ e^(−rt) f(t) dt from a spot of 100 over one year, f the first-passage density of the log-price (drift
    // r − q − σ²/2, volatility σ) to the strike: the present value per unit paid at the hit.
    private static double FirstPassageIntegral(double r, double q, double vol, double strike)
    {
        double b = Math.Log(strike / 100.0);
        double nu = r - q - (0.5 * vol * vol);
        return new GaussLobattoIntegral(100000, 1e-15).Integrate(
            s => s > 0.0
                ? Math.Exp(-r * s) * Math.Abs(b) / (vol * Math.Sqrt(2.0 * Math.PI * s * s * s))
                    * Math.Exp(-((b - (nu * s)) * (b - (nu * s))) / (2.0 * vol * vol * s))
                : 0.0,
            0.0,
            1.0);
    }

    private static void AssertNear(double bumped, double analytic, string greek) =>
        Assert.True(
            Math.Abs(bumped - analytic) <= 1e-4 * Math.Max(1.0, Math.Abs(bumped)),
            $"{greek}: bumped {bumped}, the integral's {analytic}");
}
