// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Currency"/> compares by name, ordinally, as QuantLib's <c>operator==</c> does
/// (<c>currency.hpp:179-185</c>), and QuantLib's empty currency is a null: two nulls are equal, as two empty
/// currencies are, and a null never equals a currency. Equal currencies hash equal.
/// </summary>
public class CurrencyEqualityTests
{
    [Fact]
    public void CurrenciesCompareByNameAndNullIsTheEmptyCurrency()
    {
        var usd = new Currency("U.S. dollar", "USD", 840, "$", "¢", 100, new Rounding());
        var other = new Currency("U.S. dollar", "XUS", 999, "US$", "c", 10, new ClosestRounding(2), usd, ["USc"]);
        var lower = new Currency("u.s. dollar", "USD", 840, "$", "¢", 100, new Rounding());
        Currency? none = null, alsoNone = null;

        // Another code, numeric code, symbols, rounding, triangulation and minor units, but the same name.
        Assert.True(usd == other);
        Assert.False(usd != other);
        Assert.True(usd.Equals(other));
        Assert.True(usd.Equals((object)other));
        Assert.Equal(usd.GetHashCode(), other.GetHashCode());

        // The name compares ordinally: case counts.
        Assert.False(usd == lower);
        Assert.True(usd != lower);
        Assert.False(usd.Equals(lower));

        // Nulls.
        Assert.True(none == alsoNone);
        Assert.False(none != alsoNone);
        Assert.False(usd == none);
        Assert.False(none == usd);
        Assert.True(usd != none);
        Assert.True(none != usd);
        Assert.False(usd.Equals(none));
        Assert.False(usd.Equals((object?)none));
        Assert.False(usd.Equals("U.S. dollar"));
    }
}
