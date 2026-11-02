// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Asian option specs of tools/vectors/gen/instruments/asianoption.py, which the Asian engines' generators share.
/// </summary>
internal static class AsianInputs
{
    /// <summary>The discrete-averaging option a spec describes.</summary>
    /// <param name="spec">The spec: the average type, the fixing dates, the payoff, the exercise, and either the running
    /// accumulator and the past fixings' count or every past fixing's value.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The option.</returns>
    public static DiscreteAveragingAsianOption DiscreteOptionOf(JsonElement spec, EvaluationContext context)
    {
        var average = (AverageType)spec.GetProperty("averageType").GetInt32();
        Date[] fixingDates = [.. spec.GetProperty("fixingDates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
        StrikedTypePayoff payoff = PayoffOf(spec.GetProperty("payoff"));
        Exercise exercise = ExerciseOf(spec.GetProperty("exercise"), context);
        return spec.TryGetProperty("allPastFixings", out JsonElement allPast)
            ? new DiscreteAveragingAsianOption(average, fixingDates, payoff, exercise, [.. allPast.EnumerateArray().Select(x => x.GetDouble())])
            : new DiscreteAveragingAsianOption(
                average, spec.GetProperty("runningAccumulator").GetDouble(), spec.GetProperty("pastFixings").GetInt32(), fixingDates, payoff, exercise);
    }

    /// <summary>
    /// Whether an engine spec asks for low-discrepancy draws (<c>"traits": "ld"</c>; pseudo-random without it).
    /// </summary>
    /// <param name="engine">The engine spec.</param>
    /// <returns><see langword="true"/> for the low-discrepancy traits.</returns>
    public static bool LowDiscrepancy(JsonElement engine) =>
        engine.TryGetProperty("traits", out JsonElement traits) && traits.GetString() == "ld";

    /// <summary>
    /// Checks the outputs a case records among the NPV, the error estimate and the Greeks, from one pricing, made
    /// once, its throw each output's.
    /// </summary>
    /// <param name="check">The file's checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="price">The pricing.</param>
    public static void CheckResults(VectorAssert check, VectorCase c, Func<OneAssetOptionResults> price)
    {
        var results = new Lazy<OneAssetOptionResults>(price);
        (string Name, Func<OneAssetOptionResults, double?> Read)[] outputs =
        [
            ("NPV", r => r.NPV),
            ("errorEstimate", r => r.ErrorEstimate),
            ("delta", r => r.Delta),
            ("gamma", r => r.Gamma),
            ("theta", r => r.Theta),
            ("vega", r => r.Vega),
            ("rho", r => r.Rho),
            ("dividendRho", r => r.DividendRho),
        ];
        foreach ((string name, Func<OneAssetOptionResults, double?> read) in outputs)
        {
            if (c.Expected.TryGetProperty(name, out _))
            {
                check.Output(c, name, Lazy(() => read(results.Value)));
            }
        }
    }

    /// <summary>The continuous-averaging option a spec describes.</summary>
    /// <param name="spec">The spec: the average type, the payoff, the exercise and, where seasoned, the start date.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The option.</returns>
    public static ContinuousAveragingAsianOption ContinuousOptionOf(JsonElement spec, EvaluationContext context)
    {
        var average = (AverageType)spec.GetProperty("averageType").GetInt32();
        StrikedTypePayoff payoff = PayoffOf(spec.GetProperty("payoff"));
        Exercise exercise = ExerciseOf(spec.GetProperty("exercise"), context);
        return spec.TryGetProperty("startDate", out JsonElement start)
            ? new ContinuousAveragingAsianOption(average, new Date(start.GetInt32()), payoff, exercise)
            : new ContinuousAveragingAsianOption(average, payoff, exercise);
    }
}
