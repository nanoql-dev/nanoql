// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston model's Feller constraint given fewer than three parameters is refused, with
/// <see cref="ArgumentException"/> "the Feller constraint reads three parameters". QuantLib's test reads theta, kappa
/// and sigma as the first three of any array (<c>hestonmodel.hpp:71-73</c>), past a shorter one's end, a result C++
/// leaves undefined. Three parameters or more are QuantLib's defined path, kept.
/// </summary>
public class FellerConstraintSizeTests
{
    [Fact]
    public void FewerThanThreeParametersAreRefused()
    {
        var feller = new HestonModelFellerConstraint();
        Assert.Throws<ArgumentException>(() => feller.Test(new Vector([0.04, 1.5])));
        Assert.Throws<ArgumentException>(() => feller.Test(new Vector(0)));
    }

    [Fact]
    public void ThreeParametersAreRead()
    {
        var feller = new HestonModelFellerConstraint();
        Assert.True(feller.Test(new Vector([0.04, 1.5, 0.3])));
        Assert.False(feller.Test(new Vector([0.04, 1.5, 0.4])));
    }
}
