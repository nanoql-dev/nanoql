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
/// The time-dependent Cox-Ross-Rubinstein binomial tree: equal jumps of one standard deviation of a step at the step's
/// time, the probabilities carrying the drift: QuantLib's <c>ExtendedCoxRossRubinstein</c>.
/// </summary>
/// <remarks>
/// Immutable; exact on constant parameters (<see cref="ExtendedBinomialTree{T}"/>, XRR6). The constructor checks the up
/// probability at time 0 only, as QuantLib's.
/// </remarks>
[Experimental("NQL0001")]
public sealed class ExtendedCoxRossRubinstein
    : ExtendedEqualJumpsBinomialTree<ExtendedCoxRossRubinstein>, IBinomialTree<ExtendedCoxRossRubinstein>
{
    /// <summary>Initializes a new instance of the <see cref="ExtendedCoxRossRubinstein"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <param name="strike">The strike, which this tree doesn't read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="NanoQLException">The up probability at time 0 is outside [0, 1].</exception>
    public ExtendedCoxRossRubinstein(StochasticProcess1D process, double end, int steps, double strike)
        : base(process, end, steps)
    {
        double dx = process.StdDeviation(0.0, X0, Dt);
        double pu = 0.5 + (0.5 * DriftStep(0.0) / dx);
        if (!(pu <= 1.0))
        {
            throw new NanoQLException("negative probability");
        }

        if (!(pu >= 0.0))
        {
            throw new NanoQLException("negative probability");
        }
    }

    /// <inheritdoc/>
    public static ExtendedCoxRossRubinstein Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);

    /// <inheritdoc/>
    protected override double DxStep(double stepTime) => TreeProcess.StdDeviation(stepTime, X0, Dt);

    /// <inheritdoc/>
    protected override double ProbUp(double stepTime) => 0.5 + (0.5 * DriftStep(stepTime) / DxStep(stepTime));
}
