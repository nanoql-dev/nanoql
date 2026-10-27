// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/correlations/expcorrelations.hpp ql/models/marketmodels/correlations/expcorrelations.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Utilities;

namespace NanoQL.Models;

/// <summary>
/// The exponential forward correlation, piecewise constant: at γ = 1 time-homogeneous on the rate times, otherwise each
/// period's matrix the exponential correlation at the period's midpoint.
/// </summary>
/// <remarks>
/// <para>Immutable (the S-mm plan's MR1 (i)): a matrix read returns a copy.</para>
/// <para>
/// Three departures from QuantLib (<c>Divergences/ExponentialCorrelationTests.cs</c>): one rate is accepted, which
/// QuantLib refuses as "Rate times must contain at least two values" when given two (MR3 (xiv)); at γ ≠ 1, correlation
/// times that skip a rate time but the last are refused, as QuantLib's own FIXME asks, since a period's midpoint then
/// falls after a rate that is still alive in the period has fixed and the models give that rate no variance there
/// (MR4 (b)); and the refusal of a last correlation time past the last rate time names that time, where QuantLib's text
/// calls it the next-to-last (MR3 (xv)).
/// </para>
/// </remarks>
public sealed class ExponentialForwardCorrelation : PiecewiseConstantCorrelation
{
    private readonly Matrix[] correlations;

    /// <summary>Initializes a new instance of the <see cref="ExponentialForwardCorrelation"/> class.</summary>
    /// <param name="rateTimes">The rate times: at least two, strictly increasing and positive.</param>
    /// <param name="longTermCorr">The long-term correlation, in [0, 1].</param>
    /// <param name="beta">The decay, not negative.</param>
    /// <param name="gamma">The exponent of the time to go, in [0, 1].</param>
    /// <param name="times">
    /// The correlation times, strictly increasing and positive; empty for every rate time but the last, which they must
    /// be at γ = 1 and must include otherwise.
    /// </param>
    /// <exception cref="ArgumentException">An input is outside its range, or the times don't meet γ's requirement.</exception>
    public ExponentialForwardCorrelation(
        ReadOnlySpan<double> rateTimes,
        double longTermCorr = 0.5,
        double beta = 0.2,
        double gamma = 1.0,
        ReadOnlySpan<double> times = default)
    {
        int numberOfRates = rateTimes.IsEmpty ? 0 : rateTimes.Length - 1;

        // QuantLib requires two rates or more under this text; one is accepted (MR3 (xiv)).
        if (!(numberOfRates >= 1))
        {
            throw new ArgumentException("Rate times must contain at least two values", nameof(rateTimes));
        }

        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);

        // corrTimes must include all rateTimes but the last
        ReadOnlySpan<double> corrTimes = times;
        if (corrTimes.IsEmpty)
        {
            corrTimes = rateTimes[..^1];
        }
        else
        {
            UtilitiesFunctions.CheckIncreasingTimes(corrTimes);
        }

        ReadOnlySpan<double> temp = rateTimes[..^1];
        if (ComparisonFunctions.Close(gamma, 1.0))
        {
            if (!corrTimes.SequenceEqual(temp))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"corr times {DataFormattersFunctions.Sequence(corrTimes.ToArray())} must be equal to (all) rate times (but the last) {DataFormattersFunctions.Sequence(temp.ToArray())}"),
                    nameof(times));
            }

            Matrix c = ExpCorrelationsFunctions.ExponentialCorrelations(rateTimes, longTermCorr, beta, 1.0, 0.0);
            correlations = [.. TimeHomogeneousForwardCorrelation.EvolvedMatrices(c)];
        }
        else
        {
            // QuantLib's FIXME: "should check here that all rateTimes but the last are included in rateTimes" (MR4 (b)).
            for (int i = 0; i < temp.Length; ++i)
            {
                if (!corrTimes.Contains(temp[i]))
                {
                    throw new ArgumentException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"corr times {DataFormattersFunctions.Sequence(corrTimes.ToArray())} skip the {DataFormattersFunctions.Ordinal(i + 1)} rate time ({temp[i]})"),
                        nameof(times));
                }
            }

            if (!(corrTimes[^1] <= rateTimes[numberOfRates]))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"last corr time {corrTimes[^1]} is after last rate time {rateTimes[numberOfRates]}"),
                    nameof(times));
            }

            correlations = new Matrix[corrTimes.Length];
            double time = corrTimes[0] / 2.0;
            correlations[0] = ExpCorrelationsFunctions.ExponentialCorrelations(rateTimes, longTermCorr, beta, gamma, time);
            for (int k = 1; k < corrTimes.Length; ++k)
            {
                time = (corrTimes[k] + corrTimes[k - 1]) / 2.0;
                correlations[k] = ExpCorrelationsFunctions.ExponentialCorrelations(rateTimes, longTermCorr, beta, gamma, time);
            }
        }

        NumberOfRates = numberOfRates;
        RateTimes = [.. rateTimes];
        Times = [.. corrTimes];
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> Times { get; }

    /// <inheritdoc/>
    public override ImmutableArray<double> RateTimes { get; }

    /// <inheritdoc/>
    public override int NumberOfRates { get; }

    /// <inheritdoc/>
    public override ImmutableArray<Matrix> Correlations() => [.. correlations.Select(m => m.Clone())];

    /// <inheritdoc/>
    public override Matrix Correlation(int i)
    {
        CheckIndex(i, correlations.Length);
        return correlations[i].Clone();
    }
}
