// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Currency"/> compares by name and code, ordinally, and hashes by name; QuantLib's <c>operator==</c>
/// compares names alone (<c>currency.hpp:179-185</c>), a documented divergence
/// (<c>Divergences/CurrencyNameAndCodeTests.cs</c>). QuantLib's empty currency is a null: two nulls are equal, as two
/// empty currencies are, and a null never equals a currency. Equal currencies hash equal.
/// </summary>
public class CurrencyEqualityTests
{
    [Fact]
    public void CurrenciesCompareByNameAndCodeAndNullIsTheEmptyCurrency()
    {
        var usd = new Currency("U.S. dollar", "USD", 840, "$", "¢", 100, new Rounding());
        var other = new Currency("U.S. dollar", "USD", 999, "US$", "c", 10, new ClosestRounding(2), usd, ["USc"]);
        var otherCode = new Currency("U.S. dollar", "XUS", 840, "$", "¢", 100, new Rounding());
        var lower = new Currency("u.s. dollar", "USD", 840, "$", "¢", 100, new Rounding());
        var lowerCode = new Currency("U.S. dollar", "usd", 840, "$", "¢", 100, new Rounding());
        Currency? none = null, alsoNone = null;

        // Another numeric code, symbols, rounding, triangulation and minor units, but the same name and code.
        Assert.True(usd == other);
        Assert.False(usd != other);
        Assert.True(usd.Equals(other));
        Assert.True(usd.Equals((object)other));
        Assert.Equal(usd.GetHashCode(), other.GetHashCode());

        // The same name with another code (QuantLib: equal).
        Assert.False(usd == otherCode);
        Assert.True(usd != otherCode);
        Assert.False(usd.Equals(otherCode));

        // The name and the code compare ordinally: case counts.
        Assert.False(usd == lower);
        Assert.True(usd != lower);
        Assert.False(usd.Equals(lower));
        Assert.False(usd == lowerCode);

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
