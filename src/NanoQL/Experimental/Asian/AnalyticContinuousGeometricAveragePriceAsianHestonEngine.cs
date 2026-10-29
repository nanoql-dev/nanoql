// SPDX-FileCopyrightText: Copyright (C) 2020 Jack Gillett
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/asian/analytic_cont_geom_av_price_heston.hpp ql/experimental/asian/analytic_cont_geom_av_price_heston.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// Kim and Wee's analytic engine for continuous geometric average-price Asian options under the Heston model ("Pricing
/// of geometric Asian options under Heston's stochastic volatility model", Quantitative Finance 14:10, 2014): the joint
/// characteristic function of the log geometric average and the log spot, <see cref="Phi"/>, inverted by a Fourier
/// integral on 128 Gauss-Legendre points.
/// </summary>
/// <remarks>
/// <para>
/// An unsealed, immutable object holding the process, the summation cutoff and the integral's limit, its public member
/// QuantLib's <see cref="Phi"/>, with a static <c>Calculate</c> as D14's form (the S-xeq plan's §2.0). <c>Phi</c>
/// computes its coefficients from its times on each call, where QuantLib's read the last pricing's (0 before any); the
/// recursion of its series, QuantLib's memo, is one call's working state. The value, with QuantLib's additional results
/// ("dcf", "qf", "s0", "strike", "expiryTime", "exercise", "term1", "term2", "xiRightLimit", "summationCutoff", "a1" to
/// "a5").
/// </para>
/// <para>
/// Documented divergences (<c>Divergences/HestonContinuousGeometricAsianTests.cs</c>). <c>Phi</c>'s coefficient of w is
/// log S₀ + log(D_q/D_r) − ρv₀/σ − ρκθτ/σ, log S_T's constant, where QuantLib's multiplies the log spot by the carry
/// factor and adds ρκθτ/σ, the sign its a3 doesn't take, so its <c>Phi(0, 1)</c>, the expected terminal spot, is 81.92
/// against the forward 103.05 (XER5 (xv)); the prices read w = 0 only and are QuantLib's. An option whose averaging
/// starts before or after the model's time 0 is refused, as QuantLib's Black-Scholes continuous engine refuses a start
/// date, where QuantLib's Heston engine prices it as unseasoned from today; one starting at time 0 is QuantLib's own
/// case. Times run on the model's clock, <see cref="HestonProcess"/>'s model time from the earliest reference date of
/// its curves, each discount read at its instants, none accrued before its curve's own date (XER5 (xxxiii); S-hes's
/// HR7).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class AnalyticContinuousGeometricAveragePriceAsianHestonEngine
{
    private static readonly GaussLegendreIntegration Integrator = new(128);

    private readonly double a1;
    private readonly double a2;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticContinuousGeometricAveragePriceAsianHestonEngine"/> class.
    /// </summary>
    /// <param name="process">The Heston process.</param>
    /// <param name="summationCutoff">The number of terms of the series (19) and (20).</param>
    /// <param name="xiRightLimit">The upper limit of the Fourier integral (29).</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="summationCutoff"/> is negative.</exception>
    public AnalyticContinuousGeometricAveragePriceAsianHestonEngine(
        HestonProcess process, int summationCutoff = 50, double xiRightLimit = 100.0)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegative(summationCutoff);
        Process = process;
        SummationCutoff = summationCutoff;
        XiRightLimit = xiRightLimit;

        // Some of the required constant intermediate variables can be calculated now
        // (although anything depending on T will need to be calculated dynamically later)
        a1 = 2.0 * process.V0 / (process.Sigma * process.Sigma);
        a2 = 2.0 * process.Kappa * process.Theta / (process.Sigma * process.Sigma);
    }

    /// <summary>Gets the Heston process.</summary>
    public HestonProcess Process { get; }

    /// <summary>Gets the number of terms of the series (19) and (20).</summary>
    public int SummationCutoff { get; }

    /// <summary>Gets the upper limit of the Fourier integral (29).</summary>
    public double XiRightLimit { get; }

    /// <summary>Prices a continuous geometric average-price Asian option under the Heston model.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="summationCutoff">The number of terms of the series (19) and (20).</param>
    /// <param name="xiRightLimit">The upper limit of the Fourier integral (29).</param>
    /// <returns>The option's results: the value and QuantLib's additional results; the expired results when the option
    /// has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="summationCutoff"/> is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The average isn't geometric, the exercise isn't European, the option has a start date, or the payoff isn't plain
    /// vanilla.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ContinuousAveragingAsianOption option,
        EvaluationContext context,
        HestonProcess process,
        int summationCutoff = 50,
        double xiRightLimit = 100.0) =>
        new AnalyticContinuousGeometricAveragePriceAsianHestonEngine(process, summationCutoff, xiRightLimit)
            .Calculate(option, context);

    /// <summary>Prices a continuous geometric average-price Asian option under the Heston model.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results: the value and QuantLib's additional results; the expired results when the option
    /// has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The average isn't geometric, the exercise isn't European, the option has a start date, or the payoff isn't plain
    /// vanilla.
    /// </exception>
    public OneAssetOptionResults Calculate(ContinuousAveragingAsianOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option));
    }

    /// <summary>
    /// The joint characteristic function of the log geometric average over [t, T] and the log spot at T, Kim and
    /// Wee's equation (25): E[exp(s log G + w log S_T)].
    /// </summary>
    /// <param name="s">The log geometric average's argument.</param>
    /// <param name="w">The log terminal spot's argument.</param>
    /// <param name="T">The expiry time on the model's clock.</param>
    /// <param name="t">The averaging's start time on the model's clock.</param>
    /// <param name="cutoff">The number of terms of the series (19) and (20).</param>
    /// <returns>The characteristic function's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cutoff"/> is negative.</exception>
    public Complex Phi(Complex s, Complex w, double T, double t = 0.0, int cutoff = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cutoff);
        return PhiAt(CoefficientsAt(T, t), s, w, T, t, cutoff);
    }

    // QuantLib's calculate().
    private OneAssetOptionResults Body(ContinuousAveragingAsianOption option)
    {
        if (!(option.AverageType == AverageType.Geometric))
        {
            throw new NanoQLException("not a geometric average option");
        }

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European Option");
        }

        // a seasoned or forward-starting average refused, as QuantLib's Black-Scholes continuous engine refuses it; an
        // average starting at the model's time 0 is the one QuantLib's engine prices (the code review's O1-6)
        if (option.StartDate is Date start && Process.ModelClock().TimeFromReference(start) != 0.0)
        {
            throw new NanoQLException(
                "seasoned continuous geometric Asian options not yet supported - "
                + "requires adjustment of forward price and variance based on "
                + "accumulated geometric average. This feature needs mathematical "
                + "review and implementation.");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        double strike = payoff.Strike;
        Date exercise = option.Exercise.LastDate();

        // the model's clock (HR7)
        TermStructure clock = Process.ModelClock();
        double expiryTime = Process.ModelTime(exercise);
        double expiryDcf = Process.RiskFreeRate.DiscountAt(clock, expiryTime);
        double expiryDividendDiscount = Process.DividendYield.DiscountAt(clock, expiryTime);

        // TODO: extend to cover seasoned options (discussed in paper)
        double startTime = 0.0;

        // These parameters only need to be calculated once per pricing, but are
        // functions of t and T
        double t = startTime;
        double T = expiryTime;
        Coefficients c = CoefficientsAt(T, t);

        // Calculate the two terms in eq (29) - Phi(1,0) is real (asian forward) but need to type convert
        double term1 = 0.5 * (PhiAt(c, 1, 0, T, t, SummationCutoff).Real - strike);

        double logK = Math.Log(strike);
        Complex i = Complex.ImaginaryOne;
        double Integrand(double xi)
        {
            double xiDash = (0.5 + 1e-8 + (0.5 * xi)) * XiRightLimit; // Map xi to full range
            Complex inner1 = PhiAt(c, 1.0 + (xiDash * i), 0, T, t, SummationCutoff);
            Complex inner2 = -strike * PhiAt(c, xiDash * i, 0, T, t, SummationCutoff);
            return 0.5 * XiRightLimit * ((inner1 + inner2) * Complex.Exp(-xiDash * logK * i) / (xiDash * i)).Real;
        }

        double term2 = Integrator.Evaluate(Integrand) / Math.PI;

        // Apply the payoff functions
        double value = payoff.OptionType switch
        {
            OptionType.Call => expiryDcf * (term1 + term2),
            OptionType.Put => expiryDcf * (-term1 + term2),
            _ => throw new NanoQLException("unknown option type"),
        };

        return new OneAssetOptionResults
        {
            NPV = value,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty
                .Add("dcf", expiryDcf)
                .Add("qf", expiryDividendDiscount)
                .Add("s0", Process.S0)
                .Add("strike", strike)
                .Add("expiryTime", expiryTime)
                .Add("exercise", exercise)
                .Add("term1", term1)
                .Add("term2", term2)
                .Add("xiRightLimit", XiRightLimit)
                .Add("summationCutoff", SummationCutoff)
                .Add("a1", a1)
                .Add("a2", a2)
                .Add("a3", c.A3)
                .Add("a4", c.A4)
                .Add("a5", c.A5),
        };
    }

    // a3, a4 and a5 for an average over [t, T]: QuantLib's equations (14) to (17) with r → r(t) − q(t), the discounts
    // on the model's clock; the coefficient of w log S₀ + log(D_q/D_r) − ρv₀/σ − ρκθτ/σ, log S_T's constant, where
    // QuantLib's reads log S₀ · D_q/D_r and +ρκθτ/σ (XER5 (xv); a3 carries the matching −ρκθτ²/(2σT))
    private Coefficients CoefficientsAt(double T, double t)
    {
        TermStructure clock = Process.ModelClock();
        YieldTermStructure riskFree = Process.RiskFreeRate;
        YieldTermStructure dividends = Process.DividendYield;
        double v0 = Process.V0, rho = Process.Rho, kappa = Process.Kappa, theta = Process.Theta, sigma = Process.Sigma;
        double tau = T - t;
        double logS0 = Math.Log(Process.S0);

        // To deal with non-constant rates and dividends, we reformulate Eq.s (14) to (17) with
        // r_ --> (r(t) - q(t)), which gives the new expressions for a3 and a4 used below
        double dcf = riskFree.DiscountAt(clock, T) / riskFree.DiscountAt(clock, t);
        double qdcf = dividends.DiscountAt(clock, T) / dividends.DiscountAt(clock, t);

        // QuantLib's DcfIntegrand: the log carry factors from t, integrated over [t, T]
        double denominator = Math.Log(riskFree.DiscountAt(clock, t)) - Math.Log(dividends.DiscountAt(clock, t));
        double integratedDcf = Integrator.Evaluate(u =>
        {
            double uDash = ((0.5 + 1e-8 + (0.5 * u)) * (T - t)) + t; // Map u to full range
            return 0.5 * (T - t) * (-Math.Log(riskFree.DiscountAt(clock, uDash))
                + Math.Log(dividends.DiscountAt(clock, uDash)) + denominator);
        });

        double a3 = ((tau * logS0) + integratedDcf) / T - (kappa * theta * rho * tau * tau / (2 * sigma * T))
            - (rho * tau * v0 / (sigma * T));
        double a4 = logS0 + Math.Log(qdcf / dcf) - (rho * v0 / sigma) - (rho * kappa * theta * tau / sigma);
        double a5 = ((kappa * v0) + (kappa * kappa * theta * tau)) / (sigma * sigma);
        return new Coefficients(a3, a4, a5);
    }

    // Equation (25) at given coefficients
    private Complex PhiAt(Coefficients c, Complex s, Complex w, double T, double t, int cutoff)
    {
        double tau = T - t;

        Complex z1 = Z1(s, T);
        Complex z2 = Z2(s, w, T);
        Complex z3 = Z3(s, w, T);
        Complex z4 = Z4(w);

        (Complex f, Complex fTilde) = FFTilde(z1, z2, z3, z4, tau, cutoff);
        return Complex.Exp((-a1 * fTilde / f) - (a2 * Complex.Log(f)) + (c.A3 * s) + (c.A4 * w) + c.A5);
    }

    // Equations (13)
    private Complex Z1(Complex s, double T)
    {
        double rho = Process.Rho;
        return s * s * (1 - (rho * rho)) / (2 * T * T);
    }

    private Complex Z2(Complex s, Complex w, double T)
    {
        double rho = Process.Rho, kappa = Process.Kappa, sigma = Process.Sigma;
        return (s * ((2 * rho * kappa) - sigma) / (2 * sigma * T)) + (s * w * (1 - (rho * rho)) / T);
    }

    private Complex Z3(Complex s, Complex w, double T)
    {
        double rho = Process.Rho, kappa = Process.Kappa, sigma = Process.Sigma;
        return (s * rho / (sigma * T)) + (0.5 * w * ((2 * rho * kappa) - sigma) / sigma) + (0.5 * w * w * (1 - (rho * rho)));
    }

    private Complex Z4(Complex w) => w * Process.Rho / Process.Sigma;

    // Equations (19), (20), the f recursion (21) one call's working state, QuantLib's memo of f(n)
    private (Complex F, Complex FTilde) FFTilde(Complex z1, Complex z2, Complex z3, Complex z4, double tau, int cutoff)
    {
        double kappa = Process.Kappa, sigma = Process.Sigma;
        Complex[] fs = new Complex[Math.Max(cutoff, 0)];
        Complex At(int n) => n < 0 ? Complex.Zero : fs[n];

        Complex runningSum1 = Complex.Zero;
        Complex runningSum2 = Complex.Zero;
        for (int i = 0; i < cutoff; i++)
        {
            Complex result;
            if (i == 0)
            {
                result = Complex.One;
            }
            else if (i == 1)
            {
                result = 0.5 * (kappa - (z4 * sigma * sigma)) * tau;
            }
            else
            {
                double prefactor = -0.5 * sigma * sigma * tau * tau / (i * (i - 1));
                result = prefactor * ((z1 * tau * tau * At(i - 4)) + (z2 * tau * At(i - 3))
                    + ((z3 - (0.5 * kappa * kappa / (sigma * sigma))) * At(i - 2)));
            }

            fs[i] = result;
            runningSum1 += result;
            runningSum2 += result * i / tau;
        }

        return (runningSum1, runningSum2);
    }

    // The coefficients a3, a4 and a5 of an average over [t, T]
    private readonly record struct Coefficients(double A3, double A4, double A5);
}
