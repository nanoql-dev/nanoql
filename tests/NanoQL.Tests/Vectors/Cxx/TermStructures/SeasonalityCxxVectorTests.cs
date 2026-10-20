// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's seasonality vectors, <c>tests/vectors/cxx/termstructures/inflation/seasonality.json</c>:
/// <see cref="Seasonality"/>'s default <see cref="Seasonality.IsConsistent"/>, which every QuantLib seasonality
/// overrides, on the harness's own seasonality, which overrides only the two corrections, and the rates of a curve built
/// with it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SeasonalityCxxVectorTests
{
    [Fact]
    public void SeasonalityCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/inflation/seasonality");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("curve");
            InflationTermStructure plain = InflationInputs.CurveOf(spec);
            check.Output(c, "isConsistent", new BaseSeasonality().IsConsistent(plain));
            InflationTermStructure curve = plain.SetSeasonality(new BaseSeasonality(), c.Context());
            check.Output(
                c,
                "rate",
                c.DateInputs("dates").Select(d => (Func<object?>)(() => curve is ZeroInflationTermStructure zero
                    ? zero.ZeroRate(d)
                    : ((YoYInflationTermStructure)curve).YoyRate(d))).ToArray());
        }

        check.Complete();
    }

    // The harness's own seasonality: it overrides only what Seasonality leaves abstract.
    private sealed class BaseSeasonality : Seasonality
    {
        public override double CorrectZeroRate(Date d, double r, InflationTermStructure iTS) => r + 0.001;

        public override double CorrectYoYRate(Date d, double r, InflationTermStructure iTS) => r - 0.001;
    }
}
