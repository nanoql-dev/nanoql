// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A trinomial tree's node arithmetic, kept as QuantLib defines it: a node's underlying is the column's lowest offset
/// plus the index, times the column's spacing, and its descendant along a branch is its middle descendant's offset
/// plus the branch (<c>trinomialtree.hpp:88-99, 109-112</c>), for any node and branch, past the column too.
/// </summary>
public class TreeNodeArithmeticTests
{
    [Fact]
    public void ANodesUnderlyingAndDescendantFollowQuantLibsArithmeticPastTheColumn()
    {
        var tree = new TrinomialTree(new Vasicek().Dynamics().Process, new TimeGrid(1.0, 4));
        double x0 = tree.Underlying(0, 0);

        // at the first column, every index is the process's start
        Assert.Equal(x0, tree.Underlying(0, 5));

        // a column's values are equally spaced, past its last node too
        Assert.Equal(tree.Underlying(2, 0) + (7 * tree.Dx(2)), tree.Underlying(2, 7), 1e-15);
        Assert.Equal(5, tree.Size(2));

        // a branch past the third is the middle descendant's offset plus the branch
        Assert.Equal(tree.Descendant(1, 2, 0) + 4, tree.Descendant(1, 2, 4));
    }
}
