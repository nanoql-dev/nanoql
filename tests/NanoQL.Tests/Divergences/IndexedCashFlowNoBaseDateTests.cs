// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An indexed cash flow with no base date. QuantLib's <c>baseFixing()</c> is <c>index_-&gt;fixing(baseDate())</c>
/// (<c>indexedcashflow.hpp:62</c>), so on <c>Date()</c> a zero-inflation index first takes the date's inflation period
/// (<c>inflationindex.cpp:205</c>), reading <c>Date()</c>'s month before the date tables, which is undefined. nanoql
/// refuses the base fixing, and so the amount, with <see cref="NanoQLException"/> and the text QuantLib's
/// <c>CPICashFlow</c> gives a missing base, "no base date specified" (<c>cpicoupon.cpp:164</c>).
/// </summary>
public class IndexedCashFlowNoBaseDateTests
{
    [Fact]
    public void TheBaseFixingIsRefused()
    {
        EvaluationContext context = InflationFixture.Context();
        var flow = new IndexedCashFlow(
            1e6,
            new UKRPI(InflationFixture.ZeroCurve()),
            null,
            new Date(1, Month.March, 2024),
            new Date(15, Month.June, 2025));

        Assert.Null(flow.BaseDate());
        Assert.Throws<NanoQLException>(() => flow.BaseFixing(context));
        Assert.Throws<NanoQLException>(() => flow.Amount(context));

        // With a base date it pays the ratio.
        var dated = new IndexedCashFlow(
            1e6,
            new UKRPI(InflationFixture.ZeroCurve()),
            new Date(1, Month.March, 2023),
            new Date(1, Month.March, 2024),
            new Date(15, Month.June, 2025));
        Assert.True(dated.Amount(context) > 1e6);
    }
}
