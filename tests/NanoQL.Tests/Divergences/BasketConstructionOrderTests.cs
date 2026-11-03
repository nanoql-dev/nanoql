// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A basket's remaining tranche computed after its attachment and detachment amounts: QuantLib's constructor computes
/// the basket's evaluation-date state before it sets the amounts (<c>basket.cpp:57-68</c>), so the remaining amounts
/// read 0 and 0 until the first statistic recomputes them. QuantLib's values are recorded in the test; the check is
/// QuantLib's values after its first statistic.
/// </summary>
public class BasketConstructionOrderTests
{
    [Fact]
    public void TheRemainingTrancheIsSetAtConstruction()
    {
        // QuantLib, before any statistic: remainingAttachmentAmount 0, remainingDetachmentAmount 0; after one: 6 and 30
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)];
        var basket = new Basket(Today, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves), Context, 0.1, 0.5);

        Assert.Equal(6.0, basket.RemainingAttachmentAmount(), 1e-14);
        Assert.Equal(30.0, basket.RemainingDetachmentAmount(), 1e-14);
        Assert.Equal(basket.AttachmentAmount, basket.RemainingAttachmentAmount());
        Assert.Equal(basket.DetachmentAmount, basket.RemainingDetachmentAmount());
    }
}
