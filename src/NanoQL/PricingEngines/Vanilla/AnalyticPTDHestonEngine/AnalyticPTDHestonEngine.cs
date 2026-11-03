// SPDX-FileCopyrightText: Copyright (C) 2010, 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticptdhestonengine.hpp ql/pricingengines/vanilla/analyticptdhestonengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for European plain-vanilla options under <see cref="PiecewiseTimeDependentHestonModel"/>: the
/// characteristic function's recursion across the model's time grid, integrated by Gatheral's form or Andersen and
/// Piterbarg's.
/// </summary>
/// <remarks>
/// <para>
/// An immutable object over its model, its formulation and its integration; the static <c>Calculate</c> builds one.
/// Each price returns its integrations' evaluations as the additional result "numberOfEvaluations", where QuantLib's
/// engine keeps the count in a mutable member. The model's time runs from the earlier of its curves' dates and the
/// discounts are read by date, the forward rates between its grid's times at the instants they denote on that clock; at
/// the model's time 0 the price is the forward's discounted intrinsic value, where QuantLib's Gauss-Laguerre integrates
/// sin(φ(x − ln K))/φ, which doesn't decay.
/// </para>
/// <para>
/// References: S. Heston, A Closed-Form Solution for Options with Stochastic Volatility with Applications to Bond and
/// Currency Options, The Review of Financial Studies 6(2), 1993, 327-343; J. Gatheral, The Volatility Surface: A
/// Practitioner's Guide, Wiley Finance; A. Elices, Models with time-dependent parameters using transform methods:
/// application to Heston's model, http://arxiv.org/pdf/0708.2020.
/// </para>
/// </remarks>
public sealed class AnalyticPTDHestonEngine
{
    // QuantLib's Null<Real>(): the Andersen-Piterbarg epsilon of the Gatheral-only constructors, which nothing reads
    private const double NullReal = 3.4028234663852886e38;

    // std::numeric_limits<float>::epsilon(), the integrand's floor on phi
    private const double FloatEpsilon = 1.1920928955078125e-07;

