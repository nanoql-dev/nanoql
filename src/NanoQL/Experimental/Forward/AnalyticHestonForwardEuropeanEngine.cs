// SPDX-FileCopyrightText: Copyright (C) 2020 Jack Gillett
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/forward/analytichestonforwardeuropeanengine.hpp ql/experimental/forward/analytichestonforwardeuropeanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Experimental;

/// <summary>
/// Kruse's analytic engine for forward-starting, strike-reset European options under the Heston model ("On the Pricing
/// of Forward Starting Options under Stochastic Volatility", 2003): the vanilla's probabilities from the characteristic
/// function at each variance the reset may reach, integrated over that variance's law at the reset.
/// </summary>
/// <remarks>
/// <para>
/// An unsealed, immutable object holding the process and the integration order, its public members QuantLib's (<see
/// cref="Propagator"/>, <see cref="ForwardChF"/>), with a static <c>Calculate</c> in the engines' form. QuantLib's
/// nested integrations: 128 Gauss-Legendre points over the variance at the reset, 128 over the characteristic
/// function's argument; within 1e-3 of the reset, the vanilla's integral alone. The value, with QuantLib's additional
/// results ("dcf", "qf", "expiryRatio", "resetRatio", "moneyness", "s0", "fwd", "resetTime", "expiryTime", "P1Hat",
/// "P2Hat", "phiRightLimit", "nuRightLimit").
/// </para>
/// <para>
/// Documented divergences (<c>Divergences/HestonForwardEngineTests.cs</c>), each leaving QuantLib's bits where it
/// doesn't apply. The propagator's product e^(−(Bν + Λ)/2) · I_(R/2−1)(√(ΛBν)) underflows times overflows to NaN for
/// resets just above the 1e-3 cut, where it is taken as e^(−(Bν + Λ)/2 + x) · Iₑ(x), x = √(ΛBν), QuantLib's own
/// exponentially weighted Bessel function, wherever QuantLib's product isn't finite and nonzero. At κ̂ = κ − ρσ = 0
/// QuantLib's B is 0/0 and R 0·∞: below |κ̂t| of 1e-2, B = 4 r(κ̂t)/(σ²t) with r(x) = x/(−expm1(−x)), and R = 4κθ/σ²
/// where κ̂ = 0. QuantLib's outer integral, 128 nodes over [0, max(2, ...)] in the reset variance, can't resolve the
/// propagator at short resets, where it is narrow, nor where 2κθ &lt; σ², where it is singular at 0: at a vol of vol of
/// 0.2 a one-day reset prices 10.376 against Monte Carlo's 8.662 ± 0.019, at 0.5 a 182-day reset 5.100 against 5.177 ±
/// 0.014. Where QuantLib's nodes miss the propagator's unit mass by more than 1e-6 the expectation is taken in closed
/// form: QuantLib's characteristic function from the reset is affine in the reset variance, so its expectation over the
/// propagator is the CIR transition's moment-generating function, and each probability is one Fourier integral on the
/// half-line, adaptively, where QuantLib's inner integral stops at a fixed limit that small reset variances outrun.
/// That limit also fails short tenors, where the return's distribution is narrow: at a reset today a one-day call at
/// the money prices 0.297 against the vanilla's 0.420 and calls out of the money below 0. QuantLib's probabilities
/// stand on its reset-today route and on its outer integral only where each is within 1e-7 of the resolved route's, the
/// same half-line integral at v₀ or the closed form, the resolved route elsewhere; the resolved integral runs on
/// Gauss-Legendre pieces whose width doubles from 1 to at most min(8, π/|log K|), half the strike factor's period,
/// until three pieces in a row contribute at most 1e-17. Times run on the model's clock, <see cref="HestonProcess"/>'s
/// model time from the earliest reference date of its curves, each discount read at its instants, none accrued before
/// its curve's own date.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class AnalyticHestonForwardEuropeanEngine
{
    // QuantLib's switch to the vanilla's integral, near the reset
    private const double ResetCut = 1e-3;

    // the cut below which B is taken through r(x) = x/(−expm1(−x))
    private const double SmallKappaHat = 1e-2;

    // the propagator's mass QuantLib's nodes must carry for its outer integral to stand; the resolved integral's widest
    // piece, its quiet piece's size and the end past which its integrand's tail refuses; the agreement of QuantLib's
    // probabilities with it
    private const double MassTolerance = 1e-6;
    private const double MaxPieceWidth = 8.0;
    private const double PieceQuiet = 1e-17;
    private const double ResolvedUpperEnd = 1e7;
    private const double ProbabilityAgreement = 1e-7;

    private static readonly GaussLegendreIntegration Integrator = new(128);
    private static readonly GaussLegendreIntegration PieceRule = new(32);

    private readonly double kappaHat;
    private readonly double thetaHat;
    private readonly double r;

    /// <summary>Initializes a new instance of the <see cref="AnalyticHestonForwardEuropeanEngine"/> class.</summary>
    /// <param name="process">The Heston process.</param>
    /// <param name="integrationOrder">The order of the analytic Heston engines <see cref="ForwardChF"/> builds,
    /// positive, at most 192.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The process's vol of vol is at most 0.1, QuantLib's refusal.</exception>
    public AnalyticHestonForwardEuropeanEngine(HestonProcess process, int integrationOrder = 144)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!(process.Sigma > 0.1))
        {
            throw new ArgumentException(
                "Very low values (<~10%) for Heston Vol-of-Vol cause numerical issuesin this implementation of the "
                + "propagator function, try usingMCForwardEuropeanHestonEngine Monte-Carlo engine instead",
                nameof(process));
        }

        Process = process;
        IntegrationOrder = integrationOrder;

        // Some of the required constant intermediate variables can be calculated now
        kappaHat = process.Kappa - (process.Rho * process.Sigma);
        thetaHat = process.Kappa * process.Theta / kappaHat;

        // R = 4κθ/σ² where κ̂ = 0, QuantLib's 0·∞
        r = kappaHat == 0.0
            ? 4 * process.Kappa * process.Theta / (process.Sigma * process.Sigma)
            : 4 * kappaHat * thetaHat / (process.Sigma * process.Sigma);
    }

    /// <summary>Gets the Heston process.</summary>
    public HestonProcess Process { get; }

    /// <summary>Gets the order of the analytic Heston engines <see cref="ForwardChF"/> builds.</summary>
    public int IntegrationOrder { get; }

    /// <summary>Prices a forward-starting European option.</summary>
    /// <param name="option">The option, with a plain-vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="integrationOrder">The order of the analytic Heston engines the characteristic functions come
    /// from.</param>
    /// <returns>The option's results: the value and QuantLib's additional results; the expired results when the option
    /// has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The process's vol of vol is at most 0.1.</exception>
    /// <exception cref="NanoQLException"> The option's terms are invalid, its exercise isn't European, its payoff isn't
    /// plain vanilla, or the closed form's integrand doesn't decay. </exception>
    public static OneAssetOptionResults Calculate(
        ForwardVanillaOption option, EvaluationContext context, HestonProcess process, int integrationOrder = 144) =>
        new AnalyticHestonForwardEuropeanEngine(process, integrationOrder).Calculate(option, context);

    /// <summary>Prices a forward-starting European option.</summary>
    /// <param name="option">The option, with a plain-vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results: the value and QuantLib's additional results; the expired results when the option
    /// has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException"> The option's terms are invalid, its exercise isn't European, its payoff isn't
    /// plain vanilla, or the closed form's integrand doesn't decay. </exception>
    public OneAssetOptionResults Calculate(ForwardVanillaOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate(context);
        return option.FetchResults(Body(option));
    }

    /// <summary>
    /// The density of the variance at the reset time given the variance today: the non-central χ² law of the CIR
    /// process under the measure Kruse's equation (18) takes, with κ̂ = κ − ρσ and θ̂ = κθ/κ̂.
    /// </summary>
    /// <param name="resetTime">The reset time, positive.</param>
    /// <param name="varReset">The variance at the reset, positive.</param>
    /// <returns>The density.</returns>
    public double Propagator(double resetTime, double varReset)
    {
        (double b, double lambda) = TransitionParameters(resetTime);

        // Now construct equation (18) from the paper term-by-term
        double term1 = Math.Exp(-0.5 * ((b * varReset) + lambda)) * b / 2;
        double term2 = Math.Pow(b * varReset / lambda, 0.5 * ((r / 2) - 1));
        double x = Math.Sqrt(lambda * b * varReset);
        double term3 = ModifiedBesselFunctions.ModifiedBesselFunction_i((r / 2) - 1, x);
        double product = term1 * term2 * term3;
        if (double.IsFinite(product) && product != 0.0)
        {
            return product;
        }

        // the scaled form where QuantLib's product underflows times overflows
        double scaled = Math.Exp((-0.5 * ((b * varReset) + lambda)) + x) * b / 2;
        return scaled * term2 * ModifiedBesselFunctions.ModifiedBesselFunction_i_exponentiallyWeighted((r / 2) - 1, x);
    }

    // The CIR transition's B and Λ: Bν at the reset a noncentral χ² with R degrees of freedom and noncentrality Λ; B
    // through r(x) = x/(−expm1(−x)) below |κ̂t| of 1e-2, where QuantLib's quotient cancels (0/0 at κ̂ = 0)
    private (double B, double Lambda) TransitionParameters(double resetTime)
    {
        double sigma = Process.Sigma;
        double v0 = Process.V0;
        double kt = kappaHat * resetTime;
        double b = Math.Abs(kt) < SmallKappaHat
            ? 4 * RatioToMinusExpm1(kt) / (sigma * sigma * resetTime)
            : 4 * kappaHat / (sigma * sigma * (1 - Math.Exp(-kappaHat * resetTime)));
        double lambda = b * Math.Exp(-kappaHat * resetTime) * v0;
        return (b, lambda);
    }

    /// <summary>
    /// The analytic Heston engine whose characteristic function is the forward one from the reset: the process's curves
    /// and parameters, with the spot and the variance at the reset.
    /// </summary>
    /// <param name="spotReset">The spot at the reset.</param>
    /// <param name="varReset">The variance at the reset.</param>
    /// <returns>The engine, on Gauss-Laguerre integration of the engine's order.</returns>
    public AnalyticHestonEngine ForwardChF(double spotReset, double varReset)
    {
        // Probably a wasteful implementation here, could be improved by importing
        // only the CF-generating parts of the AnalyticHestonEngine (currently private)
        var hestonProcess = new HestonProcess(
            Process.RiskFreeRate,
            Process.DividendYield,
            spotReset,
            varReset,
            Process.Kappa,
            Process.Theta,
            Process.Sigma,
            Process.Rho);
        return new AnalyticHestonEngine(new HestonModel(hestonProcess), IntegrationOrder);
    }

    // QuantLib's calculate().
    private OneAssetOptionResults Body(ForwardVanillaOption option)
    {
        // This is a european option pricer
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        // We only price plain vanillas
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non plain vanilla payoff given");
        }

        // the model's clock: its time from the earliest reference date of the curves, each discount at its instant
        TermStructure clock = Process.ModelClock();
        double resetTime = Process.ModelTime(option.ResetDate);
        double expiryTime = Process.ModelTime(option.Exercise.LastDate());
        double tenor = expiryTime - resetTime;
        double moneyness = option.Moneyness;
        double s0 = Process.S0;

        // K needs to be scaled to forward AT RESET TIME, not spot...
        double expiryDcf = Process.RiskFreeRate.DiscountAt(clock, expiryTime);
        double resetDcf = Process.RiskFreeRate.DiscountAt(clock, resetTime);
        double expiryDividendDiscount = Process.DividendYield.DiscountAt(clock, expiryTime);
        double resetDividendDiscount = Process.DividendYield.DiscountAt(clock, resetTime);
        double expiryRatio = expiryDcf / expiryDividendDiscount;
        double resetRatio = resetDcf / resetDividendDiscount;

        // Use some heuristics to decide upon phiRightLimit and nuRightLimit
        double phiRightLimit = 100.0;
        double nuRightLimit = Math.Max(
            2.0,
            10.0 * (1 + Math.Max(0.0, Process.Rho)) * Process.Sigma * Math.Sqrt(resetTime * Math.Max(Process.V0, Process.Theta)));

        // do the 2D integral calculation. For very short times, we just fall back on the standard calculation, both for
        // accuracy and because tStar==0 causes some numerical issues... QuantLib's probabilities where they agree with
        // the resolved route's
        (double P1, double P2) p1HatP2Hat = resetTime <= ResetCut
            ? Agreed(
                CalculateP1P2(tenor, s0, moneyness * s0, expiryRatio, phiRightLimit),
                () => VanillaP1P2(tenor, Math.Log(moneyness * s0 * expiryRatio / s0)))
            : CalculateP1P2Hat(tenor, resetTime, moneyness, expiryRatio / resetRatio, phiRightLimit, nuRightLimit);

        // Apply the payoff functions
        double f = s0 / expiryRatio;
        double value = payoff.OptionType switch
        {
            OptionType.Call => expiryDcf * ((f * p1HatP2Hat.P1) - (moneyness * s0 * p1HatP2Hat.P2 / resetRatio)),
            OptionType.Put => expiryDcf * ((moneyness * s0 * (1 - p1HatP2Hat.P2) / resetRatio) - (f * (1 - p1HatP2Hat.P1))),
            _ => throw new NanoQLException("unknown option type"),
        };

        return new OneAssetOptionResults
        {
            NPV = value,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty
                .Add("dcf", expiryDcf)
                .Add("qf", expiryDividendDiscount)
                .Add("expiryRatio", expiryRatio)
                .Add("resetRatio", resetRatio)
                .Add("moneyness", moneyness)
                .Add("s0", s0)
                .Add("fwd", f)
                .Add("resetTime", resetTime)
                .Add("expiryTime", expiryTime)
                .Add("P1Hat", p1HatP2Hat.P1)
                .Add("P2Hat", p1HatP2Hat.P2)
                .Add("phiRightLimit", phiRightLimit)
                .Add("nuRightLimit", nuRightLimit),
        };
    }

    // Integrate P1, P2 over the propagator function to calculate the forward-start price
    private (double P1, double P2) CalculateP1P2Hat(
        double tenor, double resetTime, double moneyness, double ratio, double phiRightLimit, double nuRightLimit)
    {
        // Re-expressing moneyness in terms of the forward here (strike fixes to spot, but in
        // our pricing calculation we need to compare it to the future at expiry)
        double logMoneyness = Math.Log(moneyness * ratio);

        double P12Hat(bool p1, double nu)
        {
            // Rescale nu to [-1, 1]
            double nuDash = nuRightLimit * ((0.5 * nu) + 0.5 + 1e-8);

            // Calculate the chF from var(t) to expiry
            AnalyticHestonEngine engine = ForwardChF(1.0, nuDash);
            double p1Integral = Integrator.Evaluate(phi => P12(engine, logMoneyness, tenor, p1, phiRightLimit, phi));

            // Calculate the value of the propagator to nu
            double propagator = Propagator(resetTime, nuDash);

            // Take the product, and scale integral's value back up to [0, right_lim]
            return propagator * (0.5 + (p1Integral / Math.PI));
        }

        // QuantLib's nodes where they carry the propagator's unit mass
        double mass = 0.5 * nuRightLimit * Integrator.Evaluate(
            nu => Propagator(resetTime, nuRightLimit * ((0.5 * nu) + 0.5 + 1e-8)));
        if (Math.Abs(mass - 1.0) <= MassTolerance)
        {
            double p1HatIntegral = 0.5 * nuRightLimit * Integrator.Evaluate(nu => P12Hat(true, nu));
            double p2HatIntegral = 0.5 * nuRightLimit * Integrator.Evaluate(nu => P12Hat(false, nu));

            // QuantLib's where they agree with the closed form
            return Agreed((p1HatIntegral, p2HatIntegral), () => AffineP1P2Hat(tenor, resetTime, logMoneyness));
        }

        return AffineP1P2Hat(tenor, resetTime, logMoneyness);
    }

    // The forward-start probabilities where QuantLib's nodes miss the propagator's mass: QuantLib's characteristic
    // function from the reset is exp(a(z) + νb(z)), affine in the reset variance (LnChF), so its expectation over the
    // propagator, the CIR transition under κ̂ and θ̂ (Bν noncentral χ² with R degrees of freedom and noncentrality Λ),
    // is exp(a) M(b), M(z) = (1 − 2z/B)^(−R/2) exp((Λ/B) z/(1 − 2z/B)), its moment-generating function; each
    // probability then the resolved half-line integral, where QuantLib integrates each variance's on 128 nodes to a
    // fixed limit and the outer integral over the variance
    private (double P1, double P2) AffineP1P2Hat(double tenor, double resetTime, double logMoneyness)
    {
        (double b, double lambda) = TransitionParameters(resetTime);
        double halfR = r / 2;
        double kappa = Process.Kappa, theta = Process.Theta, sigma = Process.Sigma, rho = Process.Rho;

        Complex Expected(Complex z)
        {
            (Complex a, Complex bz) = AffineLnChF(z, tenor, kappa, theta, sigma, rho);
            Complex den = 1.0 - (2.0 * bz / b);
            return Complex.Exp(a + (lambda / b * bz / den)) * Complex.Pow(den, -halfR);
        }

        return ResolvedP1P2(Expected, logMoneyness);
    }

    // The reset-today probabilities resolved: QuantLib's characteristic function at the spot's variance v₀ on the
    // resolved half-line integral, the vanilla's
    private (double P1, double P2) VanillaP1P2(double tenor, double logK)
    {
        double kappa = Process.Kappa, theta = Process.Theta, sigma = Process.Sigma, rho = Process.Rho, v0 = Process.V0;

        Complex ChF(Complex z)
        {
            (Complex a, Complex bz) = AffineLnChF(z, tenor, kappa, theta, sigma, rho);
            return Complex.Exp(a + (v0 * bz));
        }

        return ResolvedP1P2(ChF, logK);
    }

    // QuantLib's probabilities where each is within 1e-7 of the resolved route's, the resolved route's elsewhere: its
    // inner integral, 128 nodes to φ = 100, is off by about 1e-8 at a year's tenor and by 1e-6 and more at a month's
    // and less, where the return's distribution is narrow and the integrand hasn't decayed by 100
    private static (double P1, double P2) Agreed((double P1, double P2) quantLib, Func<(double P1, double P2)> resolved)
    {
        (double p1, double p2) = resolved();
        return Math.Abs(quantLib.P1 - p1) <= ProbabilityAgreement && Math.Abs(quantLib.P2 - p2) <= ProbabilityAgreement
            ? quantLib
            : (p1, p2);
    }

    // Both probabilities from an expected characteristic function: QuantLib's P12 integrand on the half-line, on
    // Gauss-Legendre pieces from 0 whose width doubles from 1 to at most min(8, π/|log K|), half the strike factor's
    // period, until three pieces in a row contribute at most 1e-17
    private static (double P1, double P2) ResolvedP1P2(Func<Complex, Complex> expected, double logK)
    {
        double widest = logK == 0.0 ? MaxPieceWidth : Math.Min(MaxPieceWidth, Math.PI / Math.Abs(logK));
        Complex i = Complex.ImaginaryOne;

        double Probability(bool p1)
        {
            Complex adj = p1 ? new Complex(0.0, -1.0) : Complex.Zero;
            double Integrand(double phi) => (Complex.Exp(-phi * logK * i) / (phi * i) * expected(phi + adj)).Real;

            double sum = 0.0, lo = 0.0, width = Math.Min(1.0, widest);
            int quiet = 0;
            while (quiet < 3)
            {
                if (!(lo < ResolvedUpperEnd))
                {
                    throw new NanoQLException("the forward-start probabilities' integrand did not decay");
                }

                double from = lo, half = 0.5 * width;
                double piece = half * PieceRule.Evaluate(x => Integrand(from + (half * (x + 1.0))));
                sum += piece;
                quiet = Math.Abs(piece) <= PieceQuiet ? quiet + 1 : 0;
                lo += width;
                width = Math.Min(2.0 * width, widest);
            }

            return 0.5 + (sum / Math.PI);
        }

        return (Probability(true), Probability(false));
    }

    // QuantLib's LnChF (AnalyticHestonEngine's, Andersen and Lake's form) split as a + νb, ν the variance at its start,
    // the same arithmetic
    private static (Complex A, Complex B) AffineLnChF(Complex z, double t, double kappa, double theta, double sigma, double rho)
    {
        double sigma2 = sigma * sigma;
        Complex g = kappa + (rho * sigma * new Complex(z.Imaginary, -z.Real));
        Complex d = Complex.Sqrt((g * g) + (((z * z) + new Complex(-z.Imaginary, z.Real)) * sigma2));
        Complex r = g - d;
        if ((g.Real * d.Real) + (g.Imaginary * d.Imaginary) > 0.0)
        {
            r = -sigma2 * z * new Complex(z.Real, z.Imaginary + 1) / (g + d);
        }

        Complex y = d.Real != 0.0 || d.Imaginary != 0.0 ? Expm1Functions.Expm1(-d * t) / (2.0 * d) : -0.5 * t;
        Complex a = kappa * theta / sigma2 * ((r * t) - (2.0 * Expm1Functions.Log1p(-r * y)));
        Complex bz = z * new Complex(z.Real, z.Imaginary + 1) * y / (1.0 - (r * y));
        return (a, bz);
    }

    // Integrate over the characteristic function to generate P1, P2 (fall back on this
    // when reset time is very close, to avoid numerical issues with tReset=0s)
    private (double P1, double P2) CalculateP1P2(double tenor, double st, double k, double ratio, double phiRightLimit)
    {
        AnalyticHestonEngine engine = ForwardChF(st, Process.V0);
        double logK = Math.Log(k * ratio / st);

        // Integrate the CF and the complex integrand over positive phi
        double p1Integral = Integrator.Evaluate(phi => P12(engine, logK, tenor, true, phiRightLimit, phi));
        double p2Integral = Integrator.Evaluate(phi => P12(engine, logK, tenor, false, phiRightLimit, phi));
        return (0.5 + (p1Integral / Math.PI), 0.5 + (p2Integral / Math.PI));
    }

    // QuantLib's P12Integrand: the Gauss-Legendre point phi in [−1, 1] mapped to (0, phiRightLimit], the only
    // difference between P1 and P2 the chF's shift by −i
    private static double P12(AnalyticHestonEngine engine, double logK, double tenor, bool p1, double phiRightLimit, double phi)
    {
        Complex adj = p1 ? new Complex(0.0, -1.0) : Complex.Zero;
        double phiDash = (0.5 + 1e-8 + (0.5 * phi)) * phiRightLimit; // Map phi to full range
        Complex i = Complex.ImaginaryOne;
        return 0.5 * phiRightLimit * (Complex.Exp(-phiDash * logK * i) / (phiDash * i) * engine.ChF(phiDash + adj, tenor)).Real;
    }

    // r(x) = x/(−expm1(−x)), r(0) = 1
    private static double RatioToMinusExpm1(double x) => x == 0.0 ? 1.0 : x / -Expm1Functions.StdExpm1(-x);
}
