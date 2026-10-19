// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's smile section vectors, <c>tests/vectors/cxx/termstructures/volatility/smilesection.json</c>: on the
/// harness's own section, which keeps <see cref="SmileSection"/>'s defaults over a smile that varies with the strike,
/// the inspectors, and at 10 strikes the variance, the volatilities converted by
/// <see cref="SmileSection.Volatility(double, VolatilityType, double)"/>, the option and digital prices, the vega and
/// the density; <see cref="FlatSmileSection"/> by time with QuantLib's empty day counter, which the wheel can't build;
/// and the sections QuantLib refuses to build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SmileSectionCxxVectorTests
{
    [Fact]
    public void SmileSectionCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/smilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "section":
                    CheckSection(check, c, SmileSectionInputs.SectionOf(c.Input("section"), c.Context()));
                    break;
                case "flat":
                    SmileSection flat = SmileSectionInputs.SectionOf(c.Input("section"), c.Context());
                    check.Output(c, "dayCounter", flat.DayCounter?.Name);
                    check.Output(c, "exerciseDate", flat.ExerciseDate);
                    check.Output(c, "referenceDate", () => flat.ReferenceDate());
                    check.Output(c, "exerciseTime", flat.ExerciseTime);
                    check.Output(c, "volatility", flat.Volatility(c.Double("strike")));
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
        double gap = c.Double("gap");
        double densityGap = c.Double("densityGap");
        double targetShift = c.Double("targetShift");
        var ownType = (VolatilityType)c.Input("section").GetProperty("volatilityType").GetInt32();
        double ownShift = IndexInputs.Real(c.Input("section").GetProperty("shift"));

        check.Output(c, "minStrike", s.MinStrike());
        check.Output(c, "maxStrike", s.MaxStrike());
        check.Output(c, "atmLevel", s.AtmLevel());
        check.Output(c, "exerciseDate", s.ExerciseDate);
        check.Output(c, "referenceDate", () => s.ReferenceDate());
        check.Output(c, "exerciseTime", s.ExerciseTime);
        check.Output(c, "volatilityType", s.VolatilityType);
        check.Output(c, "shift", s.Shift);
        check.Output(c, "dayCounter", s.DayCounter?.Name);
        check.Output(c, "variance", AtStrikes(strikes, s.Variance));
        check.Output(c, "volatility", AtStrikes(strikes, k => s.Volatility(k)));
        check.Output(
            c, "lognormalVolatility", AtStrikes(strikes, k => s.Volatility(k, VolatilityType.ShiftedLognormal, targetShift)));
        check.Output(c, "normalVolatility", AtStrikes(strikes, k => s.Volatility(k, VolatilityType.Normal, 0.0)));
        check.Output(c, "ownVolatility", AtStrikes(strikes, k => s.Volatility(k, ownType, ownShift)));
        check.Output(c, "callPrice", AtStrikes(strikes, k => s.OptionPrice(k, OptionType.Call, discount)));
        check.Output(c, "putPrice", AtStrikes(strikes, k => s.OptionPrice(k, OptionType.Put, discount)));
        check.Output(c, "digitalCallPrice", AtStrikes(strikes, k => s.DigitalOptionPrice(k, OptionType.Call, discount, gap)));
        check.Output(c, "digitalPutPrice", AtStrikes(strikes, k => s.DigitalOptionPrice(k, OptionType.Put, discount, gap)));
        check.Output(c, "vega", AtStrikes(strikes, k => s.Vega(k, discount)));
        check.Output(c, "density", AtStrikes(strikes, k => s.Density(k, discount, densityGap)));
    }

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] AtStrikes(double[] strikes, Func<double, double> f) =>
        [.. strikes.Select(k => (Func<object?>)(() => f(k)))];
}
