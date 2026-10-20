// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the swaption volatility cubes the cube vector files describe in their inputs, as their generators build them
/// (<c>tools/vectors/gen/termstructures/volatility/swaption/swaptionvolcube.py</c>'s functions), and reads the stability
/// filter's dropped outputs.
/// </summary>
/// <remarks>
/// A cube is <c>{"type", "atm", "optionTenors", "swapTenors", "strikeSpreads", "volSpreads", "swapIndex",
/// "shortSwapIndex", "vegaWeightedSmileFit", "enableExtrapolation"}</c>, its type
/// <c>"InterpolatedSwaptionVolatilityCube"</c>, or <c>"SabrSwaptionVolatilityCube"</c> or
/// <c>"ZabrSwaptionVolatilityCube"</c>, each of which adds
/// <c>"parametersGuess"</c>, <c>"isParameterFixed"</c>, <c>"isAtmCalibrated"</c>, <c>"endCriteria"</c>,
/// <c>"maxErrorTolerance"</c>, <c>"optMethod"</c>, <c>"errorAccept"</c>, <c>"useMaxError"</c>, <c>"maxGuesses"</c>,
/// <c>"backwardFlat"</c>, <c>"cutoffStrike"</c> and <c>"singlePassCalibration"</c>. Its at-the-money structure is a
/// matrix spec of <see cref="SwaptionInputs.MatrixOf"/> or a structure spec of
/// <see cref="VolatilityInputs.SwaptionVolatilityOf"/>, its indexes Euribor presets of
/// <see cref="SwaptionInputs.SwapIndexOf"/>, its end criteria <c>null</c> or <c>{"maxIterations",
/// "maxStationaryStateIterations", "rootEpsilon", "functionEpsilon", "gradientNormEpsilon"}</c> and its method
/// <c>null</c> or a method of <see cref="CalibrationInputs.MethodOf"/>.
/// </remarks>
internal static class CubeInputs
{
    /// <summary>The at-the-money structure a spec describes.</summary>
    /// <param name="spec">The spec, a matrix's (with its <c>"form"</c>) or a structure's.</param>
    /// <param name="context">The case's evaluation context, from which a moving structure takes its reference date.</param>
    /// <returns>The structure.</returns>
    public static SwaptionVolatilityStructure AtmOf(JsonElement spec, EvaluationContext context) =>
        spec.TryGetProperty("form", out _) ? SwaptionInputs.MatrixOf(spec, context) : VolatilityInputs.SwaptionVolatilityOf(spec, context);

    /// <summary>The cube a spec describes, built under the case's context.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The cube, with extrapolation enabled when the spec says so.</returns>
    public static SwaptionVolatilityCube CubeOf(JsonElement spec, EvaluationContext context)
    {
        SwaptionVolatilityStructure atm = AtmOf(spec.GetProperty("atm"), context);
        Period[] options = Tenors(spec.GetProperty("optionTenors"));
        Period[] swaps = Tenors(spec.GetProperty("swapTenors"));
        double[] strikeSpreads = VolatilityInputs.Reals(spec.GetProperty("strikeSpreads"));
        double[][] volSpreads = Rows(spec.GetProperty("volSpreads"));
        SwapIndex swapIndex = SwaptionInputs.SwapIndexOf(spec.GetProperty("swapIndex"));
        SwapIndex shortSwapIndex = SwaptionInputs.SwapIndexOf(spec.GetProperty("shortSwapIndex"));
        bool vega = spec.GetProperty("vegaWeightedSmileFit").GetBoolean();
        string type = spec.GetProperty("type").GetString()!;
        SwaptionVolatilityCube cube = type switch
        {
            "InterpolatedSwaptionVolatilityCube" => new InterpolatedSwaptionVolatilityCube(
                atm, options, swaps, strikeSpreads, volSpreads, swapIndex, shortSwapIndex, vega, context),
            "SabrSwaptionVolatilityCube" => new SabrSwaptionVolatilityCube(
                atm,
                options,
                swaps,
                strikeSpreads,
                volSpreads,
                swapIndex,
                shortSwapIndex,
                vega,
                Rows(spec.GetProperty("parametersGuess")),
                [.. spec.GetProperty("isParameterFixed").EnumerateArray().Select(e => e.GetBoolean())],
                spec.GetProperty("isAtmCalibrated").GetBoolean(),
                context,
                EndCriteriaOf(spec.GetProperty("endCriteria")),
                SmileSectionInputs.NullableReal(spec.GetProperty("maxErrorTolerance")),
                spec.GetProperty("optMethod").ValueKind == JsonValueKind.Null ? null : CalibrationInputs.MethodOf(spec.GetProperty("optMethod")),
                SmileSectionInputs.NullableReal(spec.GetProperty("errorAccept")),
                spec.GetProperty("useMaxError").GetBoolean(),
                spec.GetProperty("maxGuesses").GetInt32(),
                spec.GetProperty("backwardFlat").GetBoolean(),
                IndexInputs.Real(spec.GetProperty("cutoffStrike")),
                spec.GetProperty("singlePassCalibration").GetBoolean()),
            "ZabrSwaptionVolatilityCube" => new ZabrSwaptionVolatilityCube(
                atm,
                options,
                swaps,
                strikeSpreads,
                volSpreads,
                swapIndex,
                shortSwapIndex,
                vega,
                Rows(spec.GetProperty("parametersGuess")),
                [.. spec.GetProperty("isParameterFixed").EnumerateArray().Select(e => e.GetBoolean())],
                spec.GetProperty("isAtmCalibrated").GetBoolean(),
                context,
                EndCriteriaOf(spec.GetProperty("endCriteria")),
                SmileSectionInputs.NullableReal(spec.GetProperty("maxErrorTolerance")),
                spec.GetProperty("optMethod").ValueKind == JsonValueKind.Null ? null : CalibrationInputs.MethodOf(spec.GetProperty("optMethod")),
                SmileSectionInputs.NullableReal(spec.GetProperty("errorAccept")),
                spec.GetProperty("useMaxError").GetBoolean(),
                spec.GetProperty("maxGuesses").GetInt32(),
                spec.GetProperty("backwardFlat").GetBoolean(),
                IndexInputs.Real(spec.GetProperty("cutoffStrike")),
                spec.GetProperty("singlePassCalibration").GetBoolean()),
            _ => throw new InvalidOperationException($"unknown cube type '{type}'"),
        };
        return spec.GetProperty("enableExtrapolation").GetBoolean() ? cube.EnableExtrapolation() : cube;
    }

