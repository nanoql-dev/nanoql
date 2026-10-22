// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SviSmileSection vectors, <c>tests/vectors/experimental/volatility/svismilesection.json</c>:
/// <see cref="SviSmileSection"/>'s volatility and variance at strikes across and below its floor, its ATM level,
/// strike range and exercise time, on five parameter sets from a time and from a date; and QuantLib's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SviSmileSectionVectorTests
{
    [Fact]
    public void SviSmileSectionVector()
    {
        var file = VectorFile.Load("experimental/volatility/svismilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("section");
            SviSmileSection Build()
            {
                double forward = spec.GetProperty("forward").GetDouble();
                double[] parameters = Doubles(spec.GetProperty("parameters"));
                if (spec.TryGetProperty("timeToExpiry", out JsonElement t))
                {
                    return new SviSmileSection(t.GetDouble(), forward, parameters);
                }

                JsonElement dc = spec.GetProperty("dayCounter");
                return SviSmileSection.Create(
                    new Date(spec.GetProperty("date").GetInt32()),
                    forward,
                    parameters,
                    context,
                    dc.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.DayCounterOf(dc));
            }

            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => Build());
                continue;
            }

            SviSmileSection section = Build();
            double[] strikes = Doubles(c.Input("strikes"));
            check.Output(c, "volatility", strikes.Select(k => Lazy(() => section.Volatility(k))).ToArray());
            check.Output(c, "variance", strikes.Select(k => Lazy(() => section.Variance(k))).ToArray());
            check.Output(c, "atmLevel", section.AtmLevel());
            check.Output(c, "minStrike", section.MinStrike());
            check.Output(c, "maxStrike", section.MaxStrike());
            check.Output(c, "exerciseTime", section.ExerciseTime);
        }

        check.Complete();
    }
}
