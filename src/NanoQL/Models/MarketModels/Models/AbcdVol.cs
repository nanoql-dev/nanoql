// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/abcdvol.hpp ql/models/marketmodels/models/abcdvol.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Utilities;

namespace NanoQL.Models;

/// <summary>
/// The abcd-interpolated volatility market model: each rate's instantaneous volatility its own multiple k of a common
/// abcd function of the time to its fixing, correlated by a piecewise-constant correlation, each step's pseudo-root the
/// rank-reduced square root of its covariance.
/// </summary>
/// <remarks>
/// <para>Immutable: its pseudo-roots are computed at construction, and a read returns a copy.</para>
/// <para>
/// As <see cref="FlatVol"/>, two inputs whose reads QuantLib leaves undefined are refused
/// (<c>Divergences/MarketModelUndefinedReadsTests.cs</c>): correlation times ending before the last evolution time,
/// which QuantLib reads past, and a correlation with fewer rates than the model, whose matrices QuantLib reads past.
/// QuantLib's check of the rates against the factors times the steps is commented out, and so it is here.
/// </para>
/// </remarks>
public sealed class AbcdVol : MarketModel
{
    private readonly int numberOfFactors;
    private readonly int numberOfRates;
    private readonly int numberOfSteps;
    private readonly Matrix[] pseudoRoots;

