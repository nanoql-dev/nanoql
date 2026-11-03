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
/// The time-dependent Tian binomial tree: its moves and probabilities matching the step's first three moments at the
/// step's time: QuantLib's <c>ExtendedTian</c>.
/// </summary>
/// <remarks>
/// Immutable; exact on constant parameters (<see cref="ExtendedBinomialTree{T}"/>). The constructor checks the up
/// probability at time 0 only, as QuantLib's; its <c>up_</c>, <c>down_</c>, <c>pu_</c> and <c>pd_</c> members, which
/// nothing reads after that check, aren't ported.
/// </remarks>
[Experimental("NQL0001")]
public sealed class ExtendedTian : ExtendedBinomialTree<ExtendedTian>, IBinomialTree<ExtendedTian>
{
    /// <summary>Initializes a new instance of the <see cref="ExtendedTian"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <param name="strike">The strike, which this tree doesn't read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="NanoQLException">The up probability at time 0 is outside [0, 1].</exception>
    public ExtendedTian(StochasticProcess1D process, double end, int steps, double strike)
        : base(process, end, steps)
    {
        double q = Math.Exp(process.Variance(0.0, X0, Dt));
        double r = Math.Exp(DriftStep(0.0)) * Math.Sqrt(q);
        double up = 0.5 * r * q * (q + 1 + Math.Sqrt((q * q) + (2 * q) - 3));
        double down = 0.5 * r * q * (q + 1 - Math.Sqrt((q * q) + (2 * q) - 3));
        double pu = (r - down) / (up - down);
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
    public static ExtendedTian Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);

    /// <inheritdoc/>
    public override double Underlying(int i, int index)
    {
        double stepTime = i * Dt;
        double q = Math.Exp(TreeProcess.Variance(stepTime, X0, Dt));
        double r = Math.Exp(DriftStep(stepTime)) * Math.Sqrt(q);
        double up = 0.5 * r * q * (q + 1 + Math.Sqrt((q * q) + (2 * q) - 3));
        double down = 0.5 * r * q * (q + 1 - Math.Sqrt((q * q) + (2 * q) - 3));
        return X0 * Math.Pow(down, (double)((long)i - index)) * Math.Pow(up, index);
    }

    /// <inheritdoc/>
    public override double Probability(int i, int index, int branch)
    {
        double stepTime = i * Dt;
        double q = Math.Exp(TreeProcess.Variance(stepTime, X0, Dt));
        double r = Math.Exp(DriftStep(stepTime)) * Math.Sqrt(q);
        double up = 0.5 * r * q * (q + 1 + Math.Sqrt((q * q) + (2 * q) - 3));
        double down = 0.5 * r * q * (q + 1 - Math.Sqrt((q * q) + (2 * q) - 3));
        double pu = (r - down) / (up - down);
        double pd = 1.0 - pu;
        return branch == 1 ? pu : pd;
    }
}
