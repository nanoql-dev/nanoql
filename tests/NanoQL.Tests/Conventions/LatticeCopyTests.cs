// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A lattice is immutable once built (playbook §2's lattice row), its copies too: a derived lattice's copy, built
/// through the protected copy constructor, holds the rows it shares in an array of its own, so the rows a copy stores
/// while it is built leave every other copy's unchanged.
/// </summary>
public class LatticeCopyTests
{
    [Fact]
    public void ACopyThatStoresRowsLeavesItsSiblingsUnchanged()
    {
        var grid = new TimeGrid(1.0, 6);
        var tree = new TrinomialTree(new Vasicek().Dynamics().Process, grid);
        int last = grid.Size - 1;

        var original = new FlatLattice(tree, grid);
        FlatLattice plain = original.Shifted(0.0);
        double[] plainRow = plain.StatePrices(last).ToArray();

        FlatLattice shifted = original.Shifted(0.05);

        Assert.Equal(plainRow, plain.StatePrices(last).ToArray());
        Assert.Equal(new FlatLattice(tree, grid).Shifted(0.0).StatePrices(last).ToArray(), plainRow);
        Assert.NotEqual(plainRow, shifted.StatePrices(last).ToArray());
    }

    // A lattice of one's own: a flat short rate plus a shift on a trinomial tree's nodes; its copy stores every row.
    private sealed class FlatLattice : TreeLattice1D
    {
        private readonly TrinomialTree tree;
        private readonly double shift;

        public FlatLattice(TrinomialTree tree, TimeGrid grid)
            : base(grid, 3)
        {
            this.tree = tree;
        }

        private FlatLattice(FlatLattice other, double shift)
            : base(other)
        {
            tree = other.tree;
            this.shift = shift;
            ComputeStatePrices(TimeGrid.Size - 1);
        }

        public FlatLattice Shifted(double s) => new(this, s);

        public override int Size(int i) => tree.Size(i);

        public override double Discount(int i, int index) => Math.Exp(-(0.03 + shift) * TimeGrid.Dt(i));

        public override double Underlying(int i, int index) => tree.Underlying(i, index);

        public override int Descendant(int i, int index, int branch) => tree.Descendant(i, index, branch);

        public override double Probability(int i, int index, int branch) => tree.Probability(i, index, branch);
    }
}
