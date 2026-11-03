// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2022 Ignacio Anguita
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/coshestonengine.hpp ql/pricingengines/vanilla/coshestonengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The COS-method Heston engine, on efficient Fourier-cosine series expansions.</summary>
/// <remarks>
/// <para>
/// An immutable object over its <see cref="HestonModel"/>, the truncation range's width and the series' length; the
/// static <see cref="Calculate(VanillaOption, EvaluationContext, HestonModel, double, int)"/> builds one and prices
/// through it. The model's time runs from the earliest curve date and the forward and discount are read by date (<see
/// cref="AnalyticHestonEngine"/>'s reads); at the model's time 0 the price is QuantLib's bound, the forward's
/// discounted intrinsic value, before any cumulant.
/// </para>
/// <para>
/// The cumulants are QuantLib's expressions, evaluated where κt calls for it in another arithmetic: QuantLib's double,
/// and so its bits, from each cumulant's cut to its upper cut; double-double below the cut, quad-double below
/// double-double's floor and, below quad-double's, each cumulant's cubic in κ through four quad-double values; past the
/// upper cut, where e^{nκt} overflows, an extended-range double, each cut measured at the run against QuantLib's
/// expressions in 50 to 250 digits. At a term of 0 the cumulants, the skew and the kurtosis are their limits, 0. Below
/// σ = <see cref="SigmaCut"/>, where the classic form's division by σ² cancels, the characteristic function is
/// <see cref="AnalyticHestonEngine.ChF"/>'s, the same function in Andersen and Lake's form.
/// </para>
/// <para>
/// The series' length has to grow with the vol of variance and the term: at QuantLib's defaults, a range of 16 and 200
/// terms, a vol of variance of 1 prices a five-year call at twice the forward at −7.8 times its value and a twenty-year
/// one 39% low, where 2,000 terms bring them within 0.6% and 0.02%; QuantLib's own large-σ case takes a range of 25 and
/// 600 terms.
/// </para>
/// <para>
/// References: F. Fang, C.W. Oosterlee, A Novel Pricing Method for European Options based on Fourier-Cosine Series
/// Expansions; F. Le Floc'h, Fourier Integration and Stochastic Volatility Calibration.
/// </para>
/// </remarks>
public sealed class COSHestonEngine
{
    /// <summary>
    /// The σ below which the characteristic function is the analytic engine's: the classic form departs from it by more
    /// than 1e-12 relative up to σ = 0.067 on a grid of six parameter sets, ten u and nine times to 30 years, and by at
    /// most 9.4e-13 at the cut.
    /// </summary>
    internal const double SigmaCut = 0.07;

    // each cumulant's cut, double-double and quad-double floors and upper cut in kappa t, measured at the run against
    // QuantLib's expressions in 50 to 250 digits
    private const double C1Cut = 2e-3, C1Floor = 1e-9, C1QuadFloor = 1e-12, C1Upper = 500.0;
    private const double C2Cut = 0.2, C2Floor = 1e-6, C2QuadFloor = 1e-12, C2Upper = 200.0;
    private const double C3Cut = 0.5, C3Floor = 1e-3, C3QuadFloor = 5e-10, C3Upper = 200.0;
    private const double C4Cut = 1.0, C4Floor = 5e-3, C4QuadFloor = 2e-7, C4Upper = 100.0;

    private readonly HestonModel model;
    private readonly double l;
    private readonly int n;
    private readonly double kappa, theta, sigma, rho, v0;

