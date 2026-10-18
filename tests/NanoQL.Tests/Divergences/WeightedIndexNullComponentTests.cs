// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A weighted index with a null component. QuantLib's <c>StubIborCoupon::WeightedIndex</c> reads every component's
/// tenor, and the first one's conventions, in its base initializer (<c>stubiborcoupon.cpp:37-56</c>), before its own
/// check, "null component index" (<c>:64</c>), so a null component is dereferenced. nanoql refuses it first, with
/// <see cref="ArgumentException"/> and QuantLib's text, wherever it stands.
/// </summary>
public class WeightedIndexNullComponentTests
{
    [Fact]
    public void ANullComponentIsRefusedBeforeItIsRead()
    {
        void AssertRefused(params (IborIndex Index, double Weight)[] components)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => new StubIborCouponWeightedIndex(components));
            Assert.Equal("components", e.ParamName);
            Assert.StartsWith("null component index", e.Message, StringComparison.Ordinal);
        }

        AssertRefused((null!, 1.0));
        AssertRefused((new Euribor3M(), 0.5), (null!, 0.5));
        AssertRefused((null!, 0.5), (new Euribor6M(), 0.5));
    }
}
