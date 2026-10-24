// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The capped and floored year-on-year inflation coupon vectors,
/// <c>tests/vectors/cashflows/capflooredinflationcoupon.json</c>: <see cref="CappedFlooredYoYInflationCoupon"/>'s
/// options, effective strikes, rate and amount under the Black, unit-displaced Black and Bachelier pricers, and
/// <see cref="YoYInflationLeg"/>'s capped, floored and collared flows under the Black pricer.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CappedFlooredYoYInflationCouponVectorTests
{
    [Fact]
    public void CappedFlooredYoYInflationCouponVector()
    {
        var file = VectorFile.Load("cashflows/capflooredinflationcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            var yii = (YoYInflationIndex)index;
            YoYInflationCouponPricer pricer = PricerOf(c, context);
            if (c.String("kind") == "coupon")
            {
                CappedFlooredYoYInflationCoupon coupon = new CappedFlooredYoYInflationCoupon(
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
                    InflationCashFlowInputs.NullableDoubleOf(c.Input("cap")),
                    InflationCashFlowInputs.NullableDoubleOf(c.Input("floor"))).SetPricer(pricer);
                check.Output(c, "isCapped", coupon.IsCapped);
                check.Output(c, "isFloored", coupon.IsFloored);
                check.Output(c, "cap", coupon.Cap);
                check.Output(c, "floor", coupon.Floor);
                if (c.Expected.TryGetProperty("effectiveCap", out _))
                {
                    check.Output(c, "effectiveCap", coupon.EffectiveCap);
                }

                if (c.Expected.TryGetProperty("effectiveFloor", out _))
                {
                    check.Output(c, "effectiveFloor", coupon.EffectiveFloor);
                }

                check.Output(c, "underlyingRate", () => coupon.UnderlyingRate(context));
                check.Output(c, "rate", () => coupon.Rate(context));
                check.Output(c, "amount", () => coupon.Amount(context));
                check.Output(c, "fixingDate", coupon.FixingDate());
                check.Output(c, "indexFixing", () => coupon.IndexFixing(context));
                continue;
            }

            JsonElement spec = c.Input("leg");
            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(c, "construction", () => LegOf(spec, yii, context).IsDefault ? 1 : 0);
                continue;
            }

            ImmutableArray<CashFlow> leg = InflationCouponPricerFunctions.SetCouponPricer(LegOf(spec, yii, context), pricer);
            check.Output(
                c,
                "kinds",
                leg.Select(f => f switch
                {
                    CappedFlooredYoYInflationCoupon => "CappedFlooredYoYInflationCoupon",
                    YoYInflationCoupon => "YoYInflationCoupon",
                    _ => "FixedRateCoupon",
                }));
            check.Output(c, "dates", leg.Select(f => f.Date));
            check.Output(c, "fixingDates", leg.Select(f => (f as YoYInflationCoupon)?.FixingDate()));
            check.Output(c, "caps", leg.Select(f => (f as CappedFlooredYoYInflationCoupon)?.Cap));
            check.Output(c, "floors", leg.Select(f => (f as CappedFlooredYoYInflationCoupon)?.Floor));
            check.Output(c, "rates", leg.Select(f => (Func<object?>)(() => ((Coupon)f).Rate(context))).ToArray());
            check.Output(c, "amounts", leg.Select(f => (Func<object?>)(() => f.Amount(context))).ToArray());
        }

        check.Complete();
    }

    // The generator's pricer of the case's kind, on its volatility and nominal curve.
    internal static YoYInflationCouponPricer PricerOf(VectorCase c, EvaluationContext context)
    {
        YoYOptionletVolatilitySurface vol = InflationInputs.YoYVolatilityOf(c.Input("volatility"), context);
        YieldTermStructure nominal = CashFlowInputs.CurveOf(c.Input("nominalCurve"));
        return c.String("pricer") switch
        {
            "Black" => new BlackYoYInflationCouponPricer(vol, nominal),
            "UnitDisplacedBlack" => new UnitDisplacedBlackYoYInflationCouponPricer(vol, nominal),
            "Bachelier" => new BachelierYoYInflationCouponPricer(vol, nominal),
            string kind => throw new InvalidOperationException($"unknown pricer '{kind}'"),
        };
    }

    // The leg a spec describes, every setting given, a null cap or floor none: a list of one cap or floor through the
    // setter taking one value, as QuantLib's conversion repeats the last value.
    internal static ImmutableArray<CashFlow> LegOf(JsonElement spec, YoYInflationIndex index, EvaluationContext context)
    {
        double?[] caps = [.. spec.GetProperty("caps").EnumerateArray().Select(InflationCashFlowInputs.NullableDoubleOf)];
        double?[] floors = [.. spec.GetProperty("floors").EnumerateArray().Select(InflationCashFlowInputs.NullableDoubleOf)];
        YoYInflationLeg leg = new YoYInflationLeg(
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
        leg = caps.Length == 1 ? leg.WithCaps(caps[0]) : leg.WithCaps(caps);
        leg = floors.Length == 1 ? leg.WithFloors(floors[0]) : leg.WithFloors(floors);
        return leg.ToLeg();
    }

    private static double[] Doubles(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];
}
