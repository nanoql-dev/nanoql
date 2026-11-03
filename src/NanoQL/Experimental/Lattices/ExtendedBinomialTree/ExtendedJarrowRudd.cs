// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 John Maiden
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/lattices/extendedbinomialtree.hpp ql/experimental/lattices/extendedbinomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// The time-dependent Jarrow-Rudd (multiplicative) equal-probabilities binomial tree: its up move one standard deviation
/// of a step at the step's time: QuantLib's <c>ExtendedJarrowRudd</c>.
/// </summary>
/// <remarks>Immutable; exact on constant parameters (<see cref="ExtendedBinomialTree{T}"/>).</remarks>
[Experimental("NQL0001")]
public sealed class ExtendedJarrowRudd
    : ExtendedEqualProbabilitiesBinomialTree<ExtendedJarrowRudd>, IBinomialTree<ExtendedJarrowRudd>
{
    /// <summary>Initializes a new instance of the <see cref="ExtendedJarrowRudd"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <param name="strike">The strike, which this tree doesn't read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    public ExtendedJarrowRudd(StochasticProcess1D process, double end, int steps, double strike)
        : base(process, end, steps)
    {
    }

    /// <inheritdoc/>
    public static ExtendedJarrowRudd Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);

    /// <inheritdoc/>
    protected override double UpStep(double stepTime) => TreeProcess.StdDeviation(stepTime, X0, Dt);
}
