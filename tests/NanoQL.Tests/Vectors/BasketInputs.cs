// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The basket vector files' specs, as tools/vectors/gen/instruments/basketoption.py writes them: basket payoffs,
/// basket options, lists of Black-Scholes processes and matrices.
/// </summary>
internal static class BasketInputs
{
    /// <summary>The basket payoff a spec describes: a minimum, maximum, average or spread of a base payoff.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The payoff.</returns>
    public static BasketPayoff BasketPayoffOf(JsonElement spec)
    {
        StrikedTypePayoff basePayoff = PayoffOf(spec.GetProperty("base"));
        return spec.GetProperty("type").GetString() switch
        {
            "Min" => new MinBasketPayoff(basePayoff),
            "Max" => new MaxBasketPayoff(basePayoff),
            "Spread" => new SpreadBasketPayoff(basePayoff),
            "Average" => spec.TryGetProperty("weights", out JsonElement weights)
                ? new AverageBasketPayoff(basePayoff, Doubles(weights))
                : new AverageBasketPayoff(basePayoff, spec.GetProperty("n").GetInt32()),
            string type => throw new InvalidOperationException($"unknown basket payoff '{type}'"),
            null => throw new InvalidOperationException("a basket payoff spec without a type"),
        };
    }

    /// <summary>The basket option a spec describes.</summary>
    /// <param name="spec">The spec: its payoff and exercise.</param>
    /// <param name="context">The case's evaluation context, the American exercise's earliest date by default.</param>
    /// <returns>The option.</returns>
    public static BasketOption BasketOptionOf(JsonElement spec, EvaluationContext context) =>
        new(BasketPayoffOf(spec.GetProperty("payoff")), ExerciseOf(spec.GetProperty("exercise"), context));

    /// <summary>The Black-Scholes processes a list of specs describes.</summary>
    /// <param name="spec">The list of process specs.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The processes.</returns>
    public static ImmutableArray<GeneralizedBlackScholesProcess> ProcessesOf(JsonElement spec, EvaluationContext context) =>
        [.. spec.EnumerateArray().Select(p => ProcessOf(p, context))];

    /// <summary>The matrix a list of rows describes.</summary>
    /// <param name="spec">The rows.</param>
    /// <returns>A new matrix.</returns>
    public static Matrix MatrixOf(JsonElement spec)
    {
        double[][] rows = [.. spec.EnumerateArray().Select(Doubles)];
        var m = new Matrix(rows.Length, rows.Length == 0 ? 0 : rows[0].Length);
        for (int i = 0; i < rows.Length; ++i)
        {
            for (int j = 0; j < rows[i].Length; ++j)
            {
                m[i, j] = rows[i][j];
            }
        }

        return m;
    }

    /// <summary>The flat elements of a matrix, row by row, as the generators write them.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The elements.</returns>
    public static double[] Flat(Matrix m)
    {
        double[] flat = new double[m.Rows * m.Columns];
        for (int i = 0; i < m.Rows; ++i)
        {
            for (int j = 0; j < m.Columns; ++j)
            {
                flat[(i * m.Columns) + j] = m[i, j];
            }
        }

        return flat;
    }
}
