// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The lattice framework's inner loop allocates nothing (playbook §2's lattice row): a rollback step writes into the
/// asset's spare buffer and swaps the two, which the asset reserves at the widest step its rollback passes through,
/// where QuantLib allocates an array per step. The grid is non-uniform, with two short mandatory steps, so the tree is
/// wider at an earlier time than at the asset's initial one, and buffers sized at the initial step would grow.
/// </summary>
public class LatticeAllocationTests
{
    private static readonly YieldTermStructure Curve =
        new FlatForward(new Date(15, Month.January, 2026), 0.04, new Actual365Fixed());

    [Fact]
    public void ADiscountBondsRollbackAllocatesNothing()
    {
        OneFactorModelShortRateTree lattice = Tree();

        Roll(new DiscretizedDiscountBond(), lattice);
        var bond = new DiscretizedDiscountBond();
        bond.Initialize(lattice, 5.0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        bond.Rollback(0.0);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());

        // the fitted tree reprices the curve's discount bond
        Assert.Equal(Curve.Discount(5.0), bond.PresentValue(), 1e-12);
    }

    [Fact]
    public void AnOptionsRollbackAllocatesNothing()
    {
        OneFactorModelShortRateTree lattice = Tree();

        var warmBond = new DiscretizedDiscountBond();
        Roll(new DiscretizedOption(warmBond, ExerciseType.Bermudan, [1.0, 3.0]), lattice, warmBond);
        var bond = new DiscretizedDiscountBond();
        var option = new DiscretizedOption(bond, ExerciseType.Bermudan, [1.0, 3.0]);
        bond.Initialize(lattice, 5.0);
        option.Initialize(lattice, 5.0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        option.Rollback(0.0);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());

        // exercised into the bond, the option is worth the bond
        Assert.Equal(Curve.Discount(5.0), option.PresentValue(), 1e-12);
    }

    private static OneFactorModelShortRateTree Tree()
    {
        var model = new HullWhite(Curve, 0.1, 0.01);
        var grid = new TimeGrid([0.25, 0.2525, 1.0, 1.003, 3.0, 5.0], 20);
        var lattice = (OneFactorModelShortRateTree)model.Tree(grid);

        // the widest step is earlier than the last one
        int last = grid.Size - 1;
        int widest = 0;
        for (int k = 0; k < last; k++)
        {
            widest = Math.Max(widest, lattice.Size(k));
        }

        Assert.True(widest > lattice.Size(last), $"widest {widest}, last {lattice.Size(last)}");
        return lattice;
    }

    private static void Roll(DiscretizedAsset asset, Lattice lattice, DiscretizedAsset? underlying = null)
    {
        underlying?.Initialize(lattice, 5.0);
        asset.Initialize(lattice, 5.0);
        asset.Rollback(0.0);
    }
}