    private readonly PiecewiseTimeDependentHestonModel model;
    private readonly AnalyticPTDHestonEngineComplexLogFormula cpxLog;
    private readonly FourierIntegration integration;
    private readonly double andersenPiterbargEpsilon;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticPTDHestonEngine"/> class on adaptive Gauss-Lobatto
    /// integration and Gatheral's form.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="relTolerance">The integrations' relative tolerance.</param>
    /// <param name="maxEvaluations">The integrations' limit on evaluations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public AnalyticPTDHestonEngine(PiecewiseTimeDependentHestonModel model, double relTolerance, int maxEvaluations)
        : this(
            model,
            AnalyticPTDHestonEngineComplexLogFormula.Gatheral,
            FourierIntegration.GaussLobatto(relTolerance, null, maxEvaluations),
            NullReal)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticPTDHestonEngine"/> class on Gauss-Laguerre integration and
    /// Gatheral's form.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="integrationOrder">The Gauss-Laguerre order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public AnalyticPTDHestonEngine(PiecewiseTimeDependentHestonModel model, int integrationOrder = 144)
        : this(
            model,
            AnalyticPTDHestonEngineComplexLogFormula.Gatheral,
            FourierIntegration.GaussLaguerre(integrationOrder),
            NullReal)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AnalyticPTDHestonEngine"/> class.</summary>
    /// <param name="model">The model.</param>
    /// <param name="cpxLog">The formulation.</param>
    /// <param name="itg">The integration.</param>
    /// <param name="andersenPiterbargEpsilon">Andersen and Piterbarg's accuracy, which bounds an adaptive integration's
    /// range.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="itg"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cpxLog"/> isn't a member of its enum.</exception>
    public AnalyticPTDHestonEngine(
        PiecewiseTimeDependentHestonModel model,
        AnalyticPTDHestonEngineComplexLogFormula cpxLog,
        FourierIntegration itg,
        double andersenPiterbargEpsilon = 1e-8)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(itg);
        EnumArguments.Check(cpxLog, nameof(cpxLog));
        this.model = model;
        this.cpxLog = cpxLog;
        integration = itg;
        this.andersenPiterbargEpsilon = andersenPiterbargEpsilon;
    }

    /// <summary>Prices a European plain-vanilla option on Gauss-Laguerre integration and Gatheral's form.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="integrationOrder">The Gauss-Laguerre order.</param>
    /// <returns>The option's results, with the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, the maturity is past
    /// the time grid, or the grid has fewer than two points.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, PiecewiseTimeDependentHestonModel model, int integrationOrder = 144) =>
        new AnalyticPTDHestonEngine(model, integrationOrder).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option on adaptive Gauss-Lobatto integration and Gatheral's form.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="relTolerance">The integrations' relative tolerance.</param>
    /// <param name="maxEvaluations">The integrations' limit on evaluations.</param>
    /// <returns>The option's results, with the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, the maturity is past
    /// the time grid, the grid has fewer than two points, or an integration fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        PiecewiseTimeDependentHestonModel model,
        double relTolerance,
        int maxEvaluations) =>
        new AnalyticPTDHestonEngine(model, relTolerance, maxEvaluations).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="cpxLog">The formulation.</param>
    /// <param name="itg">The integration.</param>
    /// <param name="andersenPiterbargEpsilon">Andersen and Piterbarg's accuracy, which bounds an adaptive integration's
    /// range.</param>
    /// <returns>The option's results, with the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cpxLog"/> isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, the maturity is past
    /// the time grid, the grid has fewer than two points, or an integration fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        PiecewiseTimeDependentHestonModel model,
        AnalyticPTDHestonEngineComplexLogFormula cpxLog,
        FourierIntegration itg,
        double andersenPiterbargEpsilon = 1e-8) =>
        new AnalyticPTDHestonEngine(model, cpxLog, itg, andersenPiterbargEpsilon).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results, with the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, the maturity is past
    /// the time grid, the grid has fewer than two points, or an integration fails.
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
        (double value, int evaluations) = Value(option);
        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = value,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("numberOfEvaluations", evaluations),
        });
    }

    /// <summary>The normalized characteristic function of the log spot's move to a time.</summary>
    /// <param name="z">The argument.</param>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>The characteristic function's value.</returns>
    /// <exception cref="NanoQLException"><paramref name="t"/> is past the model's time grid.</exception>
    public Complex ChF(Complex z, double t) => Complex.Exp(LnChF(z, t));

    /// <summary>The logarithm of the normalized characteristic function of the log spot's move to a time.</summary>
    /// <param name="z">The argument.</param>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>The logarithm's value.</returns>
    /// <exception cref="NanoQLException"><paramref name="t"/> is past the model's time grid.</exception>
    public Complex LnChF(Complex z, double t)
    {
        double v0 = model.V0;

        Complex dd = 0.0;
        Complex c = 0.0;

        TimeGrid timeGrid = model.TimeGrid;
        double lastModelTime = timeGrid.Back();
        if (!(t <= lastModelTime))
        {
            throw new NanoQLException(string.Create(
                CultureInfo.InvariantCulture,
                $"maturity ({t}) is too large, time grid is bounded by {lastModelTime}"));
        }

        int lastI = LowerBound(timeGrid, t);

        for (int i = lastI - 1; i >= 0; --i)
        {
            double begin = timeGrid[i];
            double end = Math.Min(t, timeGrid[i + 1]);
            double tau = end - begin;
            double tm = 0.5 * (end + begin);
            double kappa = model.Kappa(tm);
            double sigma = model.Sigma(tm);
            double theta = model.Theta(tm);
            double rho = model.Rho(tm);
            double sigma2 = sigma * sigma;

            Complex k = kappa + (rho * sigma * new Complex(z.Imaginary, -z.Real));
            Complex d = Complex.Sqrt((k * k) + (((z * z) + new Complex(-z.Imaginary, z.Real)) * sigma2));
            Complex g = (k - d) / (k + d);
            Complex gt = (k - d - (dd * sigma2)) / (k + d - (dd * sigma2));
            c += kappa * theta / sigma2 * (((k - d) * tau)
                - (2.0 * Complex.Log((1.0 - (gt * Complex.Exp(-d * tau))) / (1.0 - gt))));
            dd = (k + d) / sigma2 * (g - (gt * Complex.Exp(-d * tau))) / (1.0 - (gt * Complex.Exp(-d * tau)));
        }

        return (dd * v0) + c;
    }

    // std::lower_bound over the grid: the first index whose time isn't less than t
    private static int LowerBound(TimeGrid grid, double t)
    {
        int first = 0, count = grid.Size;
        while (count > 0)
        {
            int step = count / 2;
            if (grid[first + step] < t)
            {
                first += step + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }

    // std::max and std::min, which return their first argument where neither is less than the other
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    private static double Squared(double x) => x * x;

    // QuantLib's calculate, the model's time and discounts by date and its forward rates on the model's clock
    private (double Value, int Evaluations) Value(VanillaOption option)
    {
        // this is an european option pricer
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        // plain vanilla
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        double v0 = model.V0;
        double spotPrice = model.S0;
        if (!(spotPrice > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        double strike = payoff.Strike;
        Date lastDate = option.Exercise.LastDate();
        double term = model.ModelTime(lastDate);
        if (!(term < model.TimeGrid.Back() || ComparisonFunctions.CloseEnough(term, model.TimeGrid.Back())))
        {
            throw new NanoQLException(string.Create(
                CultureInfo.InvariantCulture,
                $"maturity ({term}) is too large, time grid is bounded by {model.TimeGrid.Back()}"));
        }

        double riskFreeDiscount = model.RiskFreeRate.AccruedDiscount(lastDate);
        double dividendDiscount = model.DividendYield.AccruedDiscount(lastDate);

        // average values
        TimeGrid timeGrid = model.TimeGrid;
        if (!(timeGrid.Size > 1))
        {
            throw new NanoQLException("at least two model points needed");
        }

        if (term == 0.0)
        {
            // no variance has accrued: the forward's discounted intrinsic value
            return (riskFreeDiscount * payoff.Evaluate(spotPrice * dividendDiscount / riskFreeDiscount), 0);
        }

        int n = timeGrid.Size - 1;
        double kappaAvg = 0.0, thetaAvg = 0.0, sigmaAvg = 0.0, rhoAvg = 0.0;
        for (int i = 1; i <= n; ++i)
        {
            double t = 0.5 * (timeGrid[i - 1] + timeGrid[i]);
            kappaAvg += model.Kappa(t);
            thetaAvg += model.Theta(t);
            sigmaAvg += model.Sigma(t);
            rhoAvg += model.Rho(t);
        }

        kappaAvg /= n;
        thetaAvg /= n;
        sigmaAvg /= n;
        rhoAvg /= n;

        int evaluations = 0;
        double value;

        switch (cpxLog)
        {
            case AnalyticPTDHestonEngineComplexLogFormula.Gatheral:
            {
                double c_inf = Min(0.2, Max(0.0001, Math.Sqrt(1.0 - Squared(rhoAvg)) / sigmaAvg)) * (v0 + (kappaAvg * thetaAvg * term));

                TermStructure clock = model.ModelClock();
                IntegrationResult p1Result = integration.CalculateWithStatistics(
                    c_inf, new FjHelper(model, clock, term, strike, 1).Evaluate);
                double p1 = p1Result.Value / Math.PI;
                evaluations += p1Result.NumberOfEvaluations;

                IntegrationResult p2Result = integration.CalculateWithStatistics(
                    c_inf, new FjHelper(model, clock, term, strike, 2).Evaluate);
                double p2 = p2Result.Value / Math.PI;
                evaluations += p2Result.NumberOfEvaluations;

                switch (payoff.OptionType)
                {
                    case OptionType.Call:
                        value = (spotPrice * dividendDiscount * (p1 + 0.5)) - (strike * riskFreeDiscount * (p2 + 0.5));
                        break;
                    case OptionType.Put:
                        value = (spotPrice * dividendDiscount * (p1 - 0.5)) - (strike * riskFreeDiscount * (p2 - 0.5));
                        break;
                    default:
                        throw new NanoQLException("unknown option type");
                }
            }

            break;
            case AnalyticPTDHestonEngineComplexLogFormula.AndersenPiterbarg:
            {
                if (!(term <= timeGrid.Back()))
                {
                    throw new NanoQLException(string.Create(
                        CultureInfo.InvariantCulture,
                        $"maturity ({term}) is too large, time grid is bounded by {timeGrid.Back()}"));
                }

                double t05 = 0.5 * timeGrid.At(1);
                Complex dUInf = -new Complex(Math.Sqrt(1 - Squared(model.Rho(t05))), model.Rho(t05)) / model.Sigma(t05);

                int lastI = LowerBound(timeGrid, term);

                Complex cUInf = new(0.0, 0.0);
                for (int i = 0; i < lastI; ++i)
                {
                    double begin = timeGrid[i];
                    double end = Math.Min(term, timeGrid[i + 1]);
                    double tau = end - begin;
                    double t = 0.5 * (end + begin);
                    double kappa = model.Kappa(t);
                    double theta = model.Theta(t);
                    double sigma = model.Sigma(t);
                    double rho = model.Rho(t);
                    cUInf += -kappa * theta * tau / sigma * new Complex(Math.Sqrt(1 - (rho * rho)), rho);
                }

                double ratio = riskFreeDiscount / dividendDiscount;
                double fwdPrice = spotPrice / ratio;
                double epsilon = andersenPiterbargEpsilon * Math.PI / (Math.Sqrt(strike * fwdPrice) * riskFreeDiscount);

                double c_inf = -(cUInf + (dUInf * v0)).Real;
                double? UM() => FourierIntegration.AndersenPiterbargIntegrationLimit(c_inf, epsilon, v0, term);

                double vAvg = ((1 - Math.Exp(-kappaAvg * term)) * (v0 - thetaAvg) / (kappaAvg * term)) + thetaAvg;

                double bsPrice = new BlackCalculator(OptionType.Call, strike, fwdPrice, Math.Sqrt(vAvg * term), riskFreeDiscount).Value;

                IntegrationResult hResult = integration.CalculateWithStatistics(
                    c_inf, new APHelper(term, spotPrice, strike, ratio, Math.Sqrt(vAvg), this).Evaluate, UM);
                double h_cv = hResult.Value * Math.Sqrt(strike * fwdPrice) * riskFreeDiscount / Math.PI;
                evaluations += hResult.NumberOfEvaluations;

                switch (payoff.OptionType)
                {
                    case OptionType.Call:
                        value = bsPrice + h_cv;
                        break;
                    case OptionType.Put:
                        value = bsPrice + h_cv - (riskFreeDiscount * (fwdPrice - strike));
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

    // The integrand of the probabilities P1 and P2: the characteristic function's recursion across the grid, each
    // interval's rates the curves' continuous forwards between the instants its times denote on the model's clock
    private sealed class FjHelper
    {
        private readonly int j;
        private readonly double term;
        private readonly double v0, x, sx;
        private readonly double[] r, q;
        private readonly PiecewiseTimeDependentHestonModel model;
        private readonly TimeGrid timeGrid;

        internal FjHelper(PiecewiseTimeDependentHestonModel model, TermStructure clock, double term, double strike, int j)
        {
            this.j = j;
            this.term = term;
            v0 = model.V0;
            x = Math.Log(model.S0);
            sx = Math.Log(strike);
            r = new double[model.TimeGrid.Size - 1];
            q = new double[model.TimeGrid.Size - 1];
            this.model = model;
            timeGrid = model.TimeGrid;

            for (int i = 0; i < timeGrid.Size - 1; ++i)
            {
                double begin = Math.Min(term, timeGrid[i]);
                double end = Math.Min(term, timeGrid[i + 1]);
                r[i] = model.RiskFreeRate.ContinuousForwardRateAt(clock, begin, end);
                q[i] = model.DividendYield.ContinuousForwardRateAt(clock, begin, end);
            }
        }

        internal double Evaluate(double phi)
        {
            // avoid numeric overflow for phi->0.
            // todo: use l'Hospital's rule use to get lim_{phi->0}
            phi = Max(FloatEpsilon, phi);

            Complex dd = 0.0;
            Complex c = 0.0;

            for (int i = timeGrid.Size - 1; i > 0; --i)
            {
                double begin = timeGrid[i - 1];
                if (begin < term)
                {
                    double end = Math.Min(term, timeGrid[i]);
                    double tau = end - begin;
                    double t = 0.5 * (end + begin);
                    double rho = model.Rho(t);
                    double sigma = model.Sigma(t);
                    double kappa = model.Kappa(t);
                    double theta = model.Theta(t);

                    double sigma2 = sigma * sigma;
                    double t0 = kappa - ((j == 1) ? rho * sigma : 0);
                    double rpsig = rho * sigma * phi;

                    Complex t1 = t0 + new Complex(0, -rpsig);
                    Complex d = Complex.Sqrt((t1 * t1) - (sigma2 * phi * new Complex(-phi, (j == 1) ? 1 : -1)));
                    Complex g = (t1 - d) / (t1 + d);
                    Complex gt = (t1 - d - (dd * sigma2)) / (t1 + d - (dd * sigma2));

                    dd = (t1 + d) / sigma2 * (g - (gt * Complex.Exp(-d * tau))) / (1.0 - (gt * Complex.Exp(-d * tau)));

                    Complex lng = Complex.Log((1.0 - (gt * Complex.Exp(-d * tau))) / (1.0 - gt));

                    c = (kappa * theta / sigma2 * (((t1 - d) * tau) - (2.0 * lng)))
                        + new Complex(0.0, phi * (r[i - 1] - q[i - 1]) * tau) + c;
                }
            }

            return Complex.Exp((v0 * dd) + c + new Complex(0.0, phi * (x - sx))).Imaginary / phi;
        }
    }

    // The integrand of Andersen and Piterbarg's form: Black's characteristic function at the average variance less the
    // model's, the control variate's price added back
    private sealed class APHelper
    {
        private readonly double term;
        private readonly double sigmaBS;
        private readonly double sx, dd;
        private readonly AnalyticPTDHestonEngine engine;

        internal APHelper(double term, double s0, double strike, double ratio, double sigmaBS, AnalyticPTDHestonEngine engine)
        {
            this.term = term;
            this.sigmaBS = sigmaBS;
            double x = Math.Log(s0);
            sx = Math.Log(strike);
            dd = x - Math.Log(ratio);
            this.engine = engine;
        }

        internal double Evaluate(double u)
        {
            var z = new Complex(u, -0.5);
            Complex phiBS = Complex.Exp(-0.5 * sigmaBS * sigmaBS * term * ((z * z) + new Complex(-z.Imaginary, z.Real)));

            return (Complex.Exp(new Complex(0.0, u * (dd - sx))) * (phiBS - engine.ChF(z, term)) / ((u * u) + 0.25)).Real;
        }
    }
}
