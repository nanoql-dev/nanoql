// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's average-BMA vectors, <c>tests/vectors/cxx/cashflows/averagebmacoupon.json</c>:
/// <see cref="AverageBMACoupon"/> built directly, its fixing dates, fixings and rate, the refusal of averaging days
/// that differ from the interest days among them; and <see cref="AverageBMALeg"/> with every setter, its gearings and
/// spreads among them, and its refusal of a leg with no notional, before it reads the schedule's size.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AverageBMACouponCxxVectorTests
{
    [Fact]
    public void AverageBMACouponCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/averagebmacoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("coupon", out JsonElement spec))
            {
                var coupon = new Lazy<AverageBMACoupon>(() => CouponOf(spec));
                check.Output(c, "fixingDates", () => coupon.Value.FixingDates().Select(d => (object)d));
                check.Output(c, "indexFixings", () => coupon.Value.IndexFixings(context));
                check.Output(c, "rate", () => coupon.Value.Rate(context));
                continue;
            }

            var leg = new Lazy<AverageBMACoupon[]>(() => [.. LegOf(c.Input("leg"), context).Cast<AverageBMACoupon>()]);
            check.Output(c, "date", Each(leg, x => x.Date));
            check.Output(c, "nominal", Each(leg, x => x.Nominal(context)));
            check.Output(c, "accrualStartDate", Each(leg, x => x.AccrualStartDate));
            check.Output(c, "accrualEndDate", Each(leg, x => x.AccrualEndDate));
            check.Output(c, "referencePeriodStart", Each(leg, x => x.ReferencePeriodStart));
            check.Output(c, "referencePeriodEnd", Each(leg, x => x.ReferencePeriodEnd));
            check.Output(c, "dayCounter", Each(leg, x => x.DayCounter.Name));
            check.Output(c, "gearing", Each(leg, x => x.Gearing));
            check.Output(c, "spread", Each(leg, x => x.Spread));
            check.Output(c, "fixingDates", Each(leg, x => x.FixingDates().Select(d => (object)d)));
        }

        check.Complete();
    }

    private static AverageBMACoupon CouponOf(JsonElement spec) =>
        new(
            new Date(spec.GetProperty("paymentDate").GetInt32()),
            spec.GetProperty("nominal").GetDouble(),
            new Date(spec.GetProperty("startDate").GetInt32()),
            new Date(spec.GetProperty("endDate").GetInt32()),
            new BMAIndex(IndexInputs.CurveOf(spec.GetProperty("indexCurve"))),
            spec.GetProperty("gearing").GetDouble(),
            spec.GetProperty("spread").GetDouble(),
            CashFlowInputs.NullableDate(spec.GetProperty("refPeriodStart")),
            CashFlowInputs.NullableDate(spec.GetProperty("refPeriodEnd")),
            CouponInputs.DayCounterOrNull(spec.GetProperty("dayCounter")));

    // Each setter is called as the harness calls it: an empty list leaves it uncalled.
    private static ImmutableArray<CashFlow> LegOf(JsonElement spec, EvaluationContext context)
    {
        AverageBMALeg builder = new AverageBMALeg(
                CouponInputs.ScheduleOf(spec.GetProperty("schedule"), context),
                new BMAIndex(IndexInputs.CurveOf(spec.GetProperty("indexCurve"))))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentAdjustment").GetInt32());
        double[] notionals = CouponInputs.Reals(spec.GetProperty("notionals"));
        if (notionals.Length > 0)
        {
            builder = builder.WithNotionals(notionals);
        }

        if (CouponInputs.DayCounterOrNull(spec.GetProperty("paymentDayCounter")) is DayCounter dc)
        {
            builder = builder.WithPaymentDayCounter(dc);
        }

        // a number calls the setter taking one value, a list the one taking several
        JsonElement gearings = spec.GetProperty("gearings");
        if (gearings.ValueKind == JsonValueKind.Number)
        {
            builder = builder.WithGearings(gearings.GetDouble());
        }
        else if (gearings.GetArrayLength() > 0)
        {
            builder = builder.WithGearings(CouponInputs.Reals(gearings));
        }

        JsonElement spreads = spec.GetProperty("spreads");
        if (spreads.ValueKind == JsonValueKind.Number)
        {
            builder = builder.WithSpreads(spreads.GetDouble());
        }
        else if (spreads.GetArrayLength() > 0)
        {
            builder = builder.WithSpreads(CouponInputs.Reals(spreads));
        }

        return builder.ToLeg();
    }

    // One output per coupon, or one throw for the output where the leg is refused.
    private static object Each<TOut>(Lazy<AverageBMACoupon[]> leg, Func<AverageBMACoupon, TOut> f) =>
        (Func<object?>)(() => leg.Value.Select(x => (object?)f(x)).ToArray());
}
