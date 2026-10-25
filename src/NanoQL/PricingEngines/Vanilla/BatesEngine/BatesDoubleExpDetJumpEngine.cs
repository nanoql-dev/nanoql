// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/batesengine.hpp ql/pricingengines/vanilla/batesengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine of the Heston model with double-exponential jumps at a deterministic intensity: the
/// double-exponential jumps' term at the intensity's average over the maturity.
/// </summary>
/// <remarks>
/// A <see cref="BatesDoubleExpEngine"/>, as QuantLib's derives from it (the S-hes plan's HR1 (iv)), over its
/// <see cref="BatesDoubleExpDetJumpModel"/>; the average's weights as <see cref="BatesDetJumpEngine"/>'s, without
/// cancellation below κ_λt = 1e-2 (HR7 (iv)); η_u below 1 (HR7 (v)).
/// </remarks>
public sealed class BatesDoubleExpDetJumpEngine : BatesDoubleExpEngine
{
    private readonly BatesDoubleExpDetJumpModel detJumpModel;

    /// <summary>Initializes a new instance of the <see cref="BatesDoubleExpDetJumpEngine"/> class: Gauss-Laguerre
    /// integration.</summary>
    /// <param name="model">The model.</param>
    /// <param name="integrationOrder">The quadrature's order, positive, at most 192.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The order is zero or above 192, or the model's η_u isn't below 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public BatesDoubleExpDetJumpEngine(BatesDoubleExpDetJumpModel model, int integrationOrder = 144)
        : base(model, integrationOrder)
    {
        detJumpModel = model;
    }

    /// <summary>Initializes a new instance of the <see cref="BatesDoubleExpDetJumpEngine"/> class: adaptive
    /// Gauss-Lobatto integration.</summary>
    /// <param name="model">The model.</param>
    /// <param name="relTolerance">The integration's relative tolerance.</param>
    /// <param name="maxEvaluations">The integration's maximum number of evaluations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The model's η_u isn't below 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    public BatesDoubleExpDetJumpEngine(BatesDoubleExpDetJumpModel model, double relTolerance, int maxEvaluations)
        : base(model, relTolerance, maxEvaluations)
    {
        detJumpModel = model;
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
        VanillaOption option,
        EvaluationContext context,
        BatesDoubleExpDetJumpModel model,
        int integrationOrder = 144) =>
        new BatesDoubleExpDetJumpEngine(model, integrationOrder).Calculate(option, context);

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
        BatesDoubleExpDetJumpModel model,
        double relTolerance,
        int maxEvaluations) =>
        new BatesDoubleExpDetJumpEngine(model, relTolerance, maxEvaluations).Calculate(option, context);

    /// <summary>The double-exponential term, weighted by the intensity's average over the maturity.</summary>
    /// <param name="phi">The integration variable.</param>
    /// <param name="t">The maturity.</param>
    /// <param name="j">The probability's index, 1 or 2.</param>
    /// <returns>The term.</returns>
    protected override Complex AddOnTerm(double phi, double t, int j)
    {
        Complex l = base.AddOnTerm(phi, t, j);

        double lambda = detJumpModel.Lambda;
        double kappaLambda = detJumpModel.KappaLambda;
        double thetaLambda = detJumpModel.ThetaLambda;

        return DetJumpWeights.Term(l, lambda, kappaLambda, thetaLambda, t);
    }
}
