// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The flat smile section vectors, <c>tests/vectors/termstructures/volatility/flatsmilesection.json</c>: on sections by
/// date with a reference date, by date from the evaluation context (<see cref="FlatSmileSection.Create"/>) and by time,
/// the inspectors, and at 10 strikes the variance, volatility, option and digital prices, vega, density and converted
/// volatilities of <see cref="SmileSection"/>; and the sections QuantLib refuses to build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FlatSmileSectionVectorTests
{
    [Fact]
    public void FlatSmileSectionVector()
    {
        var file = VectorFile.Load("termstructures/volatility/flatsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "section":
                    CheckSection(check, c, SmileSectionInputs.SectionOf(c.Input("section"), c.Context()));
                    break;
                case "refusal":
                    check.Output(c, "section", () => SmileSectionInputs.SectionOf(c.Input("section"), c.Context()).ExerciseTime);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckSection(VectorAssert check, VectorCase c, SmileSection s)
    {
        double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
        double discount = c.Double("discount");
        double targetShift = c.Double("targetShift");

        check.Output(c, "exerciseDate", s.ExerciseDate);
        check.Output(c, "referenceDate", () => s.ReferenceDate());
        check.Output(c, "exerciseTime", s.ExerciseTime);
        check.Output(c, "dayCounter", s.DayCounter?.Name);
        check.Output(c, "volatilityType", s.VolatilityType);
        check.Output(c, "shift", s.Shift);
        check.Output(c, "minStrike", s.MinStrike());
        check.Output(c, "maxStrike", s.MaxStrike());
        check.Output(c, "atmLevel", s.AtmLevel());
        check.Output(c, "variance", AtStrikes(strikes, s.Variance));
        check.Output(c, "volatility", AtStrikes(strikes, k => s.Volatility(k)));
        check.Output(c, "callPrice", AtStrikes(strikes, k => s.OptionPrice(k, OptionType.Call, discount)));
        check.Output(c, "putPrice", AtStrikes(strikes, k => s.OptionPrice(k, OptionType.Put, discount)));
        check.Output(c, "digitalCallPrice", AtStrikes(strikes, k => s.DigitalOptionPrice(k, OptionType.Call, discount)));
        check.Output(c, "digitalPutPrice", AtStrikes(strikes, k => s.DigitalOptionPrice(k, OptionType.Put, discount)));
        check.Output(c, "vega", AtStrikes(strikes, k => s.Vega(k, discount)));
        check.Output(c, "density", AtStrikes(strikes, k => s.Density(k, discount)));
        check.Output(
            c, "lognormalVolatility", AtStrikes(strikes, k => s.Volatility(k, VolatilityType.ShiftedLognormal, targetShift)));
        check.Output(c, "normalVolatility", Skipped(c, AtStrikes(strikes, k => s.Volatility(k, VolatilityType.Normal, 0.0))));
    }

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] AtStrikes(double[] strikes, Func<double, double> f) =>
        [.. strikes.Select(k => (Func<object?>)(() => f(k)))];

    // The case's "skip" strikes null: QuantLib's master's Bachelier inversions where 1.44 returns 0 (#2924), which the
    // generator leaves out (Divergences/BlackFormulaUpstreamFixesTests.cs).
    private static Func<object?>[] Skipped(VectorCase c, Func<object?>[] outputs)
    {
        foreach (JsonElement j in c.Input("skip").EnumerateArray())
        {
            outputs[j.GetInt32()] = () => null;
        }

        return outputs;
    }
}
