// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="DiscretizedVanillaOption"/>'s harness vectors,
/// <c>tests/vectors/cxx/pricingengines/vanilla/discretizedvanillaoption.json</c>: the mandatory times of European,
/// American and Bermudan exercises on two day counters, with and without a grid to move them to.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscretizedVanillaOptionCxxVectorTests
{
    [Fact]
    public void DiscretizedVanillaOptionCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/discretizedvanillaoption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Date reference = c.DateInput("referenceDate");
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            var process = new BlackScholesMertonProcess(
                100.0, new FlatForward(reference, 0.01, dc), new FlatForward(reference, 0.03, dc), new BlackConstantVol(reference, null, 0.2, dc));
            Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            Exercise exercise = c.Input("exerciseType").GetString() switch
            {
                "European" => new EuropeanExercise(dates[0]),
                "American" => new AmericanExercise(dates[0], dates[1]),
                "Bermudan" => new BermudanExercise(dates),
                string type => throw new InvalidOperationException($"{c.Name}: unknown exercise {type}"),
                null => throw new InvalidOperationException($"{c.Name}: no exercise type"),
            };
            TimeGrid grid = BrownianBridgeVectorTests.Grid(c.Input("grid"));
            check.Output(
                c,
                "mandatoryTimes",
                () => new DiscretizedVanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), exercise, process, grid).MandatoryTimes());
        }

        check.Complete();
    }
}
