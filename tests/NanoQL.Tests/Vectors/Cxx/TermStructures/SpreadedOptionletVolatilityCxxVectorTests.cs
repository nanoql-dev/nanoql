// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="SpreadedOptionletVolatility"/>'s harness vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/optionlet/spreadedoptionletvol.json</c>: spreads over constant
/// optionlet volatilities and stripped adapters, the structure's inspectors (its base's, its calendar among them, and
/// its extrapolation flag copied), its volatilities by time and by date and its smile sections' volatilities.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SpreadedOptionletVolatilityCxxVectorTests
{
    [Fact]
    public void SpreadedOptionletVolatilityCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/optionlet/spreadedoptionletvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var s = new SpreadedOptionletVolatility(BaseOf(c.Input("base"), context), InterpolationInputs.Number(c.Input("spread")));
            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "maxTime", s.MaxTime);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "volatilityType", s.VolatilityType);
            check.Output(c, "displacement", s.Displacement);

            // QuantLib's refusal for a base on a fixed date is nanoql's null.
            check.Mapped(c, "settlementDays", () => s.SettlementDays, null, "a fixed reference date's settlement days are null");
            check.Output(c, "calendar", s.Calendar?.Name);
            check.Output(c, "businessDayConvention", s.BusinessDayConvention);
            check.Output(c, "allowsExtrapolation", s.AllowsExtrapolation);
            check.Output(
                c,
                "volatilityByTime",
                c.Input("byTime").EnumerateArray()
                    .Select(p => (Func<object?>)(() => s.Volatility(p[0].GetDouble(), InterpolationInputs.Number(p[1]), p[2].GetBoolean())))
                    .ToArray());
            check.Output(
                c,
                "volatilityByDate",
                c.Input("byDate").EnumerateArray()
                    .Select(p => (Func<object?>)(() => s.Volatility(new Date(p[0].GetInt32()), InterpolationInputs.Number(p[1]), p[2].GetBoolean())))
                    .ToArray());
            double[] strikes = InterpolationInputs.Numbers(c.Input("strikes"));
            check.Output(
                c,
                "smileSections",
                InterpolationInputs.Numbers(c.Input("times"))
                    .Select(t => (Func<object?>)(() =>
                    {
                        SmileSection section = s.SmileSection(t, true);
                        return strikes.Select(k => (Func<object?>)(() => section.Volatility(k))).ToArray();
                    }))
                    .ToArray());
        }

        check.Complete();
    }

    private static OptionletVolatilityStructure BaseOf(JsonElement spec, EvaluationContext context)
    {
        if (spec.GetProperty("kind").GetString() == "adapter")
        {
            StrippedOptionletAdapter adapter = StrippedOptionletAdapter.Create(
                OptionletStripperCxxVectorTests.StripperOf(spec.GetProperty("stripper"), context), context);
            return spec.GetProperty("enableExtrapolation").GetBoolean() ? adapter.EnableExtrapolation() : adapter;
        }

        return VolatilityInputs.OptionletVolatilityOf(spec.GetProperty("structure"), context);
    }
}
