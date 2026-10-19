// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A discretized asset adjusts once per time, and, as QuantLib's, <c>Initialize</c> doesn't forget the last time it
/// adjusted at (<c>discretizedasset.hpp:182-213</c>): an asset initialized a second time at that time skips the
/// adjustment, a behaviour QuantLib defines and the port keeps.
/// </summary>
public class DiscretizedAssetAdjustmentTests
{
    [Fact]
    public void AnAssetInitializedAgainAtItsLastAdjustmentTimeSkipsTheAdjustment()
    {
        Lattice lattice = new Vasicek().Tree(new TimeGrid(1.0, 4));
        var bond = new DiscretizedDiscountBond();
        var option = new DiscretizedOption(bond, ExerciseType.European, [1.0]);
        bond.Initialize(lattice, 1.0);

        // at its exercise time the option's reset exercises it into the bond
        option.Initialize(lattice, 1.0);
        Assert.All(option.Values.ToArray(), v => Assert.Equal(1.0, v));

        // initialized there again, it remembers the adjustment and skips it
        option.Initialize(lattice, 1.0);
        Assert.All(option.Values.ToArray(), v => Assert.Equal(0.0, v));

        // a fresh option exercises again
        var fresh = new DiscretizedOption(bond, ExerciseType.European, [1.0]);
        fresh.Initialize(lattice, 1.0);
        Assert.All(fresh.Values.ToArray(), v => Assert.Equal(1.0, v));
    }
}
