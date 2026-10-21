// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The cross-currency rate helper vectors,
/// <c>tests/vectors/experimental/termstructures/crosscurrencyratehelpers.json</c>: sets of
/// <see cref="ConstNotionalCrossCurrencyBasisSwapRateHelper"/>s, <see cref="MtMCrossCurrencyBasisSwapRateHelper"/>s and
/// <see cref="ConstNotionalCrossCurrencySwapRateHelper"/>s bootstrapping a piecewise discount curve against a known
/// one: the curve's dates, nodes and discounts, and each helper's dates and implied quote on it; the outputs the
/// stability filter dropped are null.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CrossCurrencyRateHelpersVectorTests
{
    [Fact]
    public void CrossCurrencyRateHelpersVector()
    {
        var file = VectorFile.Load("experimental/termstructures/crosscurrencyratehelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("curve");
            BootstrapHelper<YieldTermStructure>[]? helpers = null;
            YieldTermStructure? curve = null;
            check.Output(c, "built", () =>
            {
                helpers = [.. c.Input("helpers").EnumerateArray().Select(h => HelperOf(h, context))];
                curve = CurveOf(spec, helpers, context);
                return true;
            });
            if (curve is null || helpers is null)
            {
                continue;
            }

            Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            JsonElement dropped = c.Input("dropped");
            (Date[] nodeDates, double[] data) = Nodes(curve);
            check.Output(c, "dates", nodeDates);
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
                    dropped, "data", data.Select(x => (Func<object?>)(() => x))));
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
        DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        return spec.GetProperty("traits").GetString() switch
        {
            "ZeroYield" => new PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>(
                context.EvaluationDate, helpers, dayCounter, context),
            "Discount" => new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                context.EvaluationDate, helpers, dayCounter, context),
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
        double quote = spec.GetProperty("quote").GetDouble();
        Period tenor = SwapInputs.PeriodOf(spec.GetProperty("tenor"));
        int fixingDays = spec.GetProperty("fixingDays").GetInt32();
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        var convention = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        bool endOfMonth = spec.GetProperty("endOfMonth").GetBoolean();
        YieldTermStructure collateral = CashFlowInputs.CurveOf(spec.GetProperty("collateralCurve"));
        int paymentLag = spec.GetProperty("paymentLag").GetInt32();
        bool? indexed = CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons"));
        Frequency? FrequencyOf(string name) => (Frequency?)CouponInputs.NullableInt(spec.GetProperty(name));
        bool Flag(string name) => spec.GetProperty(name).GetBoolean();
        switch (spec.GetProperty("kind").GetString())
        {
            case "FixedVsFloating":
                return new ConstNotionalCrossCurrencySwapRateHelper(
                    quote,
                    tenor,
                    fixingDays,
                    calendar,
                    convention,
                    endOfMonth,
                    (Frequency)spec.GetProperty("fixedFrequency").GetInt32(),
                    CashFlowInputs.DayCounterOf(spec.GetProperty("fixedDayCount")),
                    CrossCurrencyInputs.IndexOf(spec.GetProperty("floatIndex")),
                    collateral,
                    Flag("collateralOnFixedLeg"),
                    context,
                    paymentLag,
                    indexed,
                    FrequencyOf("floatPaymentFrequency"));
            case "ConstNotionalBasis":
                return new ConstNotionalCrossCurrencyBasisSwapRateHelper(
                    quote,
                    tenor,
                    fixingDays,
                    calendar,
                    convention,
                    endOfMonth,
                    CrossCurrencyInputs.IndexOf(spec.GetProperty("baseIndex")),
                    CrossCurrencyInputs.IndexOf(spec.GetProperty("quoteIndex")),
                    collateral,
                    Flag("isFxBaseCurrencyCollateralCurrency"),
                    Flag("isBasisOnFxBaseCurrencyLeg"),
                    context,
                    FrequencyOf("paymentFrequency"),
                    paymentLag,
                    FrequencyOf("quoteCurrencyPaymentFrequency"),
                    indexed,
                    Flag("paymentLagOnNotionalExchanges"));
            case "MtMBasis":
                {
                    JsonElement fxCalendar = spec.GetProperty("fxResetFixingCalendar");
                    return new MtMCrossCurrencyBasisSwapRateHelper(
                        quote,
                        tenor,
                        fixingDays,
                        calendar,
                        convention,
                        endOfMonth,
                        CrossCurrencyInputs.IndexOf(spec.GetProperty("baseIndex")),
                        CrossCurrencyInputs.IndexOf(spec.GetProperty("quoteIndex")),
                        collateral,
                        Flag("isFxBaseCurrencyCollateralCurrency"),
                        Flag("isBasisOnFxBaseCurrencyLeg"),
                        Flag("isFxBaseCurrencyLegResettable"),
                        context,
                        FrequencyOf("paymentFrequency"),
                        paymentLag,
                        FrequencyOf("quoteCurrencyPaymentFrequency"),
                        spec.GetProperty("fxResetFixingDays").GetInt32(),
                        fxCalendar.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(fxCalendar),
                        indexed);
                }

            case var k:
                throw new InvalidOperationException($"unknown helper {k}");
        }
    }
}
