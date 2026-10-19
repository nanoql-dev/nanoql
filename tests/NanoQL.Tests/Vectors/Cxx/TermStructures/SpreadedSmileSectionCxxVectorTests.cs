// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's spreaded smile section vectors, <c>tests/vectors/cxx/termstructures/volatility/spreadedsmilesection.json</c>:
/// on <see cref="SpreadedSmileSection"/> over flat and SABR sections by date, from the evaluation context and by time,
/// every inspector it forwards to the underlying section, and at the underlying's strikes the spreaded volatility, the
/// variance, the option and digital prices, the vega and the density.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SpreadedSmileSectionCxxVectorTests
{
    [Fact]
    public void SpreadedSmileSectionCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/spreadedsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "section":
                    JsonElement spec = c.Input("section");
                    SmileSection underlying = SmileSectionInputs.SectionOf(spec.GetProperty("underlying"), c.Context());
                    CheckSection(check, c, new SpreadedSmileSection(underlying, IndexInputs.Real(spec.GetProperty("spread"))));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckSection(VectorAssert check, VectorCase c, SpreadedSmileSection s)
    {
        double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
        double discount = c.Double("discount");
        double gap = c.Double("gap");
        double densityGap = c.Double("densityGap");

        check.Output(c, "minStrike", s.MinStrike());
        check.Output(c, "maxStrike", s.MaxStrike());
        check.Output(c, "atmLevel", s.AtmLevel());
        check.Output(c, "exerciseDate", s.ExerciseDate);
        check.Output(c, "exerciseTime", s.ExerciseTime);
        check.Output(c, "dayCounter", s.DayCounter?.Name);
        check.Output(c, "referenceDate", () => s.ReferenceDate());
        check.Output(c, "volatilityType", s.VolatilityType);
        check.Output(c, "shift", s.Shift);
        check.Output(c, "volatility", AtStrikes(strikes, k => s.Volatility(k)));
        check.Output(c, "variance", AtStrikes(strikes, s.Variance));
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
