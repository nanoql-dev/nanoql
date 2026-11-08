// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticroughhestonengine.hpp ql/pricingengines/vanilla/analyticroughhestonengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for European plain-vanilla options under <see cref="RoughHestonModel"/>: the characteristic
/// function's fractional Riccati equation solved by the Adams scheme, the Padé approximation or the lifted
/// approximation, and the price by Andersen and Piterbarg's Fourier integral with a Black-Scholes control variate.
/// </summary>
/// <remarks>
/// <para>
/// An immutable object over its model and settings; the static <c>Calculate</c> builds one. It keeps QuantLib's caches
/// inside itself: the characteristic function by (z, t) and the lifted grid by t, keyed by their arguments' bits, and
/// the two Riemann-Liouville weight sets, built once; each entry is computed from the engine's immutable model and
/// settings, by QuantLib's arithmetic, and filled thread-safely, so every answer is what a fresh engine returns,
/// whatever the engine priced before and on whichever thread. A model with other parameters is a new model and so a new
/// engine, where QuantLib's <c>update()</c> drops the caches. Each price returns its integration's evaluations as the
/// additional result "numberOfEvaluations", where QuantLib's engine keeps the count in a mutable member.
/// </para>
/// <para>
/// The model's time runs from the earliest curve date (<see cref="HestonProcess"/>'s model time), the date form reading
/// the curves by date and the time form at the instants its time denotes on that clock, where QuantLib's time form
/// reads both curves at the risk-free curve's times; a market whose curves share the risk-free curve's dates and day
/// counter keeps QuantLib's times. At the model's time 0 the engine refuses, as QuantLib's does ("maturity must be
/// positive").
/// </para>
/// <para>
/// The Adams scheme is the reference route, O(N²) in the time steps; the Padé approximation is a closed form matching
/// the Riccati solution's short- and long-time asymptotics, most precise at the strongly negative correlation of equity
/// indices; the lifted approximation replaces the power-law kernel by a sum of n exponentials, an n-dimensional
/// classical Riccati equation solved in O(nN).
/// </para>
/// <para>
/// References: O. El Euch and M. Rosenbaum, The characteristic function of rough Heston models, Mathematical Finance
/// 29(1), 3-38 (2019); K. Diethelm, N. J. Ford, A. D. Freed, A Predictor-Corrector Approach for the Numerical Solution
/// of Fractional Differential Equations, Nonlinear Dynamics 29, 3-22 (2002); J. Gatheral and R. Radoičić, Rational
/// approximation of the rough Heston solution, International Journal of Theoretical and Applied Finance 22(3), 1950010
/// (2019); E. Abi Jaber, Lifting the Heston model, Quantitative Finance 19(12), 1995-2013 (2019).
/// </para>
/// </remarks>
public sealed class AnalyticRoughHestonEngine
{
    private readonly RoughHestonModel model;
    private readonly int timeSteps;
    private readonly FourierIntegration integration;
    private readonly double andersenPiterbargEpsilon, alpha;
    private readonly AnalyticRoughHestonEngineApproximation approximation;
    private readonly int nFactors;

    // QuantLib's caches: the characteristic function by the bits of (Re z, Im z, t), the lifted grid by the bits of t,
    // and the weights of the alpha = 1 and alpha = 1 - a integrals
    private readonly ConcurrentDictionary<(long, long, long), Complex> chFCache = new();
    private readonly ConcurrentDictionary<long, LiftedGrid> liftedGridCache = new();
    private readonly Lazy<RiemannLiouvilleWeights> unitWeights;
    private readonly Lazy<RiemannLiouvilleWeights> fractionalWeights;

