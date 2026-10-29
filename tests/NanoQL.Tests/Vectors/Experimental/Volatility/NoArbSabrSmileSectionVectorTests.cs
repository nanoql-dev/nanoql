// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The no-arbitrage SABR section vectors, <c>tests/vectors/experimental/volatility/noarbsabrsmilesection.json</c>:
/// <see cref="NoArbSabrSmileSection"/>'s prices, digitals, density, volatilities, at-the-money level and strike range
/// on five parameter sets and two sections dated by an exercise date (with their exercise times), and two refusals.
/// The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NoArbSabrSmileSectionVectorTests
{
    [Fact]
    public void NoArbSabrSmileSectionVector()
    {
        var file = VectorFile.Load("experimental/volatility/noarbsabrsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("section");
            double[] parameters = Doubles(s.GetProperty("params"));
            EvaluationContext context = c.Context();
            var section = new Lazy<NoArbSabrSmileSection>(() => s.TryGetProperty("exerciseDays", out JsonElement days)
                ? NoArbSabrSmileSection.Create(
                    context.EvaluationDate + days.GetInt32(),
                    s.GetProperty("forward").GetDouble(),
                    parameters,
                    context,
                    CashFlowInputs.DayCounterOf(s.GetProperty("dayCounter")),
                    s.GetProperty("shift").GetDouble())
                : new NoArbSabrSmileSection(
                    s.GetProperty("expiry").GetDouble(), s.GetProperty("forward").GetDouble(), parameters, s.GetProperty("shift").GetDouble()));
            double[] strikes = Doubles(c.Input("strikes"));
            check.Output(c, "atmLevel", () => section.Value.AtmLevel());
            if (!c.Expected.TryGetProperty("call", out _))
            {
                continue;
            }

            check.Output(c, "minStrike", () => section.Value.MinStrike());
            check.Output(c, "maxStrike", () => section.Value.MaxStrike());
            if (c.Expected.TryGetProperty("exerciseTime", out _))
            {
                check.Output(c, "exerciseTime", () => section.Value.ExerciseTime);
            }

            check.Output(c, "call", strikes.Select(k => Lazy(() => section.Value.OptionPrice(k, OptionType.Call))).ToArray());
            check.Output(c, "put", strikes.Select(k => Lazy(() => section.Value.OptionPrice(k, OptionType.Put))).ToArray());
            check.Output(c, "digitalCall", strikes.Select(k => Lazy(() => section.Value.DigitalOptionPrice(k, OptionType.Call))).ToArray());
            check.Output(c, "digitalPut", strikes.Select(k => Lazy(() => section.Value.DigitalOptionPrice(k, OptionType.Put))).ToArray());
            check.Output(c, "density", strikes.Select(k => Lazy(() => section.Value.Density(k))).ToArray());
            check.Output(c, "volatility", strikes.Select(k => Lazy(() => section.Value.Volatility(k))).ToArray());
        }

        check.Complete();
    }
}
