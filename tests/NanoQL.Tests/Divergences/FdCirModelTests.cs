// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference engine for a Black-Scholes equity under a Cox-Ingersoll-Ross short rate (the S-van plan's VR4
/// (xv) and VR8). QuantLib's operator's rate diffusion is σ²r where the CIR generator's is ½σ²r, and its covariance
/// 2ρσ_Sσ where it is ρσ_Sσ√r (<c>fdmcirop.cpp:55-60</c>, <c>:74-82</c>): through its own machinery a cash-like put's
/// implied bond is the CIR bond at σ√2 (0.8612436 at σ 0.15, where the CIR bond is 0.8609795 and at σ√2 0.8612489),
/// it blows up (1e32 to 1e226) at ρ ±0.5 on its schemes for every σ, and at σ 0.3 even at ρ = 0; and its rate grid,
/// the process's time-averaged quantiles, leaves the rate's path where it drifts or σ is small (3.58 to 15.7 where the
/// deterministic-rate limit is 4.21 and 3.28) [executed by the S-van planner: VP26, VP45]. Its test's 4.275 is its own
/// operator on its 10-step grid (4.27310 refined). nanoql's operator is the CIR generator's, its rate grid uniform on
/// [0, max(upper, r₀, θ, E[r_T])], upper the last point of QuantLib's, where at r = 0 the equation is its own boundary
/// condition, and its default scheme Hundsdorfer's, where QuantLib's modified Hundsdorfer blows up on fine grids when the
/// rate drifts up fast. The checks: QuantLib's CIR model's closed-form bond (the rates part, every ρ); the
/// deterministic-rate limit against Black's formula at the mean path's average rate; and a Monte Carlo of QuantLib's
/// CIR process, the log spot's step exact given the rate's path and draws, for ρ ≠ 0 and the rising rate. Flat
/// Actual/365 (Fixed) curves from 15 October 2026.
/// </summary>
public class FdCirModelTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Theory]
    [InlineData(0.024, 0.0)]
    [InlineData(0.15, 0.0)]
    [InlineData(0.15, -0.5)]
    [InlineData(0.15, 0.5)]
    [InlineData(0.3, 0.0)]
    [InlineData(0.3, 0.5)]
    public void ACashLikePutsBondIsTheCirModels(double sigmaR, double rho)
    {
        // a put struck at 1e4 on a spot of 100, three years: its value is 1e4 times the bond less the spot; κ 2,
        // θ = r₀ = 5%; QuantLib's model's closed form, within 3e-6 on the engine's default grid
        var cir = new CoxIngersollRossProcess(2.0, sigmaR, 0.05, 0.05);
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 1e4), new EuropeanExercise(Today + (3 * 365)));
        double value = FdCIRVanillaEngine.Calculate(put, Context, cir, Equity(100.0, 0.05), 50, 100, 100, 0, rho).NPV!.Value;
        double bond = new CoxIngersollRoss(0.05, 0.05, 2.0, sigmaR, false).DiscountBond(0.0, 3.0, 0.05);
        Assert.Equal(bond, (value + 100.0) / 1e4, 3e-6);
    }

    [Fact]
    public void QuantLibsBondIsTheCirBondAtTwiceTheVariance()
    {
        // QuantLib's implied bond at σ 0.15, 0.8612436 (VP26), is QuantLib's model's at σ√2, not at σ
        double atSigma = new CoxIngersollRoss(0.05, 0.05, 2.0, 0.15, false).DiscountBond(0.0, 3.0, 0.05);
        double atSigmaRoot2 = new CoxIngersollRoss(0.05, 0.05, 2.0, 0.15 * Math.Sqrt(2.0), false).DiscountBond(0.0, 3.0, 0.05);
        Assert.Equal(0.8609795, atSigma, 1e-7);
        Assert.Equal(0.8612436, atSigmaRoot2, 6e-6);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(0.5)]
    public void AtANonZeroCorrelationTheValueIsTheMonteCarlos(double rho)
    {
        // σ 0.15, κ 2, θ = r₀ = 5%, where QuantLib's engine blows up: a year's put at 40 on 36, σ_S 20%
        var cir = new CoxIngersollRossProcess(2.0, 0.15, 0.05, 0.05);
        double fd = FdCIRVanillaEngine.Calculate(Put(), Context, cir, Equity(36.0, 0.06), 100, 200, 100, 0, rho).NPV!.Value;
        (double mean, double error) = ConditionalMonteCarlo(cir, 36.0, 0.2, 40.0, rho, 40000, 250, 42);
        Assert.True(Math.Abs(fd - mean) <= 3 * error, $"{fd} against {mean} ± {error}");

        // the correlation moves the value by 0.067 a half: the check sees the covariance term
        double atZero = FdCIRVanillaEngine.Calculate(Put(), Context, cir, Equity(36.0, 0.06), 100, 200, 100, 0, 0.0).NPV!.Value;
        Assert.True(Math.Abs(fd - atZero) > 20 * error, $"{fd} against {atZero}");
    }

    [Fact]
    public void ARisingRateAtThirtyPercentIsTheMonteCarlos()
    {
        // σ 0.3, the rate from 1% to 12% at κ 3, ρ 0: on a grid from zero where QuantLib's blows up; the reviewer's
        // conditional estimator read 3.29627 ± 0.00129
        var cir = new CoxIngersollRossProcess(3.0, 0.3, 0.01, 0.12);
        double fd = FdCIRVanillaEngine.Calculate(Put(), Context, cir, Equity(36.0, 0.06), 100, 200, 100, 0, 0.0).NPV!.Value;
        (double mean, double error) = ConditionalMonteCarlo(cir, 36.0, 0.2, 40.0, 0.0, 40000, 250, 4242);
        Assert.True(Math.Abs(fd - mean) <= 3 * error, $"{fd} against {mean} ± {error}");
    }

    [Theory]
    [InlineData(1.2188 + (0.02438 * -0.5726), 0.0183 * 1.2188 / (1.2188 + (0.02438 * -0.5726)), 0.06)] // fdcir's, down
    [InlineData(1.2, 0.08, 0.02)] // drifting up
    public void TheDeterministicRateLimitIsBlacksFormula(double kappa, double theta, double r0)
    {
        // σ 1e-6: Black's put at the mean path's average rate, within 1.5e-4 at 200 × 400 × 200; QuantLib's grid
        // collapses there (3.246071 on fdcir's drift down, 3.579656 drifting up, VP45)
        var cir = new CoxIngersollRossProcess(kappa, 1e-6, r0, theta);
        double fd = FdCIRVanillaEngine.Calculate(Put(), Context, cir, Equity(36.0, 0.06), 200, 400, 200, 0, 0.0).NPV!.Value;
        Assert.Equal(Black(kappa, theta, r0), fd, 1.5e-4);
    }

    [Fact]
    public void TheDefaultSchemeHoldsTheFastDriftUp()
    {
        // the rate from 1% to 12% at κ 3, σ 1e-6: Hundsdorfer's, the default, within 1.5e-4 of Black's 3.278907 at
        // 200 × 400 × 200, where QuantLib's default, modified Hundsdorfer, blows up (−1.05e43, VP46)
        var cir = new CoxIngersollRossProcess(3.0, 1e-6, 0.01, 0.12);
        double fd = FdCIRVanillaEngine.Calculate(Put(), Context, cir, Equity(36.0, 0.06), 200, 400, 200, 0, 0.0).NPV!.Value;
        Assert.Equal(Black(3.0, 0.12, 0.01), fd, 1.5e-4);
        double modified = FdCIRVanillaEngine.Calculate(
            Put(), Context, cir, Equity(36.0, 0.06), 200, 400, 200, 0, 0.0, FdmSchemeDesc.ModifiedHundsdorfer()).NPV!.Value;
        Assert.True(Math.Abs(modified) > 1e30, $"{modified}");
    }

    private static VanillaOption Put() =>
        new(new PlainVanillaPayoff(OptionType.Put, 40.0), new EuropeanExercise(Today + 365));

    private static BlackScholesMertonProcess Equity(double spot, double r) =>
        new(spot, new FlatForward(Today, 0.0, Dc), new FlatForward(Today, r, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.2, Dc));

    // Black's put at 40 on 36, σ_S 20%, a year, at the mean path's average rate θ + (r₀ − θ)(1 − e^{−κ})/κ
    private static double Black(double kappa, double theta, double r0)
    {
        double rbar = theta + ((r0 - theta) * (1 - Math.Exp(-kappa)) / kappa);
        return BlackFormulaFunctions.BlackFormula(OptionType.Put, 40.0, 36.0 * Math.Exp(rbar), 0.2, Math.Exp(-rbar));
    }

    // A Monte Carlo of the put over a year: the rate by QuantLib's CIR process (its quadratic-exponential steps), the log
    // spot normal given the rate's path and draws, ln S_T = ln S₀ + ∫r − ½σ² + σρ·W_r(1) + σ√(1 − ρ²)·W⊥(1), W_r(1)
    // the draws' sum, so each path's value is Black's discounted by its rate; less, as a control variate, the same
    // Black's on the mean rate path, whose mean over W_r(1) is Black's at the full σ on that path, exactly; the mean and
    // its standard error
    private static (double Mean, double Error) ConditionalMonteCarlo(
        CoxIngersollRossProcess cir, double s0, double sigma, double strike, double rho, int paths, int steps, long seed)
    {
        var rng = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(new MersenneTwisterUniformRng(seed));
        double dt = 1.0 / steps, sqrtDt = Math.Sqrt(dt);
        double residual = sigma * Math.Sqrt(1 - (rho * rho));
        double kappa = cir.Speed, theta = cir.Level;
        double meanIntegral = theta + ((cir.X0 - theta) * (1 - Math.Exp(-kappa)) / kappa);
        double control = BlackFormulaFunctions.BlackFormula(
            OptionType.Put, strike, s0 * Math.Exp(meanIntegral), sigma, Math.Exp(-meanIntegral));
        double sum = 0.0, sum2 = 0.0;
        for (int p = 0; p < paths; ++p)
        {
            double r = cir.X0, integral = 0.0, w = 0.0;
            for (int i = 0; i < steps; ++i)
            {
                double dw = rng.Next().Value;
                double next = cir.Evolve(i * dt, r, dt, dw);
                integral += 0.5 * (r + next) * dt;
                w += dw * sqrtDt;
                r = next;
            }

            double meanLog = Math.Log(s0) + integral - (0.5 * sigma * sigma) + (sigma * rho * w);
            double forward = Math.Exp(meanLog + (0.5 * residual * residual));
            double meanForward = Math.Exp(Math.Log(s0) + meanIntegral - (0.5 * sigma * sigma) + (sigma * rho * w) + (0.5 * residual * residual));
            double value = BlackFormulaFunctions.BlackFormula(OptionType.Put, strike, forward, residual, Math.Exp(-integral))
                - BlackFormulaFunctions.BlackFormula(OptionType.Put, strike, meanForward, residual, Math.Exp(-meanIntegral))
                + control;
            sum += value;
            sum2 += value * value;
        }

        double mean = sum / paths;
        return (mean, Math.Sqrt(((sum2 / paths) - (mean * mean)) / (paths - 1)));
    }
}
