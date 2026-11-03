// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analytichestonengine.hpp ql/pricingengines/vanilla/analytichestonengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic Heston engine: European plain-vanilla options under the Heston model, by the Fourier transform of the
/// model's characteristic function, integrated by a <see cref="FourierIntegration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two algebraically equivalent formulations of the characteristic function's complex logarithm exist. Gatheral's
/// (also Duffie, Pan and Singleton's, and Schoutens, Simons and Tistaert's) has no discontinuities and works with
/// adaptive integrations; Heston's original needs a branch correction, which refuses them. The control-variate forms,
/// Andersen and Piterbarg's, the asymptotic expansion's and the angled contour's, integrate the difference from a value
/// they price in closed form.
/// </para>
/// <para>
/// Immutable: it holds the model and the integration settings. Each price returns its number of evaluations with it
/// (<see cref="PriceVanillaPayoffWithStatistics(PlainVanillaPayoff, Date)"/>, and the results' additional result
/// "numberOfEvaluations"), where QuantLib keeps the last call's count on the engine. The static <c>Calculate</c>
/// overloads build an engine with the given settings and price through it.
/// </para>
/// <para>
/// An extension point, as in QuantLib: jump-diffusion engines such as Bates's derive from it and add their term to
/// the characteristic function through <see cref="AddOnTerm"/>.
/// </para>
/// <para>
/// A price at a maturity date runs the model's time on the risk-free curve's day counter from the earliest reference
/// date of the model's curves, each accruing nothing before its own, so the variance accrues from the evaluation date
/// beside a risk-free curve settling later, as QuantLib's engine on the market restated from that date prices it;
/// QuantLib's time runs from the risk-free curve's date, so its reads throw before that date and its integrations read
/// 0/0 on it. An option expiring at the model's time 0, on or before every curve's date, is priced at the forward's
/// intrinsic value. Where the dividend curve isn't dated before the risk-free curve, the time is QuantLib's. A
/// documented divergence (<c>Divergences/ExerciseBeforeReferenceDateTests.cs</c>,
/// <c>Divergences/HestonMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
public class AnalyticHestonEngine
{
    // QuantLib's Null<Real>(): the Gauss-Laguerre engine's Andersen-Piterbarg epsilon, which no bound reads
    private const double NullReal = 3.4028234663852886e38;

    private readonly AnalyticHestonEngineComplexLogFormula cpxLog;
    private readonly FourierIntegration integration;
    private readonly double andersenPiterbargEpsilon;
    private readonly double alpha;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticHestonEngine"/> class: adaptive Gauss-Lobatto integration,
    /// the best control variate for each option. Too large a maximum number of evaluations deepens the recursion.
    /// </summary>
    /// <param name="model">The Heston model.</param>
    /// <param name="relTolerance">The integration's relative tolerance.</param>
    /// <param name="maxEvaluations">The integration's maximum number of evaluations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    public AnalyticHestonEngine(HestonModel model, double relTolerance, int maxEvaluations)
        : this(
            model,
            AnalyticHestonEngineComplexLogFormula.OptimalCV,
            FourierIntegration.GaussLobatto(relTolerance, null, maxEvaluations),
            1e-40,
            -0.5,
            false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticHestonEngine"/> class: Gauss-Laguerre integration, the best
    /// control variate for each option.
    /// </summary>
    /// <param name="model">The Heston model.</param>
    /// <param name="integrationOrder">The quadrature's order, positive, at most 192.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The order is zero or above 192.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public AnalyticHestonEngine(HestonModel model, int integrationOrder = 144)
        : this(
            model,
            AnalyticHestonEngineComplexLogFormula.OptimalCV,
            FourierIntegration.GaussLaguerre(integrationOrder),
            NullReal,
            -0.5,
            false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticHestonEngine"/> class, with full control over the Fourier
    /// integration.
    /// </summary>
    /// <param name="model">The Heston model.</param>
    /// <param name="cpxLog">The complex logarithm's formulation, or the control variate.</param>
    /// <param name="integration">The Fourier integration.</param>
    /// <param name="andersenPiterbargEpsilon">
    /// The tolerance of Andersen and Piterbarg's integration limit, which bounds an adaptive integration under a
    /// control variate.
    /// </param>
    /// <param name="alpha">The control variates' damping exponent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="integration"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cpxLog"/> isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">The branch correction is given an adaptive integration.</exception>
    public AnalyticHestonEngine(
        HestonModel model,
        AnalyticHestonEngineComplexLogFormula cpxLog,
        FourierIntegration integration,
        double andersenPiterbargEpsilon = 1e-25,
        double alpha = -0.5)
        : this(model, cpxLog, integration, andersenPiterbargEpsilon, alpha, true)
    {
    }

    private AnalyticHestonEngine(
        HestonModel model,
        AnalyticHestonEngineComplexLogFormula cpxLog,
        FourierIntegration integration,
        double andersenPiterbargEpsilon,
        double alpha,
        bool checkBranchCorrection)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(integration);
        EnumArguments.Check(cpxLog, nameof(cpxLog));
        if (checkBranchCorrection
            && !(cpxLog != AnalyticHestonEngineComplexLogFormula.BranchCorrection || !integration.IsAdaptiveIntegration))
        {
            throw new ArgumentException(
                "Branch correction does not work in conjunction with adaptive integration methods", nameof(cpxLog));
        }

        Model = model;
        this.cpxLog = cpxLog;
        this.integration = integration;
        this.andersenPiterbargEpsilon = andersenPiterbargEpsilon;
        this.alpha = alpha;
    }

    /// <summary>Gets the Heston model the engine prices with.</summary>
    protected internal HestonModel Model { get; }

    /// <summary>
    /// Prices a European plain-vanilla option with Gauss-Laguerre integration and the best control variate for it.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="integrationOrder">The quadrature's order, positive, at most 192.</param>
    /// <returns>The option's results: its value, and the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The order is zero or above 192.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, or the integration
    /// fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, HestonModel model, int integrationOrder = 144)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        return new AnalyticHestonEngine(model, integrationOrder).Calculate(option, context);
    }

