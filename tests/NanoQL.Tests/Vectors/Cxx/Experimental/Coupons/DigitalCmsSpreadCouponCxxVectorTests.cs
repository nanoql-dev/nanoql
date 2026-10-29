// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The digital CMS spread legs' harness vectors,
/// <c>tests/vectors/cxx/experimental/coupons/digitalcmsspreadcoupon.json</c>: legs on a 10Y-2Y swap spread
/// index priced by the lognormal CMS spread pricer; per coupon the inspectors and the rates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DigitalCmsSpreadCouponCxxVectorTests
{
    [Fact]
    public void DigitalCmsSpreadCouponCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/coupons/digitalcmsspreadcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("leg");
            if (c.Expected.TryGetProperty("leg", out _))
            {
                check.Output(c, "leg", () => LegOf(spec, context));
                continue;
            }

            DigitalInputs.CheckLeg(check, c, LegOf(spec, context), context, true);
        }

        check.Complete();
    }

    // The leg a spec describes: tools/cxx/src/cashflows/digitalspec.hpp's leg spec, each list through the setter
    // taking a list, on the spec's lognormal pricer.
    private static ImmutableArray<CashFlow> LegOf(JsonElement spec, EvaluationContext context) =>
        Priced(BuilderOf(spec, context).WithNakedOption(spec.GetProperty("nakedOption").GetBoolean()), spec, context);

    /// <summary>A builder's leg on a spec's pricer: the lognormal CMS spread pricer over the spec's CMS pricer.</summary>
    /// <param name="leg">The builder.</param>
    /// <param name="spec">The leg spec, whose "pricer" is {"cms", "correlation"}.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The priced leg.</returns>
    internal static ImmutableArray<CashFlow> Priced(DigitalCmsSpreadLeg leg, JsonElement spec, EvaluationContext context)
    {
        JsonElement pricer = spec.GetProperty("pricer");
        return CouponPricerFunctions.SetCouponPricer(
            leg.ToLeg(),
            new LognormalCmsSpreadPricer(
                ConundrumPricerCxxVectorTests.PricerOf(pricer.GetProperty("cms"), context),
                pricer.GetProperty("correlation").GetDouble()));
    }

    /// <summary>A leg spec's builder, every setting but the naked option flag.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The builder.</returns>
    internal static DigitalCmsSpreadLeg BuilderOf(JsonElement spec, EvaluationContext context) =>
        new DigitalCmsSpreadLeg(CouponInputs.ScheduleOf(spec.GetProperty("schedule"), context), new SwapSpreadIndex(
                "CMS10Y2Y",
                SwaptionInputs.SwapIndexOf(spec.GetProperty("index1")),
                SwaptionInputs.SwapIndexOf(spec.GetProperty("index2"))))
            .WithNotionals(CouponInputs.Reals(spec.GetProperty("notionals")))
            .WithPaymentDayCounter(CashFlowInputs.DayCounterOf(spec.GetProperty("paymentDayCounter")))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentAdjustment").GetInt32())
            .WithFixingDays(spec.GetProperty("fixingDays").EnumerateArray().Select(CouponInputs.NullableInt))
            .WithGearings(CouponInputs.Reals(spec.GetProperty("gearings")))
            .WithSpreads(CouponInputs.Reals(spec.GetProperty("spreads")))
            .InArrears(spec.GetProperty("inArrears").GetBoolean())
            .WithCallStrikes(Rates(spec.GetProperty("callStrikes")))
            .WithLongCallOption((PositionType)spec.GetProperty("longCallOption").GetInt32())
            .WithCallAtm(spec.GetProperty("callATM").GetBoolean())
            .WithCallPayoffs(Rates(spec.GetProperty("callPayoffs")))
            .WithPutStrikes(Rates(spec.GetProperty("putStrikes")))
            .WithLongPutOption((PositionType)spec.GetProperty("longPutOption").GetInt32())
            .WithPutAtm(spec.GetProperty("putATM").GetBoolean())
            .WithPutPayoffs(Rates(spec.GetProperty("putPayoffs")))
            .WithReplication(DigitalInputs.ReplicationOf(spec.GetProperty("replication")));

    private static IEnumerable<double?> Rates(JsonElement e) => e.EnumerateArray().Select(CouponInputs.NullableReal);
}
