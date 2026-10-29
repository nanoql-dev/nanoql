// SPDX-FileCopyrightText: Copyright (C) 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/cmsspread.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CmsSpreadTests
{
    // Adapted: TestData's evaluation date and its enforcesTodaysHistoricFixings switch are the context's; the indexes'
    // fixings are the context's fixing store, addFixing returning a new store, and clearHistories a context with none;
    // a pricer set on a coupon is a copy of the coupon holding it, and the Monte Carlo reference's mean a plain sum.
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

    [Fact]
    public void TestCouponPricing()
    {
        // Testing pricing of cms spread coupons...

        var d = new TestData();
        double tol = 1E-6; // abs tolerance coupon rate

        SwapIndex cms10y = new EuriborSwapIsdaFixA(10 * TimeUnit.Years, d.Yts2, d.Yts2);
        SwapIndex cms2y = new EuriborSwapIsdaFixA(2 * TimeUnit.Years, d.Yts2, d.Yts2);
        var cms10y2y = new SwapSpreadIndex("cms10y2y", cms10y, cms2y);

        Date valueDate = cms10y2y.ValueDate(d.RefDate);
        Date payDate = valueDate + (1 * TimeUnit.Years);
        CmsCoupon cpn1a = new CmsCoupon(
            payDate, 10000.0, valueDate, payDate, cms10y.FixingDays, cms10y, 1.0, 0.0, null, null, new Actual360(), false)
            .SetPricer(d.CmsPricerLn);
        CmsCoupon cpn1b = new CmsCoupon(
            payDate, 10000.0, valueDate, payDate, cms2y.FixingDays, cms2y, 1.0, 0.0, null, null, new Actual360(), false)
            .SetPricer(d.CmsPricerLn);
        CmsSpreadCoupon cpn1 = new CmsSpreadCoupon(
            payDate, 10000.0, valueDate, payDate, cms10y2y.FixingDays, cms10y2y, 1.0, 0.0, null, null, new Actual360(),
            false).SetPricer(d.CmsspPricerLn);
        Assert.Equal(d.RefDate, cpn1.FixingDate());

        const double eqTol = 100 * QL_EPSILON;
        EvaluationContext context = d.Context;
        CheckClose(cpn1.Rate(context), cpn1a.Rate(context) - cpn1b.Rate(context), eqTol);
        context = context with { Fixings = context.Fixings.AddFixing(cms10y, d.RefDate, 0.05) };
        CheckClose(cpn1.Rate(context), cpn1a.Rate(context) - cpn1b.Rate(context), eqTol);
        context = context with { Fixings = context.Fixings.AddFixing(cms2y, d.RefDate, 0.03) };
        CheckClose(cpn1.Rate(context), cpn1a.Rate(context) - cpn1b.Rate(context), eqTol);

        // IndexManager::instance().clearHistories()
        context = d.Context;

        var cpn2a = new CmsCoupon(
            new Date(23, February, 2029), 10000.0, new Date(23, February, 2028), new Date(23, February, 2029), 2, cms10y,
            1.0, 0.0, null, null, new Actual360(), false);
        var cpn2b = new CmsCoupon(
            new Date(23, February, 2029), 10000.0, new Date(23, February, 2028), new Date(23, February, 2029), 2, cms2y,
            1.0, 0.0, null, null, new Actual360(), false);

        var plainCpn = new CappedFlooredCmsSpreadCoupon(
            new Date(23, February, 2029), 10000.0, new Date(23, February, 2028), new Date(23, February, 2029), 2,
            cms10y2y, 1.0, 0.0, null, null, null, null, new Actual360(), false);
        var cappedCpn = new CappedFlooredCmsSpreadCoupon(
            new Date(23, February, 2029), 10000.0, new Date(23, February, 2028), new Date(23, February, 2029), 2,
            cms10y2y, 1.0, 0.0, 0.03, null, null, null, new Actual360(), false);
        var flooredCpn = new CappedFlooredCmsSpreadCoupon(
            new Date(23, February, 2029), 10000.0, new Date(23, February, 2028), new Date(23, February, 2029), 2,
            cms10y2y, 1.0, 0.0, null, 0.01, null, null, new Actual360(), false);
        var collaredCpn = new CappedFlooredCmsSpreadCoupon(
            new Date(23, February, 2029), 10000.0, new Date(23, February, 2028), new Date(23, February, 2029), 2,
            cms10y2y, 1.0, 0.0, 0.03, 0.01, null, null, new Actual360(), false);

        (CmsCouponPricer Cms, CmsSpreadCouponPricer Spread, SwaptionVolatilityStructure Vol)[] models =
        [
            (d.CmsPricerLn, d.CmsspPricerLn, d.SwLn),
            (d.CmsPricerSln, d.CmsspPricerSln, d.SwSln),
            (d.CmsPricerN, d.CmsspPricerN, d.SwN),
        ];
        foreach ((CmsCouponPricer cmsPricer, CmsSpreadCouponPricer cmsspPricer, SwaptionVolatilityStructure vol) in models)
        {
            CmsCoupon a = cpn2a.SetPricer(cmsPricer);
            CmsCoupon b = cpn2b.SetPricer(cmsPricer);

            CheckSmall(
                Math.Abs(plainCpn.SetPricer(cmsspPricer).Rate(context)
                    - McReferenceValue(a, b, double.MaxValue, -double.MaxValue, vol, d.Correlation, context)),
                tol);
            CheckSmall(
                Math.Abs(cappedCpn.SetPricer(cmsspPricer).Rate(context)
                    - McReferenceValue(a, b, 0.03, -double.MaxValue, vol, d.Correlation, context)),
                tol);
            CheckSmall(
                Math.Abs(flooredCpn.SetPricer(cmsspPricer).Rate(context)
                    - McReferenceValue(a, b, double.MaxValue, 0.01, vol, d.Correlation, context)),
                tol);
            CheckSmall(
                Math.Abs(collaredCpn.SetPricer(cmsspPricer).Rate(context)
                    - McReferenceValue(a, b, 0.03, 0.01, vol, d.Correlation, context)),
                tol);
        }
    }

    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // QuantLib's Monte Carlo reference: the two swap rates jointly (shifted) lognormal or normal about their adjusted
    // fixings, by a million points of a two-dimensional Sobol sequence seeded 42, the spread floored and capped.
    private static double McReferenceValue(
        CmsCoupon cpn1,
        CmsCoupon cpn2,
        double cap,
        double floor,
        SwaptionVolatilityStructure vol,
        double correlation,
        EvaluationContext context)
    {
        int samples = 1000000;
        double sum = 0.0;
        var cov = new Matrix(2, 2);
        var index1 = (SwapIndex)cpn1.Index;
        var index2 = (SwapIndex)cpn2.Index;
        cov[0, 0] = vol.BlackVariance(cpn1.FixingDate(), index1.Tenor, cpn1.IndexFixing(context));
        cov[1, 1] = vol.BlackVariance(cpn2.FixingDate(), index2.Tenor, cpn2.IndexFixing(context));
        cov[0, 1] = cov[1, 0] = Math.Sqrt(cov[0, 0] * cov[1, 1]) * correlation;
        Matrix c = PseudoSqrtFunctions.PseudoSqrt(cov);

        var atmRate = new Vector(2);
        var adjRate = new Vector(2);
        var avg = new Vector(2);
        var volShift = new Vector(2);
        atmRate[0] = cpn1.IndexFixing(context);
        atmRate[1] = cpn2.IndexFixing(context);
        adjRate[0] = cpn1.AdjustedFixing(context);
        adjRate[1] = cpn2.AdjustedFixing(context);
        if (vol.VolatilityType == VolatilityType.ShiftedLognormal)
        {
            volShift[0] = vol.Shift(cpn1.FixingDate(), index1.Tenor);
            volShift[1] = vol.Shift(cpn2.FixingDate(), index2.Tenor);
            avg[0] = Math.Log((adjRate[0] + volShift[0]) / (atmRate[0] + volShift[0])) - (0.5 * cov[0, 0]);
            avg[1] = Math.Log((adjRate[1] + volShift[1]) / (atmRate[1] + volShift[1])) - (0.5 * cov[1, 1]);
        }
        else
        {
            avg[0] = adjRate[0];
            avg[1] = adjRate[1];
        }

        var icn = new InverseCumulativeNormal();
        var sb = new SobolRsg(2, 42);
        var w = new Vector(2);
        for (int i = 0; i < samples; ++i)
        {
            ReadOnlySpan<double> seq = sb.NextSequence().Value.Span;
            w[0] = icn.Evaluate(seq[0]);
            w[1] = icn.Evaluate(seq[1]);
            Vector z = (c * w) + avg;
            for (int j = 0; j < 2; ++j)
            {
                if (vol.VolatilityType == VolatilityType.ShiftedLognormal)
                {
                    z[j] = ((atmRate[j] + volShift[j]) * Math.Exp(z[j])) - volShift[j];
                }
            }

            sum += Math.Min(Math.Max(z[0] - z[1], floor), cap);
        }

        return sum / samples;
    }

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |a - b| within the tolerance, in percent, of both values.
    private static void CheckClose(double a, double b, double tolerancePercent)
    {
        double diff = Math.Abs(a - b);
        Assert.True(
            diff <= tolerancePercent / 100.0 * Math.Abs(a) && diff <= tolerancePercent / 100.0 * Math.Abs(b),
            $"difference between {a} and {b} exceeds {tolerancePercent}%");
    }

    // QL_CHECK_SMALL, Boost's BOOST_CHECK_SMALL: |a| within the tolerance.
    private static void CheckSmall(double a, double tolerance) =>
        Assert.True(Math.Abs(a) <= tolerance, $"{a} exceeds {tolerance}");

    private sealed class TestData
    {
        public TestData()
        {
            RefDate = new Date(23, February, 2018);
            Context = new EvaluationContext { EvaluationDate = RefDate };

            Yts2 = new FlatForward(RefDate, 0.02, new Actual365Fixed());

            SwLn = new ConstantSwaptionVolatility(
                RefDate, new TARGET(), BusinessDayConvention.Following, 0.20, new Actual365Fixed(),
                VolatilityType.ShiftedLognormal, 0.0);
            SwSln = new ConstantSwaptionVolatility(
                RefDate, new TARGET(), BusinessDayConvention.Following, 0.10, new Actual365Fixed(),
                VolatilityType.ShiftedLognormal, 0.01);
            SwN = new ConstantSwaptionVolatility(
                RefDate, new TARGET(), BusinessDayConvention.Following, 0.0075, new Actual365Fixed(),
                VolatilityType.Normal, 0.01);

            Reversion = 0.01;
            CmsPricerLn = new LinearTsrPricer(SwLn, Reversion, Yts2);
            CmsPricerSln = new LinearTsrPricer(SwSln, Reversion, Yts2);
            CmsPricerN = new LinearTsrPricer(SwN, Reversion, Yts2);

            Correlation = 0.6;
            CmsspPricerLn = new LognormalCmsSpreadPricer(CmsPricerLn, Correlation, Yts2, 32);
            CmsspPricerSln = new LognormalCmsSpreadPricer(CmsPricerSln, Correlation, Yts2, 32);
            CmsspPricerN = new LognormalCmsSpreadPricer(CmsPricerN, Correlation, Yts2, 32);
        }

        public Date RefDate { get; }

        public EvaluationContext Context { get; }

        public YieldTermStructure Yts2 { get; }

        public SwaptionVolatilityStructure SwLn { get; }

        public SwaptionVolatilityStructure SwSln { get; }

        public SwaptionVolatilityStructure SwN { get; }

        public double Reversion { get; }

        public double Correlation { get; }

        public CmsCouponPricer CmsPricerLn { get; }

        public CmsCouponPricer CmsPricerSln { get; }

        public CmsCouponPricer CmsPricerN { get; }

        public CmsSpreadCouponPricer CmsspPricerLn { get; }

        public CmsSpreadCouponPricer CmsspPricerSln { get; }

        public CmsSpreadCouponPricer CmsspPricerN { get; }
    }
}
