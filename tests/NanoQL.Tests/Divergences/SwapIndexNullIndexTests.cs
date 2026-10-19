// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A swap index given a null Ibor index, or an overnight-indexed swap index given a null overnight index, is refused
/// on entry, naming the parameter. QuantLib's <c>SwapIndex</c> accepts a null Ibor index (<c>swapindex.cpp:38-43</c>)
/// and dereferences it later, in <c>forwardingTermStructure()</c>, <c>underlyingSwap()</c> and the clones
/// (<c>:65, 85, 95, 125, 136, 150</c>), and <c>OvernightIndexedSwapIndex</c> dereferences its overnight index in its
/// base's initializer (<c>:193, 196</c>): results C++ leaves undefined, where the wheel stops on Boost's assertion.
/// </summary>
public class SwapIndexNullIndexTests
{
    [Fact]
    public void ASwapIndexGivenANullIborIndexIsRefused()
    {
        var thirty360 = new Thirty360(Thirty360Convention.BondBasis);
        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(() => new SwapIndex(
                "EurSwap",
                new Period(5, TimeUnit.Years),
                2,
                null,
                new TARGET(),
                new Period(1, TimeUnit.Years),
                BusinessDayConvention.ModifiedFollowing,
                thirty360,
                null!)).ParamName);
        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(() => new SwapIndex(
                "EurSwap",
                new Period(5, TimeUnit.Years),
                2,
                null,
                new TARGET(),
                new Period(1, TimeUnit.Years),
                BusinessDayConvention.ModifiedFollowing,
                thirty360,
                null!,
                null)).ParamName);
    }

    [Fact]
    public void AnOvernightIndexedSwapIndexGivenANullOvernightIndexIsRefused()
    {
        Assert.Equal(
            "overnightIndex",
            Assert.Throws<ArgumentNullException>(
                () => new OvernightIndexedSwapIndex("EstrSwap", new Period(1, TimeUnit.Years), 2, null, null!))
            .ParamName);
    }
}