    /// <summary>The periods of a list of tenors, each <c>[length, units]</c>.</summary>
    /// <param name="tenors">The tenors.</param>
    /// <returns>The periods.</returns>
    public static Period[] Tenors(JsonElement tenors) => [.. tenors.EnumerateArray().Select(VolatilityInputs.TenorOf)];

    /// <summary>The rows of a matrix input, an array of arrays of reals.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows.</returns>
    public static double[][] Rows(JsonElement rows) => [.. rows.EnumerateArray().Select(VolatilityInputs.Reals)];

    /// <summary>A matrix's elements, row by row, as the generators flatten them.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The elements.</returns>
    public static double[] Flat(Matrix m)
    {
        var values = new double[m.Rows * m.Columns];
        for (int i = 0; i < m.Rows; i++)
        {
            for (int j = 0; j < m.Columns; j++)
            {
                values[(i * m.Columns) + j] = m[i, j];
            }
        }

        return values;
    }

    /// <summary>
    /// The outputs the stability filter kept: those whose indexes the case's inputs <c>"dropped"</c> doesn't list under
    /// the output's name.
    /// </summary>
    /// <param name="c">The case.</param>
    /// <param name="output">The output's name.</param>
    /// <param name="all">Every element of the output, each computed when compared.</param>
    /// <returns>The kept elements.</returns>
    public static Func<object?>[] Kept(VectorCase c, string output, IEnumerable<Func<object?>> all)
    {
        HashSet<int> dropped = c.Input("dropped").TryGetProperty(output, out JsonElement d)
            ? [.. d.EnumerateArray().Select(e => e.GetInt32())]
            : [];
        return [.. all.Where((_, i) => !dropped.Contains(i))];
    }

    /// <summary>The outputs the stability filter kept, of values computed at once.</summary>
    /// <typeparam name="T">The elements' type.</typeparam>
    /// <param name="c">The case.</param>
    /// <param name="output">The output's name.</param>
    /// <param name="all">Every element of the output.</param>
    /// <returns>The kept elements.</returns>
    public static Func<object?>[] Kept<T>(VectorCase c, string output, IEnumerable<T> all) =>
        Kept(c, output, all.Select(x => (Func<object?>)(() => x)));

    private static EndCriteria? EndCriteriaOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : new EndCriteria(
                spec.GetProperty("maxIterations").GetInt32(),
                spec.GetProperty("maxStationaryStateIterations").GetInt32(),
                IndexInputs.Real(spec.GetProperty("rootEpsilon")),
                IndexInputs.Real(spec.GetProperty("functionEpsilon")),
                IndexInputs.Real(spec.GetProperty("gradientNormEpsilon")));
}
