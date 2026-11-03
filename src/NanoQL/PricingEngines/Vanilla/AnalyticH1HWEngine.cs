// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analytich1hwengine.hpp ql/pricingengines/vanilla/analytich1hwengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic Heston-Hull-White engine on Grzelak and Oosterlee's H1-HW approximation: the equity correlated with the
/// short rate, ρ_Sr ≥ 0, the variance independent of it.
/// </summary>
/// <remarks>
/// <para>
/// The approximation replaces √v_t in the equity-rate covariance by a fit E[√v_t] ≈ a + b·e^{−ct}, its term I₄ added to
/// <see cref="AnalyticHestonHullWhiteEngine"/>'s. QuantLib's values are corrected where they're wrong:
/// </para>
/// <list type="bullet">
/// <item><description>Where the fit has no decaying exponential, the ratio (Λ − a)/b that gives c at or below 0, or b =
/// 0, c is the limit c = +∞, the b terms dropped, and at or above 1 the limit c = 0, its b terms b·T and −b(1 −
/// e^{−λT})/λ; QuantLib's c is NaN there, and so is its price, at ρ_Sr = 0 too, or the fit grows exponentially. In
/// between, QuantLib's fit and bits.</description></item>
/// <item><description>I₄'s last term is b·e^{−cT}·(−expm1((c − λ)T))/(c − λ), with its limit −b·T·e^{−cT} at c = λ,
/// where QuantLib's b/(c − λ) is 0/0, and below λT = 2e-2, where I₄'s terms cancel to order λ and QuantLib's double
/// departs from the expression's exact value by more than 1e-12 relative, I₄ is evaluated in double-double, below 1e-10
/// in quad-double and below 1e-12 by the cubic through four quad-double values, the cut and floors measured against the
/// expression in 120 digits.</description></item>
/// </list>
/// <para>
/// References: Lech A. Grzelak, Cornelis W. Oosterlee, On The Heston Model with Stochastic; Lech A. Grzelak, Equity and
/// Foreign Exchange Hybrid Models for Pricing Long-Maturity Financial Derivatives.
/// </para>
/// </remarks>
public sealed class AnalyticH1HWEngine : AnalyticHestonHullWhiteEngine
{
    // the lambda T below which I4 runs in double-double, where QuantLib's double departs from the expression's exact
    // value by more than 1e-12 relative, its error about 2·eps/(lambda T)²; measured against 120 digits
    private const double I4Cut = 2e-2;

    // the lambda T below which double-double departs by more than 1e-12 and quad-double takes over, and below which the
    // cubic through four quad-double values does, measured likewise
    private const double I4Floor = 1e-10;
    private const double I4QuadFloor = 1e-12;

    // std::numeric_limits<float>::epsilon(), 2^-23, which QuantLib's Lambda series stops at
    private const double FloatEpsilon = 1.1920928955078125e-07;

    private static readonly GammaFunction Gamma = new();

    private readonly double rhoSr;

    /// <summary>Initializes a new instance of the <see cref="AnalyticH1HWEngine"/> class: Gauss-Laguerre
    /// integration.</summary>
    /// <param name="model">The Heston model.</param>
    /// <param name="hullWhiteModel">The Hull-White model.</param>
    /// <param name="rhoSr">The equity-rate correlation, non-negative.</param>
    /// <param name="integrationOrder">The quadrature's order, positive, at most 192.</param>
    /// <exception cref="ArgumentNullException">A model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The order is zero or above 192, or <paramref name="rhoSr"/> is negative
    /// ("Fourier integration is not stable if the equity interest rate correlation is negative").</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public AnalyticH1HWEngine(HestonModel model, HullWhite hullWhiteModel, double rhoSr, int integrationOrder = 144)
        : base(model, hullWhiteModel, integrationOrder)
    {
        if (!(rhoSr >= 0.0))
        {
            throw new ArgumentException(
                "Fourier integration is not stable if the equity interest rate correlation is negative", nameof(rhoSr));
        }

        this.rhoSr = rhoSr;
    }

    /// <summary>Initializes a new instance of the <see cref="AnalyticH1HWEngine"/> class: adaptive Gauss-Lobatto
    /// integration.</summary>
    /// <param name="model">The Heston model.</param>
    /// <param name="hullWhiteModel">The Hull-White model.</param>
    /// <param name="rhoSr">The equity-rate correlation, unchecked, as QuantLib's constructor leaves it.</param>
    /// <param name="relTolerance">The integration's relative tolerance.</param>
    /// <param name="maxEvaluations">The integration's maximum number of evaluations.</param>
    /// <exception cref="ArgumentNullException">A model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    public AnalyticH1HWEngine(HestonModel model, HullWhite hullWhiteModel, double rhoSr, double relTolerance, int maxEvaluations)
        : base(model, hullWhiteModel, relTolerance, maxEvaluations) => this.rhoSr = rhoSr;

