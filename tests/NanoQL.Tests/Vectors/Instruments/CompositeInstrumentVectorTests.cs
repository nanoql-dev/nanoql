// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The composite instrument vectors, <c>tests/vectors/instruments/compositeinstrument.json</c>:
/// <see cref="CompositeInstrument"/>'s NPV and expiry for composites of European options, added and subtracted with
/// multipliers, each priced by <see cref="AnalyticEuropeanEngine"/> on the generator's process.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CompositeInstrumentVectorTests
{
    [Fact]
    public void CompositeInstrumentVector()
    {
        var file = VectorFile.Load("instruments/compositeinstrument");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date today = context.EvaluationDate;
            DayCounter dc = new Actual365Fixed();
            var process = new BlackScholesMertonProcess(
                100.0,
                new FlatForward(today, 0.01, dc),
                new FlatForward(today, 0.03, dc),
                new BlackConstantVol(today, new NullCalendar(), 0.2, dc));

            var composite = new CompositeInstrument();
            foreach (JsonElement spec in c.Input("components").EnumerateArray())
            {
                var option = new EuropeanOption(
                    new PlainVanillaPayoff((OptionType)spec.GetProperty("type").GetInt32(), spec.GetProperty("strike").GetDouble()),
                    new EuropeanExercise(today + spec.GetProperty("maturityDays").GetInt32()));
                InstrumentResults Engine(EvaluationContext x) => AnalyticEuropeanEngine.Calculate(option, x, process);
                double multiplier = spec.GetProperty("multiplier").GetDouble();
                composite = spec.GetProperty("subtract").GetBoolean()
                    ? composite.Subtract(option, Engine, multiplier)
                    : composite.Add(option, Engine, multiplier);
            }

            check.Output(c, "NPV", () => composite.Calculate(context).NPV);
            check.Output(c, "isExpired", () => composite.IsExpired(context));
        }

        check.Complete();
    }
}
