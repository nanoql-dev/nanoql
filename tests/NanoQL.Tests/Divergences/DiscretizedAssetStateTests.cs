// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A discretized asset used before it is initialized, and an American option with fewer than two exercise times.
/// QuantLib's <c>DiscretizedAsset</c> leaves its lattice null until <c>initialize</c> and dereferences it in
/// <c>rollback</c>, <c>partialRollback</c> and <c>presentValue</c> (<c>discretizedasset.hpp:38-40, 182-199</c>), and its
/// <c>DiscretizedOption</c> reads <c>exerciseTimes_[0]</c> and <c>[1]</c> for an American exercise
/// (<c>discretizedasset.cpp:35-37</c>), past the end of a shorter vector. nanoql refuses the first with
/// <see cref="NanoQLException"/>, "asset not initialized", and the second, where QuantLib's read would go past the
/// times, with <see cref="NanoQLException"/>, "American exercise needs two exercise times".
/// </summary>
public class DiscretizedAssetStateTests
{
    [Fact]
    public void AnUninitializedAssetIsRefused()
    {
        var bond = new DiscretizedDiscountBond();

        Assert.Equal("asset not initialized", Assert.Throws<NanoQLException>(() => bond.Rollback(0.0)).Message);
        Assert.Equal("asset not initialized", Assert.Throws<NanoQLException>(() => bond.PartialRollback(0.0)).Message);
        Assert.Equal("asset not initialized", Assert.Throws<NanoQLException>(() => bond.PresentValue()).Message);
        Assert.Null(bond.Method);
    }

    [Fact]
    public void AnAmericanOptionReadPastItsExerciseTimesIsRefused()
    {
        Lattice lattice = new Vasicek().Tree(new TimeGrid(1.0, 4));

        foreach (double[] times in (double[][])[[], [0.25]])
        {
            var bond = new DiscretizedDiscountBond();
            var option = new DiscretizedOption(bond, ExerciseType.American, times);
            bond.Initialize(lattice, 1.0);
            NanoQLException e = Assert.Throws<NanoQLException>(() => option.Initialize(lattice, 1.0));
            Assert.Equal("American exercise needs two exercise times", e.Message);
        }

        // Before its one exercise time, QuantLib's test reads only the first time, so the option rolls back.
        var early = new DiscretizedDiscountBond();
        var option2 = new DiscretizedOption(early, ExerciseType.American, [0.75]);
        early.Initialize(lattice, 0.5);
        option2.Initialize(lattice, 0.5);
        option2.Rollback(0.0);
        Assert.Equal(0.0, option2.PresentValue());
    }
}
