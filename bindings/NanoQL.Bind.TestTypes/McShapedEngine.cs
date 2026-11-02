// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Bind.TestTypes;

/// <summary>
/// An engine shaped as nanoql's Monte Carlo engines are (docs/bindings.md, "Bound engines"): nullable value inputs with
/// no default, which a bound engine left open on both sides takes as null, and a collection of tuples a <c>Bind</c>
/// leaves open by its first element's array. Its price is the European Monte Carlo engine's on pseudo-random draws,
/// scaled by the shape's weights.
/// </summary>
public static class McShapedEngine
{
    /// <summary>Prices a European vanilla option by Monte Carlo, scaled by a shape's weights.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or null for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or null for a number of steps.</param>
    /// <param name="requiredSamples">The number of samples, or null.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or null.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="shape">The weights by time, their sum scaling the price; null for none.</param>
    /// <returns>The option's results, the NPV scaled.</returns>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        int? requiredSamples,
        double? requiredTolerance,
        long seed,
        IReadOnlyList<(double time, double weight)>? shape = null)
    {
        OneAssetOptionResults r = MCEuropeanEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, context, process, timeSteps, timeStepsPerYear, false, false, requiredSamples, requiredTolerance, null, seed);
        return shape is null ? r : r with { NPV = r.NPV * shape.Sum(s => s.weight) };
    }
}
