// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The seasonality vectors, <c>tests/vectors/termstructures/inflation/seasonality.json</c>: the multiplicative and
/// Kerkhof seasonalities' inspectors and factors around their base dates, their construction refusals, and their
/// corrections of zero-inflation and year-on-year curves' rates, with the refusal of a multi-year seasonality
/// inconsistent with its curve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SeasonalityVectorTests
{
    [Fact]
    public void SeasonalityVector()
    {
        var file = VectorFile.Load("termstructures/inflation/seasonality");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "factors":
                    var s = (MultiplicativePriceSeasonality)InflationInputs.SeasonalityOf(c.Input("seasonality"))!;
                    check.Output(c, "seasonalityBaseDate", s.SeasonalityBaseDate);
                    check.Output(c, "frequency", s.Frequency);
                    check.Output(c, "seasonalityFactors", s.SeasonalityFactors());
                    check.Output(
                        c,
                        "seasonalityFactor",
                        c.DateInputs("dates").Select(d => (Func<object?>)(() => s.SeasonalityFactor(d))).ToArray());
                    break;
                case "construction":
                    check.Output(c, "construction", () => InflationInputs.SeasonalityOf(c.Input("seasonality")) is null ? 1 : 0);
                    break;
                case "corrections":
                    check.Output(c, "construction", () => InflationInputs.CurveOf(c.Input("curve")) is null ? 1 : 0);
                    if (c.Expected.TryGetProperty("rate", out _))
                    {
                        InflationTermStructure curve = InflationInputs.CurveOf(c.Input("curve"));
                        check.Output(
                            c,
                            "rate",
                            c.DateInputs("dates").Select(d => (Func<object?>)(() => RateOf(curve, d))).ToArray());
                    }

                    break;
                default:
                    throw new InvalidOperationException($"unknown case kind in {c.Name}");
            }
        }

        check.Complete();
    }

    private static double RateOf(InflationTermStructure curve, Date d) =>
        curve is ZeroInflationTermStructure zero ? zero.ZeroRate(d) : ((YoYInflationTermStructure)curve).YoyRate(d);
}
