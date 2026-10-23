// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's Kahale smile section vectors, <c>tests/vectors/termstructures/volatility/kahalesmilesection.json</c>:
/// <see cref="KahaleSmileSection"/> over QuantLib's own test smiles (linearly interpolated, arbitrage free and with an
/// arbitrageable point) and an unshifted SABR smile, plain, interpolated, exponentially extrapolated and deleting
/// arbitrage points, and with the core forced to given indices: its inspectors, and at strikes in the grid and in both
/// wings the call and put prices, the volatility, the digital call price and the density, each where stable (the S7
/// plan's S7R6).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class KahaleSmileSectionVectorTests
{
    [Fact]
    public void KahaleSmileSectionVector()
    {
        var file = VectorFile.Load("termstructures/volatility/kahalesmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("kahale");
            JsonElement sourceSpec = spec.GetProperty("source");
            SmileSection source = sourceSpec.GetProperty("type").GetString() == "SabrSmileSection"
                ? SmileSectionInputs.SectionOf(sourceSpec, c.Context())
                : InterpolatedSmileSectionVectorTests.SectionOf(sourceSpec, c.Context());
            var s = new KahaleSmileSection(
                source,
                SmileSectionInputs.NullableReal(spec.GetProperty("atm")),
                spec.GetProperty("interpolate").GetBoolean(),
                spec.GetProperty("exponentialExtrapolation").GetBoolean(),
                spec.GetProperty("deleteArbitragePoints").GetBoolean(),
                VolatilityInputs.Reals(spec.GetProperty("moneynessGrid")),
                IndexInputs.Real(spec.GetProperty("gap")),
                spec.GetProperty("forcedLeftIndex").GetInt32(),
                spec.GetProperty("forcedRightIndex").GetInt32());
            double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
            var outputs = new Dictionary<string, Func<object?>>
            {
                ["minStrike"] = () => s.MinStrike(),
                ["maxStrike"] = () => s.MaxStrike(),
                ["atmLevel"] = () => s.AtmLevel(),
                ["exerciseTime"] = () => s.ExerciseTime,
            };
            for (int i = 0; i < strikes.Length; i++)
            {
                double k = strikes[i];
                outputs[$"call[{i}]"] = () => s.OptionPrice(k, OptionType.Call, 1.0);
                outputs[$"put[{i}]"] = () => s.OptionPrice(k, OptionType.Put, 1.0);
                outputs[$"volatility[{i}]"] = () => s.Volatility(k);
                outputs[$"digital[{i}]"] = () => s.DigitalOptionPrice(k, OptionType.Call, 1.0, 1.0e-5);
                outputs[$"density[{i}]"] = () => s.Density(k, 1.0, 1.0e-4);
            }

            foreach (JsonProperty output in c.Expected.EnumerateObject())
            {
                check.Output(c, output.Name, outputs[output.Name]);
            }
        }

        check.Complete();
    }
}
