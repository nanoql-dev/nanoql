// SPDX-FileCopyrightText: Copyright (C) 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/cmsspread.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCouponPricing (S-xrt's LognormalCmsSpreadPricer)

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CmsSpreadTests
{
    // Adapted: TestData's evaluation date and its enforcesTodaysHistoricFixings switch are the context's; its CMS and
    // CMS spread pricers, which only testCouponPricing reads, wait with it for S-xrt's LognormalCmsSpreadPricer; the
    // indexes' fixings are the context's fixing store, addFixing returning a new store, and clearHistories a context
    // with none.
    [Fact]
    public void TestFixings()
    {
        // Testing fixings of cms spread indices...

        var d = new TestData();

        SwapIndex cms10y = new EuriborSwapIsdaFixA(10 * TimeUnit.Years, d.Yts2, d.Yts2);
        SwapIndex cms2y = new EuriborSwapIsdaFixA(2 * TimeUnit.Years, d.Yts2, d.Yts2);
        var cms10y2y = new SwapSpreadIndex("cms10y2y", cms10y, cms2y);

        EvaluationContext context = d.Context with { EnforcesTodaysHistoricFixings = false };

        Assert.ThrowsAny<Exception>(() => cms10y2y.Fixing(d.RefDate - 1, context));
        Assert.Null(Record.Exception(() => cms10y2y.Fixing(d.RefDate, context)));
        Assert.Equal(cms10y.Fixing(d.RefDate, context) - cms2y.Fixing(d.RefDate, context), cms10y2y.Fixing(d.RefDate, context));
        context = context with { Fixings = context.Fixings.AddFixing(cms10y, d.RefDate, 0.05) };
        Assert.Equal(cms10y.Fixing(d.RefDate, context) - cms2y.Fixing(d.RefDate, context), cms10y2y.Fixing(d.RefDate, context));
        context = context with { Fixings = context.Fixings.AddFixing(cms2y, d.RefDate, 0.04) };
        Assert.Equal(cms10y.Fixing(d.RefDate, context) - cms2y.Fixing(d.RefDate, context), cms10y2y.Fixing(d.RefDate, context));
        Date futureFixingDate = new TARGET().Adjust(d.RefDate + (1 * TimeUnit.Years));
        Assert.Equal(
            cms10y.Fixing(futureFixingDate, context) - cms2y.Fixing(futureFixingDate, context),
            cms10y2y.Fixing(futureFixingDate, context));

        // IndexManager::instance().clearHistories()
        context = d.Context with { EnforcesTodaysHistoricFixings = true };
        Assert.ThrowsAny<Exception>(() => cms10y2y.Fixing(d.RefDate, context));
        context = context with { Fixings = context.Fixings.AddFixing(cms10y, d.RefDate, 0.05) };
        Assert.ThrowsAny<Exception>(() => cms10y2y.Fixing(d.RefDate, context));
        context = context with { Fixings = context.Fixings.AddFixing(cms2y, d.RefDate, 0.04) };
        Assert.Equal(cms10y.Fixing(d.RefDate, context) - cms2y.Fixing(d.RefDate, context), cms10y2y.Fixing(d.RefDate, context));
    }

    private sealed class TestData
    {
        public TestData()
        {
            RefDate = new Date(23, February, 2018);
            Context = new EvaluationContext { EvaluationDate = RefDate };

            Yts2 = new FlatForward(RefDate, 0.02, new Actual365Fixed());
        }

        public Date RefDate { get; }

        public EvaluationContext Context { get; }

        public YieldTermStructure Yts2 { get; }
    }
}
