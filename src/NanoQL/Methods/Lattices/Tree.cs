// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/tree.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>A tree approximating a single-factor diffusion.</summary>
/// <remarks>
/// QuantLib's curiously recurring template: a derived tree implements <see cref="Underlying"/>, <see cref="Size"/>,
/// <see cref="Descendant"/> and <see cref="Probability"/>, and <see cref="Branches"/>, QuantLib's
/// <c>enum { branches = N }</c> (2 for a binomial tree, 3 for a trinomial one). Immutable once built. An extension point,
/// as in QuantLib: the trinomial tree derives from it, as later ports' binomial trees will.
/// </remarks>
public abstract class Tree
{
    /// <summary>Initializes a new instance of the <see cref="Tree"/> class.</summary>
    /// <param name="columns">The number of columns, one per time of the tree's grid.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="columns"/> is negative.</exception>
    protected Tree(int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columns);
        Columns = columns;
    }

    /// <summary>Gets the number of columns, one per time of the tree's grid.</summary>
    public int Columns { get; }

    /// <summary>Gets the number of branches from each node.</summary>
    public abstract int Branches { get; }

    /// <summary>The underlying variable's value at a node.</summary>
    /// <param name="i">The column.</param>
    /// <param name="index">The node's index in the column.</param>
    /// <returns>The value.</returns>
    public abstract double Underlying(int i, int index);

    /// <summary>The number of nodes in a column.</summary>
    /// <param name="i">The column.</param>
    /// <returns>The number of nodes.</returns>
    public abstract int Size(int i);

    /// <summary>The index, in the next column, of a node's descendant along a branch.</summary>
    /// <param name="i">The column.</param>
    /// <param name="index">The node's index in the column.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The descendant's index.</returns>
    public abstract int Descendant(int i, int index, int branch);

    /// <summary>The probability of a node's branch.</summary>
    /// <param name="i">The column.</param>
    /// <param name="index">The node's index in the column.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The probability.</returns>
    public abstract double Probability(int i, int index, int branch);
}
