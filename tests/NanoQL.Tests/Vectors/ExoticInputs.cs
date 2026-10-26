// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The single-asset exotics' specs the S-exo vectors share: cliquets
/// (<c>tools/vectors/gen/pricingengines/cliquet/analyticcliquetengine.py</c>) and forward-start options
/// (<c>tools/vectors/gen/pricingengines/forward/forwardengine.py</c>).
/// </summary>
internal static class ExoticInputs
{
    /// <summary>The cliquet option a spec describes: {"payoff", "exercise", "resets"}.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The option.</returns>
    public static CliquetOption CliquetOf(JsonElement spec, EvaluationContext context) =>
        new(
            (PercentageStrikePayoff)PayoffOf(spec.GetProperty("payoff")),
            (EuropeanExercise)ExerciseOf(spec.GetProperty("exercise"), context),
            [.. spec.GetProperty("resets").EnumerateArray().Select(DateOf)]);

    /// <summary>The forward-start option a spec describes: {"moneyness", "resetDate", "payoff", "exercise"}.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The option.</returns>
    public static ForwardVanillaOption ForwardOf(JsonElement spec, EvaluationContext context) =>
        new(
            spec.GetProperty("moneyness").GetDouble(),
            DateOf(spec.GetProperty("resetDate")),
            PayoffOf(spec.GetProperty("payoff")),
            ExerciseOf(spec.GetProperty("exercise"), context));

    private static Date DateOf(JsonElement spec) => new(spec.GetInt32());
}
