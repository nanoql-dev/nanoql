// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's CPI cap and floor price surface vectors,
/// <c>tests/vectors/cxx/experimental/inflation/cpicapfloortermpricesurface.json</c>: the strikes, option dates, range
/// and base date, the cap, floor and price at tenors and quoted strikes, and the at-the-money rate at dates, on UK RPI's
/// curve and fixings; and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CPICapFloorTermPriceSurfaceCxxVectorTests
{
    [Fact]
    public void CPICapFloorTermPriceSurfaceCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/inflation/cpicapfloortermpricesurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.String("kind") == "refused")
            {
                check.Output(c, "construction", () => InflationSurfaceInputs.CPISurfaceOf(c.Input("surface"), context));
                continue;
            }

            CPICapFloorTermPriceSurface surface = InflationSurfaceInputs.CPISurfaceOf(c.Input("surface"), context);
            Period[] tenors = [.. c.Input("tenors").EnumerateArray().Select(InflationInputs.LagOf)];
            double[] strikes = c.Doubles("strikes");
            Date[] dates = c.DateInputs("dates");
            check.Output(c, "strikes", surface.Strikes);
            check.Output(c, "optionDates", surface.Maturities.Select(m => (object?)surface.CpiOptionDateFromTenor(m)).ToArray());
            check.Output(c, "minDate", surface.MinDate);
            check.Output(c, "maxDate", surface.MaxDate);
            check.Output(c, "baseDate", surface.BaseDate());
            check.Output(c, "capPrice", Grid(tenors, strikes, (t, k) => surface.CapPrice(t, k)));
            check.Output(c, "floorPrice", Grid(tenors, strikes, (t, k) => surface.FloorPrice(t, k)));
            check.Output(c, "price", Grid(tenors, strikes, (t, k) => surface.Price(t, k, context)));
            check.Output(c, "atmRate", dates.Select(d => (Func<object?>)(() => surface.AtmRate(d, context))).ToArray());
        }

        check.Complete();
    }

    // One array per tenor, one lazy value per strike, so a refused read is compared as one.
    private static object?[] Grid(Period[] tenors, double[] strikes, Func<Period, double, double> f) =>
        [.. tenors.Select(t => (object?)strikes.Select(k => (Func<object?>)(() => f(t, k))).ToArray())];
}
