// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A period of length <see cref="int.MinValue"/> has no negation. QuantLib's unary minus negates the length
/// (<c>period.hpp:156</c>) and <c>frequency()</c> takes its absolute value (<c>period.cpp:71</c>), both overflowing
/// there, which is undefined; nanoql refuses both with <see cref="NanoQLException"/>, where it used to wrap and to throw
/// <see cref="OverflowException"/>. An ex-coupon, last-recent or tenor period reaches them.
/// </summary>
public class PeriodNegationTests
{
    [Fact]
    public void NegatingTheMostNegativeLengthIsRefused()
    {
        var p = new Period(int.MinValue, TimeUnit.Days);

        Assert.Throws<NanoQLException>(() => -p);
        Assert.Throws<NanoQLException>(() => new Period(1, TimeUnit.Days) - p);
        Assert.Equal(new Period(-int.MaxValue, TimeUnit.Months), -new Period(int.MaxValue, TimeUnit.Months));
    }

    [Fact]
    public void TheFrequencyOfTheMostNegativeLengthIsRefused()
    {
        Assert.Throws<NanoQLException>(() => new Period(int.MinValue, TimeUnit.Months).Frequency());
        Assert.Equal(Frequency.Semiannual, new Period(-6, TimeUnit.Months).Frequency());
        Assert.Equal(Frequency.OtherFrequency, new Period(int.MinValue + 1, TimeUnit.Months).Frequency());
    }
}
