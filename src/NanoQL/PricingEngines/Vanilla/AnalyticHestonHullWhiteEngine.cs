// SPDX-FileCopyrightText: Copyright (C) 2007 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analytichestonhullwhiteengine.hpp ql/pricingengines/vanilla/analytichestonhullwhiteengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic Heston engine with a Hull-White short rate independent of the equity and its variance: dS = (r − d)S dt
/// + √v S dW₁, dv = κ(θ − v) dt + σ√v dW₂, dr = (θ(t) − ar) dt + η dW₃, dW₁dW₂ = ρ dt, dW₁dW₃ = dW₂dW₃ = 0.
/// </summary>
/// <remarks>
/// <para>
/// An object over its Heston and Hull-White models (the S-hes plan's HD7), unsealed as QuantLib's
/// (<see cref="AnalyticH1HWEngine"/> derives). The short rate enters the characteristic function through
/// <see cref="AnalyticHestonEngine.AddOnTerm"/>, its variance offset m at the model's time, which runs from the earliest
/// curve date (HR7 (xi), <see cref="AnalyticHestonEngine"/>'s).
/// </para>
/// <para>
/// Below a·t = 1e-2 the offset is S-van's Black-Scholes-Merton Hull-White series to (a·t)⁴, half that engine's v, where
/// QuantLib's closed form, used above a·t = ε^¼, cancels to a relative error of about 3ε/(a·t)³ and its series below
/// stops at (a·t)², the price moving by 5e-8 relative across its cut (HR7 (xiv)); QuantLib's closed form and bits above
/// 1e-2.
/// </para>
/// <para>
/// References: Karel in't Hout, Joris Bierkens, Antoine von der Ploeg, Joe in't Panhuis, A Semi closed-from analytic
/// pricing formula for call options in a hybrid Heston-Hull-White Model; A. Sepp, Pricing European-Style Options under
/// Jump Diffusion Processes with Stochastic Volatility: Applications of Fourier Transform.
/// </para>
/// </remarks>
public class AnalyticHestonHullWhiteEngine : AnalyticHestonEngine
{
    // where S-van's series replaces QuantLib's closed form (HR7 (xiv); the S-van plan's VR4 (xxvii))
    private const double SeriesCut = 1e-2;

    private readonly double a;
    private readonly double sigma;

    /// <summary>Initializes a new instance of the <see cref="AnalyticHestonHullWhiteEngine"/> class: Gatheral's form,
    /// Gauss-Laguerre integration.</summary>
    /// <param name="hestonModel">The Heston model.</param>
    /// <param name="hullWhiteModel">The Hull-White model.</param>
    /// <param name="integrationOrder">The quadrature's order, positive, at most 192.</param>
    /// <exception cref="ArgumentNullException">A model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The order is zero or above 192.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public AnalyticHestonHullWhiteEngine(HestonModel hestonModel, HullWhite hullWhiteModel, int integrationOrder = 144)
        : base(hestonModel, AnalyticHestonEngineComplexLogFormula.Gatheral, FourierIntegration.GaussLaguerre(integrationOrder))
    {
        ArgumentNullException.ThrowIfNull(hullWhiteModel);
        HullWhiteModel = hullWhiteModel;
        a = hullWhiteModel.A();
        sigma = hullWhiteModel.Sigma;
    }

    /// <summary>Initializes a new instance of the <see cref="AnalyticHestonHullWhiteEngine"/> class: Gatheral's form,
    /// adaptive Gauss-Lobatto integration.</summary>
    /// <param name="hestonModel">The Heston model.</param>
    /// <param name="hullWhiteModel">The Hull-White model.</param>
    /// <param name="relTolerance">The integration's relative tolerance.</param>
    /// <param name="maxEvaluations">The integration's maximum number of evaluations.</param>
    /// <exception cref="ArgumentNullException">A model is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    public AnalyticHestonHullWhiteEngine(HestonModel hestonModel, HullWhite hullWhiteModel, double relTolerance, int maxEvaluations)
        : base(
            hestonModel,
            AnalyticHestonEngineComplexLogFormula.Gatheral,
            FourierIntegration.GaussLobatto(relTolerance, null, maxEvaluations))
    {
        ArgumentNullException.ThrowIfNull(hullWhiteModel);
        HullWhiteModel = hullWhiteModel;
        a = hullWhiteModel.A();
        sigma = hullWhiteModel.Sigma;
    }

    /// <summary>Gets the Hull-White model.</summary>
    public HullWhite HullWhiteModel { get; }

    /// <summary>Prices a European plain-vanilla option with Gauss-Laguerre integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="hestonModel">The Heston model.</param>
    /// <param name="hullWhiteModel">The Hull-White model.</param>
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
        VanillaOption option, EvaluationContext context, HestonModel hestonModel, HullWhite hullWhiteModel, int integrationOrder = 144) =>
        new AnalyticHestonHullWhiteEngine(hestonModel, hullWhiteModel, integrationOrder).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option with adaptive Gauss-Lobatto integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="hestonModel">The Heston model.</param>
    /// <param name="hullWhiteModel">The Hull-White model.</param>
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
        VanillaOption option,
        EvaluationContext context,
        HestonModel hestonModel,
        HullWhite hullWhiteModel,
        double relTolerance,
        int maxEvaluations) =>
        new AnalyticHestonHullWhiteEngine(hestonModel, hullWhiteModel, relTolerance, maxEvaluations).Calculate(option, context);

    /// <summary>The short rate's term: (−m u², u(m − 2m(j − 1))), m the variance offset at the maturity.</summary>
    /// <param name="phi">The integration variable.</param>
    /// <param name="t">The maturity.</param>
    /// <param name="j">The probability's index, 1 or 2.</param>
    /// <returns>The term.</returns>
    protected override Complex AddOnTerm(double phi, double t, int j)
    {
        double m = VarianceOffset(t);
        return new Complex(-m * phi * phi, phi * (m - (2 * m * (j - 1))));
    }

    // QuantLib's m_ at the maturity (analytichestonhullwhiteengine.cpp:57-67), S-van's series below a·t = 1e-2
    private double VarianceOffset(double t)
    {
        if (a * t > SeriesCut)
        {
            return sigma * sigma / (2 * a * a)
                * (t + (2 / a * Math.Exp(-a * t)) - (1 / (2 * a) * Math.Exp(-2 * a * t)) - (3 / (2 * a)));
        }

        // low-a algebraic limit, to (a·t)⁴ (HR7 (xiv); the S-van plan's VR4 (xxvii))
        return 0.5 * sigma * sigma * t * t * t
            * ((1 / 3.0) - (0.25 * a * t) + (7 / 60.0 * a * a * t * t)
               - (a * a * a * t * t * t / 24.0) + (31 / 2520.0 * a * a * a * a * t * t * t * t));
    }
}
