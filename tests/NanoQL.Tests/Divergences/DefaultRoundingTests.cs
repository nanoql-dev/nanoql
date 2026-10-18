// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's default <c>Rounding()</c> initializes only its type, to <c>None</c>: <c>precision_</c> and
/// <c>digit_</c> have no initializer (<c>rounding.hpp:74, 87, 89</c>), so <c>precision()</c> and
/// <c>roundingDigit()</c> read indeterminate values, a result C++ leaves undefined. nanoql's default rounding has no
/// precision or digit: <see cref="Rounding.Precision"/> and <see cref="Rounding.RoundingDigit"/> throw
/// <see cref="NanoQLException"/>. Its type and its rounding are QuantLib's: <see cref="RoundingType.None"/>, and every
/// value back unchanged.
/// </summary>
public class DefaultRoundingTests
{
    [Fact]
    public void ADefaultRoundingHasNoPrecisionOrDigit()
    {
        var rounding = new Rounding();

        Assert.Equal(
            "no precision for a default rounding",
            Assert.Throws<NanoQLException>(() => rounding.Precision()).Message);
        Assert.Equal(
            "no rounding digit for a default rounding",
            Assert.Throws<NanoQLException>(() => rounding.RoundingDigit()).Message);
        Assert.Equal(RoundingType.None, rounding.Type);
        Assert.Equal(1.23456789, rounding.Evaluate(1.23456789));

        // A rounding of type None built with a precision and a digit keeps both, as QuantLib's does.
        var none = new Rounding(3, RoundingType.None, 7);
        Assert.Equal(3, none.Precision());
        Assert.Equal(7, none.RoundingDigit());
        Assert.Equal(1.23456789, none.Evaluate(1.23456789));
    }
}
