// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/lossdistribution.hpp ql/experimental/credit/lossdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The probability of exactly n default events, as a function of the names' probabilities.</summary>
/// <param name="n">The number of defaults.</param>
[Experimental("NQL0001")]
public sealed class ProbabilityOfNEvents(int n)
{
    /// <summary>The probability: <see cref="LossDist.ProbabilityOfNEvents(int, IReadOnlyList{double})"/>.</summary>
    /// <param name="p">The names' probabilities.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The number of defaults lies outside [0, the number of
    /// names].</exception>
    public double Evaluate(IReadOnlyList<double> p) => LossDist.ProbabilityOfNEvents(n, p);
}
