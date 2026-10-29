// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's year-on-year optionlet helper vectors, <c>tests/vectors/experimental/inflation/yoyoptionlethelpers.json</c>:
/// helpers on the euro index priced by the unit-displaced Black engine on a constant volatility, their implied quotes
/// and dates; each helper bound to the volatility the wheel's engine holds.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class YoYOptionletHelperVectorTests
{
    [Fact]
    public void YoYOptionletHelperVector()
    {
        var file = VectorFile.Load("experimental/inflation/yoyoptionlethelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            (var nominal, var index) = InflationSurfaceInputs.EuroMarketOf(c.Input("market"));
            var lag = new Period(3, TimeUnit.Months);
            ConstantYoYOptionletVolatility vol = ConstantYoYOptionletVolatility.Create(
                c.Input("volatility").GetDouble(), 0, new TARGET(), BusinessDayConvention.ModifiedFollowing,
                new Actual365Fixed(), lag, Frequency.Monthly, false, context, -1.0, 3.0);
            check.Output(c, "helpers", c.Input("helpers").EnumerateArray().Select(h =>
            {
                YoYOptionletHelper helper = HelperOf(h, index, nominal, context);
                return (object?)new object?[]
                {
                    (Func<object?>)(() => helper.SetTermStructure(vol).ImpliedQuote(context)), helper.EarliestDate,
                    helper.LatestDate, helper.PillarDate, helper.MaturityDate, helper.LatestRelevantDate,
                };
            }).ToArray());
        }

        check.Complete();
    }

    private static YoYOptionletHelper HelperOf(
        JsonElement h, YoYInflationIndex index, YieldTermStructure nominal, EvaluationContext context) =>
        new(
            h.GetProperty("price").GetDouble(),
            h.GetProperty("notional").GetDouble(),
            (YoYInflationCapFloorType)h.GetProperty("type").GetInt32(),
            InflationInputs.LagOf(h.GetProperty("lag")),
            new Actual365Fixed(),
            new TARGET(),
            h.GetProperty("fixingDays").GetInt32(),
            index,
            (CPIInterpolationType)h.GetProperty("interpolation").GetInt32(),
            h.GetProperty("strike").GetDouble(),
            h.GetProperty("n").GetInt32(),
            (cf, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(cf, ctx, index, v, nominal),
            context);
}
