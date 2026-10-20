// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The lattice framework's refusals, by type, message and parameter (playbook §2): an argument is refused with
/// <see cref="ArgumentException"/> or <see cref="ArgumentOutOfRangeException"/> naming it, a check of a value already
/// stored with <see cref="NanoQLException"/>.
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
