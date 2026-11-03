// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/batesengine.hpp ql/pricingengines/vanilla/batesengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine of the Heston model with double-exponential jumps: the jumps' term
/// tλ(p/(1 − gη_u) + q/(1 + gη_d) − 1 − g(p/(1 − η_u) + q/(1 + η_d) − 1)) added to the Heston exponent, q = 1 − p.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="AnalyticHestonEngine"/> on Gatheral's formulation, as QuantLib's derives from it, over its
/// <see cref="BatesDoubleExpModel"/>. An extension point: <see cref="BatesDoubleExpDetJumpEngine"/> derives from it.
/// </para>
/// <para>
/// For η_u ≥ 1 the up-jumps' exponential moment diverges and the term's compensator p/(1 − η_u) has no meaning, yet
/// QuantLib's engine prices: 816,076 for a 100 call at η_u = 1.01, a plausible 26.98 at 1.5, and at 1.0 its Lobatto
/// integration throws. Here the engine refuses a model at η_u ≥ 1, an <see cref="ArgumentException"/> naming the model,
/// as the model's constraint refuses one at construction (a refusal, <c>Divergences/BatesDoubleExpUpJumpTests.cs</c>);
/// QuantLib's bits below it.
/// </para>
/// </remarks>
public class BatesDoubleExpEngine : AnalyticHestonEngine
{
    private readonly BatesDoubleExpModel doubleExpModel;

    /// <summary>Initializes a new instance of the <see cref="BatesDoubleExpEngine"/> class: Gauss-Laguerre
    /// integration.</summary>
    /// <param name="model">The model.</param>
    /// <param name="integrationOrder">The quadrature's order, positive, at most 192.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The order is zero or above 192, or the model's η_u isn't below 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public BatesDoubleExpEngine(BatesDoubleExpModel model, int integrationOrder = 144)
        : base(
            CheckedUpJumps(model),
            AnalyticHestonEngineComplexLogFormula.Gatheral,
            FourierIntegration.GaussLaguerre(integrationOrder))
    {
        doubleExpModel = model;
    }

    /// <summary>Initializes a new instance of the <see cref="BatesDoubleExpEngine"/> class: adaptive Gauss-Lobatto
    /// integration.</summary>
    /// <param name="model">The model.</param>
    /// <param name="relTolerance">The integration's relative tolerance.</param>
    /// <param name="maxEvaluations">The integration's maximum number of evaluations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The model's η_u isn't below 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    public BatesDoubleExpEngine(BatesDoubleExpModel model, double relTolerance, int maxEvaluations)
        : base(
            CheckedUpJumps(model),
            AnalyticHestonEngineComplexLogFormula.Gatheral,
            FourierIntegration.GaussLobatto(relTolerance, null, maxEvaluations))
    {
        doubleExpModel = model;
    }

    /// <summary>Prices a European plain-vanilla option with Gauss-Laguerre integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="integrationOrder">The quadrature's order, positive, at most 192.</param>
    /// <returns>The option's results: its value, and the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The order is zero or above 192, or the model's η_u isn't below 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, or the integration
    /// fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, BatesDoubleExpModel model, int integrationOrder = 144) =>
        new BatesDoubleExpEngine(model, integrationOrder).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option with adaptive Gauss-Lobatto integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="relTolerance">The integration's relative tolerance.</param>
    /// <param name="maxEvaluations">The integration's maximum number of evaluations.</param>
    /// <returns>The option's results: its value, and the additional result "numberOfEvaluations".</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The model's η_u isn't below 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, or the integration
    /// fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        BatesDoubleExpModel model,
        double relTolerance,
        int maxEvaluations) =>
        new BatesDoubleExpEngine(model, relTolerance, maxEvaluations).Calculate(option, context);

    /// <summary>The double-exponential jumps' term.</summary>
    /// <param name="phi">The integration variable.</param>
    /// <param name="t">The maturity.</param>
    /// <param name="j">The probability's index, 1 or 2.</param>
    /// <returns>The term.</returns>
    protected override Complex AddOnTerm(double phi, double t, int j)
    {
        double p = doubleExpModel.P;
        double q = 1.0 - p;
        double nuDown = doubleExpModel.NuDown;
        double nuUp = doubleExpModel.NuUp;
        double lambda = doubleExpModel.Lambda;
        double i = (j == 1) ? 1.0 : 0.0;
        var g = new Complex(i, phi);

        return t * lambda * ((p / (1.0 - (g * nuUp))) + (q / (1.0 + (g * nuDown))) - 1.0
            - (g * ((p / (1 - nuUp)) + (q / (1 + nuDown)) - 1)));
    }

    // The up-jumps' exponential moment is finite for η_u below 1 only
    private static BatesDoubleExpModel CheckedUpJumps(BatesDoubleExpModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!(model.NuUp < 1.0))
        {
            throw new ArgumentException("the double exponential model's up-jump mean must be below 1", nameof(model));
        }

        return model;
    }
}
