// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A built lattice is immutable (playbook §2's lattice row): QuantLib's <c>ShortRateTree::setSpread</c>, which changes the
/// tree, returns a copy whose short rates carry the spread, sharing the tree and the state prices the receiver stored
/// when it was built; the receiver keeps no spread.
/// </summary>
public class ShortRateTreeSpreadTests
{
    [Fact]
    public void SetSpreadReturnsACopyAndLeavesTheReceiver()
    {
        var curve = new FlatForward(new Date(15, Month.January, 2026), 0.04, new Actual365Fixed());
        var grid = new TimeGrid(2.0, 8);
        var tree = (OneFactorModelShortRateTree)new HullWhite(curve).Tree(grid);
        double discount = tree.Discount(3, 2);

        OneFactorModelShortRateTree spread = tree.SetSpread(0.01);

        Assert.NotSame(tree, spread);
        Assert.Equal(discount, tree.Discount(3, 2));
        double r = -Math.Log(discount) / grid.Dt(3);
        Assert.Equal(Math.Exp(-(r + 0.01) * grid.Dt(3)), spread.Discount(3, 2), 1e-14);

        // the stored rows are the receiver's
        for (int i = 0; i < grid.Size - 1; i++)
        {
            Assert.Equal(tree.StatePrices(i), spread.StatePrices(i));
        }

        Assert.Equal(tree.Size(5), spread.Size(5));
        Assert.Equal(tree.Underlying(5, 1), spread.Underlying(5, 1));
    }
}
