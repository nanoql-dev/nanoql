// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An affine model's discount bond given an empty vector of factors. QuantLib's
/// <c>OneFactorAffineModel::discountBond(now, maturity, Array factors)</c> reads <c>factors[0]</c>
/// (<c>onefactormodel.hpp:132-134</c>), past the end of an empty array, which crashes the wheel with an access
/// violation. nanoql refuses it with <see cref="ArgumentException"/>, "no factors given", naming <c>factors</c>; given a
/// factor, it prices as QuantLib's, on the vector's first element.
/// </summary>
public class AffineDiscountBondFactorsTests
{
    [Fact]
    public void AnEmptyFactorVectorIsRefused()
    {
        var model = new Vasicek();

        ArgumentException e = Assert.Throws<ArgumentException>(() => model.DiscountBond(0.0, 1.0, new Vector()));
        Assert.Equal("factors", e.ParamName);
        Assert.Equal(model.DiscountBond(0.0, 1.0, 0.05), model.DiscountBond(0.0, 1.0, new Vector(1, 0.05)));
        Assert.Equal(model.DiscountBond(0.0, 1.0, 0.05), model.DiscountBond(0.0, 1.0, new Vector(2, 0.05, 1.0)));
    }
}
