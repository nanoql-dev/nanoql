// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The digital legs' zero-gearing period: QuantLib's <c>FloatingDigitalLeg</c> builds a period of zero gearing as a
/// fixed coupon at the period's spread, read with a default of 1.0 (<c>cashflowvectors.hpp:282-286</c>), where
/// <c>FloatingLeg</c> reads 0.0, so with no spreads given the period pays 100%: on the C++ harness, a two-year
/// semiannual digital Ibor leg from 17 January 2025 with gearings (0, 1) and no spreads pays its first coupon at a
/// fixed 1.0, at 0.0 with a spread of 0 given and at 0.01 with 0.01. nanoql reads 0.0, as <c>FloatingLeg</c> does.
/// QuantLib's check: its Ibor leg on the same schedule and gearings, whose first coupon is fixed at 0.0 with no spreads
/// given (the wheel).
/// </summary>
public class FloatingDigitalLegSpreadTests
{
    [Fact]
    public void AZeroGearingPeriodWithoutASpreadPaysNothing()
    {
        VectorCase c = VectorFile.Load("cxx/cashflows/digitaliborcoupon").Cases.Single(x => x.Name == "cash calls at 5%");
        EvaluationContext context = c.Context();
        DigitalIborLeg builder = DigitalIborCouponCxxVectorTests.BuilderOf(c.Input("leg"), context).WithGearings([0.0, 1.0]);
        foreach ((double[] spreads, double expected) in new[] { (Array.Empty<double>(), 0.0), ([0.0], 0.0), ([0.01], 0.01) })
        {
            ImmutableArray<CashFlow> leg = DigitalIborCouponCxxVectorTests.Priced(builder.WithSpreads(spreads), c.Input("leg"), context);
            var fixedCoupon = Assert.IsType<FixedRateCoupon>(leg[0]);
            Assert.Equal(expected, fixedCoupon.Rate(context));

            // the next period QuantLib's: 0.030808939221519362 with no spread, 0.04080893922151936 with 0.01
            double next = ((DigitalCoupon)leg[1]).Rate(context);
            Assert.True(Math.Abs(next - (spreads.Length == 0 ? 0.030808939221519362 : 0.030808939221519362 + spreads[0])) <= 1e-15, $"{next}");
        }
    }

    [Fact]
    public void TheCmsLegReadsTheSameDefault()
    {
        VectorCase c = VectorFile.Load("cxx/cashflows/digitalcmscoupon").Cases.Single(x => x.Name == "cash calls at 5%");
        EvaluationContext context = c.Context();
        DigitalCmsLeg builder = DigitalCmsCouponCxxVectorTests.BuilderOf(c.Input("leg"), context).WithGearings([0.0, 1.0]);
        ImmutableArray<CashFlow> leg = DigitalCmsCouponCxxVectorTests.Priced(builder, c.Input("leg"), context);
        Assert.Equal(0.0, Assert.IsType<FixedRateCoupon>(leg[0]).Rate(context));
        Assert.IsType<DigitalCmsCoupon>(leg[1]);
    }
}
