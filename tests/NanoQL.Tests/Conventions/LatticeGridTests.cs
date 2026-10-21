// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Lattice.Grid(double, Span{double})"/>, the non-allocating grid read nanoql adds (the S-eq plan's SEQD18):
/// it writes <see cref="Lattice.Grid(double)"/>'s values and returns their count, on a tree lattice through its own
/// override and on any other lattice through the base's copy; and a destination shorter than the grid is an
/// <see cref="ArgumentException"/> naming it.
/// </summary>
public class LatticeGridTests
{
    [Fact]
    public void ATreeLatticeWritesItsGrid()
    {
        var today = new Date(15, Month.October, 2026);
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.01, dc), new FlatForward(today, 0.03, dc), new BlackConstantVol(today, null, 0.2, dc));
        var lattice = new BlackScholesLattice<CoxRossRubinstein>(CoxRossRubinstein.Create(process, 1.0, 10, 100.0), 0.03, 1.0, 10);

        Written(lattice, 0.5);
    }

    [Fact]
    public void AnyOtherLatticeCopiesItsGrid() => Written(new ThreeNodeLattice(new TimeGrid(1.0, 4)), 0.5);

    private static void Written(Lattice lattice, double t)
    {
        Vector expected = lattice.Grid(t);
        double[] destination = new double[expected.Size + 2];
        Assert.Equal(expected.Size, lattice.Grid(t, destination));
        Assert.Equal(expected.AsSpan().ToArray(), destination[..expected.Size]);

        ArgumentException e = Assert.Throws<ArgumentException>(() => lattice.Grid(t, new double[expected.Size - 1]));
        Assert.Equal("destination", e.ParamName);
        Assert.StartsWith($"a destination of {expected.Size} values needed, not {expected.Size - 1}", e.Message, StringComparison.Ordinal);
    }

    // A lattice with no grid read of its own: three nodes at every time.
    private sealed class ThreeNodeLattice(TimeGrid grid) : Lattice(grid)
    {
        public override void Initialize(DiscretizedAsset asset, double time) => asset.Time = time;

        public override void Rollback(DiscretizedAsset asset, double to) => asset.Time = to;

        public override void PartialRollback(DiscretizedAsset asset, double to) => asset.Time = to;

        public override double PresentValue(DiscretizedAsset asset) => 0.0;

        public override Vector Grid(double t) => new([t, 2.0 * t, 3.0 * t]);
    }
}