    /// <summary>Initializes a new instance of the <see cref="AbcdVol"/> class.</summary>
    /// <param name="a">The abcd function's a.</param>
    /// <param name="b">The abcd function's b.</param>
    /// <param name="c">The abcd function's c.</param>
    /// <param name="d">The abcd function's d.</param>
    /// <param name="ks">Each rate's multiple of the abcd volatility, one per rate.</param>
    /// <param name="corr">The rates' correlation, its times ending at or after the last evolution time.</param>
    /// <param name="evolution">The evolution.</param>
    /// <param name="numberOfFactors">The number of factors, from 1 to the number of rates.</param>
    /// <param name="initialRates">The rates at time zero, one per rate time but the last.</param>
    /// <param name="displacements">The rates' displacements, one per rate.</param>
    /// <exception cref="ArgumentException">
    /// The sizes don't match, the abcd parameters are outside their domain, or the correlation doesn't cover the
    /// evolution.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfFactors"/> is negative.</exception>
    public AbcdVol(
        double a,
        double b,
        double c,
        double d,
        ReadOnlySpan<double> ks,
        PiecewiseConstantCorrelation corr,
        EvolutionDescription evolution,
        int numberOfFactors,
        ReadOnlySpan<double> initialRates,
        ReadOnlySpan<double> displacements)
    {
        ArgumentNullException.ThrowIfNull(corr);
        ArgumentNullException.ThrowIfNull(evolution);
        ArgumentOutOfRangeException.ThrowIfNegative(numberOfFactors);
        this.numberOfFactors = numberOfFactors;
        numberOfRates = initialRates.Length;
        numberOfSteps = evolution.EvolutionTimes.Length;

        ImmutableArray<double> rateTimes = evolution.RateTimes;
        if (!(numberOfRates == rateTimes.Length - 1))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"mismatch between number of rates ({numberOfRates}) and rate times"),
                nameof(initialRates));
        }

        if (!(numberOfRates == displacements.Length))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mismatch between number of rates ({numberOfRates}) and displacements ({displacements.Length})"),
                nameof(displacements));
        }

        if (!(numberOfRates == ks.Length))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"mismatch between number of rates ({numberOfRates}) and ks ({ks.Length})"),
                nameof(ks));
        }

        if (!(numberOfFactors <= numberOfRates))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"number of factors ({numberOfFactors}) cannot be greater than numberOfRates ({numberOfRates})"),
                nameof(numberOfFactors));
        }

        if (!(numberOfFactors > 0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"number of factors ({numberOfFactors}) must be greater than zero"),
                nameof(numberOfFactors));
        }

        var abcd = new AbcdFunction(a, b, c, d);
        ImmutableArray<double> corrTimes = corr.Times;
        ImmutableArray<double> evolTimes = evolution.EvolutionTimes;

        // QuantLib reads past the correlation times where they end before the last evolution time: refused.
        if (!(!corrTimes.IsEmpty && corrTimes[^1] >= evolTimes[^1]))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the correlation times end ({(corrTimes.IsEmpty ? "none given" : corrTimes[^1].ToString(CultureInfo.InvariantCulture))}) before the last evolution time ({evolTimes[^1]})"),
                nameof(corr));
        }

        InitialRates = [.. initialRates];
        Displacements = [.. displacements];
        Evolution = evolution;
        pseudoRoots = new Matrix[numberOfSteps];

        double effStopTime = 0.0;
        var covariance = new Matrix(numberOfRates, numberOfRates);
        for (int k = 0, kk = 0; k < numberOfSteps; ++k)
        {
            // one covariance per evolution step
            covariance.AsSpan().Clear();

            // there might be more than one correlation matrix in a single evolution step,
            for (; corrTimes[kk] < evolTimes[k]; ++kk)
            {
                double effStartTime = effStopTime;
                effStopTime = corrTimes[kk];
                Matrix corrMatrix = CorrelationOf(corr, kk);
                for (int i = 0; i < numberOfRates; ++i)
                {
                    for (int j = i; j < numberOfRates; ++j)
                    {
                        double cov = ks[i] * ks[j] * abcd.Covariance(effStartTime, effStopTime, rateTimes[i], rateTimes[j]);
                        covariance[i, j] += cov * corrMatrix[i, j];
                    }
                }
            }

            // last part in the evolution step
            {
                double effStartTime = effStopTime;
                effStopTime = evolTimes[k];
                Matrix corrMatrix = CorrelationOf(corr, kk);
                for (int i = 0; i < numberOfRates; ++i)
                {
                    for (int j = i; j < numberOfRates; ++j)
                    {
                        double cov = ks[i] * ks[j] * abcd.Covariance(effStartTime, effStopTime, rateTimes[i], rateTimes[j]);
                        covariance[i, j] += cov * corrMatrix[i, j];
                    }
                }
            }

            // no more use for the kk-th correlation matrix
            while (kk < corrTimes.Length && corrTimes[kk] <= evolTimes[k])
            {
                ++kk;
            }

            // make it symmetric
            for (int i = 0; i < numberOfRates; ++i)
            {
                for (int j = i + 1; j < numberOfRates; ++j)
                {
                    covariance[j, i] = covariance[i, j];
                }
            }

            pseudoRoots[k] = PseudoSqrtFunctions.RankReducedSqrt(covariance, numberOfFactors, 1.0, SalvagingAlgorithmType.None);

            if (!(pseudoRoots[k].Rows == numberOfRates))
            {
                throw new NanoQLException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"step {k} abcd vol wrong number of rows: {pseudoRoots[k].Rows} instead of {numberOfRates}"));
            }

            if (!(pseudoRoots[k].Columns == numberOfFactors))
            {
                throw new NanoQLException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"step {k} abcd vol wrong number of columns: {pseudoRoots[k].Columns} instead of {numberOfFactors}"));
            }
        }
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> InitialRates { get; }

    /// <inheritdoc/>
    public override ImmutableArray<double> Displacements { get; }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution { get; }

    /// <inheritdoc/>
    public override int NumberOfRates => numberOfRates;

    /// <inheritdoc/>
    public override int NumberOfFactors => numberOfFactors;

    /// <inheritdoc/>
    public override int NumberOfSteps => numberOfSteps;

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public override Matrix PseudoRoot(int i)
    {
        if (!(i >= 0 && i < numberOfSteps))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                string.Create(
                    CultureInfo.InvariantCulture, $"the index {i} is invalid: it must be less than number of steps ({numberOfSteps})"));
        }

        return pseudoRoots[i].Clone();
    }

    // A correlation matrix the covariance reads: QuantLib reads its first rates' rows and columns, past it where the
    // correlation has fewer rates than the model, refused.
    private Matrix CorrelationOf(PiecewiseConstantCorrelation corr, int kk)
    {
        Matrix m = corr.Correlation(kk);
        if (!(m.Rows >= numberOfRates && m.Columns >= numberOfRates))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the {DataFormattersFunctions.Ordinal(kk + 1)} correlation matrix ({m.Rows}x{m.Columns}) is smaller than the rates ({numberOfRates})"),
                nameof(corr));
        }

        return m;
    }
}
