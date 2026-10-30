// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ZABR smile section vectors, <c>tests/vectors/termstructures/volatility/zabrsmilesection.json</c>: on
/// <see cref="ZabrShortMaturityLognormalSmileSection"/>, <see cref="ZabrShortMaturityNormalSmileSection"/>,
/// <see cref="ZabrLocalVolatilitySmileSection"/> and <see cref="ZabrFullFdSmileSection"/>, by
/// exercise time and by date from the evaluation context (<c>Create</c>), the inspectors, and at 12 strikes the
/// variance, volatility, option and digital prices, vega, density and converted normal volatility of
/// <see cref="SmileSection"/>; and the sections QuantLib refuses to build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZabrSmileSectionVectorTests
{
    [Fact]
    public void ZabrSmileSectionVector()
    {
        var file = VectorFile.Load("termstructures/volatility/zabrsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "section":
                    CheckSection(check, c, SectionOf(c.Input("section"), c.Context()));
                    break;
                case "refusal":
                    check.Output(c, "section", () => SectionOf(c.Input("section"), c.Context()).ExerciseTime);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    /// <summary>The named section a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, from which a floating section takes its reference date.</param>
    /// <returns>The section.</returns>
    internal static SmileSection SectionOf(JsonElement spec, EvaluationContext context)
    {
        string kernel = spec.GetProperty("kernel").GetString()!;
        double forward = IndexInputs.Real(spec.GetProperty("forward"));
        double[] parameters = VolatilityInputs.Reals(spec.GetProperty("parameters"));
        double[] moneyness = VolatilityInputs.Reals(spec.GetProperty("moneyness"));
        int fdRefinement = spec.GetProperty("fdRefinement").GetInt32();
        if (spec.GetProperty("form").GetString() == "time")
        {
            double t = IndexInputs.Real(spec.GetProperty("exerciseTime"));
            return kernel switch
            {
                "ShortMaturityLognormal" => new ZabrShortMaturityLognormalSmileSection(t, forward, parameters, moneyness, fdRefinement),
                "ShortMaturityNormal" => new ZabrShortMaturityNormalSmileSection(t, forward, parameters, moneyness, fdRefinement),
                "LocalVolatility" => new ZabrLocalVolatilitySmileSection(t, forward, parameters, moneyness, fdRefinement),
                "FullFd" => new ZabrFullFdSmileSection(t, forward, parameters, moneyness, fdRefinement),
                _ => throw new InvalidOperationException($"unknown ZABR kernel '{kernel}'"),
            };
        }

        var d = new Date(spec.GetProperty("exerciseDate").GetInt32());
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        return kernel switch
        {
            "ShortMaturityLognormal" => ZabrShortMaturityLognormalSmileSection.Create(
                d, forward, parameters, context, dc, moneyness, fdRefinement),
            "ShortMaturityNormal" => ZabrShortMaturityNormalSmileSection.Create(
                d, forward, parameters, context, dc, moneyness, fdRefinement),
            "LocalVolatility" => ZabrLocalVolatilitySmileSection.Create(
                d, forward, parameters, context, dc, moneyness, fdRefinement),
            "FullFd" => ZabrFullFdSmileSection.Create(
                d, forward, parameters, context, dc, moneyness, fdRefinement),
            _ => throw new InvalidOperationException($"unknown ZABR kernel '{kernel}'"),
        };
    }

    private static void CheckSection(VectorAssert check, VectorCase c, SmileSection s)
    {
        double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
        double discount = c.Double("discount");

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
        check.Output(c, "vega", AtStrikes(strikes, k => s.Vega(k, discount)));
        check.Output(c, "density", AtStrikes(strikes, k => s.Density(k, discount)));
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
