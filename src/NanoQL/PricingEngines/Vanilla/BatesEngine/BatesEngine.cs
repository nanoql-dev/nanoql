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
/// The analytic Bates engine: European plain-vanilla options under the Heston model with log-normal jumps, by the
/// Fourier transform of the characteristic function, the jumps' term added to the Heston exponent.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="AnalyticHestonEngine"/> on Gatheral's formulation, as QuantLib's derives from it: an immutable object
/// over its <see cref="BatesModel"/>; the static <c>Calculate</c> overloads build one and price through it. An
/// extension point: <see cref="BatesDetJumpEngine"/> derives from it. The model's time, its curves and an option at
/// model time 0 are the analytic engine's.
/// </para>
/// <para>
/// References: D. Bates, Jumps and stochastic volatility: exchange rate processes implicit in Deutsche mark options,
/// Review of Financial Studies 9, 69-107; A. Sepp, Pricing European-Style Options under Jump Diffusion Processes with
/// Stochastic Volatility: Applications of Fourier Transform.
/// </para>
/// </remarks>
public class BatesEngine : AnalyticHestonEngine
{
    private readonly BatesModel batesModel;

    /// <summary>Initializes a new instance of the <see cref="BatesEngine"/> class: Gauss-Laguerre integration.</summary>
    /// <param name="model">The Bates model.</param>
    /// <param name="integrationOrder">The quadrature's order, positive, at most 192.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The order is zero or above 192.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public BatesEngine(BatesModel model, int integrationOrder = 144)
        : base(model, AnalyticHestonEngineComplexLogFormula.Gatheral, FourierIntegration.GaussLaguerre(integrationOrder))
    {
        batesModel = model;
    }

    /// <summary>Initializes a new instance of the <see cref="BatesEngine"/> class: adaptive Gauss-Lobatto
    /// integration.</summary>
    /// <param name="model">The Bates model.</param>
    /// <param name="relTolerance">The integration's relative tolerance.</param>
    /// <param name="maxEvaluations">The integration's maximum number of evaluations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    public BatesEngine(BatesModel model, double relTolerance, int maxEvaluations)
        : base(
            model,
            AnalyticHestonEngineComplexLogFormula.Gatheral,
            FourierIntegration.GaussLobatto(relTolerance, null, maxEvaluations))
    {
        batesModel = model;
    }

    /// <summary>Prices a European plain-vanilla option with Gauss-Laguerre integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Bates model.</param>
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
        VanillaOption option, EvaluationContext context, BatesModel model, int integrationOrder = 144) =>
        new BatesEngine(model, integrationOrder).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option with adaptive Gauss-Lobatto integration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Bates model.</param>
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
        VanillaOption option, EvaluationContext context, BatesModel model, double relTolerance, int maxEvaluations) =>
        new BatesEngine(model, relTolerance, maxEvaluations).Calculate(option, context);

    /// <summary>The log-normal jumps' term: tλ(e^{νg + δ²g²/2} − 1 − g(e^{ν + δ²/2} − 1)), g = (j == 1, φ).</summary>
    /// <param name="phi">The integration variable.</param>
    /// <param name="t">The maturity.</param>
    /// <param name="j">The probability's index, 1 or 2.</param>
    /// <returns>The term.</returns>
    protected override Complex AddOnTerm(double phi, double t, int j)
    {
        double nu = batesModel.Nu;
        double delta2 = 0.5 * batesModel.Delta * batesModel.Delta;
        double lambda = batesModel.Lambda;
        double i = (j == 1) ? 1.0 : 0.0;
        var g = new Complex(i, phi);

        // it can throw: to be fixed
        return t * lambda * (ComplexFunctions.Exp((nu * g) + (delta2 * g * g)) - 1.0 - (g * (Math.Exp(nu + delta2) - 1.0)));
    }
}
