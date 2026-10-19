// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the exercises and payoffs the option vector files describe in their inputs, with the specs the C++
/// harness reads (<c>tools/cxx/src/exercise.cpp</c>, <c>tools/cxx/src/instruments/payoffspec.hpp</c>).
/// </summary>
/// <remarks>
/// An exercise is <c>{"type": "Exercise", "exerciseType"}</c>, <c>{"type": "EarlyExercise", "exerciseType",
/// "payoffAtExpiry"}</c>, <c>{"type": "AmericanExercise", "earliestDate", "latestDate", "payoffAtExpiry"}</c> (without
/// <c>"earliestDate"</c> for the constructor from the latest date only), <c>{"type": "BermudanExercise", "dates",
/// "payoffAtExpiry"}</c> or <c>{"type": "EuropeanExercise", "date"}</c>. A payoff is <c>{"type", ...}</c> with its
/// constructor's arguments: <c>"optionType"</c> (QuantLib's integer), <c>"strike"</c>, <c>"moneyness"</c>,
/// <c>"secondStrike"</c> and <c>"cashPayoff"</c>, as the payoff takes them.
/// </remarks>
internal static class OptionInputs
{
    /// <summary>The exercise a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The exercise.</returns>
    public static Exercise ExerciseOf(JsonElement spec) => spec.GetProperty("type").GetString() switch
    {
        "Exercise" => new Exercise((ExerciseType)spec.GetProperty("exerciseType").GetInt32()),
        "EarlyExercise" => new EarlyExercise(
            (ExerciseType)spec.GetProperty("exerciseType").GetInt32(), spec.GetProperty("payoffAtExpiry").GetBoolean()),
        "AmericanExercise" => spec.TryGetProperty("earliestDate", out JsonElement earliest)
            ? new AmericanExercise(
                new Date(earliest.GetInt32()),
                new Date(spec.GetProperty("latestDate").GetInt32()),
                spec.GetProperty("payoffAtExpiry").GetBoolean())
            : new AmericanExercise(
                new Date(spec.GetProperty("latestDate").GetInt32()), spec.GetProperty("payoffAtExpiry").GetBoolean()),
        "BermudanExercise" => new BermudanExercise(
            spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())),
            spec.GetProperty("payoffAtExpiry").GetBoolean()),
        "EuropeanExercise" => new EuropeanExercise(new Date(spec.GetProperty("date").GetInt32())),
        string type => throw new InvalidOperationException($"unknown exercise {type}"),
        null => throw new InvalidOperationException("an exercise spec without a type"),
    };

    /// <summary>The payoff a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The payoff.</returns>
    public static Payoff PayoffOf(JsonElement spec)
    {
        OptionType Type() => (OptionType)spec.GetProperty("optionType").GetInt32();
        double Real(string name) => IndexInputs.Real(spec.GetProperty(name));
        return spec.GetProperty("type").GetString() switch
        {
            "NullPayoff" => new NullPayoff(),
            "FloatingTypePayoff" => new FloatingTypePayoff(Type()),
            "PlainVanillaPayoff" => new PlainVanillaPayoff(Type(), Real("strike")),
            "PercentageStrikePayoff" => new PercentageStrikePayoff(Type(), Real("moneyness")),
            "AssetOrNothingPayoff" => new AssetOrNothingPayoff(Type(), Real("strike")),
            "CashOrNothingPayoff" => new CashOrNothingPayoff(Type(), Real("strike"), Real("cashPayoff")),
            "GapPayoff" => new GapPayoff(Type(), Real("strike"), Real("secondStrike")),
            "SuperFundPayoff" => new SuperFundPayoff(Real("strike"), Real("secondStrike")),
            "SuperSharePayoff" => new SuperSharePayoff(Real("strike"), Real("secondStrike"), Real("cashPayoff")),
            string type => throw new InvalidOperationException($"unknown payoff {type}"),
            null => throw new InvalidOperationException("a payoff spec without a type"),
        };
    }
}
