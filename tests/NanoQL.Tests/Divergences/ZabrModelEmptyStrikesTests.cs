// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The ZABR model's vector members given no strikes. QuantLib's <c>ZabrModel::x</c>, which <c>lognormalVolatility</c>,
/// <c>normalVolatility</c> and <c>localVolatility</c> call on their vector, reads <c>strikes[0]</c> and starts its
/// ascending check at <c>strikes.begin() + 1</c> (<c>zabr.cpp:321-327</c>), past the end of an empty vector; the wheel
/// doesn't bind the model. nanoql refuses an empty vector with <see cref="ArgumentException"/>, "no strikes given",
/// naming the member's parameter; given strikes, each member returns QuantLib's values, its scalar form's among them.
/// </summary>
public class ZabrModelEmptyStrikesTests
{
    [Fact]
    public void AnEmptyStrikeVectorIsRefused()
    {
        var model = new ZabrModel(1.0, 0.03, 0.08, 0.7, 0.2, -0.3, 0.75);

        Assert.Equal("strikes", Assert.Throws<ArgumentException>(() => model.LognormalVolatility([])).ParamName);
        Assert.Equal("strikes", Assert.Throws<ArgumentException>(() => model.NormalVolatility([])).ParamName);
        Assert.Equal("f", Assert.Throws<ArgumentException>(() => model.LocalVolatility([])).ParamName);
        Assert.Equal(model.LognormalVolatility(0.02), model.LognormalVolatility([0.02])[0]);
        Assert.Equal(model.NormalVolatility(0.02), model.NormalVolatility([0.02])[0]);
        Assert.Equal(model.LocalVolatility(0.02), model.LocalVolatility([0.02])[0]);
    }
}