    /// <summary>Initializes a new instance of the <see cref="AnalyticRoughHestonEngine"/> class on Gauss-Laguerre
    /// integration.</summary>
    /// <param name="model">The model.</param>
    /// <param name="integrationOrder">The Gauss-Laguerre order.</param>
    /// <param name="timeSteps">The fractional Riccati equation's time steps.</param>
    /// <param name="approximation">The route.</param>
    /// <param name="nFactors">The lifted kernel's exponentials, which the other routes ignore.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="approximation"/> isn't a member of its
    /// enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> or <paramref name="nFactors"/> isn't
    /// positive.</exception>
    public AnalyticRoughHestonEngine(
        RoughHestonModel model,
        int integrationOrder = 128,
        int timeSteps = 256,
        AnalyticRoughHestonEngineApproximation approximation = AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector,
        int nFactors = 20)
        : this(model, FourierIntegration.GaussLaguerre(integrationOrder), timeSteps, 1e-25, -0.5, approximation, nFactors, false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AnalyticRoughHestonEngine"/> class.</summary>
    /// <param name="model">The model.</param>
    /// <param name="integration">The Fourier integration.</param>
    /// <param name="timeSteps">The fractional Riccati equation's time steps.</param>
    /// <param name="andersenPiterbargEpsilon">Andersen and Piterbarg's accuracy, which bounds an adaptive integration's
    /// range.</param>
    /// <param name="alpha">The payoff's damping exponent, in (−1, 0) to keep the moments it needs finite.</param>
    /// <param name="approximation">The route.</param>
    /// <param name="nFactors">The lifted kernel's exponentials, which the other routes ignore.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="integration"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="approximation"/> isn't a member of its
    /// enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> or <paramref name="nFactors"/> isn't positive,
    /// or <paramref name="alpha"/> isn't in (−1, 0).</exception>
    public AnalyticRoughHestonEngine(
        RoughHestonModel model,
        FourierIntegration integration,
        int timeSteps = 256,
        double andersenPiterbargEpsilon = 1e-25,
        double alpha = -0.5,
        AnalyticRoughHestonEngineApproximation approximation = AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector,
        int nFactors = 20)
        : this(model, integration, timeSteps, andersenPiterbargEpsilon, alpha, approximation, nFactors, true)
    {
    }

    private AnalyticRoughHestonEngine(
        RoughHestonModel model,
        FourierIntegration integration,
        int timeSteps,
        double andersenPiterbargEpsilon,
        double alpha,
        AnalyticRoughHestonEngineApproximation approximation,
        int nFactors,
        bool checkAlpha)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(integration);
        EnumArguments.Check(approximation, nameof(approximation));
        if (!(timeSteps > 0))
        {
            throw new ArgumentException("at least one time step required", nameof(timeSteps));
        }

        if (checkAlpha && !(alpha > -1.0 && alpha < 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"alpha ({alpha}) must be in (-1, 0)"), nameof(alpha));
        }

        if (!(nFactors > 0))
        {
            throw new ArgumentException("at least one lifted factor required", nameof(nFactors));
        }

        this.model = model;
        this.timeSteps = timeSteps;
        this.integration = integration;
        this.andersenPiterbargEpsilon = andersenPiterbargEpsilon;
        this.alpha = alpha;
        this.approximation = approximation;
        this.nFactors = nFactors;