    /// <summary>Initializes a new instance of the <see cref="COSHestonEngine"/> class.</summary>
    /// <param name="model">The Heston model.</param>
    /// <param name="l">The truncation range's half-width in standard deviations.</param>
    /// <param name="n">The number of terms of the cosine series.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public COSHestonEngine(HestonModel model, double l = 16, int n = 200)
    {
        ArgumentNullException.ThrowIfNull(model);
        this.model = model;
        this.l = l;
        this.n = n;
        kappa = model.Kappa;
        theta = model.Theta;
        sigma = model.Sigma;
        rho = model.Rho;
        v0 = model.V0;
    }

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="l">The truncation range's half-width in standard deviations.</param>
    /// <param name="n">The number of terms of the cosine series.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, the payoff isn't plain vanilla or the underlying
    /// isn't positive.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, HestonModel model, double l = 16, int n = 200) =>
        new COSHestonEngine(model, l, n).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, the payoff isn't plain vanilla or the underlying
    /// isn't positive.</exception>
    public OneAssetOptionResults Calculate(VanillaOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = Value(option) });
    }

    /// <summary>The normalized characteristic function of the log-forward's return at <paramref name="u"/>.</summary>
    /// <param name="u">The argument.</param>
    /// <param name="t">The time.</param>
    /// <returns>The value.</returns>
    public Complex ChF(double u, double t)
    {
        if (sigma < SigmaCut)
        {
            // the analytic engine's form of the same function
            return new AnalyticHestonEngine(model).ChF(new Complex(u, 0.0), t);
        }

        double sigma2 = sigma * sigma;
        Complex d = Complex.Sqrt(Squared(new Complex(kappa, -rho * sigma * u)) + (new Complex(u * u, u) * sigma2));
        var g = new Complex(kappa, -rho * sigma * u);
        Complex gg = (g - d) / (g + d);

        return Complex.Exp(
            (v0 / sigma2 * (1.0 - Complex.Exp(-d * t)) / (1.0 - (gg * Complex.Exp(-d * t))) * (g - d))
            + (kappa * theta / sigma2 * (((g - d) * t) - (2.0 * Complex.Log((1.0 - (gg * Complex.Exp(-d * t))) / (1.0 - gg))))));
    }

    /// <summary>The first cumulant of the log-forward's return at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The cumulant; 0 at a time of 0.</returns>
    public double C1(double t) => t == 0.0 ? 0.0 : IKappaTExpression.Evaluate(new C1Expression(t, theta, sigma, rho, v0), kappa, t, C1Cut, C1Floor, C1QuadFloor, C1Upper);

    /// <summary>The second cumulant at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The cumulant; 0 at a time of 0.</returns>
    public double C2(double t) => t == 0.0 ? 0.0 : IKappaTExpression.Evaluate(new C2Expression(t, theta, sigma, rho, v0), kappa, t, C2Cut, C2Floor, C2QuadFloor, C2Upper);

    /// <summary>The third cumulant at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The cumulant; 0 at a time of 0.</returns>
    public double C3(double t) => t == 0.0 ? 0.0 : IKappaTExpression.Evaluate(new C3Expression(t, theta, sigma, rho, v0), kappa, t, C3Cut, C3Floor, C3QuadFloor, C3Upper);

    /// <summary>The fourth cumulant at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The cumulant; 0 at a time of 0.</returns>
    public double C4(double t) => t == 0.0 ? 0.0 : IKappaTExpression.Evaluate(new C4Expression(t, theta, sigma, rho, v0), kappa, t, C4Cut, C4Floor, C4QuadFloor, C4Upper);

    /// <summary>The mean of the log-forward's return at a time, its first cumulant.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The mean.</returns>
    public double Mu(double t) => C1(t);

    /// <summary>The variance at a time, the second cumulant.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The variance.</returns>
    public double Var(double t) => C2(t);

    /// <summary>The skewness at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The skewness; its limit 0 at a time of 0.</returns>
    public double Skew(double t) => t == 0.0 ? 0.0 : C3(t) / Math.Pow(C2(t), 1.5);

    /// <summary>The excess kurtosis at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The kurtosis; its limit 0 at a time of 0.</returns>
    public double Kurtosis(double t) => t == 0.0 ? 0.0 : C4(t) / Squared(C2(t));

    private static double Squared(double x) => x * x;

    private static Complex Squared(Complex x) => x * x;

    // std::max, which returns its first argument where neither is less than the other
    private static double Max(double a, double b) => a < b ? b : a;

    // QuantLib's calculate, the model's time from the earliest curve date and the curves read by date
    private double Value(VanillaOption option)
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

        HestonProcess process = model.Process;
        Date maturityDate = option.Exercise.LastDate();
        double maturity = process.ModelTime(maturityDate);

        double k = payoff.Strike;
        double spot = process.S0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        double df = process.RiskFreeRate.AccruedDiscount(maturityDate);
        double qf = process.DividendYield.AccruedDiscount(maturityDate);
        if (maturity == 0.0)
        {
            // no variance has accrued: QuantLib's bound, the forward's discounted intrinsic value
            return Bound(payoff.OptionType, spot, qf, k, df);
        }

        double cum1 = C1(maturity);
        double w = Math.Sqrt(Math.Abs(C2(maturity))

            // the 4th order doesn't necessarily improve the precision
            // + std::sqrt(std::fabs(c4(maturity)))
        );

        double fwd = spot * qf / df;
        double x = Math.Log(fwd / k);
        double a = x + cum1 - (l * w);
        double b = x + cum1 + (l * w);

        // Check if it exceeds the truncation bound
        if (x >= b / 2 || x <= a / 2)
        {
            // returns lower/upper bounds
            return Bound(payoff.OptionType, spot, qf, k, df);
        }

        double d = 1.0 / (b - a);
        double expA = Math.Exp(a);
        double s = ChF(0, maturity).Real * (expA - 1 - a) * d;

        for (int i = 1; i < n; ++i)
        {
            double r = i * Math.PI * d;
            double un = 2.0 * d * ((1.0 / (1.0 + (r * r)) * (expA + (r * Math.Sin(r * a)) - Math.Cos(r * a))) - (1.0 / r * Math.Sin(r * a)));
            s += un * (ChF(r, maturity) * Complex.Exp(new Complex(0, r * (x - a)))).Real;
        }

        return payoff.OptionType == OptionType.Put ? k * df * s : (spot * qf) - (k * df * (1 - s));
    }

    // QuantLib's lower and upper bounds
    private static double Bound(OptionType type, double spot, double qf, double k, double df) =>
        type == OptionType.Put ? Max((-spot * qf) + (k * df), 0.0) : Max((spot * qf) - (k * df), 0.0);

    // QuantLib's COSHestonEngine::c1, its members as parameters
    internal static T C1<T>(T kappa, T theta, T sigma, T rho, T v0, T t)
        where T : struct, IRealArithmetic<T>
    {
        return (-theta + T.Exp(kappa*t)
            *( theta - kappa*t*theta -
            v0) + v0)/(2*T.Exp(kappa*t)*kappa);
    }

    // QuantLib's COSHestonEngine::c2, its members as parameters
    internal static T C2<T>(T kappa, T theta, T sigma, T rho, T v0, T t)
        where T : struct, IRealArithmetic<T>
    {
        T sigma2 = sigma*sigma;
        T kappa2 = kappa*kappa;
        T kappa3 = kappa2*kappa;
        return (sigma2*(theta - 2*v0) +
            T.Exp(2*kappa*t)*(8*kappa3*t*theta -
            8*kappa2*(theta + rho*sigma*t*theta - v0) +
            sigma2*(-5*theta + 2*v0) + 2*kappa*sigma*(8*rho*theta +
            sigma*t*theta - 4*rho*v0)) +
            4*T.Exp(kappa*t)*(sigma2*theta -
            2*kappa2*(-1 + rho*sigma*t)*(theta - v0) +
            kappa*sigma*(sigma*t*(theta - v0) + 2*rho*(-2*theta +
            v0))))/(8.0*T.Exp(2*kappa*t)*kappa3);
    }

    // QuantLib's COSHestonEngine::c3, its members as parameters
    internal static T C3<T>(T kappa, T theta, T sigma, T rho, T v0, T t)
        where T : struct, IRealArithmetic<T>
    {
        T sigma2 = sigma*sigma;
        T sigma3 = sigma2*sigma;
        T kappa2 = kappa*kappa;
        T kappa3 = kappa2*kappa;
        T kappa4 = kappa3*kappa;
        T rho2   = rho*rho;
        return
            -(sigma*(sigma3*(theta - 3*v0) +
            T.Exp(3*kappa*t)*(2*(-11*sigma3 -
            24*kappa4*rho*t + 3*kappa*sigma2*(20*rho +
            sigma*t) - 6*kappa2*sigma*(5 + 3*rho*(4*rho + sigma*t)) +
            12*kappa3*(sigma*t + 2*rho*(2 + rho*sigma*t)))*theta -
            6*(2*kappa*rho - sigma)*(4*kappa2 - 4*kappa*rho*sigma +
            sigma2)*v0) + 6*T.Exp(kappa*t)*sigma*(-2*kappa2*(-1 +
            rho*sigma*t)*(theta - 2*v0) + sigma2*(theta - v0) +
            kappa*sigma*(-4*rho*theta + sigma*t*theta + 6*rho*v0 - 2*sigma*t*v0)) +
            3*T.Exp(2*kappa*t)*(2*kappa*sigma2*(-16*rho*theta +
            sigma*t*(3*theta - v0)) + 8*kappa4*rho*t*(-2 +
            rho*sigma*t)*(theta - v0) + sigma3*(5*theta + v0) +
            8*kappa3*(-(rho*(4 + sigma2*t*t)*theta) + 2*sigma*t*(theta - v0) +
            2*rho2*sigma*t*(2*theta - v0) + rho*(2 +
            sigma2*t*t)*v0) + 2*kappa2*sigma*((8
            + 24*rho2 - 16*rho*sigma*t + sigma2*t*t)*theta - (8*rho2 -
            8*rho*sigma*t + sigma2*t*t)*v0))))/(16.0*T.Exp(3*kappa*t)*
            kappa*kappa4);
    }

    // QuantLib's COSHestonEngine::c4, its members as parameters
    internal static T C4<T>(T kappa, T theta, T sigma, T rho, T v0, T t)
        where T : struct, IRealArithmetic<T>
    {
        T sigma2 = sigma*sigma;
        T sigma3 = sigma2*sigma;
        T sigma4 = sigma2*sigma2;
        T kappa2 = kappa*kappa;
        T kappa3 = kappa2*kappa;
        T kappa4 = kappa2*kappa2;
        T kappa5 = kappa2*kappa3;
        T kappa6 = kappa3*kappa3;
        T kappa7 = kappa4*kappa3;
        T rho2   = rho*rho;
        T rho3   = rho2*rho;
        T t2     = t*t;
        T t3     = t2*t;
        return
            (sigma2*(3*sigma4*(theta - 4*v0) +
            3*T.Exp(4*kappa*t)*((-93*sigma4 +
            64*kappa5*(t + 4*rho2*t) +
            4*kappa*sigma3*(176*rho + 5*sigma*t) -
            32*kappa2*sigma2*(11 + 50*rho2 +
            5*rho*sigma*t) + 32*kappa3*sigma*(3*sigma*t + 4*rho*(10 +
            8*rho2 + 3*rho*sigma*t)) - 32*kappa4*(5 +
            4*rho*(6*rho + (3 + 2*rho2)*sigma*t)))*theta +
            4*(4*kappa2 - 4*kappa*rho*sigma +
            sigma2)*(4*kappa2*(1 + 4*rho2) -
            20*kappa*rho*sigma + 5*sigma2)*v0) +
            24*T.Exp(kappa*t)*sigma2*(-2*kappa2*(-1 +
            rho*sigma*t)*(theta - 3*v0) + sigma2*(theta - 2*v0) +
            kappa*sigma*(-4*rho*theta + sigma*t*theta + 10*rho*v0 -
            3*sigma*t*v0)) + 12*T.Exp(2*kappa*t)*(sigma4*(7*theta -
            4*v0) + 8*kappa4*(1 + 2*rho*sigma*t*(-2 +
            rho*sigma*t))*(theta - 2*v0) +
            2*kappa*sigma3*(-24*rho*theta + 5*sigma*t*theta +
            20*rho*v0 - 6*sigma*t*v0) + 4*kappa2*sigma2*((6
            + 20*rho2 - 14*rho*sigma*t +
            sigma2*t2)*theta - 2*(3 + 12*rho2 -
            10*rho*sigma*t + sigma2*t2)*v0) +
            8*kappa3*sigma*((3*sigma*t + 2*rho*(-4 + sigma*t*(4*rho -
            sigma*t)))*theta + 2*(-3*sigma*t + 2*rho*(3 + sigma*t*(-3*rho +
            sigma*t)))*v0)) -
            8*T.Exp(3*kappa*t)*(16*kappa6*rho2*t2*(-3 + rho*sigma*t)*(theta - v0) - 3*sigma4*(7*theta +
            2*v0) + 2*kappa3*sigma*((192*(rho + rho3) -
            6*(9 + 40*rho2)*sigma*t + 42*rho*sigma2*t2 -
            sigma3*t3)*theta + (-48*rho3 + 18*(1
            + 4*rho2)*sigma*t - 24*rho*sigma2*t2
            + sigma3*t3)*v0) + 12*kappa4*((-4 -
            24*rho2 + 8*rho*(4 + 3*rho2)*sigma*t - (3 +
            14*rho2)*sigma2*t2 + rho*sigma3*t3)*theta + (8*rho2 -
            8*rho*(2 + rho2)*sigma*t + (3 +
            8*rho2)*sigma2*t2 - rho*sigma3*t3)*v0) -
            6*kappa2*sigma2*((15 + 80*rho2 -
            35*rho*sigma*t + 2*sigma2*t2)*theta + (3 +
            sigma*t*(7*rho - sigma*t))*v0) + 24*kappa5*t*((-2 +
            rho*(4*sigma*t + rho*(-8 + sigma*t*(4*rho - sigma*t))))*theta + (2 +
            rho*(-4*sigma*t + rho*(4 + sigma*t*(-2*rho + sigma*t))))*v0) +
            3*kappa*sigma3*(sigma*t*(-9*theta + v0) + 10*rho*(6*theta
            + v0)))))/(64.0*T.Exp(4*kappa*t)*kappa7);
    }

    // c1 as a function of kappa, its other inputs fixed
    private readonly struct C1Expression(double t, double theta, double sigma, double rho, double v0) : IKappaTExpression
    {
        public T Evaluate<T>(T kappa)
            where T : struct, IRealArithmetic<T> =>
            C1(kappa, T.Of(theta), T.Of(sigma), T.Of(rho), T.Of(v0), T.Of(t));
    }

    // c2 as a function of kappa, its other inputs fixed
    private readonly struct C2Expression(double t, double theta, double sigma, double rho, double v0) : IKappaTExpression
    {
        public T Evaluate<T>(T kappa)
            where T : struct, IRealArithmetic<T> =>
            C2(kappa, T.Of(theta), T.Of(sigma), T.Of(rho), T.Of(v0), T.Of(t));
    }

    // c3 as a function of kappa, its other inputs fixed
    private readonly struct C3Expression(double t, double theta, double sigma, double rho, double v0) : IKappaTExpression
    {
        public T Evaluate<T>(T kappa)
            where T : struct, IRealArithmetic<T> =>
            C3(kappa, T.Of(theta), T.Of(sigma), T.Of(rho), T.Of(v0), T.Of(t));
    }

    // c4 as a function of kappa, its other inputs fixed
    private readonly struct C4Expression(double t, double theta, double sigma, double rho, double v0) : IKappaTExpression
    {
        public T Evaluate<T>(T kappa)
            where T : struct, IRealArithmetic<T> =>
            C4(kappa, T.Of(theta), T.Of(sigma), T.Of(rho), T.Of(v0), T.Of(t));
    }
}