    /// <summary>Prices a European plain-vanilla option with Gauss-Laguerre integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="hullWhiteModel">The Hull-White model.</param>
    /// <param name="rhoSr">The equity-rate correlation, non-negative.</param>
    /// <param name="integrationOrder">The quadrature's order, positive, at most 192.</param>
    /// <returns>The option's results: its value, and the additional result "numberOfEvaluations".</returns>
    /// <remarks>It hides the base engine's Gauss-Lobatto overload of the same parameter types; that one is reached
    /// through <see cref="AnalyticHestonHullWhiteEngine"/>.</remarks>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The order is zero or above 192, or <paramref name="rhoSr"/> is
    /// negative.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, Λ's series doesn't
    /// converge, or the integration fails.
    /// </exception>
    public static new OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, HestonModel model, HullWhite hullWhiteModel, double rhoSr, int integrationOrder = 144) =>
        new AnalyticH1HWEngine(model, hullWhiteModel, rhoSr, integrationOrder).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option with adaptive Gauss-Lobatto integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="hullWhiteModel">The Hull-White model.</param>
    /// <param name="rhoSr">The equity-rate correlation.</param>
    /// <param name="relTolerance">The integration's relative tolerance.</param>
    /// <param name="maxEvaluations">The integration's maximum number of evaluations.</param>
    /// <returns>The option's results: its value, and the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, Λ's series doesn't
    /// converge, or the integration fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        HullWhite hullWhiteModel,
        double rhoSr,
        double relTolerance,
        int maxEvaluations) =>
        new AnalyticH1HWEngine(model, hullWhiteModel, rhoSr, relTolerance, maxEvaluations).Calculate(option, context);

    /// <summary>The short rate's term, <see cref="AnalyticHestonHullWhiteEngine"/>'s, plus the equity-rate covariance's
    /// I₄ term.</summary>
    /// <param name="phi">The integration variable.</param>
    /// <param name="t">The maturity.</param>
    /// <param name="j">The probability's index, 1 or 2.</param>
    /// <returns>The term.</returns>
    protected override Complex AddOnTerm(double phi, double t, int j) =>
        base.AddOnTerm(phi, t, j) + new FjHelper(Model, HullWhiteModel, rhoSr, t, j).Evaluate(phi);

    // QuantLib's Fj_Helper (analytich1hwengine.cpp:29-135)
    private sealed class FjHelper
    {
        private readonly int j;
        private readonly double lambda;
        private readonly double eta;
        private readonly double v0;
        private readonly double kappa;
        private readonly double theta;
        private readonly double gamma;
        private readonly double d;
        private readonly double rhoSr;
        private readonly double term;

        internal FjHelper(HestonModel hestonModel, HullWhite hullWhiteModel, double rhoSr, double term, int j)
        {
            this.j = j;
            lambda = hullWhiteModel.A();
            eta = hullWhiteModel.Sigma;
            v0 = hestonModel.V0;
            kappa = hestonModel.Kappa;
            theta = hestonModel.Theta;
            gamma = hestonModel.Sigma;
            d = 4.0 * kappa * theta / (gamma * gamma);
            this.rhoSr = rhoSr;
            this.term = term;
        }

        internal Complex Evaluate(double u)
        {
            double gamma2 = gamma * gamma;

            double a, b, ratio;
            Fit fit;
            if (8.0 * kappa * theta / gamma2 > 1.0)
            {
                a = Math.Sqrt(theta - (gamma2 / (8.0 * kappa)));
                b = Math.Sqrt(v0) - a;
                ratio = (LambdaApprox(1.0) - a) / b;
                fit = FitOf(ratio, b);
                double c = fit == Fit.Decaying ? -Math.Log(ratio) : 0.0;
                return eta * rhoSr * I4(u, a, b, c, fit);
            }
            else
            {
                a = Math.Sqrt(gamma2 / (2.0 * kappa))
                    * Math.Exp(Gamma.LogValue(0.5 * (d + 1.0)) - Gamma.LogValue(0.5 * d));

                const double t1 = 0.0;
                double t2 = 1.0 / kappa;

                double lambdaT1 = Math.Sqrt(v0);
                double lambdaT2 = Lambda(t2);

                ratio = (lambdaT2 - a) / (lambdaT1 - a);
                fit = FitOf(ratio, lambdaT1 - a);
                double c = fit == Fit.Decaying ? Math.Log(ratio) / (t1 - t2) : 0.0;
                b = fit == Fit.Decaying ? Math.Exp(c * t1) * (lambdaT1 - a) : lambdaT1 - a;

                return eta * rhoSr * I4(u, a, b, c, fit);
            }
        }

        // where the fit's ratio puts it: a decaying exponential in (0, 1), QuantLib's; c's limits outside
        private static Fit FitOf(double ratio, double b) =>
            b == 0.0 || !(ratio > 0.0) ? Fit.NoExponential
            : ratio >= 1.0 ? Fit.Constant
            : Fit.Decaying;

        // I4 = -1/lambda (u^2, -/+u) (b/c (1 - e^{-cT}) + aT + a/lambda (e^{-lambda T} - 1) + b e^{-cT} F), its real factor
        // in the arithmetic lambda T calls for
        private Complex I4(double u, double a, double b, double c, Fit fit)
        {
            double factor = IKappaTExpression.Evaluate(
                new I4Factor(a, b, c, term, fit), lambda, term, I4Cut, I4Floor, I4QuadFloor, double.PositiveInfinity);
            return new Complex(u * u, (j == 1) ? -u : u) * factor;
        }

        private double C(double t) => gamma * gamma / (4 * kappa) * (1.0 - Math.Exp(-kappa * t));

        private double LambdaOf(double t) => 4.0 * kappa * v0 * Math.Exp(-kappa * t) / (gamma * gamma * (1.0 - Math.Exp(-kappa * t)));

        private double LambdaApprox(double t) =>
            Math.Sqrt((C(t) * (LambdaOf(t) - 1.0)) + (C(t) * d * (1.0 + (1.0 / (2.0 * (d + LambdaOf(t)))))));

        private double Lambda(double t)
        {
            const int maxIter = 1000;
            double lambdaT = LambdaOf(t);

            int i = 0;
            double retVal = 0.0, s;

            do
            {
                double k = i;
                s = Math.Exp((k * Math.Log(0.5 * lambdaT)) + Gamma.LogValue((0.5 * (1 + d)) + k)
                             - Gamma.LogValue(k + 1) - Gamma.LogValue((0.5 * d) + k));
                retVal += s;
            }
            while (s > FloatEpsilon && ++i < maxIter);

            if (!(i < maxIter))
            {
                throw new NanoQLException("can not calculate Lambda");
            }

            retVal *= Math.Sqrt(2 * C(t)) * Math.Exp(-0.5 * lambdaT);
            return retVal;
        }
    }

    // the fit E[sqrt(v_t)] = a + b e^{-ct}: QuantLib's decaying exponential, or c's limits
    private enum Fit
    {
        Decaying,
        NoExponential,
        Constant,
    }

    // I4's real factor as a function of lambda, the Hull-White reversion; its last term b e^{-cT}(-expm1((c - lambda)
    // T))/(c - lambda), -b T e^{-cT} at c = lambda. Each term runs in the arithmetic: aT against a/lambda (e^{-lambda
    // T} - 1), and b/c (1 - e^{-cT}) against the last term, each pair cancels to order lambda, so a rounding in either
    // is divided by lambda; in double the first three terms and their sum are QuantLib's operations in its order, not
    // its bits: the last term is the expm1 form, and -1/lambda multiplies the sum where QuantLib multiplies (u^2, -/+u)
    // by -1/lambda first
    private readonly struct I4Factor(double a, double b, double c, double term, Fit fit) : IKappaTExpression
    {
        public T Evaluate<T>(T lambda)
            where T : struct, IRealArithmetic<T>
        {
            T aTerm = T.Of(a) * term;
            T aOverLambda = a / lambda * (T.Exp(-lambda * term) - 1.0);

            // QuantLib's order: ((b/c (1 - e^{-cT}) + aT) + a/lambda (e^{-lambda T} - 1)) + the last term
            T sum = fit switch
            {
                Fit.Decaying => ((T.Of(b) / c * (1.0 - T.Exp(T.Of(-c) * term))) + aTerm + aOverLambda) + LastTerm(lambda),

                // the c = 0 limit: b T and -b (1 - e^{-lambda T})/lambda
                Fit.Constant => ((T.Of(b) * term) + aTerm + aOverLambda) - (b * (1.0 - T.Exp(-lambda * term)) / lambda),

                // the c = +infinity limit: the b terms dropped
                _ => aTerm + aOverLambda,
            };

            return -1.0 / lambda * sum;
        }

        private T LastTerm<T>(T lambda)
            where T : struct, IRealArithmetic<T>
        {
            T x = c - lambda;
            T bDecay = T.Of(b) * T.Exp(T.Of(-c) * term);
            if (x.Value == 0.0)
            {
                return -(bDecay * term);
            }

            // -expm1(x T)/x in the arithmetic: 1 - e^{xT} over x
            T oneMinus = typeof(T) == typeof(RealDouble)
                ? T.Of(-Expm1Functions.StdExpm1(x.Value * term))
                : 1.0 - T.Exp(x * term);
            return bDecay * oneMinus / x;
        }
    }
}
