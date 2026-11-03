// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The linear-flat interpolation's primitive outside its range, the flat value's integral (fixed). QuantLib's
/// <c>LinearFlatInterpolationImpl::primitive</c> extends the end intervals' lines past the ends, where its value is
/// flat, so its primitive's slope there isn't its value: on x 1, 2, 3 and y 1, 3, 2 its primitive at 0.5 is −0.25 for
/// the flat value's −0.5, and at 4 it is 6 for 6.5; on a single point its primitive and derivative read before their
/// arrays (<c>generalizedhullwhite.hpp</c>). nanoql's primitive is the flat value's integral outside the range, its
/// derivative there the value's 0, and a single point's the flat value's; inside the range all three are QuantLib's.
/// </summary>
public class LinearFlatPrimitiveTests
{
    [Fact]
    public void OutsideTheRangeThePrimitiveIntegratesTheFlatValue()
    {
        Interpolation f = default(LinearFlat).Interpolate([1.0, 2.0, 3.0], [1.0, 3.0, 2.0]).EnableExtrapolation();
        Assert.Equal(-0.5, f.Primitive(0.5));
        Assert.Equal(6.5, f.Primitive(4.0));
        Assert.Equal(1.0, f.Evaluate(0.5));
        Assert.Equal(2.0, f.Evaluate(4.0));
        Assert.Equal(0.0, f.Derivative(4.0));

        // inside the range, QuantLib's: the interval's line and its primitive
        Assert.Equal(2.0, f.Evaluate(1.5));
        Assert.Equal(1.0 * (1.0 + (0.5 * 1.0 * 2.0)), f.Primitive(2.0));
        Assert.Equal(-1.0, f.Derivative(2.5));
    }

    [Fact]
    public void ASinglePointIsFlat()
    {
        Interpolation f = default(LinearFlat).Interpolate([1.0], [3.0]).EnableExtrapolation();
        Assert.Equal(3.0, f.Evaluate(-2.0));
        Assert.Equal(3.0, f.Evaluate(5.0));
        Assert.Equal(6.0, f.Primitive(3.0));
        Assert.Equal(0.0, f.Derivative(1.0));
    }
}
