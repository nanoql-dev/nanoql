// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's overnight leg vectors, <c>tests/vectors/cxx/cashflows/overnightindexedcoupon.json</c>:
/// <see cref="OvernightLeg"/>'s setters the wheel doesn't reach, <see cref="OvernightLeg.WithLastRecentPeriod"/>,
/// <see cref="OvernightLeg.WithLastRecentPeriodCalendar"/> and <see cref="OvernightLeg.WithCouponPricer"/>, with each
/// cash flow's type, dates, nominal, rate-computation dates, pricers, rate and amount.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexedCouponCxxVectorTests
{
    [Fact]
    public void OvernightIndexedCouponCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/overnightindexedcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("leg");
            OvernightIndex index = OvernightInputs.IndexOf(spec.GetProperty("index"));
            ImmutableArray<CashFlow> leg = default;
            check.Output(c, "size", () =>
            {
                leg = OvernightInputs.LegBuilderOf(spec, index, context).ToLeg();
                return leg.Length;
            });
            if (leg.IsDefault)
            {
                continue;
            }

            Coupon[] coupons = leg.Cast<Coupon>().ToArray();
            check.Output(c, "types", leg.Select(OvernightInputs.TypeName).ToArray());
            check.Output(c, "paymentDates", coupons.Select(x => x.Date).ToArray());
            check.Output(c, "accrualStartDates", coupons.Select(x => x.AccrualStartDate).ToArray());
            check.Output(c, "accrualEndDates", coupons.Select(x => x.AccrualEndDate).ToArray());
            check.Output(c, "referencePeriodStarts", coupons.Select(x => x.ReferencePeriodStart).ToArray());
            check.Output(c, "referencePeriodEnds", coupons.Select(x => x.ReferencePeriodEnd).ToArray());
            check.Output(c, "nominals", coupons.Select(x => x.Nominal(context)).ToArray());
            check.Output(c, "rateComputationStartDates", coupons.Select(x => Underlying(x)?.RateComputationStartDate).ToArray());
            check.Output(c, "rateComputationEndDates", coupons.Select(x => Underlying(x)?.RateComputationEndDate).ToArray());
            check.Output(c, "pricers", coupons.Select(x => (x as FloatingRateCoupon)?.Pricer?.GetType().Name).ToArray());
            check.Output(
                c,
                "underlyingPricers",
                coupons.Select(x => (x as CappedFlooredOvernightIndexedCoupon)?.Underlying.Pricer?.GetType().Name).ToArray());
            check.Output(c, "rates", coupons.Select(x => (Func<object?>)(() => x.Rate(context))).ToArray());
            check.Output(c, "amounts", coupons.Select(x => (Func<object?>)(() => x.Amount(context))).ToArray());
        }

        check.Complete();
    }

    private static OvernightIndexedCoupon? Underlying(Coupon c) => c switch
    {
        CappedFlooredOvernightIndexedCoupon cf => cf.Underlying,
        OvernightIndexedCoupon on => on,
        _ => null,
    };
}
