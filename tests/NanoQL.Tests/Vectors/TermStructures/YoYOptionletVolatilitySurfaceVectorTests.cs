// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The year-on-year optionlet volatility vectors,
/// <c>tests/vectors/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.json</c>: on constant
/// surfaces built from settlement days and the evaluation context, the inspectors, the base date, and the volatilities,
/// total variances and times from the base date by date, with the surface's lag and another, and by option tenor, with
/// the range and strike refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class YoYOptionletVolatilitySurfaceVectorTests
{
    [Fact]
    public void YoYOptionletVolatilitySurfaceVector()
    {
        var file = VectorFile.Load("termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YoYOptionletVolatilitySurface s = InflationInputs.YoYVolatilityOf(c.Input("surface"), c.Context());
            Date[] dates = c.DateInputs("dates");
            Period?[] lags = [.. c.Input("lags").EnumerateArray().Select(InflationInputs.NullableLagOf)];
            double[] strikes = c.Doubles("strikes");
            Period[] tenors = [.. c.Input("tenors").EnumerateArray().Select(InflationInputs.LagOf)];

            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "observationLagLength", s.ObservationLag.Length);
            check.Output(c, "observationLagUnits", s.ObservationLag.Units);
            check.Output(c, "frequency", s.Frequency);
            check.Output(c, "indexIsInterpolated", s.IndexIsInterpolated);
            check.Output(c, "baseDate", s.BaseDate());
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "baseLevel", () => s.BaseLevel());
            check.Output(c, "volatility", Grid(dates, lags, strikes, (d, g, x) => s.Volatility(d, x, g)));
            check.Output(c, "totalVariance", Grid(dates, lags, strikes, (d, g, x) => s.TotalVariance(d, x, g)));
            check.Output(
                c,
                "timeFromBase",
                dates.Select(d => lags.Select(g => (Func<object?>)(() => s.TimeFromBase(d, g))).ToArray()).ToArray());
            check.Output(c, "volatilityAtTenors", VolatilityInputs.Grid(tenors, strikes, (p, x) => s.Volatility(p, x)));
            check.Output(
                c, "totalVarianceAtTenors", VolatilityInputs.Grid(tenors, strikes, (p, x) => s.TotalVariance(p, x)));
        }

        check.Complete();
    }

    private static Func<object?>[][][] Grid(
        Date[] dates, Period?[] lags, double[] strikes, Func<Date, Period?, double, double> f) =>
        [.. dates.Select(d => lags.Select(g => strikes.Select(x => (Func<object?>)(() => f(d, g, x))).ToArray()).ToArray())];
}
