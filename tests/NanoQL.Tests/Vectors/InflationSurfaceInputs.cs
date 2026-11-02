// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Readers of the C++ harness's inflation price surface specs, as
/// <c>tools/cxx/src/experimental/inflation/cpicapfloortermpricesurface.cpp</c> reads them.
/// </summary>
internal static class InflationSurfaceInputs
{
    /// <summary>A CPI cap and floor price surface, built on the context's evaluation date and fixings.</summary>
    /// <param name="spec">The surface spec.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The surface.</returns>
    public static CPICapFloorTermPriceSurface CPISurfaceOf(JsonElement spec, EvaluationContext context)
    {
        var index = (ZeroInflationIndex)InflationInputs.IndexOf(spec.GetProperty("index"));
        double nominal = spec.GetProperty("nominal").GetDouble();
        double startRate = spec.GetProperty("startRate").GetDouble();
        Period lag = InflationInputs.LagOf(spec.GetProperty("observationLag"));
        Calendar cal = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        var bdc = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        var interpolation = (CPIInterpolationType)spec.GetProperty("interpolation").GetInt32();
        var yts = CashFlowInputs.CurveOf(spec.GetProperty("nominalCurve"));
        double[] cStrikes = Doubles(spec.GetProperty("capStrikes"));
        double[] fStrikes = Doubles(spec.GetProperty("floorStrikes"));
        Period[] maturities = [.. spec.GetProperty("maturities").EnumerateArray().Select(InflationInputs.LagOf)];
        Matrix cPrice = MatrixOf(spec.GetProperty("capPrices"));
        Matrix fPrice = MatrixOf(spec.GetProperty("floorPrices"));
        return spec.GetProperty("interpolator").GetString() switch
        {
            "Bilinear" => InterpolatedCPICapFloorTermPriceSurface<Bilinear>.Create(
                nominal, startRate, lag, cal, bdc, dc, index, interpolation, yts, cStrikes, fStrikes, maturities, cPrice,
                fPrice, context),
            "Bicubic" => InterpolatedCPICapFloorTermPriceSurface<Bicubic>.Create(
                nominal, startRate, lag, cal, bdc, dc, index, interpolation, yts, cStrikes, fStrikes, maturities, cPrice,
                fPrice, context),
            var other => throw new ArgumentException($"unknown interpolator {other}", nameof(spec)),
        };
    }

    /// <summary>A CPI cap or floor on an index.</summary>
    /// <param name="spec">The instrument spec.</param>
    /// <param name="index">The index.</param>
    /// <returns>The instrument.</returns>
    public static CPICapFloor CPICapFloorOf(JsonElement spec, ZeroInflationIndex index) =>
        new(
            (OptionType)spec.GetProperty("type").GetInt32(),
            spec.GetProperty("nominal").GetDouble(),
            new Date(spec.GetProperty("startDate").GetInt32()),
            spec.GetProperty("baseCPI").GetDouble(),
            new Date(spec.GetProperty("maturity").GetInt32()),
            CashFlowInputs.CalendarOf(spec.GetProperty("fixCalendar")),
            (BusinessDayConvention)spec.GetProperty("fixConvention").GetInt32(),
            CashFlowInputs.CalendarOf(spec.GetProperty("payCalendar")),
            (BusinessDayConvention)spec.GetProperty("payConvention").GetInt32(),
            spec.GetProperty("strike").GetDouble(),
            index,
            InflationInputs.LagOf(spec.GetProperty("observationLag")),
            (CPIInterpolationType)spec.GetProperty("observationInterpolation").GetInt32());

    /// <summary>
    /// The wheel's euro market (<c>tools/vectors/gen/experimental/inflation/yoycapfloortermpricesurface.py</c>): a
    /// cubic zero nominal curve and EU HICP's year-on-year index on a linear year-on-year curve, both Actual/365
    /// (Fixed).
    /// </summary>
    /// <param name="spec">The market spec.</param>
    /// <returns>The nominal curve and the index.</returns>
    public static (YieldTermStructure Nominal, YoYInflationIndex Index) EuroMarketOf(JsonElement spec)
    {
        JsonElement n = spec.GetProperty("nominal");
        var nominal = new InterpolatedZeroCurve<Cubic>(
            Dates(n.GetProperty("dates")), Doubles(n.GetProperty("rates")), new Actual365Fixed());
        JsonElement y = spec.GetProperty("yoy");
        var yoy = new InterpolatedYoYInflationCurve<Linear>(
            new Date(y.GetProperty("referenceDate").GetInt32()),
            Dates(y.GetProperty("dates")),
            Doubles(y.GetProperty("rates")),
            Frequency.Monthly,
            new Actual365Fixed());
        return (nominal, new YoYInflationIndex(new EUHICP(), yoy));
    }

    /// <summary>
    /// The wheel's year-on-year price surface, bicubic in time and strike, its swap rates cubic, on TARGET, Modified
    /// Following and Actual/365 (Fixed).
    /// </summary>
    /// <param name="spec">The surface spec.</param>
    /// <param name="nominal">The nominal curve.</param>
    /// <param name="index">The year-on-year index.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The surface.</returns>
    public static InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic> YoYSurfaceOf(
        JsonElement spec, YieldTermStructure nominal, YoYInflationIndex index, EvaluationContext context) =>
        YoYInflationCapFloorTermPriceSurface.Create(
            spec.GetProperty("fixingDays").GetInt32(),
            InflationInputs.LagOf(spec.GetProperty("lag")),
            index,
            (CPIInterpolationType)spec.GetProperty("interpolation").GetInt32(),
            nominal,
            new Actual365Fixed(),
            new TARGET(),
            BusinessDayConvention.ModifiedFollowing,
            Doubles(spec.GetProperty("capStrikes")),
            Doubles(spec.GetProperty("floorStrikes")),
            [.. spec.GetProperty("maturities").EnumerateArray().Select(InflationInputs.LagOf)],
            MatrixOf(spec.GetProperty("capPrices")),
            MatrixOf(spec.GetProperty("floorPrices")),
            context);

    /// <summary>A matrix, a row per array.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The matrix.</returns>
    public static Matrix MatrixOf(JsonElement rows)
    {
        double[][] r = [.. rows.EnumerateArray().Select(Doubles)];
        var m = new Matrix(r.Length, r.Length == 0 ? 0 : r[0].Length);
        for (int i = 0; i < r.Length; i++)
        {
            for (int j = 0; j < r[i].Length; j++)
            {
                m[i, j] = r[i][j];
            }
        }

        return m;
    }

    private static double[] Doubles(JsonElement spec) => [.. spec.EnumerateArray().Select(x => x.GetDouble())];

    private static Date[] Dates(JsonElement spec) => [.. spec.EnumerateArray().Select(x => new Date(x.GetInt32()))];
}
