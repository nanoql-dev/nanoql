// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The range accrual leg's null entries: QuantLib's <c>RangeAccrualLeg</c> builds <c>Leg leg(n)</c>, n null cash
/// flows, then pushes its n coupons after them (<c>rangeaccrual.cpp</c>, its <c>operator Leg</c>), so the leg holds 2n
/// flows, the first n null, which setCouponPricer and every reader of the leg dereference: on the wheel, the vectors'
/// semiannual leg of four periods has eight flows, and setCouponPricer refuses it ("Boost assertion failed: px != 0").
/// nanoql's leg holds the n coupons. QuantLib's check: its coupons, the leg's second half, bit for bit (the vectors).
/// </summary>
public class RangeAccrualLegTests
{
    [Fact]
    public void TheLegHoldsItsCouponsAlone()
    {
        VectorCase c = VectorFile.Load("cashflows/rangeaccrual").Cases
            .Single(x => x.Name.StartsWith("leg semiannual, weekly /", StringComparison.Ordinal));
        EvaluationContext context = c.Context();
        IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
        RangeAccrualLeg builder = RangeAccrualVectorTests.LegOf(c.Input("leg"), index, context);
        ImmutableArray<CashFlow> leg = CouponPricerFunctions.SetCouponPricer(
            builder.ToLeg(context), RangeAccrualVectorTests.PricerOf(c.Input("pricer"), context));

        Assert.Equal(4, leg.Length);
        Assert.All(leg, f => Assert.IsType<RangeAccrualFloatersCoupon>(f));
        Date[] expected = [.. c.Expected.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
        Assert.Equal(expected, leg.Select(f => f.Date));
    }
}