    /// <summary>
    /// Prices a European plain-vanilla option with adaptive Gauss-Lobatto integration and the best control variate for
    /// it.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="relTolerance">The integration's relative tolerance.</param>
    /// <param name="maxEvaluations">The integration's maximum number of evaluations.</param>
    /// <returns>The option's results: its value, and the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, or the integration
    /// fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, HestonModel model, double relTolerance, int maxEvaluations)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        return new AnalyticHestonEngine(model, relTolerance, maxEvaluations).Calculate(option, context);
    }

    /// <summary>Prices a European plain-vanilla option, with full control over the Fourier integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="cpxLog">The complex logarithm's formulation, or the control variate.</param>
    /// <param name="integration">The Fourier integration.</param>
    /// <param name="andersenPiterbargEpsilon">The tolerance of Andersen and Piterbarg's integration limit.</param>
    /// <param name="alpha">The control variates' damping exponent.</param>
    /// <returns>The option's results: its value, and the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cpxLog"/> isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">The branch correction is given an adaptive integration.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, the asymptotic
    /// control variate's damping exponent isn't −0.5, or the integration fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        AnalyticHestonEngineComplexLogFormula cpxLog,
        FourierIntegration integration,
        double andersenPiterbargEpsilon = 1e-25,
        double alpha = -0.5)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        return new AnalyticHestonEngine(model, cpxLog, integration, andersenPiterbargEpsilon, alpha)
            .Calculate(option, context);
    }

    /// <summary>
    /// The control variate that <see cref="AnalyticHestonEngineComplexLogFormula.OptimalCV"/> chooses: the asymptotic
    /// expansion where it converges fast, the angled contour otherwise.
    /// </summary>
    /// <param name="t">The maturity.</param>
    /// <param name="v0">The variance's value.</param>
    /// <param name="kappa">The variance's mean-reversion speed.</param>
    /// <param name="theta">The variance's long-run mean.</param>
    /// <param name="sigma">The variance's volatility.</param>
    /// <param name="rho">The correlation.</param>
    /// <returns><see cref="AnalyticHestonEngineComplexLogFormula.AsymptoticChF"/> or
    /// <see cref="AnalyticHestonEngineComplexLogFormula.AngledContour"/>.</returns>
    public static AnalyticHestonEngineComplexLogFormula OptimalControlVariate(
        double t, double v0, double kappa, double theta, double sigma, double rho)
    {
        if (t > 0.15 && (v0 + (t * kappa * theta)) / sigma * Math.Sqrt(1 - (rho * rho)) < 0.15
            && ((((kappa - (0.5 * rho * sigma)) * (v0 + (t * kappa * theta)))
                 + (kappa * theta * Math.Log(4 * (1 - (rho * rho))))) / (sigma * sigma)) < 0.1)
        {
            return AnalyticHestonEngineComplexLogFormula.AsymptoticChF;
        }
        else
        {
            return AnalyticHestonEngineComplexLogFormula.AngledContour;
        }
    }

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results: its value, and the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, the asymptotic
    /// control variate's damping exponent isn't −0.5, or the integration fails.
    /// </exception>
    public OneAssetOptionResults Calculate(VanillaOption option, EvaluationContext context)
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

    /// <summary>The model's normalized characteristic function at <paramref name="z"/>.</summary>
    /// <param name="z">The argument.</param>
    /// <param name="t">The time.</param>
    /// <returns>The characteristic function's value.</returns>
    /// <remarks>Its second-order expansion in σ where σ is at most 1e-6 and κ at least 1e-8; the exponential of
    /// <see cref="LnChF"/> otherwise.</remarks>
    public Complex ChF(Complex z, double t)
    {
        if (Model.Sigma > 1e-6 || Model.Kappa < 1e-8)
        {
            return Complex.Exp(LnChF(z, t));
        }
        else
        {
            double kappa = Model.Kappa;
            double sigma = Model.Sigma;
            double theta = Model.Theta;
            double rho = Model.Rho;
            double v0 = Model.V0;

            double sigma2 = sigma * sigma;

            double kt = kappa * t;
            double ekt = Math.Exp(kt);
            double e2kt = Math.Exp(2 * kt);
            double rho2 = rho * rho;
            Complex zpi = z + new Complex(0.0, 1.0);

            double a = theta - v0 + (ekt * (((-1 + kt) * theta) + v0));
            double b = (2 * theta) + (kt * theta) - v0 - (kt * v0) + (ekt * (((-2 + kt) * theta) + v0));

            return Complex.Exp(-(a * z * zpi / ekt) / (2.0 * kappa))

                + (Complex.Exp(-kt - (a * z * zpi / (2.0 * ekt * kappa))) * rho * b
                   * (1.0 - new Complex(-z.Imaginary, z.Real)) * z * z / (2.0 * kappa * kappa) * sigma)

                + (Complex.Exp((-2 * kt) - (a * z * zpi / (2.0 * ekt * kappa))) * z * z * zpi
                   * ((-2 * rho2 * Squared(b) * z * z * zpi)
                      + (2 * kappa * v0 * (-zpi + (e2kt * (zpi + (4 * rho2 * z)))
                                           - (2 * ekt * ((2 * rho2 * z) + (kt * (zpi + (rho2 * (2 + kt) * z)))))))
                      + (kappa * theta * (zpi + (e2kt * ((-5.0 * zpi) - (24 * rho2 * z) + (2 * kt * (zpi + (4 * rho2 * z)))))
                                          + (4 * ekt * (zpi + (6 * rho2 * z) + (kt * (zpi + (rho2 * (4 + kt) * z))))))))
                   / (16.0 * Squared(Squared(kappa))) * sigma2);
        }
    }

    /// <summary>The logarithm of the model's normalized characteristic function at <paramref name="z"/>.</summary>
    /// <param name="z">The argument.</param>
    /// <param name="t">The time.</param>
    /// <returns>The logarithm, by Andersen and Lake's form, which reduces cancellation errors.</returns>
    public Complex LnChF(Complex z, double t)
    {
        double kappa = Model.Kappa;
        double sigma = Model.Sigma;
        double theta = Model.Theta;
        double rho = Model.Rho;
        double v0 = Model.V0;

        double sigma2 = sigma * sigma;

        Complex g = kappa + (rho * sigma * new Complex(z.Imaginary, -z.Real));

        Complex d = Complex.Sqrt((g * g) + (((z * z) + new Complex(-z.Imaginary, z.Real)) * sigma2));

        // reduce cancelation errors, see. L. Andersen and M. Lake
        Complex r = g - d;
        if ((g.Real * d.Real) + (g.Imaginary * d.Imaginary) > 0.0)
        {
            r = -sigma2 * z * new Complex(z.Real, z.Imaginary + 1) / (g + d);
        }

        Complex y;
        if (d.Real != 0.0 || d.Imaginary != 0.0)
        {
            y = Expm1Functions.Expm1(-d * t) / (2.0 * d);
        }
        else
        {
            y = -0.5 * t;
        }

        Complex a = kappa * theta / sigma2 * ((r * t) - (2.0 * Expm1Functions.Log1p(-r * y)));
        Complex b = z * new Complex(z.Real, z.Imaginary + 1) * y / (1.0 - (r * y));

        return a + (v0 * b);
    }

    /// <summary>The value of a European plain-vanilla payoff at a maturity date.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="maturity">The maturity: the forward and the discount read by date, the model's time from the
    /// earliest curve date.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The underlying isn't positive, the asymptotic control variate's damping exponent isn't −0.5, or the integration
    /// fails.
    /// </exception>
    public double PriceVanillaPayoff(PlainVanillaPayoff payoff, Date maturity) =>
        PriceVanillaPayoffWithStatistics(payoff, maturity).Price;

    /// <summary>The value of a European plain-vanilla payoff at a maturity time.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="maturity">The maturity, the time each curve is read at.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The underlying isn't positive, the asymptotic control variate's damping exponent isn't −0.5, or the integration
    /// fails.
    /// </exception>
    public double PriceVanillaPayoff(PlainVanillaPayoff payoff, double maturity) =>
        PriceVanillaPayoffWithStatistics(payoff, maturity).Price;

    /// <summary>The value of a European plain-vanilla payoff at a maturity date, with the call's number of
    /// evaluations.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="maturity">The maturity: the forward and the discount read by date, the model's time from the
    /// earliest curve date.</param>
    /// <returns>The value, and the integrations' number of evaluations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The underlying isn't positive, the asymptotic control variate's damping exponent isn't −0.5, or the integration
    /// fails.
    /// </exception>
    public (double Price, int Evaluations) PriceVanillaPayoffWithStatistics(PlainVanillaPayoff payoff, Date maturity)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        HestonProcess process = Model.Process;

        // the forward by date, each curve accruing nothing before its reference date, where QuantLib's reads throw; the
        // model's time on the risk-free curve's day counter from the earliest curve date, where QuantLib's runs from
        // the risk-free curve's
        double fwd = process.S0 * process.DividendYield.AccruedDiscount(maturity) / process.RiskFreeRate.AccruedDiscount(maturity);

        return PriceVanillaPayoff(payoff, process.ModelTime(maturity), fwd, process.RiskFreeRate.AccruedDiscount(maturity));
    }

    /// <summary>The value of a European plain-vanilla payoff at a maturity time, with the call's number of
    /// evaluations.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="maturity">The maturity, the time each curve is read at.</param>
    /// <returns>The value, and the integrations' number of evaluations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The underlying isn't positive, the asymptotic control variate's damping exponent isn't −0.5, or the integration
    /// fails.
    /// </exception>
    public (double Price, int Evaluations) PriceVanillaPayoffWithStatistics(PlainVanillaPayoff payoff, double maturity)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        HestonProcess process = Model.Process;
        double fwd = process.S0 * process.DividendYield.Discount(maturity) / process.RiskFreeRate.Discount(maturity);

        return PriceVanillaPayoff(payoff, maturity, fwd, process.RiskFreeRate.Discount(maturity));
    }

    // The time form on another structure's clock (the Heston surface's): the time on the clock, the forward and the
    // risk-free discount read at the instant it denotes; QuantLib's time form where the clock shares the curves' times.
    internal (double Price, int Evaluations) PriceVanillaPayoffWithStatistics(PlainVanillaPayoff payoff, TermStructure clock, double maturity)
    {
        HestonProcess process = Model.Process;
        double dr = process.RiskFreeRate.DiscountAt(clock, maturity);
        double fwd = process.S0 * process.DividendYield.DiscountAt(clock, maturity) / dr;
        return PriceVanillaPayoff(payoff, maturity, fwd, dr);
    }

    // The add-on term, for the control variate's check that the engine prices the Heston model alone.
    internal Complex AddOnTermOf(double phi, double t, int j) => AddOnTerm(phi, t, j);

    /// <summary>
    /// The term an extended stochastic-volatility or jump-diffusion engine adds to the characteristic function's
    /// exponent: none for the Heston model.
    /// </summary>
    /// <param name="phi">The integration variable.</param>
    /// <param name="t">The maturity.</param>
    /// <param name="j">The probability's index, 1 or 2.</param>
    /// <returns>The term; zero here.</returns>
    protected virtual Complex AddOnTerm(double phi, double t, int j) => new(0, 0);

    // std::max and std::min, which return their first argument where neither is less than the other
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    private static double Squared(double x) => x * x;

    // QuantLib's calculate: the engine's body, without the instrument's expiry check
    private OneAssetOptionResults Body(VanillaOption option)
    {
        // this is a european option pricer
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        // plain vanilla
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non plain vanilla payoff given");
        }

        Date exerciseDate = option.Exercise.LastDate();

        (double value, int evaluations) = PriceVanillaPayoffWithStatistics(payoff, exerciseDate);
        return new OneAssetOptionResults
        {
            NPV = value,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("numberOfEvaluations", evaluations),
        };
    }

    // The value at the model's time to maturity, on the forward and the risk-free discount the caller read: QuantLib's
    // reads the discount at that time, the risk-free curve's own, and the date form reads it by date, since its model
    // time runs from the earliest curve date
    private (double Price, int Evaluations) PriceVanillaPayoff(PlainVanillaPayoff payoff, double maturity, double fwd, double dr)
    {
        double value;

        HestonProcess process = Model.Process;

        double strike = payoff.Strike;
        double spot = process.S0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        // at the model's time 0, on or before every curve's reference date, no variance has accrued: the forward's
        // intrinsic value, where QuantLib's integrations read 0/0 (QuantLib's European engine at zero volatility checks
        // it)
        if (maturity == 0.0)
        {
            return (dr * payoff.Evaluate(fwd), 0);
        }

        double df = spot / fwd;
        double dd = dr / df;

        double kappa = Model.Kappa;
        double sigma = Model.Sigma;
        double theta = Model.Theta;
        double rho = Model.Rho;
        double v0 = Model.V0;

        int evaluations = 0;

        switch (cpxLog)
        {
            case AnalyticHestonEngineComplexLogFormula.Gatheral:
            case AnalyticHestonEngineComplexLogFormula.BranchCorrection:
            {
                double c_inf = Min(0.2, Max(0.0001, Math.Sqrt(1.0 - (rho * rho)) / sigma)) * (v0 + (kappa * theta * maturity));

                IntegrationResult p1Result = integration.CalculateWithStatistics(
                    c_inf, new FjHelper(kappa, theta, sigma, v0, spot, rho, this, cpxLog, maturity, strike, df, 1).Evaluate);
                double p1 = p1Result.Value / Math.PI;
                evaluations += p1Result.NumberOfEvaluations;

                IntegrationResult p2Result = integration.CalculateWithStatistics(
                    c_inf, new FjHelper(kappa, theta, sigma, v0, spot, rho, this, cpxLog, maturity, strike, df, 2).Evaluate);
                double p2 = p2Result.Value / Math.PI;
                evaluations += p2Result.NumberOfEvaluations;

                switch (payoff.OptionType)
                {
                    case OptionType.Call:
                        value = (spot * dd * (p1 + 0.5)) - (strike * dr * (p2 + 0.5));
                        break;
                    case OptionType.Put:
                        value = (spot * dd * (p1 - 0.5)) - (strike * dr * (p2 - 0.5));
                        break;
                    default:
                        throw new NanoQLException("unknown option type");
                }
            }

            break;
            case AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg:
            case AnalyticHestonEngineComplexLogFormula.AndersenPiterbargOptCV:
            case AnalyticHestonEngineComplexLogFormula.AsymptoticChF:
            case AnalyticHestonEngineComplexLogFormula.AngledContour:
            case AnalyticHestonEngineComplexLogFormula.AngledContourNoCV:
            case AnalyticHestonEngineComplexLogFormula.OptimalCV:
            {
                double c_inf = Math.Sqrt(1.0 - (rho * rho)) * (v0 + (kappa * theta * maturity)) / sigma;

                double epsilon = andersenPiterbargEpsilon * Math.PI / (Math.Sqrt(strike * fwd) * dr);

                double? UM() => FourierIntegration.AndersenPiterbargIntegrationLimit(c_inf, epsilon, v0, maturity);

                AnalyticHestonEngineComplexLogFormula finalLog = cpxLog == AnalyticHestonEngineComplexLogFormula.OptimalCV
                    ? OptimalControlVariate(maturity, v0, kappa, theta, sigma, rho)
                    : cpxLog;

                var cvHelper = new AnalyticHestonEngineAPHelper(maturity, fwd, strike, finalLog, this, alpha);

                double cvValue = cvHelper.ControlVariateValue();

                double vAvg = ((1 - Math.Exp(-kappa * maturity)) * (v0 - theta) / (kappa * maturity)) + theta;

                double scalingFactor = cpxLog != AnalyticHestonEngineComplexLogFormula.OptimalCV
                                       && cpxLog != AnalyticHestonEngineComplexLogFormula.AsymptoticChF
                    ? Max(0.25, Min(1000.0, 0.25 / Math.Sqrt(0.5 * vAvg * maturity)))
                    : 1.0;

                IntegrationResult hResult = integration.CalculateWithStatistics(c_inf, cvHelper.Evaluate, UM, scalingFactor);
                double h_cv = fwd / Math.PI * hResult.Value;

                evaluations += hResult.NumberOfEvaluations;

                switch (payoff.OptionType)
                {
                    case OptionType.Call:
                        value = (cvValue + h_cv) * dr;
                        break;
                    case OptionType.Put:
                        value = (cvValue + h_cv - (fwd - strike)) * dr;
                        break;
                    default:
                        throw new NanoQLException("unknown option type");
                }
            }

            break;
            default:
                throw new NanoQLException("unknown complex log formula");
        }

        return (value, evaluations);
    }

    // The integrand of the probabilities P1 and P2 of Heston's formula, by Gatheral's form or the branch-corrected one;
    // the branch correction counts the logarithm's branches across the integrand's calls, in order.
    private sealed class FjHelper
    {
        private readonly int j;
        private readonly double kappa;
        private readonly double theta;
        private readonly double sigma;
        private readonly double v0;
        private readonly AnalyticHestonEngineComplexLogFormula cpxLog;

        // helper variables
        private readonly double term;
        private readonly double x;
        private readonly double sx;
        private readonly double dd;
        private readonly double sigma2;
        private readonly double rsigma;
        private readonly double t0;

        private readonly AnalyticHestonEngine engine;

        // log branch counter
        private int b;

        // imag part of last log value
        private double gKm1;

        public FjHelper(
            double kappa,
            double theta,
            double sigma,
            double v0,
            double s0,
            double rho,
            AnalyticHestonEngine engine,
            AnalyticHestonEngineComplexLogFormula cpxLog,
            double term,
            double strike,
            double ratio,
            int j)
        {
            this.j = j;
            this.kappa = kappa;
            this.theta = theta;
            this.sigma = sigma;
            this.v0 = v0;
            this.cpxLog = cpxLog;
            this.term = term;
            x = Math.Log(s0);
            sx = Math.Log(strike);
            dd = x - Math.Log(ratio);
            sigma2 = this.sigma * this.sigma;
            rsigma = rho * this.sigma;
            t0 = kappa - (j == 1 ? rho * sigma : 0.0);
            this.engine = engine;
        }

        public double Evaluate(double phi)
        {
            double rpsig = rsigma * phi;

            Complex t1 = t0 + new Complex(0, -rpsig);
            Complex d = Complex.Sqrt((t1 * t1) - (sigma2 * phi * new Complex(-phi, j == 1 ? 1 : -1)));
            Complex ex = Complex.Exp(-d * term);
            Complex addOnTerm = engine.AddOnTerm(phi, term, j);

            if (cpxLog == AnalyticHestonEngineComplexLogFormula.Gatheral)
            {
                if (phi != 0.0)
                {
                    if (sigma > 1e-5)
                    {
                        Complex p = (t1 - d) / (t1 + d);
                        Complex g = Complex.Log((1.0 - (p * ex)) / (1.0 - p));

                        return Complex.Exp(
                            (v0 * (t1 - d) * (1.0 - ex) / (sigma2 * (1.0 - (ex * p))))
                            + (kappa * theta / sigma2 * (((t1 - d) * term) - (2.0 * g)))
                            + new Complex(0.0, phi * (dd - sx))
                            + addOnTerm).Imaginary / phi;
                    }
                    else
                    {
                        Complex td = phi / (2.0 * t1) * new Complex(-phi, j == 1 ? 1 : -1);
                        Complex p = td * sigma2 / (t1 + d);
                        Complex g = p * (1.0 - ex);

                        return Complex.Exp(
                            (v0 * td * (1.0 - ex) / (1.0 - (p * ex)))
                            + (kappa * theta * ((td * term) - (2.0 * g / sigma2)))
                            + new Complex(0.0, phi * (dd - sx))
                            + addOnTerm).Imaginary / phi;
                    }
                }
                else
                {
                    // use l'Hospital's rule to get lim_{phi->0}
                    if (j == 1)
                    {
                        double kmr = rsigma - kappa;
                        if (Math.Abs(kmr) > 1e-7)
                        {
                            return dd - sx
                                + (((Math.Exp(kmr * term) * kappa * theta) - (kappa * theta * ((kmr * term) + 1.0)))
                                   / (2 * kmr * kmr))
                                - (v0 * (1.0 - Math.Exp(kmr * term)) / (2.0 * kmr));
                        }
                        else
                        {
                            // \kappa = \rho * \sigma
                            return dd - sx + (0.25 * kappa * theta * term * term) + (0.5 * v0 * term);
                        }
                    }
                    else
                    {
                        return dd - sx
                            - (((Math.Exp(-kappa * term) * kappa * theta) + (kappa * theta * ((kappa * term) - 1.0)))
                               / (2 * kappa * kappa))
                            - (v0 * (1.0 - Math.Exp(-kappa * term)) / (2 * kappa));
                    }
                }
            }
            else if (cpxLog == AnalyticHestonEngineComplexLogFormula.BranchCorrection)
            {
                Complex p = (t1 + d) / (t1 - d);

                // next term: g = std::log((1.0 - p*std::exp(d*term_))/(1.0 - p))
                Complex g;

                // the exp of the following expression is needed.
                Complex e = Complex.Log(p) + (d * term);

                // does it fit to the machine precision?
                if (Math.Exp(-e.Real) > NumericLimits.QL_EPSILON)
                {
                    g = Complex.Log((1.0 - (p / ex)) / (1.0 - p));
                }
                else
                {
                    // use a "big phi" approximation
                    g = (d * term) + Complex.Log(p / (p - 1.0));

                    if (g.Imaginary > Math.PI || g.Imaginary <= -Math.PI)
                    {
                        // get back to principal branch of the complex logarithm
                        double im = g.Imaginary % (2 * Math.PI);
                        if (im > Math.PI)
                        {
                            im -= 2 * Math.PI;
                        }
                        else if (im <= -Math.PI)
                        {
                            im += 2 * Math.PI;
                        }

                        g = new Complex(g.Real, im);
                    }
                }

                // be careful here as we have to use a log branch correction
                // to deal with the discontinuities of the complex logarithm.
                // the principal branch is not always the correct one.
                // (s. A. Sepp, chapter 4)
                // remark: there is still the change that we miss a branch
                // if the order of the integration is not high enough.
                double tmp = g.Imaginary - gKm1;
                if (tmp <= -Math.PI)
                {
                    ++b;
                }
                else if (tmp > Math.PI)
                {
                    --b;
                }

                gKm1 = g.Imaginary;
                g += new Complex(0, 2 * b * Math.PI);

                return Complex.Exp(
                    (v0 * (t1 + d) * (ex - 1.0) / (sigma2 * (ex - p)))
                    + (kappa * theta / sigma2 * (((t1 + d) * term) - (2.0 * g)))
                    + new Complex(0, phi * (dd - sx))
                    + addOnTerm).Imaginary / phi;
            }
            else
            {
                throw new NanoQLException("unknown complex logarithm formula");
            }
        }
    }
}
