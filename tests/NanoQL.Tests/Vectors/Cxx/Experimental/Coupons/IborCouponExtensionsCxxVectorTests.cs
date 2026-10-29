// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's quanto pricer, proxy index and stripped coupon vectors,
/// <c>tests/vectors/cxx/experimental/coupons/iborcouponextensions.json</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IborCouponExtensionsCxxVectorTests
{
    [Fact]
    public void IborCouponExtensionsCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/coupons/iborcouponextensions");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            var reference = new Date(a[0].GetInt32());
            var context = new EvaluationContext { EvaluationDate = reference };
            YieldTermStructure curve = new FlatForward(reference, a[1].GetDouble(), new Actual365Fixed());
            var index = new Euribor6M(curve);
            Date start = new TARGET().Advance(reference, 1, TimeUnit.Years);
            Date end = new TARGET().Advance(start, 6, TimeUnit.Months);
            if (c.Name.StartsWith("quanto", StringComparison.Ordinal))
            {
                var pricer = new BlackIborQuantoCouponPricer(
                    new BlackConstantVol(reference, new TARGET(), a[5].GetDouble(), new Actual365Fixed()),
                    a[6].GetDouble(),
                    OptionletVol(reference, a[2].GetDouble(), a[3].GetBoolean(), a[4].GetDouble()),
                    context);
                IborCoupon coupon = new IborCoupon(end, 100.0, start, end, 2, index).SetPricer(pricer);
                check.Output(c, "swaplet", pricer.SwapletRate(coupon, context));
                check.Output(c, "caplet", pricer.CapletRate(coupon, a[7].GetDouble(), context));
                check.Output(c, "floorlet", pricer.FloorletRate(coupon, a[8].GetDouble(), context));
            }
            else if (c.Name.StartsWith("proxy", StringComparison.Ordinal))
            {
                var proxy = new ProxyIbor(
                    "PX", 6 * TimeUnit.Months, 2, new EURCurrency(), new TARGET(), BusinessDayConvention.ModifiedFollowing,
                    false, new Actual360(), a[2].GetDouble(), index, a[3].GetDouble());
                Date fixingDate = proxy.FixingDate(start);
                Date past = new TARGET().Advance(reference, -5, TimeUnit.Days);
                EvaluationContext withFixing = context with { Fixings = context.Fixings.AddFixing(proxy, past, a[4].GetDouble()) };
                check.Output(c, "name", proxy.Name);
                check.Output(c, "fixingDate", fixingDate.SerialNumber);
                check.Output(c, "valueDate", proxy.ValueDate(fixingDate).SerialNumber);
                check.Output(c, "maturityDate", proxy.MaturityDate(proxy.ValueDate(fixingDate)).SerialNumber);
                check.Output(c, "pastFixing", proxy.Fixing(past, withFixing));
            }
            else
            {
                double? cap = a[3].ValueKind == JsonValueKind.Null ? null : a[3].GetDouble();
                double? floor = a[4].ValueKind == JsonValueKind.Null ? null : a[4].GetDouble();
                var pricer = new BlackIborCouponPricer(context, OptionletVol(reference, a[2].GetDouble(), false, 0.0));
                CappedFlooredCoupon capped = new CappedFlooredCoupon(new IborCoupon(end, 100.0, start, end, 2, index), cap, floor)
                    .SetPricer(pricer);
                var stripped = new StrippedCappedFlooredCoupon(capped);
                check.Output(c, "underlying", capped.Underlying.Rate(context));
                check.Output(c, "capped", stripped.Underlying.Rate(context));
                check.Output(c, "stripped", stripped.Rate(context));
                check.Output(c, "cap", stripped.Cap);
                check.Output(c, "floor", stripped.Floor);
                check.Output(c, "effectiveCap", stripped.EffectiveCap);
                check.Output(c, "effectiveFloor", stripped.EffectiveFloor);
                check.Output(c, "isCap", stripped.IsCap);
                check.Output(c, "isFloor", stripped.IsFloor);
                check.Output(c, "isCollar", stripped.IsCollar);
                check.Output(c, "convexityAdjustment", stripped.ConvexityAdjustment(context));
                var leg = new List<CashFlow>();
                for (int i = 0; i < 3; ++i)
                {
                    Date s = new TARGET().Advance(start, 6 * i, TimeUnit.Months), e = new TARGET().Advance(s, 6, TimeUnit.Months);
                    leg.Add(new CappedFlooredCoupon(new IborCoupon(e, 100.0, s, e, 2, index), cap, floor).SetPricer(pricer));
                }

                ImmutableArray<CashFlow> strippedLeg = new StrippedCappedFlooredCouponLeg(leg).ToLeg();
                check.Output(c, "legAmounts", strippedLeg.Select(cf => cf.Amount(context)).ToArray());
            }
        }

        check.Complete();
    }

    private static OptionletVolatilityStructure OptionletVol(Date reference, double vol, bool normal, double shift) =>
        new ConstantOptionletVolatility(
            reference, new TARGET(), BusinessDayConvention.Following, vol, new Actual365Fixed(),
            normal ? VolatilityType.Normal : VolatilityType.ShiftedLognormal, shift);
}
