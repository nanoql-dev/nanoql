// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The basis swap rate helper vectors, <c>tests/vectors/experimental/termstructures/basisswapratehelpers.json</c>:
/// sets of <see cref="IborIborBasisSwapRateHelper"/>s, <see cref="OvernightIborBasisSwapRateHelper"/>s and
/// <see cref="OvernightOvernightBasisSwapRateHelper"/>s bootstrapping a piecewise curve against a known one: the
/// curve's dates, nodes and discounts, and each helper's dates and implied quote on it; the outputs the stability
/// filter dropped are null.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BasisSwapRateHelpersVectorTests
{
    [Fact]
    public void BasisSwapRateHelpersVector()
    {
        var file = VectorFile.Load("experimental/termstructures/basisswapratehelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BootstrapHelper<YieldTermStructure>[] helpers =
                [.. c.Input("helpers").EnumerateArray().Select(h => HelperOf(h, context))];
            JsonElement spec = c.Input("curve");
            YieldTermStructure? curve = null;
            check.Output(c, "built", () =>
            {
                curve = CurveOf(spec, helpers, context);
                return true;
            });
            if (curve is null)
            {
                continue;
            }

            Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            JsonElement dropped = c.Input("dropped");
            check.Output(c, "dates", Nodes(curve).Dates);
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "earliestDates", helpers.Select(h => h.EarliestDate).ToArray());
            check.Output(c, "maturityDates", helpers.Select(h => h.MaturityDate).ToArray());
            check.Output(c, "latestRelevantDates", helpers.Select(h => h.LatestRelevantDate).ToArray());
            check.Output(c, "pillarDates", helpers.Select(h => h.PillarDate).ToArray());
            check.Output(c, "latestDates", helpers.Select(h => h.LatestDate).ToArray());
            check.Output(
                c,
                "data",
                PiecewiseZeroInflationCurveVectorTests.Kept(
                    dropped, "data", Nodes(curve).Data.Select(x => (Func<object?>)(() => x))));
            check.Output(
                c,
                "discounts",
                PiecewiseZeroInflationCurveVectorTests.Kept(
                    dropped, "discounts", dates.Select(d => (Func<object?>)(() => curve.Discount(d, true)))));
            check.Output(
                c,
                "impliedQuotes",
                PiecewiseZeroInflationCurveVectorTests.Kept(
                    dropped,
                    "impliedQuotes",
                    helpers.Select(h => (Func<object?>)(() => h.SetTermStructure(curve).ImpliedQuote(context)))));
        }

        check.Complete();
    }

    private static YieldTermStructure CurveOf(
        JsonElement spec, BootstrapHelper<YieldTermStructure>[] helpers, EvaluationContext context)
    {
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        return spec.GetProperty("traits").GetString() switch
        {
            "ZeroYield" => PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>.Create(
                0, calendar, helpers, dayCounter, context),
            "Discount" => PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>.Create(
                0, calendar, helpers, dayCounter, context),
            var t => throw new InvalidOperationException($"unknown traits {t}"),
        };
    }

    // The curve's dates and nodes, through whichever class built it.
    private static (Date[] Dates, double[] Data) Nodes(YieldTermStructure curve) => curve switch
    {
        PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap> z => ([.. z.Dates()], [.. z.Data()]),
        PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap> d => ([.. d.Dates()], [.. d.Data()]),
        _ => throw new InvalidOperationException("unknown curve"),
    };

    private static BootstrapHelper<YieldTermStructure> HelperOf(JsonElement spec, EvaluationContext context)
    {
        double basis = spec.GetProperty("basis").GetDouble();
        JsonElement tenor = spec.GetProperty("tenor");
        var period = new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32());
        int settlementDays = spec.GetProperty("settlementDays").GetInt32();
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        var convention = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        bool endOfMonth = spec.GetProperty("endOfMonth").GetBoolean();
        InterestRateIndex baseIndex = IndexOf(spec.GetProperty("baseIndex"));
        InterestRateIndex otherIndex = IndexOf(spec.GetProperty("otherIndex"));
        YieldTermStructure? discount = Curve(spec.GetProperty("discount"));
        bool bootstrapBaseCurve = spec.GetProperty("bootstrapBaseCurve").GetBoolean();
        int paymentLag = spec.GetProperty("paymentLag").GetInt32();
        return spec.GetProperty("kind").GetString() switch
        {
            "IborIbor" => new IborIborBasisSwapRateHelper(
                basis,
                period,
                settlementDays,
                calendar,
                convention,
                endOfMonth,
                (IborIndex)baseIndex,
                (IborIndex)otherIndex,
                discount,
                bootstrapBaseCurve,
                context,
                CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")),
                (DateGenerationRule)spec.GetProperty("rule").GetInt32(),
                paymentLag),
            "OvernightIbor" => new OvernightIborBasisSwapRateHelper(
                basis,
                period,
                settlementDays,
                calendar,
                convention,
                endOfMonth,
                (OvernightIndex)baseIndex,
                (IborIndex)otherIndex,
                context,
                discount,
                bootstrapBaseCurve,
                paymentLag,
                (Frequency?)CouponInputs.NullableInt(spec.GetProperty("overnightPaymentFrequency")),
                CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")),
                (DateGenerationRule)spec.GetProperty("rule").GetInt32(),
                (RateAveragingType)spec.GetProperty("averaging").GetInt32(),
                false,
                spec.GetProperty("basisOnIborLeg").GetBoolean()),
            "OvernightOvernight" => new OvernightOvernightBasisSwapRateHelper(
                basis,
                period,
                settlementDays,
                calendar,
                convention,
                endOfMonth,
                (OvernightIndex)baseIndex,
                (OvernightIndex)otherIndex,
                context,
                discount,
                bootstrapBaseCurve,
                paymentLag,
                (Frequency)spec.GetProperty("paymentFrequency").GetInt32(),
                (RateAveragingType)spec.GetProperty("baseAveraging").GetInt32(),
                (RateAveragingType)spec.GetProperty("otherAveraging").GetInt32()),
            var k => throw new InvalidOperationException($"unknown helper {k}"),
        };
    }

    // A preset of instruments/fixedvsfloatingswap.py, or USD LIBOR or Fed Funds.
    private static InterestRateIndex IndexOf(JsonElement spec)
    {
        YieldTermStructure? curve = Curve(spec.GetProperty("curve"));
        return spec.GetProperty("type").GetString() switch
        {
            "USDLibor" => new USDLibor(
                new Period(spec.GetProperty("tenor")[0].GetInt32(), (TimeUnit)spec.GetProperty("tenor")[1].GetInt32()),
                curve),
            "FedFunds" => new FedFunds(curve),
            _ => SwapInputs.IndexOf(spec),
        };
    }

    private static YieldTermStructure? Curve(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(spec);
}
