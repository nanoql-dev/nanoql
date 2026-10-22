// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/generalstatistics.hpp ql/math/statistics/generalstatistics.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A statistics accumulator that keeps its samples, so that it gives the empirical distribution's percentiles and
/// conditional expectations: what QuantLib's empirical risk measures, <c>GenericRiskStatistics&lt;S&gt;</c>, read of
/// <see cref="GeneralStatistics"/>.
/// </summary>
public interface IEmpiricalStatistics : IStatistics
{
    /// <summary>
    /// Gets the collected samples, each with its weight: a view of the accumulator's own storage, valid until it next
    /// changes, sorted only after a call that sorts it (<see cref="Percentile"/>, <see cref="TopPercentile"/>).
    /// </summary>
    ReadOnlySpan<(double Value, double Weight)> Data { get; }

    /// <summary>
    /// The expectation of a function over the samples in a range, E[f | R] = Σ_{xᵢ ∈ R} f(xᵢ)wᵢ / Σ_{xᵢ ∈ R} wᵢ, with the
    /// number of samples in the range.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="inRange">Whether a sample belongs to the range.</param>
    /// <returns>The expectation, <see langword="null"/> where no sample is in the range, and the number of samples.</returns>
    (double? Value, int Samples) ExpectationValue(Func<double, double> f, Func<double, bool> inRange);

    /// <summary>The expectation of a function over every sample.</summary>
    /// <param name="f">The function.</param>
    /// <returns>The expectation, <see langword="null"/> where there's no sample, and the number of samples.</returns>
    (double? Value, int Samples) ExpectationValue(Func<double, double> f);

    /// <summary>
    /// The y-th percentile: the value x̄ such that y = Σ_{xᵢ &lt; x̄} wᵢ / Σᵢ wᵢ.
    /// </summary>
    /// <param name="y">The percentile, in (0, 1].</param>
    /// <returns>The percentile.</returns>
    double Percentile(double y);

    /// <summary>
    /// The y-th top percentile: the value x̄ such that y = Σ_{xᵢ &gt; x̄} wᵢ / Σᵢ wᵢ.
    /// </summary>
    /// <param name="y">The percentile, in (0, 1].</param>
    /// <returns>The top percentile.</returns>
    double TopPercentile(double y);
}
