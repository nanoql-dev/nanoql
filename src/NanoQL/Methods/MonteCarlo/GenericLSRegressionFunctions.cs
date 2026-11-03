// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/genericlsregression.hpp ql/methods/montecarlo/genericlsregression.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>The Longstaff-Schwartz regression on collected node data: QuantLib's free function.</summary>
public static class GenericLSRegressionFunctions
{
    /// <summary>
    /// Regresses each exercise's continuation value on its basis functions, from the last exercise back, and adds each
    /// valid path's value at an exercise, the exercise value where it beats the estimated continuation and the cash
    /// flows after it otherwise, into the path's cash flows at the exercise before; the estimate is the paths' mean at
    /// the start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The data are QuantLib's: n exercises give n + 1 rows, row 0 each path's cash flows before the first exercise and
    /// row i + 1 its data at exercise i. The cash flows of every row before the last are changed in place, as QuantLib
    /// changes them.
    /// </para>
    /// <para>
    /// The number of basis functions is the first valid path's (a documented divergence): QuantLib takes path 0's,
    /// valid or not, and a path ended before an exercise has none, so the valid paths' values are copied past a
    /// one-entry buffer (<c>genericlsregression.cpp:39</c>). An exercise no path reaches is refused, with the
    /// parametric optimisation's text.
    /// </para>
    /// </remarks>
    /// <param name="simulationData">The node data, an exercise a row and a path a column, changed in place.</param>
    /// <returns>The biased estimate and each exercise's coefficients.</returns>
    /// <exception cref="ArgumentException">
    /// There's no row, a row is shorter than the one after it, or a valid path holds another number of values than the
    /// exercise's first valid path, inputs QuantLib writes past or reads stale.
    /// </exception>
    /// <exception cref="NanoQLException">No path is valid at an exercise.</exception>
    public static GenericLSRegressionResults GenericLongstaffSchwartzRegression(NodeData[][] simulationData)
    {
        ArgumentNullException.ThrowIfNull(simulationData);
        int steps = simulationData.Length;
        if (steps == 0)
        {
            throw new ArgumentException("the simulation data must hold the cash flows before the first exercise", nameof(simulationData));
        }

        for (int i = 0; i < steps; ++i)
        {
            NodeData[] row = simulationData[i];
            ArgumentNullException.ThrowIfNull(row, nameof(simulationData));
            if (i > 0 && simulationData[i - 1].Length < row.Length)
            {
                throw new ArgumentException(
                    $"exercise {i - 1}'s row holds {simulationData[i - 1].Length} paths, fewer than the next one's {row.Length}",
                    nameof(simulationData));
            }

            foreach (NodeData d in row)
            {
                ArgumentNullException.ThrowIfNull(d, nameof(simulationData));
            }
        }

        var basisCoefficients = new ImmutableArray<double>[steps - 1];

        for (int i = steps - 1; i != 0; --i)
        {
            NodeData[] exerciseData = simulationData[i];

            // 1) find the covariance matrix of basis function values and
            //    deflated cash-flows
            NodeData first = Array.Find(exerciseData, d => d.IsValid)
                ?? throw new NanoQLException("no valid paths");
            int n = first.Values.Length;
            double[] temp = new double[n + 1];
            var stats = new SequenceStatistics(n + 1);

            int j;
            for (j = 0; j < exerciseData.Length; ++j)
            {
                if (exerciseData[j].IsValid)
                {
                    // QuantLib copies each path's values over the last's: more write past, fewer leave the last's tail
                    if (exerciseData[j].Values.Length != n)
                    {
                        throw new ArgumentException(
                            $"path {j} holds {exerciseData[j].Values.Length} values, not the first valid path's {n}",
                            nameof(simulationData));
                    }

                    exerciseData[j].Values.CopyTo(temp, 0);
                    temp[n] = exerciseData[j].CumulatedCashFlows - exerciseData[j].ControlValue;

                    stats.Add(temp);
                }
            }

            ImmutableArray<double> means = stats.Mean();
            Matrix covariance = stats.Covariance();

            var c = new Matrix(n, n);
            var target = new Vector(n);
            for (int k = 0; k < n; ++k)
            {
                target[k] = covariance[k, n] + (means[k] * means[n]);
                for (int l = 0; l <= k; ++l)
                {
                    c[k, l] = c[l, k] = covariance[k, l] + (means[k] * means[l]);
                }
            }

            // 2) solve for least squares regression
            Vector alphas = new SVD(c).SolveFor(target);
            basisCoefficients[i - 1] = [.. alphas.AsSpan()];

            // 3) use exercise strategy to divide paths into exercise and
            //    non-exercise domains
            for (j = 0; j < exerciseData.Length; ++j)
            {
                if (exerciseData[j].IsValid)
                {
                    double exerciseValue = exerciseData[j].ExerciseValue;
                    double continuationValue = exerciseData[j].CumulatedCashFlows;
                    double estimatedContinuationValue = exerciseData[j].ControlValue;
                    double[] values = exerciseData[j].Values;
                    for (int k = 0; k < values.Length; ++k)
                    {
                        estimatedContinuationValue += values[k] * alphas[k];
                    }

                    // for exercise paths, add deflated rebate to
                    // deflated cash-flows at previous time frame;
                    // for non-exercise paths, add deflated cash-flows to
                    // deflated cash-flows at previous time frame
                    double value = estimatedContinuationValue <= exerciseValue ? exerciseValue : continuationValue;

                    simulationData[i - 1][j].CumulatedCashFlows += value;
                }
            }
        }

        // the value of the product can now be estimated by averaging
        // over all paths
        var estimate = new GeneralStatistics();
        foreach (NodeData d in simulationData[0])
        {
            estimate.Add(d.CumulatedCashFlows);
        }

        return new GenericLSRegressionResults(estimate.Mean(), [.. basisCoefficients]);
    }
}
