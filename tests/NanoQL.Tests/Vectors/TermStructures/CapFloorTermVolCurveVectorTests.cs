// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="CapFloorTermVolCurve"/>'s vectors, <c>tests/vectors/termstructures/volatility/capfloor/capfloortermvolcurve.json</c>:
/// curves on fixed and moving reference dates, their inspectors and their volatilities by time, by date and by tenor,
/// inside and outside their range; and the curves QuantLib refuses to build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapFloorTermVolCurveVectorTests
{
    [Fact]
    public void CapFloorTermVolCurveVector()
    {
        var file = VectorFile.Load("termstructures/volatility/capfloor/capfloortermvolcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CapFloorTermVolCurve? curve = null;
            check.Output(c, "built", () =>
            {
                curve = CurveOf(c.Input("curve"), context);
                return true;
            });
            if (curve is null || !c.Inputs.TryGetProperty("byTime", out _))
            {
                continue;
            }

            CapFloorTermVolCurve s = curve;
            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "maxTime", s.MaxTime);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "businessDayConvention", s.BusinessDayConvention);
            check.Output(c, "volatilityByTime", Points(c, "byTime", (p, k, e) => s.Volatility(p.GetDouble(), k, e)));
            check.Output(c, "volatilityByDate", Points(c, "byDate", (p, k, e) => s.Volatility(new Date(p.GetInt32()), k, e)));
            check.Output(c, "volatilityByTenor", Points(c, "byTenor", (p, k, e) => s.Volatility(SwapInputs.PeriodOf(p), k, e)));
        }

        check.Complete();
    }

    /// <summary>The curve a spec describes: the constructor for a settlement date, <see cref="CapFloorTermVolCurve.Create"/> for settlement days.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The curve, with extrapolation enabled when the spec says so.</returns>
    internal static CapFloorTermVolCurve CurveOf(JsonElement spec, EvaluationContext context)
    {
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        var bdc = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        Period[] tenors = [.. spec.GetProperty("optionTenors").EnumerateArray().Select(SwapInputs.PeriodOf)];
        double[] vols = InterpolationInputs.Numbers(spec.GetProperty("volatilities"));
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        CapFloorTermVolCurve curve = spec.TryGetProperty("settlementDays", out JsonElement days)
            ? CapFloorTermVolCurve.Create(days.GetInt32(), calendar, bdc, tenors, vols, context, dc)
            : new CapFloorTermVolCurve(new Date(spec.GetProperty("settlementDate").GetInt32()), calendar, bdc, tenors, vols, dc);
        return spec.GetProperty("enableExtrapolation").GetBoolean() ? curve.EnableExtrapolation() : curve;
    }

    private static object?[] Points(VectorCase c, string name, Func<JsonElement, double, bool, double> volatility) =>
        [.. c.Input(name).EnumerateArray().Select(p => (Func<object?>)(() => volatility(p[0], InterpolationInputs.Number(p[1]), p[2].GetBoolean())))];
}
