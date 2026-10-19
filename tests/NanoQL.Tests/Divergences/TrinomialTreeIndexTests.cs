// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The trees' and lattices' indices out of range. QuantLib's <c>TrinomialTree</c> reads <c>branchings_[i-1]</c>,
/// <c>k_[index]</c>, <c>probs_[branch][index]</c> and <c>dx_[i]</c> unchecked (<c>trinomialtree.hpp:84-103</c>), and
/// <c>TreeLattice::statePrices(i)</c> extends its cache to any <c>i</c> through <c>size(i+1)</c>
/// (<c>lattice.hpp:97-118</c>), reading past the trees beyond the grid's last time. nanoql refuses each index that
/// reads past an array with <see cref="ArgumentOutOfRangeException"/> naming it, and a negative one as an unsigned
/// count; a node's underlying and descendant, which QuantLib computes from the column's offsets for any node and
/// branch without reading at them, it computes as QuantLib does.
/// </summary>
public class TrinomialTreeIndexTests
{
    [Fact]
    public void ATreesIndicesOutOfRangeAreRefused()
    {
        var grid = new TimeGrid(1.0, 4);
        var tree = new TrinomialTree(new Vasicek().Dynamics().Process, grid);

        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Size(5)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Size(-1)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Dx(5)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Underlying(5, 0)).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Underlying(1, -1)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Descendant(4, 0, 0)).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Descendant(1, 3, 0)).ParamName);
        Assert.Equal("branch", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Descendant(1, 0, -1)).ParamName);
        Assert.Equal("branch", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Probability(0, 0, 3)).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Probability(1, 3, 0)).ParamName);

        // The last column has nodes and values, but no descendants.
        Assert.Equal(9, tree.Size(4));
        Assert.Equal(tree.Underlying(4, 0) + (8 * tree.Dx(4)), tree.Underlying(4, 8), 1e-15);
    }

    [Fact]
    public void StatePricesBeyondTheGridAreRefused()
    {
        var grid = new TimeGrid(1.0, 4);
        var lattice = (OneFactorModelShortRateTree)new Vasicek().Tree(grid);

        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => lattice.StatePrices(5)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => lattice.StatePrices(-1)).ParamName);
        Assert.Equal(lattice.Size(4), lattice.StatePrices(4).Length);
    }
}
