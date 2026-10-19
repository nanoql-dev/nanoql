// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A swaption builder given no swap index is refused at construction, with <see cref="ArgumentNullException"/> naming
/// <c>swapIndex</c>, as nanoql's swap builders refuse theirs. QuantLib's <c>MakeSwaption</c> takes it unchecked and
/// dereferences it at the conversion, for its fixing calendar, its forwarding curve and its conventions
/// (<c>makeswaption.cpp:57-58, 82, 112</c>): results C++ leaves undefined, where the wheel stops on Boost's assertion.
/// </summary>
public class MakeSwaptionNullIndexTests
{
    [Fact]
    public void ABuilderWithoutASwapIndexIsRefused()
    {
        Assert.Equal(
            "swapIndex",
            Assert.Throws<ArgumentNullException>(
                () => new MakeSwaption(null!, new Period(1, TimeUnit.Years))).ParamName);
        Assert.Equal(
            "swapIndex",
            Assert.Throws<ArgumentNullException>(
                () => new MakeSwaption(null!, new Date(15, Month.January, 2026), 0.03)).ParamName);
    }
}
