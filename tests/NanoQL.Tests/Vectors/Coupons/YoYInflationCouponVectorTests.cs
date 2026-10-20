// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The year-on-year inflation coupon vectors, <c>tests/vectors/cashflows/yoyinflationcoupon.json</c>:
/// <see cref="YoYInflationCoupon"/>'s fixing, rate and amount under a Black pricer, whose swaplet is the base pricer's,
/// and <see cref="YoYInflationLeg"/>'s flows with QuantLib's default pricer.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class YoYInflationCouponVectorTests
{
    [Fact]
    public void YoYInflationCouponVector()
    {
        var file = VectorFile.Load("cashflows/yoyinflationcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            var yii = (YoYInflationIndex)index;
            if (c.String("kind") == "coupon")
            {
                YoYOptionletVolatilitySurface vol = ConstantYoYOptionletVolatility.Create(
                    0.01,
                    0,
                    new UnitedKingdom(),
                    BusinessDayConvention.ModifiedFollowing,
                    new Actual365Fixed(),
                    new Period(2, TimeUnit.Months),
                    Frequency.Monthly,
                    false,
                    context);
                var pricer = new BlackYoYInflationCouponPricer(vol, CashFlowInputs.CurveOf(c.Input("nominalCurve")));
                YoYInflationCoupon coupon = new YoYInflationCoupon(
                    c.DateInput("paymentDate"),
                    c.Double("nominal"),
                    c.DateInput("startDate"),
                    c.DateInput("endDate"),
                    c.Int("fixingDays"),
                    yii,
                    InflationCashFlowInputs.PeriodOf(c.Input("observationLag")),
                    (CPIInterpolationType)c.Int("interpolation"),
                    CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
                    c.Double("gearing"),
                    c.Double("spread"),
                    c.NullableDateInput("refPeriodStart"),
                    c.NullableDateInput("refPeriodEnd")).SetPricer(pricer);
                check.Output(c, "fixingDate", coupon.FixingDate());
                check.Output(c, "indexFixing", () => coupon.IndexFixing(context));
                check.Output(c, "rate", () => coupon.Rate(context));
                check.Output(c, "amount", () => coupon.Amount(context));
                check.Output(c, "adjustedFixing", () => coupon.AdjustedFixing(context));
                check.Output(c, "gearing", coupon.Gearing);
                check.Output(c, "spread", coupon.Spread);
                check.Output(c, "interpolation", (int)coupon.Interpolation);
                check.Output(c, "accrualPeriod", coupon.AccrualPeriod());
                continue;
            }

            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(c, "construction", () => LegOf(c.Input("leg"), yii, context).IsDefault ? 1 : 0);
                continue;
            }

            ImmutableArray<CashFlow> leg = LegOf(c.Input("leg"), yii, context);
            check.Output(c, "kinds", leg.Select(f => f is YoYInflationCoupon ? "YoYInflationCoupon" : "FixedRateCoupon"));
            check.Output(c, "dates", leg.Select(f => f.Date));
            check.Output(c, "fixingDates", leg.Select(f => (f as YoYInflationCoupon)?.FixingDate()));
            check.Output(c, "amounts", leg.Select(f => (Func<object?>)(() => f.Amount(context))).ToArray());
            check.Output(c, "rates", leg.Select(f => (Func<object?>)(() => ((Coupon)f).Rate(context))).ToArray());
        }

        check.Complete();
    }

    /// <summary>The leg a <c>"leg"</c> spec describes, every setting given.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="index">The index.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The leg.</returns>
    internal static ImmutableArray<CashFlow> LegOf(JsonElement spec, YoYInflationIndex index, EvaluationContext context)
    {
        YoYInflationLeg builder = new YoYInflationLeg(
                CashFlowInputs.ScheduleOf(spec.GetProperty("schedule"), context),
                CashFlowInputs.CalendarOf(spec.GetProperty("paymentCalendar")),
                index,
                InflationCashFlowInputs.PeriodOf(spec.GetProperty("observationLag")),
                (CPIInterpolationType)spec.GetProperty("interpolation").GetInt32())
            .WithNotionals(Doubles(spec.GetProperty("notionals")))
            .WithPaymentDayCounter(CashFlowInputs.DayCounterOf(spec.GetProperty("paymentDayCounter")))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentAdjustment").GetInt32())
            .WithFixingDays(spec.GetProperty("fixingDays").EnumerateArray().Select(d => d.GetInt32()))
            .WithGearings(Doubles(spec.GetProperty("gearings")))
            .WithSpreads(Doubles(spec.GetProperty("spreads")));
        return builder.ToLeg();
    }

    private static double[] Doubles(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];
}
