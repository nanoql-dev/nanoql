// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/binomialtree.hpp ql/methods/lattices/binomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The Jarrow-Rudd binomial tree: equal probabilities, the up move one standard deviation of a step.</summary>
/// <remarks>Immutable.</remarks>
public sealed class JarrowRudd : EqualProbabilitiesBinomialTree<JarrowRudd>, IBinomialTree<JarrowRudd>
{
    /// <summary>Initializes a new instance of the <see cref="JarrowRudd"/> class.</summary>
    /// <param name="process">The process, read at time 0.</param>
    /// <param name="end">The tree's last time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <param name="strike">Not read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    public JarrowRudd(StochasticProcess1D process, double end, int steps, double strike)
        : base(process, end, steps)
    {
        // drift removed
        Up = process.StdDeviation(0.0, X0, Dt);
    }

    /// <inheritdoc/>
    public static JarrowRudd Create(StochasticProcess1D process, double end, int steps, double strike) =>
        new(process, end, steps, strike);
}
