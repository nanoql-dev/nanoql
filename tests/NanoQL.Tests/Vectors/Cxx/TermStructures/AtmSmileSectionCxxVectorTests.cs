// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's ATM smile section vectors, <c>tests/vectors/cxx/termstructures/volatility/atmsmilesection.json</c>:
/// <see cref="AtmSmileSection"/> over harness and SABR sections, with the source's level and its own, every inspector,
/// and at strikes around the level the volatility, the variance, the option and digital prices, the vega and the
/// density; and <see cref="AtmAdjustedSmileSection"/>, recentred and not, its inspectors, volatility and variance.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AtmSmileSectionCxxVectorTests
{
    [Fact]
    public void AtmSmileSectionCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/atmsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("section");
            SmileSection source = SmileSectionInputs.SectionOf(spec.GetProperty("source"), c.Context());
            double? atm = SmileSectionInputs.NullableReal(spec.GetProperty("atm"));
            double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
            SmileSection s = c.String("class") == "AtmSmileSection"
                ? new AtmSmileSection(source, atm)
                : new AtmAdjustedSmileSection(source, atm, spec.GetProperty("recenterSmile").GetBoolean());

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
            if (s is AtmSmileSection)
            {
                check.Output(c, "call", AtStrikes(strikes, k => s.OptionPrice(k, OptionType.Call, 0.97)));
                check.Output(c, "put", AtStrikes(strikes, k => s.OptionPrice(k, OptionType.Put, 0.97)));
                check.Output(c, "digitalCall", AtStrikes(strikes, k => s.DigitalOptionPrice(k, OptionType.Call, 0.97, 1.0e-5)));
                check.Output(c, "digitalPut", AtStrikes(strikes, k => s.DigitalOptionPrice(k, OptionType.Put, 0.97, 1.0e-5)));
                check.Output(c, "vega", AtStrikes(strikes, k => s.Vega(k, 0.97)));
                check.Output(c, "density", AtStrikes(strikes, k => s.Density(k, 0.97, 1.0e-4)));
            }
        }

        check.Complete();
    }

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] AtStrikes(double[] strikes, Func<double, double> f) =>
        [.. strikes.Select(k => (Func<object?>)(() => f(k)))];
}
