// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The minimum and maximum basket payoffs on no prices. QuantLib dereferences <c>std::min_element</c> and
/// <c>std::max_element</c> of the empty range, its end (<c>basketoption.hpp:56-58</c>, <c>:65-67</c>), undefined.
/// nanoql refuses an empty basket (master §2.2's undefined-path row).
/// </summary>
public class BasketPayoffEmptyTests
{
    [Fact]
    public void AnEmptyBasketIsRefused()
    {
        var call = new PlainVanillaPayoff(OptionType.Call, 100.0);
        Assert.Throws<ArgumentException>(() => new MinBasketPayoff(call).Evaluate(ReadOnlySpan<double>.Empty));
        Assert.Throws<ArgumentException>(() => new MaxBasketPayoff(call).Evaluate(ReadOnlySpan<double>.Empty));
    }
}
