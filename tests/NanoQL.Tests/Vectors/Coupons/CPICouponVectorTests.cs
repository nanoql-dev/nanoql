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
/// The CPI coupon vectors, <c>tests/vectors/cashflows/cpicoupon.json</c>: <see cref="CPICoupon"/>'s three forms with
/// QuantLib's default pricer, <see cref="CPICashFlow"/>, and <see cref="CPILeg"/>'s flows.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CPICouponVectorTests
{
    [Fact]
    public void CPICouponVector()
    {
        var file = VectorFile.Load("cashflows/cpicoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            var zii = (ZeroInflationIndex)index;
            switch (c.String("kind"))
            {
                case "coupon":
                    Coupon(c, check, zii, context);
                    break;
                case "cashflow":
                    CashFlow(c, check, zii, context);
                    break;
                case "leg":
                    Leg(c, check, zii, context);
                    break;
                default:
                    throw new InvalidOperationException($"unknown case kind in {c.Name}");
            }
        }

        check.Complete();
    }

    /// <summary>The leg a <c>"leg"</c> spec describes, every setting given.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="index">The index.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The leg.</returns>
    internal static ImmutableArray<CashFlow> LegOf(JsonElement spec, ZeroInflationIndex index, EvaluationContext context)
    {
        CPILeg builder = new CPILeg(
                ScheduleOf(spec.GetProperty("schedule"), context),
                index,
                InflationCashFlowInputs.NullableDoubleOf(spec.GetProperty("baseCPI")),
                InflationCashFlowInputs.PeriodOf(spec.GetProperty("observationLag")))
            .WithNotionals(Doubles(spec.GetProperty("notionals")))
            .WithPaymentDayCounter(CashFlowInputs.DayCounterOf(spec.GetProperty("paymentDayCounter")))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentAdjustment").GetInt32())
            .WithFixedRates(Doubles(spec.GetProperty("fixedRates")))
            .WithCaps(Doubles(spec.GetProperty("caps")).Select(x => (double?)x))
            .WithFloors(Doubles(spec.GetProperty("floors")).Select(x => (double?)x))
            .WithPaymentCalendar(CashFlowInputs.CalendarOf(spec.GetProperty("paymentCalendar")))
            .WithSubtractInflationNominal(spec.GetProperty("subtractInflationNominal").GetBoolean())
            .WithObservationInterpolation((CPIInterpolationType)spec.GetProperty("interpolation").GetInt32());
        if (spec.GetProperty("exCouponPeriod") is { ValueKind: JsonValueKind.Object } ex)
        {
            builder = builder.WithExCouponPeriod(
                InflationCashFlowInputs.PeriodOf(ex.GetProperty("period")),
                CashFlowInputs.CalendarOf(ex.GetProperty("calendar")),
                (BusinessDayConvention)ex.GetProperty("convention").GetInt32(),
                ex.GetProperty("endOfMonth").GetBoolean());
        }

        return builder.ToLeg();
    }

    /// <summary>A schedule spec: <c>cashflows.py</c>'s rule-based one, or <c>{"dates", "calendar",
    /// "convention"}</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The schedule.</returns>
    internal static Schedule ScheduleOf(JsonElement spec, EvaluationContext context) =>
        spec.TryGetProperty("dates", out JsonElement dates)
            ? new Schedule(
                dates.EnumerateArray().Select(d => new Date(d.GetInt32())),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32())
            : CashFlowInputs.ScheduleOf(spec, context);

    private static void Coupon(VectorCase c, VectorAssert check, ZeroInflationIndex index, EvaluationContext context)
    {
        double? baseCPI = InflationCashFlowInputs.NullableDoubleOf(c.Input("baseCPI"));
        Date? baseDate = c.NullableDateInput("baseDate");
        Date payment = c.DateInput("paymentDate");
        double nominal = c.Double("nominal");
        Date start = c.DateInput("startDate"), end = c.DateInput("endDate");
        Period lag = InflationCashFlowInputs.PeriodOf(c.Input("observationLag"));
        var interpolation = (CPIInterpolationType)c.Int("interpolation");
        DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
        double rate = c.Double("fixedRate");
        Date? refStart = c.NullableDateInput("refPeriodStart"), refEnd = c.NullableDateInput("refPeriodEnd");
        Date? exCoupon = c.NullableDateInput("exCouponDate");
        Func<CPICoupon> make = c.String("form") switch
        {
            "baseCPI" => () => new CPICoupon(
                baseCPI!.Value, payment, nominal, start, end, index, lag, interpolation, dc, rate, refStart, refEnd, exCoupon),
            "baseDate" => () => new CPICoupon(
                baseDate!.Value, payment, nominal, start, end, index, lag, interpolation, dc, rate, refStart, refEnd, exCoupon),
            _ => () => new CPICoupon(
                baseCPI, baseDate, payment, nominal, start, end, index, lag, interpolation, dc, rate, refStart, refEnd, exCoupon),
        };
        if (c.Expected.TryGetProperty("construction", out _))
        {
            check.Output(c, "construction", () => make() is null ? 1 : 0);
            return;
        }

        CPICoupon coupon = make().SetPricer(new CPICouponPricer());
        Date[] dates = c.DateInputs("dates");
        check.Output(c, "fixedRate", coupon.FixedRate);
        check.Output(c, "baseCPI", coupon.BaseCPI);
        check.Output(c, "baseDate", coupon.BaseDate);
        check.Output(c, "observationInterpolation", (int)coupon.ObservationInterpolation);
        check.Output(c, "fixingDate", coupon.FixingDate());
        check.Output(c, "indexFixing", () => coupon.IndexFixing(context));
        check.Output(c, "rate", () => coupon.Rate(context));
        check.Output(c, "amount", () => coupon.Amount(context));
        check.Output(c, "adjustedIndexGrowth", () => coupon.AdjustedIndexGrowth(context));
        check.Output(c, "indexRatio", dates.Select(d => (Func<object?>)(() => coupon.IndexRatio(d, context))).ToArray());
        check.Output(
            c, "accruedAmount", dates.Select(d => (Func<object?>)(() => coupon.AccruedAmount(d, context))).ToArray());
    }

    private static void CashFlow(VectorCase c, VectorAssert check, ZeroInflationIndex index, EvaluationContext context)
    {
        CPICashFlow Make() => new(
            c.Double("notional"),
            index,
            c.NullableDateInput("baseDate"),
            InflationCashFlowInputs.NullableDoubleOf(c.Input("baseFixing")),
            c.DateInput("observationDate"),
            InflationCashFlowInputs.PeriodOf(c.Input("observationLag")),
            (CPIInterpolationType)c.Int("interpolation"),
            c.DateInput("paymentDate"),
            c.Bool("growthOnly"));
        if (c.Expected.TryGetProperty("construction", out _))
        {
            check.Output(c, "construction", () => Make() is null ? 1 : 0);
            return;
        }

        CPICashFlow cf = Make();
        check.Output(c, "notional", cf.Notional);
        check.Output(c, "baseDate", () => cf.BaseDate());
        check.Output(c, "baseFixing", () => cf.BaseFixing(context));
        check.Output(c, "indexFixing", () => cf.IndexFixing(context));
        check.Output(c, "fixingDate", cf.FixingDate);
        check.Output(c, "frequency", (int)cf.Frequency);
        check.Output(c, "interpolation", (int)cf.Interpolation);
        check.Output(c, "growthOnly", cf.GrowthOnly);
        check.Output(c, "amount", () => cf.Amount(context));
    }

    private static void Leg(VectorCase c, VectorAssert check, ZeroInflationIndex index, EvaluationContext context)
    {
        if (c.Expected.TryGetProperty("construction", out _))
        {
            check.Output(c, "construction", () => LegOf(c.Input("leg"), index, context).IsDefault ? 1 : 0);
            return;
        }

        ImmutableArray<CashFlow> leg = LegOf(c.Input("leg"), index, context);
        check.Output(c, "kinds", leg.Select(Kind));
        check.Output(c, "dates", leg.Select(f => f.Date));
        check.Output(c, "amounts", leg.Select(f => (Func<object?>)(() => f.Amount(context))).ToArray());
        check.Output(c, "accrualStartDates", leg.Select(f => (f as Coupon)?.AccrualStartDate));
        check.Output(c, "accrualEndDates", leg.Select(f => (f as Coupon)?.AccrualEndDate));
        check.Output(
            c,
            "rates",
            leg.Select(f => f is Coupon coupon ? (Func<object?>)(() => coupon.Rate(context)) : () => null).ToArray());
    }

    private static string Kind(CashFlow f) => f switch
    {
        CPICoupon => "CPICoupon",
        CPICashFlow => "CPICashFlow",
        FixedRateCoupon => "FixedRateCoupon",
        _ => throw new InvalidOperationException("an unexpected cash flow in a CPI leg"),
    };

    private static double[] Doubles(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];
}
