// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's discretized barrier option vectors,
/// <c>tests/vectors/cxx/pricingengines/barrier/discretizedbarrieroption.json</c>: the rollback of
/// <see cref="DiscretizedBarrierOption"/> and <see cref="DiscretizedDermanKaniBarrierOption"/> on a Cox-Ross-Rubinstein
/// lattice, their present values and mandatory times, and <see cref="DiscretizedBarrierOption.CheckBarrier"/> on given
/// values; the option and lattice as <c>tools/cxx/src/pricingengines/barrier/discretizedbarrieroption.cpp</c> builds
/// them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscretizedBarrierOptionCxxVectorTests
{
    [Fact]
    public void DiscretizedBarrierOptionCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/barrier/discretizedbarrieroption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Setup s = SetupOf(c.Input("option"));
            if (c.Inputs.TryGetProperty("enhanced", out _))
            {
                DiscretizedAsset asset = c.Bool("enhanced")
                    ? new DiscretizedDermanKaniBarrierOption(s.Option, s.Process, s.Grid)
                    : new DiscretizedBarrierOption(s.Option, s.Process, s.Grid);
                asset.Initialize(s.Lattice, s.Maturity);
                asset.Rollback(0.0);
                check.Output(c, "presentValue", asset.PresentValue());
                check.Output(c, "mandatoryTimes", asset.MandatoryTimes().Select(t => (object?)t).ToArray());
            }
            else
            {
                var asset = new DiscretizedBarrierOption(s.Option, s.Process, s.Grid);
                double t = s.Grid[c.Int("step")];
                asset.Initialize(s.Lattice, t);
                double[] values = c.Doubles("values");
                asset.CheckBarrier(values, s.Lattice.Grid(t).AsSpan());
                check.Output(c, "values", values.Select(v => (object?)v).ToArray());
            }
        }

        check.Complete();
    }

    // The harness's process, option and lattice for an option spec
    private static Setup SetupOf(JsonElement o)
    {
        var reference = new Date(o.GetProperty("referenceDate").GetInt32());
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(reference, 0.01, dc), new FlatForward(reference, 0.03, dc),
            new BlackConstantVol(reference, new NullCalendar(), 0.2, dc));
        Date[] dates = [.. o.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
        Exercise exercise = o.GetProperty("exerciseType").GetString() switch
        {
            "European" => new EuropeanExercise(dates[0]),
            "American" => new AmericanExercise(dates[0], dates[1]),
            _ => new BermudanExercise(dates),
        };
        double strike = o.GetProperty("strike").GetDouble();
        var option = new BarrierOption(
            (BarrierType)o.GetProperty("barrierType").GetInt32(),
            o.GetProperty("barrier").GetDouble(),
            o.GetProperty("rebate").GetDouble(),
            new PlainVanillaPayoff((OptionType)o.GetProperty("optionType").GetInt32(), strike),
            exercise);
        int steps = o.GetProperty("steps").GetInt32();
        double maturity = process.Time(dates[^1]);
        var tree = new CoxRossRubinstein(process, maturity, steps, strike);
        return new Setup(process, option, maturity, new TimeGrid(maturity, steps), new BlackScholesLattice<CoxRossRubinstein>(tree, 0.03, maturity, steps));
    }

    private sealed record Setup(
        GeneralizedBlackScholesProcess Process, BarrierOption Option, double Maturity, TimeGrid Grid, Lattice Lattice);
}
