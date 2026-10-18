// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib documents a rounding's precision as lying in [0, 16] and doesn't check it: "supplying an unsupported
/// rounding value may result in undefined behavior" (<c>rounding.hpp:31-34</c>). Its code reads a 32-entry table of
/// powers of ten at <c>precision &amp; 0x1F</c> (<c>rounding.cpp:30-37</c>), whose entries past 16 are zero: a
/// precision of 17 to 31, or -1, scales by zero and gives NaN, and others alias (32 rounds as 0, 33 as 1, -16 as 16).
/// nanoql refuses a precision outside the documented range when the rounding is built, with
/// <see cref="ArgumentOutOfRangeException"/> on <c>precision</c> and the text QuantLib's overnight-indexed swap uses
/// for the same check (<c>overnightindexedswap.cpp:178-180</c>).
/// </summary>
public class RoundingPrecisionRangeTests
{
    [Fact]
    public void APrecisionOutsideZeroToSixteenIsRefused()
    {
        Func<int, Rounding>[] constructors =
        [
            p => new Rounding(p),
            p => new Rounding(p, RoundingType.None),
            p => new UpRounding(p),
            p => new DownRounding(p),
            p => new ClosestRounding(p),
            p => new FloorTruncation(p),
            p => new CeilingTruncation(p),
        ];
        foreach (Func<int, Rounding> make in constructors)
        {
            foreach (int p in new[] { -1, 17, 20, 31, 32, 33, -16, int.MinValue, int.MaxValue })
            {
                ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => make(p));
                Assert.Equal("precision", e.ParamName);
                Assert.Equal(p, e.ActualValue);
            }

            Assert.Equal(0, make(0).Precision());
            Assert.Equal(16, make(16).Precision());
        }
    }
}
