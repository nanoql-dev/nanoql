// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's year-on-year cap and floor price surface vectors,
/// <c>tests/vectors/experimental/inflation/yoycapfloortermpricesurface.json</c>: on QuantLib's euro market, the strikes,
/// range and dates, the at-the-money swap rates by time, the swap and year-on-year rates at dates, the cap, floor and
/// price at tenors and strikes, and the bootstrapped year-on-year curve's rate at dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class YoYCapFloorTermPriceSurfaceVectorTests
{
    [Fact]
    public void YoYCapFloorTermPriceSurfaceVector()
    {
        var file = VectorFile.Load("experimental/inflation/yoycapfloortermpricesurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            (var nominal, var index) = InflationSurfaceInputs.EuroMarketOf(c.Input("market"));
            InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic> surf =
                InflationSurfaceInputs.YoYSurfaceOf(c.Input("surface"), nominal, index, context);
            Date[] dates = c.DateInputs("dates");
            Period[] tenors = [.. c.Input("tenors").EnumerateArray().Select(InflationInputs.LagOf)];
            double[] strikes = c.Doubles("strikes");
            (var times, var rates) = surf.AtmYoYSwapTimeRates();
            check.Output(c, "strikes", surf.Strikes);
            check.Output(c, "minMaturity", surf.MinMaturity);
            check.Output(c, "maxMaturity", surf.MaxMaturity);
            check.Output(c, "baseDate", surf.BaseDate());
            check.Output(c, "maxDate", surf.MaxDate);
            check.Output(c, "atmYoYSwapTimeRates", new object?[] { times, rates });
            check.Output(c, "atmYoYSwapRate", Each(dates, d => surf.AtmYoYSwapRate(d)));
            check.Output(c, "atmYoYRate", Each(dates, d => surf.AtmYoYRate(d)));
            check.Output(c, "capPrice", Grid(tenors, strikes, (t, k) => surf.CapPrice(t, k)));
            check.Output(c, "floorPrice", Grid(tenors, strikes, (t, k) => surf.FloorPrice(t, k)));
            check.Output(c, "price", Grid(tenors, strikes, (t, k) => surf.Price(t, k)));
            check.Output(c, "yoyRate", Each(dates, d => surf.YoYTS().YoyRate(d)));
        }

        check.Complete();
    }

    private static object?[] Each(Date[] dates, Func<Date, double> f) =>
        [.. dates.Select(d => (Func<object?>)(() => f(d)))];

    private static object?[] Grid(Period[] tenors, double[] strikes, Func<Period, double, double> f) =>
        [.. tenors.Select(t => (object?)strikes.Select(k => (Func<object?>)(() => f(t, k))).ToArray())];
}
