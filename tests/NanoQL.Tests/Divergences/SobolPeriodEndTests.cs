// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Sobol generator's period end with the Gray code, where QuantLib's behaviour is undefined. Its
/// <c>skipTo(4294967295)</c> computes <c>skip + 1</c>, which wraps to 0, and converts <c>log(0)</c>'s −∞ to an
/// <c>unsigned int</c>, which C++ leaves undefined; its draw that takes the counter to 4294967295 finds the rightmost
/// zero bit at 32 and reads row 32 of its 32 rows of direction integers (the wheel crashes there). nanoql refuses that
/// skip with <see cref="ArgumentOutOfRangeException"/>, and throws "period exceeded" on that draw, one draw before
/// QuantLib's own check. Without the Gray code QuantLib's end is defined, and nanoql keeps it (the harness's vectors,
/// <c>cxx/math/randomnumbers/sobolrsg</c>).
/// </summary>
public class SobolPeriodEndTests
{
    [Fact]
    public void SkipToTheLastIndexIsRefusedWithTheGrayCode()
    {
        var rsg = new SobolRsg(8, 1);
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => rsg.SkipTo(4294967295));
        Assert.Equal("n", e.ParamName);

        // The index below it is a skip like any other.
        Assert.Equal(8, rsg.SkipTo(4294967294).Length);

        // Without the Gray code, QuantLib's skip to the last index sets every integer to 0.
        var plain = new SobolRsg(8, 1, SobolRsgDirectionIntegers.Jaeckel, useGrayCode: false);
        Assert.All(plain.SkipTo(4294967295).ToArray(), x => Assert.Equal(0L, x));
    }

    [Fact]
    public void TheDrawThatWouldReachTheLastCounterThrowsWithTheGrayCode()
    {
        var rsg = new SobolRsg(8, 1);
        rsg.NextInt32Sequence();

        // The draw that takes the counter to 4294967294 is the last.
        rsg.SkipTo(4294967293);
        ReadOnlyMemory<long> integers = rsg.NextInt32Sequence();
        long[] last = integers.ToArray();

        NanoQLException e = Assert.Throws<NanoQLException>(() => rsg.NextInt32Sequence());
        Assert.Equal("period exceeded", e.Message);

        // The generator is left as it was, and refuses the draw again, an integer draw or a sequence.
        Assert.Equal(last, integers.ToArray());
        Assert.Throws<NanoQLException>(() => rsg.NextInt32Sequence());
        Assert.Throws<NanoQLException>(() => rsg.NextSequence());
    }
}
