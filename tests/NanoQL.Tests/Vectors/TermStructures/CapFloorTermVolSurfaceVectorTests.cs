// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="CapFloorTermVolSurface"/>'s vectors,
/// <c>tests/vectors/termstructures/volatility/capfloor/capfloortermvolsurface.json</c>: surfaces on fixed and moving
/// reference dates, their inspectors and their volatilities by time, by date and by tenor, inside and outside their
/// range; and the surfaces QuantLib refuses to build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapFloorTermVolSurfaceVectorTests
{
    [Fact]
    public void CapFloorTermVolSurfaceVector()
    {
        var file = VectorFile.Load("termstructures/volatility/capfloor/capfloortermvolsurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CapFloorTermVolSurface? surface = null;
            check.Output(c, "built", () =>
            {
                surface = SurfaceOf(c.Input("surface"), context);
                return true;
            });
            if (surface is null || !c.Inputs.TryGetProperty("byTime", out _))
            {
                continue;
            }

            check.Output(c, "referenceDate", surface.ReferenceDate);
            check.Output(c, "optionTenors", surface.OptionTenors.Select(t => new[] { t.Length, (int)t.Units }).ToArray());
            check.Output(c, "strikes", surface.Strikes);
            check.Output(c, "optionDates", surface.OptionDates);
            check.Output(c, "optionTimes", surface.OptionTimes);
            check.Output(c, "maxDate", surface.MaxDate);
            check.Output(c, "maxTime", surface.MaxTime);
            check.Output(c, "minStrike", surface.MinStrike());
            check.Output(c, "maxStrike", surface.MaxStrike());
            check.Output(c, "businessDayConvention", surface.BusinessDayConvention);
            CapFloorTermVolSurface s = surface;
            check.Output(c, "volatilityByTime", Points(c, "byTime", (p, k, e) => s.Volatility(p.GetDouble(), k, e)));
            check.Output(c, "volatilityByDate", Points(c, "byDate", (p, k, e) => s.Volatility(new Date(p.GetInt32()), k, e)));
            check.Output(c, "volatilityByTenor", Points(c, "byTenor", (p, k, e) => s.Volatility(SwapInputs.PeriodOf(p), k, e)));
        }

        check.Complete();
    }

    /// <summary>The surface a spec describes: the constructor for a settlement date, <see cref="CapFloorTermVolSurface.Create"/> for settlement days.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The surface, with extrapolation enabled when the spec says so.</returns>
    internal static CapFloorTermVolSurface SurfaceOf(JsonElement spec, EvaluationContext context)
    {
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        var bdc = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        Period[] tenors = [.. spec.GetProperty("optionTenors").EnumerateArray().Select(SwapInputs.PeriodOf)];
        double[] strikes = InterpolationInputs.Numbers(spec.GetProperty("strikes"));
        Matrix vols = InterpolationInputs.MatrixOf(spec.GetProperty("volatilities"));
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        CapFloorTermVolSurface surface = spec.TryGetProperty("settlementDays", out JsonElement days)
            ? CapFloorTermVolSurface.Create(days.GetInt32(), calendar, bdc, tenors, strikes, vols, context, dc)
            : new CapFloorTermVolSurface(new Date(spec.GetProperty("settlementDate").GetInt32()), calendar, bdc, tenors, strikes, vols, dc);
        return spec.GetProperty("enableExtrapolation").GetBoolean() ? surface.EnableExtrapolation() : surface;
    }

    private static object?[] Points(VectorCase c, string name, Func<JsonElement, double, bool, double> volatility) =>
        [.. c.Input(name).EnumerateArray().Select(p => (Func<object?>)(() => volatility(p[0], InterpolationInputs.Number(p[1]), p[2].GetBoolean())))];
}
