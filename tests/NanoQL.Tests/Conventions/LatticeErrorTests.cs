// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The lattice framework's refusals, by type, message and parameter (playbook §2): an argument is refused with
/// <see cref="ArgumentException"/> or <see cref="ArgumentOutOfRangeException"/> naming it, a check of a value already
/// stored with <see cref="NanoQLException"/>; a step back on values and room fewer than the nodes, or a trinomial
/// tree's from the grid's last step, is refused before QuantLib's read past them.
/// </summary>
public class LatticeErrorTests
{
    [Fact]
    public void TheTrinomialTreesRefusals()
    {
        StochasticProcess1D process = new Vasicek().Dynamics().Process;

        // QuantLib's step count wraps to the largest Size for an empty grid, passing its own check, and allocating
        // the step caches throws std::length_error (trinomialtree.cpp:42-43, 58; the wheel's "vector too long").
        ArgumentException empty = Assert.Throws<ArgumentException>(() => new TrinomialTree(process, new TimeGrid()));
        Assert.Equal(typeof(ArgumentException), empty.GetType());
        Assert.Equal("timeGrid", empty.ParamName);
        Assert.StartsWith("null time steps for trinomial tree", empty.Message, StringComparison.Ordinal);

        Assert.Equal("process", Assert.Throws<ArgumentNullException>(() => new TrinomialTree(null!, new TimeGrid(1.0, 2))).ParamName);
        Assert.Equal("timeGrid", Assert.Throws<ArgumentNullException>(() => new TrinomialTree(process, null!)).ParamName);

        var tree = new TrinomialTree(process, new TimeGrid(1.0, 2));
        ArgumentOutOfRangeException index = Assert.Throws<ArgumentOutOfRangeException>(() => tree.Size(3));
        Assert.Equal("i", index.ParamName);
        Assert.StartsWith("index 3 out of range [0, 3)", index.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTreeLatticesRefusals()
    {
        var lattice = (OneFactorModelShortRateTree)new Vasicek().Tree(new TimeGrid(1.0, 2));

        ArgumentOutOfRangeException step = Assert.Throws<ArgumentOutOfRangeException>(() => lattice.StatePrices(3));
        Assert.Equal("i", step.ParamName);
        Assert.StartsWith("step 3 outside the grid's 3 times", step.Message, StringComparison.Ordinal);

        // An asset whose values have another size than the lattice's step, here initialized on a finer lattice.
        var fine = new DiscretizedDiscountBond();
        fine.Initialize(new Vasicek().Tree(new TimeGrid(1.0, 4)), 1.0);
        ArgumentException sizes = Assert.Throws<ArgumentException>(() => lattice.PresentValue(fine));
        Assert.Equal(typeof(ArgumentException), sizes.GetType());
        Assert.Equal("asset", sizes.ParamName);
        Assert.StartsWith("arrays with different sizes (9, 5) cannot be multiplied", sizes.Message, StringComparison.Ordinal);

        // Rolling an asset forward is a check of its stored time against the argument.
        var bond = new DiscretizedDiscountBond();
        bond.Initialize(lattice, 0.5);
        NanoQLException forward = Assert.Throws<NanoQLException>(() => bond.Rollback(1.0));
        Assert.Equal(typeof(NanoQLException), forward.GetType());
        Assert.Equal("cannot roll the asset back to1 (it is already at t = 0.5)", forward.Message);
    }

    [Fact]
    public void ASteppingBackFromTheGridsLastStepOrOnShortValuesIsRefused()
    {
        // TreeLattice's stepback, and the Black-Scholes lattice's own loop, read step i + 1's values and write step
        // i's: QuantLib reads past values or room fewer than the nodes, and the trinomial tree's branchings past the
        // grid's last step; the binomial tree's nodes need no grid, so a step at or past its last computes.
        var today = new Date(15, Month.October, 2026);
        DayCounter dc = new Actual365Fixed();
        var curve = new FlatForward(today, 0.03, dc);
        var process = new BlackScholesMertonProcess(100.0, curve, curve, new BlackConstantVol(today, null, 0.25, dc));
        foreach (TreeLattice lattice in new TreeLattice[]
                 {
                     (OneFactorModelShortRateTree)new Vasicek().Tree(new TimeGrid(1.0, 2)),
                     new BlackScholesLattice<CoxRossRubinstein>(new CoxRossRubinstein(process, 1.0, 2, 100.0), 0.04, 1.0, 2),
                 })
        {
            int last = lattice.TimeGrid.Size - 1;
            int n0 = lattice.Size(0), n1 = lattice.Size(1);
            if (lattice is OneFactorModelShortRateTree)
            {
                ArgumentOutOfRangeException past = Assert.Throws<ArgumentOutOfRangeException>(
                    () => lattice.Stepback(last, new double[lattice.Size(last)], new double[lattice.Size(last)]));
                Assert.Equal("i", past.ParamName);
                Assert.StartsWith($"i ('{last}') must be less than '{last}'.", past.Message, StringComparison.Ordinal);
            }
            else
            {
                foreach (int i in new[] { last, last + 3 })
                {
                    double[] stepped = new double[lattice.Size(i)];
                    lattice.Stepback(i, [.. Enumerable.Repeat(1.0, lattice.Size(i) + 1)], stepped);
                    Assert.All(stepped, v => Assert.True(v > 0.0 && v < 1.0));
                }
            }

            ArgumentOutOfRangeException step = Assert.Throws<ArgumentOutOfRangeException>(
                () => lattice.Stepback(-1, new double[n0], new double[n0]));
            Assert.Equal("i", step.ParamName);
            Assert.StartsWith("i ('-1') must be a non-negative value.", step.Message, StringComparison.Ordinal);

            ArgumentException e = Assert.Throws<ArgumentException>(() => lattice.Stepback(0, new double[n1 - 1], new double[n0]));
            Assert.Equal("values", e.ParamName);
            Assert.StartsWith($"{n1 - 1} values given for step 1's {n1} nodes", e.Message, StringComparison.Ordinal);
            e = Assert.Throws<ArgumentException>(() => lattice.Stepback(1, new double[lattice.Size(2)], new double[n1 - 1]));
            Assert.Equal("newValues", e.ParamName);
            Assert.StartsWith($"room for {n1 - 1} values given for step 1's {n1} nodes", e.Message, StringComparison.Ordinal);

            double[] back = new double[n0];
            lattice.Stepback(0, [.. Enumerable.Repeat(1.0, n1)], back);
            Assert.All(back, v => Assert.True(v > 0.0 && v < 1.0));
        }
    }

    [Fact]
    public void TheDiscretizedAssetsRefusals()
    {
        Lattice lattice = new Vasicek().Tree(new TimeGrid(1.0, 4));

        NanoQLException uninitialized = Assert.Throws<NanoQLException>(() => new DiscretizedDiscountBond().PresentValue());
        Assert.Equal(typeof(NanoQLException), uninitialized.GetType());
        Assert.Equal("asset not initialized", uninitialized.Message);

        var bond = new DiscretizedDiscountBond();
        var american = new DiscretizedOption(bond, ExerciseType.American, [0.25]);
        bond.Initialize(lattice, 1.0);
        NanoQLException times = Assert.Throws<NanoQLException>(() => american.Initialize(lattice, 1.0));
        Assert.Equal(typeof(NanoQLException), times.GetType());
        Assert.Equal("American exercise needs two exercise times", times.Message);

        // The option and its underlying on different lattices.
        var other = new DiscretizedDiscountBond();
        var option = new DiscretizedOption(other, ExerciseType.European, [0.5]);
        other.Initialize(new Vasicek().Tree(new TimeGrid(1.0, 4)), 1.0);
        Assert.Equal(
            "option and underlying were initialized on different methods",
            Assert.Throws<NanoQLException>(() => option.Initialize(lattice, 1.0)).Message);

        // QuantLib's switch refuses an exercise type outside the enum; the option refuses it on entry, with its text.
        ArgumentOutOfRangeException type = Assert.Throws<ArgumentOutOfRangeException>(
            () => new DiscretizedOption(bond, (ExerciseType)7, [0.5]));
        Assert.Equal("exerciseType", type.ParamName);
        Assert.StartsWith("invalid exercise type", type.Message, StringComparison.Ordinal);
    }
}
