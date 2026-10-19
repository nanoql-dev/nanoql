// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="StrippedOptionletAdapter"/>'s harness vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/optionlet/strippedoptionletadapter.json</c>: the smile sections, which
/// the wheel doesn't bind, their fields and volatilities at strikes inside and outside the stripper's; and the
/// volatility by tenor. A section's at-the-money level isn't in the file: QuantLib's read of it
/// throws (<c>Divergences/NullRealSentinelTests.cs</c>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StrippedOptionletAdapterCxxVectorTests
{
    [Fact]
    public void StrippedOptionletAdapterCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/optionlet/strippedoptionletadapter");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            StrippedOptionletAdapter built = StrippedOptionletAdapter.Create(
                OptionletStripperCxxVectorTests.StripperOf(c.Input("stripper"), context), context);
            StrippedOptionletAdapter a = c.Input("enableExtrapolation").GetBoolean() ? built.EnableExtrapolation() : built;
            double[] times = InterpolationInputs.Numbers(c.Input("times"));
            double[] strikes = InterpolationInputs.Numbers(c.Input("strikes"));

            object?[] Field(Func<SmileSection, object?> f) =>
                [.. times.Select(t => (Func<object?>)(() => f(a.SmileSection(t))))];

            check.Output(c, "smile.exerciseTime", Field(s => s.ExerciseTime));
            check.Output(c, "smile.minStrike", Field(s => s.MinStrike()));
            check.Output(c, "smile.maxStrike", Field(s => s.MaxStrike()));
            check.Output(c, "smile.volatilityType", Field(s => s.VolatilityType));
            check.Output(c, "smile.shift", Field(s => s.Shift));
            check.Output(
                c,
                "smile.volatilities",
                Field(s => strikes.Select(k => (Func<object?>)(() => s.Volatility(k))).ToArray()));
            check.Output(
                c,
                "volatilityByTenor",
                c.Input("byTenor").EnumerateArray()
                    .Select(p => (Func<object?>)(() => a.Volatility(SwapInputs.PeriodOf(p[0]), InterpolationInputs.Number(p[1]), p[2].GetBoolean())))
                    .ToArray());
        }

        check.Complete();
    }
}
