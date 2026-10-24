// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The CMS pricers' state (the S-cms plan's CMR1 (i) and (ii)): each query builds QuantLib's <c>initialize</c> state
/// for itself, so two queries in either order, or on any number of threads, give the same bits; and a capped or
/// collared coupon's rate reads its three rates from one state, as QuantLib's one <c>initialize</c> serves them, with
/// the bits of the three public calls.
/// </summary>
public class CmsPricerStateTests
{
    private static readonly SwapIndex Index = CmsInputs.SwapIndex(new Period(10, TimeUnit.Years));

    [Fact]
    public void QueriesDontDependOnEachOther()
    {
        var pricer = new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.NonParallelShifts, 0.03);
        CmsCoupon a = CmsInputs.Coupon(Index, 5), b = CmsInputs.Coupon(Index, 10);
        double capletFirst = pricer.CapletRate(a, 0.04, CmsInputs.Context);
        double swapletAfter = pricer.SwapletRate(b, CmsInputs.Context);
        double swapletFirst = pricer.SwapletRate(b, CmsInputs.Context);
        double capletAfter = pricer.CapletRate(a, 0.04, CmsInputs.Context);
        Assert.Equal(capletFirst, capletAfter);
        Assert.Equal(swapletFirst, swapletAfter);
    }

    [Fact]
    public void OnePricerServesEveryThread()
    {
        var pricer = new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.NonParallelShifts, 0.03);
        CmsCoupon[] coupons = [.. new[] { 1, 5, 10, 15 }.Select(t => CmsInputs.Coupon(Index, t))];
        double[] expected = [.. coupons.Select(c => pricer.SwapletRate(c, CmsInputs.Context))];
        var results = new double[8][];
        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, k =>
        {
            results[k] = [.. Enumerable.Range(0, 4).Select(i => pricer.SwapletRate(coupons[(i + k) % 4], CmsInputs.Context))];
        });
        for (int k = 0; k < 8; k++)
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(expected[(i + k) % 4], results[k][i]);
            }
        }
    }

    [Fact]
    public void ACollaredRateBuildsOneState()
    {
        foreach (CmsCouponPricer pricer in new CmsCouponPricer[]
        {
            new AnalyticHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0),
            new LinearTsrPricer(CmsInputs.Volatility(), 0.0),
        })
        {
            (Date start, Date end) = (CmsInputs.Coupon(Index).AccrualStartDate, CmsInputs.Coupon(Index).Date);
            var collar = new CappedFlooredCmsCoupon(end, 1.0, start, end, 2, Index, 1.0, 0.0, 0.05, 0.02, null, null, new Actual360());
            var priced = (CappedFlooredCmsCoupon)collar.SetPricer(pricer);
            var underlying = (CmsCoupon)priced.Underlying;

            // the bits of the three public calls
            double three = pricer.SwapletRate(underlying, CmsInputs.Context)
                + pricer.FloorletRate(underlying, priced.EffectiveFloor!.Value, CmsInputs.Context)
                - pricer.CapletRate(underlying, priced.EffectiveCap!.Value, CmsInputs.Context);
            Assert.Equal(three, priced.Rate(CmsInputs.Context));

            // one state: the collared rate allocates well under what the three public calls do, each building its own
            // (about 0.34 of them on the analytic Hagan pricer, 0.60 on the TSR, whose optionlets integrate)
            priced.Rate(CmsInputs.Context);
            long collared = Allocated(() => priced.Rate(CmsInputs.Context));
            long calls = Allocated(() => pricer.SwapletRate(underlying, CmsInputs.Context))
                + Allocated(() => pricer.CapletRate(underlying, priced.EffectiveCap!.Value, CmsInputs.Context))
                + Allocated(() => pricer.FloorletRate(underlying, priced.EffectiveFloor!.Value, CmsInputs.Context));
            Assert.True(collared <= 0.75 * calls, $"{pricer.GetType().Name}: {collared} bytes against {calls}");
        }
    }

    private static long Allocated(Func<double> rate)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = rate();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