        unitWeights = new Lazy<RiemannLiouvilleWeights>(() => new RiemannLiouvilleWeights(1.0, timeSteps));
        fractionalWeights = new Lazy<RiemannLiouvilleWeights>(
            () => new RiemannLiouvilleWeights(1.0 - (model.Hurst + 0.5), timeSteps));
    }

    /// <summary>Prices a European plain-vanilla option on Gauss-Laguerre integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="integrationOrder">The Gauss-Laguerre order.</param>
    /// <param name="timeSteps">The fractional Riccati equation's time steps.</param>
    /// <param name="approximation">The route.</param>
    /// <param name="nFactors">The lifted kernel's exponentials, which the other routes ignore.</param>
    /// <returns>The option's results, with the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="approximation"/> isn't a member of its
    /// enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> or <paramref name="nFactors"/> isn't
    /// positive.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the maturity is at the model's time 0, the underlying
    /// isn't positive, the characteristic function's moment isn't positive, or the Padé approximation is singular.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        RoughHestonModel model,
        int integrationOrder = 128,
        int timeSteps = 256,
        AnalyticRoughHestonEngineApproximation approximation = AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector,
        int nFactors = 20) =>
        new AnalyticRoughHestonEngine(model, integrationOrder, timeSteps, approximation, nFactors).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="integration">The Fourier integration.</param>
    /// <param name="timeSteps">The fractional Riccati equation's time steps.</param>
    /// <param name="andersenPiterbargEpsilon">Andersen and Piterbarg's accuracy, which bounds an adaptive integration's
    /// range.</param>
    /// <param name="alpha">The payoff's damping exponent, in (−1, 0).</param>
    /// <param name="approximation">The route.</param>
    /// <param name="nFactors">The lifted kernel's exponentials, which the other routes ignore.</param>
    /// <returns>The option's results, with the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="approximation"/> isn't a member of its
    /// enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> or <paramref name="nFactors"/> isn't positive,
    /// or <paramref name="alpha"/> isn't in (−1, 0).</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the maturity is at the model's time 0, the underlying
    /// isn't positive, the characteristic function's moment isn't positive, the Padé approximation is singular, or an
    /// integration fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        RoughHestonModel model,
        FourierIntegration integration,
        int timeSteps = 256,
        double andersenPiterbargEpsilon = 1e-25,
        double alpha = -0.5,
        AnalyticRoughHestonEngineApproximation approximation = AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector,
        int nFactors = 20) =>
        new AnalyticRoughHestonEngine(model, integration, timeSteps, andersenPiterbargEpsilon, alpha, approximation, nFactors)
            .Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results, with the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the maturity is at the model's time 0, the underlying
    /// isn't positive, the characteristic function's moment isn't positive, the Padé approximation is singular, or an
    /// integration fails.
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
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non plain vanilla payoff given");
        }

        Date maturity = option.Exercise.LastDate();
        double t = model.Process.ModelTime(maturity);
        if (!(t > 0.0))
        {
            throw new NanoQLException("maturity must be positive");
        }

        (double value, int evaluations) = PriceVanillaPayoffAt(payoff, maturity, t);
        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = value,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("numberOfEvaluations", evaluations),
        });
    }

    /// <summary>The price of a plain-vanilla payoff at a date, its curves read by date.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="maturity">The date.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="maturity"/> is at the model's time 0.</exception>
    /// <exception cref="NanoQLException">
    /// The underlying isn't positive, the characteristic function's moment isn't positive, the Padé approximation is
    /// singular, or an integration fails.
    /// </exception>
    public double PriceVanillaPayoff(PlainVanillaPayoff payoff, Date maturity)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        double t = model.Process.ModelTime(maturity);
        if (!(t > 0.0))
        {
            throw new ArgumentException("maturity must be positive", nameof(maturity));
        }

        return PriceVanillaPayoffAt(payoff, maturity, t).Price;
    }

    /// <summary>The price of a plain-vanilla payoff at a time, on the model's clock, its curves read at the instant the
    /// time denotes.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="maturity">The time, on the model's clock.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="maturity"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">
    /// The underlying isn't positive, the characteristic function's moment isn't positive, the Padé approximation is
    /// singular, or an integration fails.
    /// </exception>
    public double PriceVanillaPayoff(PlainVanillaPayoff payoff, double maturity)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        if (!(maturity > 0.0))
        {
            throw new ArgumentException("maturity must be positive", nameof(maturity));
        }

        HestonProcess process = model.Process;
        TermStructure clock = process.ModelClock();
        double dr = process.RiskFreeRate.DiscountAt(clock, maturity);
        double fwd = process.S0 * process.DividendYield.DiscountAt(clock, maturity) / dr;
        return PriceVanillaPayoff(payoff, maturity, fwd, dr).Price;
    }

    /// <summary>The normalized characteristic function of the log forward moneyness, E[exp(iz ln(S_t/F_t))].</summary>
    /// <param name="z">The argument.</param>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>The characteristic function's value.</returns>
    /// <exception cref="NanoQLException">The Padé approximation is singular at <paramref name="z"/>.</exception>
    public Complex ChF(Complex z, double t) =>
        chFCache.GetOrAdd(
            (BitConverter.DoubleToInt64Bits(z.Real), BitConverter.DoubleToInt64Bits(z.Imaginary), BitConverter.DoubleToInt64Bits(t)),
            static (_, state) => ComplexFunctions.Exp(state.Engine.LnChF(state.Z, state.T)),
            (Engine: this, Z: z, T: t));

    /// <summary>The logarithm of the normalized characteristic function of the log forward moneyness.</summary>
    /// <param name="z">The argument.</param>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>The logarithm's value.</returns>
    /// <exception cref="NanoQLException">The Padé approximation is singular at <paramref name="z"/>.</exception>
    public Complex LnChF(Complex z, double t) => approximation switch
    {
        AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector => LnChFAdams(z, t),
        AnalyticRoughHestonEngineApproximation.Pade => LnChFPade(z, t),
        AnalyticRoughHestonEngineApproximation.Lifted => LnChFLifted(z, t),
        _ => throw new NanoQLException("unknown approximation"),
    };

    /// <summary>The fractional Riccati equation's solution h(z, t), by the engine's route.</summary>
    /// <param name="z">The argument.</param>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>The solution.</returns>
    /// <exception cref="ArgumentException"><paramref name="t"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The Padé approximation is singular at <paramref name="z"/>.</exception>
    public Complex RiccatiSolution(Complex z, double t)
    {
        if (!(t > 0.0))
        {
            throw new ArgumentException("maturity must be positive", nameof(t));
        }

        return approximation switch
        {
            AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector => SolveAdamsRiccati(z, t)[^1],
            AnalyticRoughHestonEngineApproximation.Pade => PadeRiccati(z, t),
            AnalyticRoughHestonEngineApproximation.Lifted => SolveLiftedRiccati(z, t).Psi,
            _ => throw new NanoQLException("unknown approximation"),
        };
    }

    // psi_p(theta) = sum_{m >= 0} (-theta)^m / (m + p)!, the entire functions generating the weights of the exponential
    // integrator; the closed forms cancel to nothing as theta -> 0
    private static double PhiFunction(int p, double theta)
    {
        if (theta < 1.0)
        {
            double term = 1.0;
            for (int k = 2; k <= p; ++k)
            {
                term /= k;
            }

            double sum = term;

            // Safety cap, not the operative stopping rule: the tolerance
            // break below always fires first. Consecutive terms shrink
            // by a factor theta / (m + p) < 1 / (m + p), so the
            // slowest case (theta -> 1, p = 1) reaches the 1e-18
            // relative tolerance by m = 19.
            const int MaxTerms = 24;
            for (int m = 1; m < MaxTerms; ++m)
            {
                term *= -theta / (m + p);
                sum += term;
                if (Math.Abs(term) <= 1e-18 * Math.Abs(sum))
                {
                    break;
                }
            }

            return sum;
        }

        double e = Expm1Functions.StdExpm1(-theta);
        return p switch
        {
            1 => -e / theta,
            2 => (theta + e) / (theta * theta),
            3 => ((0.5 * theta * theta) - theta - e) / (theta * theta * theta),
            _ => throw new NanoQLException(string.Create(CultureInfo.InvariantCulture, $"unsupported phi function order {p}")),
        };
    }

    // std::max and std::min, which return their first argument where neither is less than the other
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    // The (3, 3) rational approximation h = N(y) / D(y)
    private static Complex EvaluatePade(PadeCoefficients c, double y)
    {
        Complex num = y * (c.P1 + (y * (c.P2 + (y * c.P3))));
        Complex den = 1.0 + (y * (c.Q1 + (y * (c.Q2 + (y * c.Q3)))));
        return num / den;
    }

    private static Complex Det3(
        Complex a11, Complex a12, Complex a13, Complex a21, Complex a22, Complex a23, Complex a31, Complex a32, Complex a33) =>
        (a11 * ((a22 * a33) - (a23 * a32)))
        - (a12 * ((a21 * a33) - (a23 * a31)))
        + (a13 * ((a21 * a32) - (a22 * a31)));

    // std::complex's operator<<
    private static string Format(Complex z) =>
        string.Create(CultureInfo.InvariantCulture, $"({z.Real},{z.Imaginary})");

    // The date form: the time on the model's clock, the forward and the risk-free discount read by date
    private (double Price, int Evaluations) PriceVanillaPayoffAt(PlainVanillaPayoff payoff, Date maturity, double t)
    {
        HestonProcess process = model.Process;
        double dr = process.RiskFreeRate.AccruedDiscount(maturity);
        double fwd = process.S0 * process.DividendYield.AccruedDiscount(maturity) / dr;
        return PriceVanillaPayoff(payoff, t, fwd, dr);
    }

    // QuantLib's priceVanillaPayoff(payoff, maturity, fwd), on the risk-free discount the caller read
    private (double Price, int Evaluations) PriceVanillaPayoff(PlainVanillaPayoff payoff, double maturity, double fwd, double dr)
    {
        double spot = model.Process.S0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        double strike = payoff.Strike;
        double kappa = model.Kappa;
        double theta = model.Theta;
        double sigma = model.Sigma;
        double rho = model.Rho;
        double v0 = model.V0;
        double a = model.Hurst + 0.5;

        // Decay estimate of the characteristic function along the
        // integration contour; recovers the classical Heston engine value
        // sqrt(1 - rho ^ 2)(v0 + kappa theta t) / sigma for alpha = 1
        // TODO: the a < 1 rescaling is a heuristic, not a derived asymptotic;
        // revisit with a proper derivation or a truncation stress test.
        double c_inf = Math.Sqrt(1.0 - (rho * rho)) / sigma
            * ((v0 * Math.Pow(maturity, 1.0 - a) / new GammaFunction().Value(2.0 - a)) + (kappa * theta * maturity));
        double epsilon = andersenPiterbargEpsilon * Math.PI / (Math.Sqrt(strike * fwd) * dr);

        double? UM() => FourierIntegration.AndersenPiterbargIntegrationLimit(c_inf, epsilon, v0, maturity);

        var cvHelper = new APHelper(maturity, fwd, strike, alpha, this);
        double cvValue = cvHelper.ControlVariateValue();
        double scalingFactor = Max(0.25, Min(1000.0, 0.25 / Math.Sqrt(0.5 * cvHelper.VAvg * maturity)));

        IntegrationResult result = integration.CalculateWithStatistics(c_inf, cvHelper.Evaluate, UM, scalingFactor);
        double h_cv = fwd / Math.PI * result.Value;

        return payoff.OptionType switch
        {
            OptionType.Call => ((cvValue + h_cv) * dr, result.NumberOfEvaluations),
            OptionType.Put => ((cvValue + h_cv - (fwd - strike)) * dr, result.NumberOfEvaluations),
            _ => throw new NanoQLException("unknown option type"),
        };
    }

    // Coefficients of the non-fractional Riccati equation satisfied by the
    // rough Heston h(z, t), shared by the Adams and Pade routes
    private RiccatiCoefficients Coefficients(Complex z)
    {
        double kappa = model.Kappa;
        double sigma = model.Sigma;
        double rho = model.Rho;
        var i = new Complex(0.0, 1.0);
        Complex c0 = -0.5 * z * (z + i);
        Complex c1 = (i * (rho * sigma) * z) - kappa;
        double c2 = 0.5 * sigma * sigma;
        return new RiccatiCoefficients(c0, c1, new Complex(c2, 0.0));
    }

    // Fractional Adams solution grid h(z, j dt), j = 0, ..., timeSteps
    private Complex[] SolveAdamsRiccati(Complex z, double t)
    {
        double a = model.Hurst + 0.5;
        RiccatiCoefficients c = Coefficients(z);
        return new FractionalAdams<Complex>(a).Solve((_, x) => c.C0 + ((c.C1 + (c.C2 * x)) * x), new Complex(0.0, 0.0), t, timeSteps);
    }

    private Complex LnChFAdams(Complex z, double t)
    {
        double kappa = model.Kappa;
        double theta = model.Theta;
        double v0 = model.V0;
        Complex[] h = SolveAdamsRiccati(z, t);
        double dt = t / timeSteps;
        return (kappa * theta * unitWeights.Value.Integrate<Complex>(h, dt))
            + (v0 * fractionalWeights.Value.Integrate<Complex>(h, dt));
    }

    private Complex LnChFPade(Complex z, double t)
    {
        double kappa = model.Kappa;
        double theta = model.Theta;
        double sigma = model.Sigma;
        double v0 = model.V0;
        double a = model.Hurst + 0.5;
        PadeCoefficients c = Coefficients3x3(z);
        double dt = t / timeSteps;
        var h = new Complex[timeSteps + 1];
        for (int j = 0; j <= timeSteps; ++j)
        {
            h[j] = EvaluatePade(c, sigma * Math.Pow(j * dt, a));
        }

        return (kappa * theta * unitWeights.Value.Integrate<Complex>(h, dt))
            + (v0 * fractionalWeights.Value.Integrate<Complex>(h, dt));
    }

    // (3, 3) global rational approximation of Gatheral-Radoicic
    private PadeCoefficients Coefficients3x3(Complex z)
    {
        double kappa = model.Kappa;
        double sigma = model.Sigma;
        double rho = model.Rho;
        double a = model.Hurst + 0.5;
        var i = new Complex(0.0, 1.0);
        RiccatiCoefficients rc = Coefficients(z);
        Complex c0 = rc.C0;
        Complex c1 = rc.C1;
        double c2 = rc.C2.Real;

        // c0 = -z * (z + i) / 2 vanishes exactly at z = 0 and z = -i. There,
        // v = 0 solves the Riccati equation identically, so h is exactly
        // zero for all t. It also makes the long-time root gamma0 vanish,
        // which degenerates the (3, 3) system.
        if (Complex.Abs(c0) < 1e-14)
        {
            Complex zero = new(0.0, 0.0);
            return new PadeCoefficients(zero, zero, zero, zero, zero, zero);
        }

        var g = new GammaFunction();

        // Reciprocal gamma 1 / Gamma(x), which vanishes at the poles
        // 0, -1, -2, ...; keeps the long-time coefficients finite as
        // a -> 1
        double RGamma(double x) => 1.0 / g.Value(x);

        // Short-time Taylor coefficients beta_k = b_k / sigma^k of
        // h = sum_k b_k t^(k a), rescaled to the variable y = sigma t^a
        Complex b1 = c0 * RGamma(1.0 + a);
        Complex b2 = c1 * b1 * (g.Value(1.0 + a) / g.Value(1.0 + (2.0 * a)));
        Complex b3 = ((c1 * b2) + (c2 * b1 * b1)) * (g.Value(1.0 + (2.0 * a)) / g.Value(1.0 + (3.0 * a)));
        Complex beta1 = b1 / sigma;
        Complex beta2 = b2 / (sigma * sigma);
        Complex beta3 = b3 / (sigma * sigma * sigma);

        // Long-time coefficients gamma_k = g_k sigma^k: h -> r_- / sigma,
        // the attracting root of the Riccati quadratic, with algebraic
        // corrections in 1 / y.
        Complex w = (kappa / sigma) - (i * (rho * z));
        Complex aRoot = Complex.Sqrt((z * (z + i)) + (w * w));

        // A is the discriminant root separating the two fixed points of the
        // Riccati quadratic; near z where they coincide (A -> 0), gamma_1 and
        // gamma_2 below are ill-conditioned and the (3, 3) approximant is not
        // reliable here.
        if (!(Complex.Abs(aRoot) > 1e-8))
        {
            throw new NanoQLException(
                $"Pade approximation is singular at z = {Format(z)}; use the Adams "
                + "predictor-corrector engine for this contour point");
        }

        Complex rMinus = w - aRoot;
        Complex gamma0 = rMinus / sigma;
        Complex gamma1 = -gamma0 * RGamma(1.0 - a) / aRoot;
        Complex gamma2 = (gamma0 * RGamma(1.0 - (2.0 * a)) / (aRoot * aRoot)) + (0.5 * sigma * gamma1 * gamma1 / aRoot);

        // The two matching sets pin down the denominator (q_1, q_2, q_3)
        // through a 3x3 complex linear system, by its closed-form cofactor
        // expansion.
        Complex det = Det3(gamma0, gamma1, gamma2, -beta1, gamma0, gamma1, -beta2, -beta1, gamma0);
        if (!(Complex.Abs(det) > 1e-12))
        {
            throw new NanoQLException(
                $"Pade denominator system is ill-conditioned at z = {Format(z)}; use the Adams predictor-corrector engine for "
                + "this contour point");
        }

        Complex q1 = Det3(beta1, gamma1, gamma2, beta2, gamma0, gamma1, beta3, -beta1, gamma0) / det;
        Complex q2 = Det3(gamma0, beta1, gamma2, -beta1, beta2, gamma1, -beta2, beta3, gamma0) / det;
        Complex q3 = Det3(gamma0, gamma1, beta1, -beta1, gamma0, beta2, -beta2, -beta1, beta3) / det;
        Complex p1 = beta1;
        Complex p2 = beta2 + (beta1 * q1);
        Complex p3 = beta3 + (beta2 * q1) + (beta1 * q2);
        return new PadeCoefficients(p1, p2, p3, q1, q2, q3);
    }

    private Complex PadeRiccati(Complex z, double t)
    {
        double sigma = model.Sigma;
        double a = model.Hurst + 0.5;
        return EvaluatePade(Coefficients3x3(z), sigma * Math.Pow(t, a));
    }

    // Kernel nodes and step weights of the lifted route. The linear part -x_i psi_i is integrated exactly through its
    // integrating factor and F as its linear interpolant over the step. None of it depends on the frequency z, hence
    // the per-maturity cache.
    private LiftedGrid GridAt(double t) =>
        liftedGridCache.GetOrAdd(BitConverter.DoubleToInt64Bits(t), static (_, state) => state.Engine.BuildLiftedGrid(state.T), (Engine: this, T: t));

    private LiftedGrid BuildLiftedGrid(double t)
    {
        double a = model.Hurst + 0.5;
        var kernel = new FractionalKernelApproximation(a, nFactors, t);
        double dt = t / timeSteps;
        int n = kernel.Size;
        double[] c = [.. kernel.Weights];
        double[] expDt = new double[n], cExpDt = new double[n], w0 = new double[n], w1 = new double[n], cA = new double[n];
        double cASum = 0.0, cP = 0.0, cQ = 0.0;
        ImmutableArray<double> x = kernel.Rates;
        for (int i = 0; i < n; ++i)
        {
            double theta = x[i] * dt;
            double p1 = PhiFunction(1, theta);
            double p2 = PhiFunction(2, theta);
            double p3 = PhiFunction(3, theta);
            expDt[i] = Math.Exp(-theta);
            cExpDt[i] = c[i] * expDt[i];

            // The two sum to dt * p1, the weight of a constant F
            w0[i] = dt * (p1 - p2);
            w1[i] = dt * p2;
            cA[i] = c[i] * dt * p1;
            cASum += cA[i];
            cP += c[i] * dt * dt * p2;
            cQ += c[i] * dt * dt * p3;
        }

        return new LiftedGrid(c, expDt, cExpDt, w0, w1, cA, cASum, cP, cQ);
    }

    private LiftedSolution SolveLiftedRiccati(Complex z, double t)
    {
        LiftedGrid g = GridAt(t);
        int n = g.C.Length;
        RiccatiCoefficients c = Coefficients(z);

        // RHS of the (non-fractional) Riccati equation, evaluated
        // at the lifted state psi^n in place of the true h
        Complex F(Complex p) => c.C0 + ((c.C1 + (c.C2 * p)) * p);

        double dt = t / timeSteps;
        var psi = new Complex[n];
        Complex psiN = new(0.0, 0.0), f = F(psiN);
        Complex fIntegral = new(0.0, 0.0), psiIntegral = new(0.0, 0.0);
        for (int k = 0; k < timeSteps; ++k)
        {
            // Predictor: decay of the factors plus the response to a
            // constant F over the step
            Complex predicted = g.CASum * f;
            for (int i = 0; i < n; ++i)
            {
                predicted += g.CExpDt[i] * psi[i];
            }

            Complex fPredicted = F(predicted);

            // Accumulated before the factors advance: it weights psi(t_k)
            Complex stepIntegral = new(0.0, 0.0), psiNext = new(0.0, 0.0);
            for (int i = 0; i < n; ++i)
            {
                stepIntegral += g.CA[i] * psi[i];
                psi[i] = (g.ExpDt[i] * psi[i]) + (g.W0[i] * f) + (g.W1[i] * fPredicted);
                psiNext += g.C[i] * psi[i];
            }

            Complex fNext = F(psiNext);
            fIntegral += 0.5 * dt * (f + fNext);
            psiIntegral += stepIntegral + (g.CP * f) + (g.CQ * (fNext - f));
            psiN = psiNext;
            f = fNext;
        }

        return new LiftedSolution(psiN, fIntegral, psiIntegral);
    }

    // The lifted characteristic function is ln phi = V_0 int_0^t F(z, psi^n) + kappa theta int_0^t psi^n, the exact
    // counterpart of the fractional kappa theta I^1 h + V_0 I^{1-a} h because I^{1-a} h = I^{1-a} I^a F(z, h) = I^1 F(z, h)
    private Complex LnChFLifted(Complex z, double t)
    {
        LiftedSolution s = SolveLiftedRiccati(z, t);
        return (model.V0 * s.FIntegral) + (model.Kappa * model.Theta * s.PsiIntegral);
    }

    // Coefficients c0, c1, c2 of the Riccati equation h' = c0 + c1 h + c2 h^2
    private readonly record struct RiccatiCoefficients(Complex C0, Complex C1, Complex C2);

    // Coefficients of the (3, 3) Gatheral-Radoicic rational approximation h(z, t) = N(y) / D(y), y = sigma t^a,
    // matching the short-time series and the long-time root of the Riccati solution
    private readonly record struct PadeCoefficients(Complex P1, Complex P2, Complex P3, Complex Q1, Complex Q2, Complex Q3);

    // psi^n(z, t) together with the two integrals the chF is built from
    private readonly record struct LiftedSolution(Complex Psi, Complex FIntegral, Complex PsiIntegral);

    // Per-maturity data of the lifted approximation: the kernel's weights c_i; e^{-x_i dt}, its c_i-weighted form and the
    // weights of F_k and F_{k+1} in the step; c_i (1 - e^{-x_i dt}) / x_i and its sum, the psi_i and constant-F weights of
    // the psi-integral's quadrature; and that quadrature's F_k and (F_{k+1} - F_k) weights. Never written once built.
    private sealed record LiftedGrid(
        double[] C, double[] ExpDt, double[] CExpDt, double[] W0, double[] W1, double[] CA, double CASum, double CP, double CQ);

    // Andersen-Piterbarg integrand with Black-Scholes control variate,
    // following AnalyticHestonEngine's AP helper
    private sealed class APHelper
    {
        private readonly double term;
        private readonly double fwd, strike, alpha, freq, sAlpha;
        private readonly AnalyticRoughHestonEngine engine;

        internal APHelper(double term, double fwd, double strike, double alpha, AnalyticRoughHestonEngine engine)
        {
            this.term = term;
            this.fwd = fwd;
            this.strike = strike;
            this.alpha = alpha;
            freq = Math.Log(fwd / strike);
            sAlpha = Math.Exp(alpha * freq);
            this.engine = engine;
            double moment = engine.ChF(new Complex(0.0, alpha), term).Real;
            if (!(moment > 0.0))
            {
                throw new NanoQLException(
                    "invalid characteristic function value; try to increase the number of fractional Riccati time steps");
            }

            VAvg = -8.0 * Math.Log(moment) / term;
        }

        internal double VAvg { get; }

        internal double Evaluate(double u)
        {
            var z = new Complex(u, -alpha);
            var zPrime = new Complex(u, -alpha - 1.0);
            Complex phiBS = ComplexFunctions.Exp(
                -0.5 * VAvg * term * ((zPrime * zPrime) + new Complex(-zPrime.Imaginary, zPrime.Real)));
            Complex phi = engine.ChF(zPrime, term);

            // The fractional Adams scheme diverges once the oscillation is
            // under-resolved by the time grid. The exact characteristic
            // function satisfies |phi| <= 1 and decays exponentially
            // in u, so far-tail values are truncated to zero.
            if (!double.IsFinite(phi.Real) || !double.IsFinite(phi.Imaginary)
                || (phi.Real * phi.Real) + (phi.Imaginary * phi.Imaginary) > 1.0)
            {
                phi = new Complex(0.0, 0.0);
            }

            return (ComplexFunctions.Exp(new Complex(0.0, u * freq)) * (phiBS - phi) / (z * zPrime)).Real * sAlpha;
        }

        internal double ControlVariateValue() =>
            new BlackCalculator(OptionType.Call, strike, fwd, Math.Sqrt(VAvg * term)).Value;
    }
}
