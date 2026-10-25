// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BlackVolatilitySurfaceDelta vectors, <c>tests/vectors/termstructures/volatility/equityfx/blackvolsurfacedelta.json</c>:
/// surfaces over one to three deltas each side, every delta, at-the-money, smile interpolation and time extrapolation
/// convention, a switch tenor and two day counters, on flat curves sharing the surface's clock; and the constructor's
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackVolSurfaceDeltaVectorTests
{
    // QuantLib's Null<Real>(), the strike the file's null stands for
    private const double NullReal = 3.4028234663852886e38;

    [Fact]
    public void BlackVolSurfaceDeltaVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/blackvolsurfacedelta");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("surface");
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", Lazy(() => SurfaceOf(spec, context.EvaluationDate).MaxDate));
                continue;
            }

            BlackVolatilitySurfaceDelta s = SurfaceOf(spec, context.EvaluationDate).EnableExtrapolation();
            double[] times = c.Doubles("times");
            double[] strikes = [.. c.Input("strikes").EnumerateArray().Select(k => k.ValueKind == JsonValueKind.Null ? NullReal : k.GetDouble())];
            check.Output(c, "dates", s.Dates.Select(d => (object?)d).ToArray());
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "atmLevel", times.Select(t => (object?)s.AtmLevel(t)!.Value).ToArray());
            check.Output(c, "blackVol", Grid(times, strikes, (t, k) => s.BlackVol(t, k, true)));
            check.Output(c, "blackVariance", Grid(times, strikes, (t, k) => s.BlackVariance(t, k, true)));
        }

        check.Complete();
    }

    // The surface a spec describes, on flat curves from its reference date on its day counter.
    private static BlackVolatilitySurfaceDelta SurfaceOf(JsonElement spec, Date reference)
    {
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        Date[] dates = [.. spec.GetProperty("offsets").EnumerateArray().Select(n => reference + n.GetInt32())];
        double[][] rows = [.. spec.GetProperty("vols").EnumerateArray().Select(Doubles)];
        var m = new Matrix(rows.Length, rows.Length == 0 ? 0 : rows[0].Length);
        for (int i = 0; i < m.Rows; i++)
        {
            for (int j = 0; j < m.Columns; j++)
            {
                m[i, j] = rows[i][j];
            }
        }

        JsonElement switchTenor = spec.GetProperty("switchTenor");
        return new BlackVolatilitySurfaceDelta(
            reference,
            dates,
            Doubles(spec.GetProperty("putDeltas")),
            Doubles(spec.GetProperty("callDeltas")),
            spec.GetProperty("hasAtm").GetBoolean(),
            m,
            dc,
            CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
            spec.GetProperty("spot").GetDouble(),
            new FlatForward(reference, spec.GetProperty("domesticRate").GetDouble(), dc),
            new FlatForward(reference, spec.GetProperty("foreignRate").GetDouble(), dc),
            (DeltaVolQuoteDeltaType)spec.GetProperty("deltaType").GetInt32(),
            (DeltaVolQuoteAtmType)spec.GetProperty("atmType").GetInt32(),
            NullableDeltaType(spec.GetProperty("atmDeltaType")),
            (BlackVolatilitySurfaceDeltaSmileInterpolationMethod)spec.GetProperty("interpolationMethod").GetInt32(),
            spec.GetProperty("flatStrikeExtrapolation").GetBoolean(),
            (BlackVolTimeExtrapolationType)spec.GetProperty("timeExtrapolationType").GetInt32(),
            switchTenor.ValueKind == JsonValueKind.Null ? null : new Period(switchTenor[0].GetInt32(), (TimeUnit)switchTenor[1].GetInt32()),
            (DeltaVolQuoteDeltaType)spec.GetProperty("longTermDeltaType").GetInt32(),
            (DeltaVolQuoteAtmType)spec.GetProperty("longTermAtmType").GetInt32(),
            NullableDeltaType(spec.GetProperty("longTermAtmDeltaType")));
    }

    private static DeltaVolQuoteDeltaType? NullableDeltaType(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : (DeltaVolQuoteDeltaType)e.GetInt32();

    private static object?[] Grid(double[] times, double[] strikes, Func<double, double, double> f) =>
        [.. times.Select(t => (object?)strikes.Select(k => Lazy(() => f(t, k))).ToArray())];
}
