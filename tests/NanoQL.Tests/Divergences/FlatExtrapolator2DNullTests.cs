// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A flat extrapolator decorating no interpolation. QuantLib's constructor calls the decorated interpolation's
/// <c>update()</c> through the pointer it is given (<c>flatextrapolation2d.hpp:39-48, 62-64</c>), a null dereference,
/// undefined under <c>std::shared_ptr</c> and a Boost assertion by default. nanoql refuses it with
/// <see cref="ArgumentNullException"/> naming <c>decoratedInterpolation</c>.
/// </summary>
public class FlatExtrapolator2DNullTests
{
    [Fact]
    public void ANullDecoratedInterpolationIsRefused()
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => new FlatExtrapolator2D(null!));
        Assert.Equal("decoratedInterpolation", e.ParamName);
    }
}
