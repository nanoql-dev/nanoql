// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of <see cref="Rounding"/>'s refusals: a precision outside [0, 16], a path QuantLib leaves
/// undefined, throws <see cref="ArgumentOutOfRangeException"/> on <c>precision</c> with QuantLib's text; a rounding
/// method outside <see cref="RoundingType"/>, <see cref="ArgumentOutOfRangeException"/> on <c>type</c> with the text of
/// QuantLib's <c>QL_FAIL</c> (<c>rounding.cpp:75</c>); and the precision and digit of a default rounding, which
/// QuantLib leaves uninitialized, <see cref="NanoQLException"/>.
/// </summary>
public class RoundingErrorTests
{
    [Fact]
    public void RoundingRefusals()
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => new Rounding(17));
        Assert.Equal("precision", e.ParamName);
        Assert.Equal(17, e.ActualValue);
        Assert.StartsWith("rounding precision (17) must be between 0 and 16", e.Message);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new FloorTruncation(-1, 3));
        Assert.Equal("precision", e.ParamName);
        Assert.StartsWith("rounding precision (-1) must be between 0 and 16", e.Message);

        e = Assert.Throws<ArgumentOutOfRangeException>(() => new Rounding(2, (RoundingType)6));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("unknown rounding method", e.Message);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new Rounding(2, (RoundingType)(-1)));
        Assert.Equal("type", e.ParamName);

        var rounding = new Rounding();
        Assert.Equal(
            "no precision for a default rounding",
            Assert.Throws<NanoQLException>(() => rounding.Precision()).Message);
        Assert.Equal(
            "no rounding digit for a default rounding",
            Assert.Throws<NanoQLException>(() => rounding.RoundingDigit()).Message);
    }
}
