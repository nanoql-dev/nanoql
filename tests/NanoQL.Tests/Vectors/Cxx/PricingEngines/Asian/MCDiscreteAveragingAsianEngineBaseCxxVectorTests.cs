// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

[Trait("Oracle", "QuantLib")]
public class MCDiscreteAveragingAsianEngineBaseCxxVectorTests
{
    [Fact]
    public void MCDiscreteAveragingAsianEngineBaseCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/asian/mcdiscreteasianenginebase");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date today = context.EvaluationDate;
            var option = new DiscreteAveragingAsianOption(
                AverageType.Arithmetic, 0.0, 0, c.Ints("fixingDays").Select(d => today + d), new PlainVanillaPayoff(OptionType.Call, 100.0),
                new EuropeanExercise(today + c.Int("exerciseDays")));
            var engine = new GridEngine(option, context, ProcessAt(today), c.NullableInt("steps"), c.NullableInt("stepsPerYear"), c.Bool("includeExerciseDate"));
            var grid = new Lazy<NanoQL.TimeGrid>(engine.Grid);
            check.Output(c, "times", Lazy(() => Enumerable.Range(0, grid.Value.Size).Select(i => grid.Value[i]).ToArray()));
            check.Output(c, "mandatoryTimes", Lazy(() => grid.Value.MandatoryTimes.ToArray()));
        }

        check.Complete();
    }

    private static GeneralizedBlackScholesProcess ProcessAt(Date today)
    {
        DayCounter dc = new Actual365Fixed();
        return new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.01, dc), new FlatForward(today, 0.03, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.2, dc));
    }

    // the base with a path pricer it never builds, its protected grid made public
    private sealed class GridEngine(
        DiscreteAveragingAsianOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, int? steps, int? stepsPerYear, bool include)
        : MCDiscreteAveragingAsianEngineBase<SingleVariate<PseudoRandom>, MonteCarloPath, GeneralStatistics>(
            option, context, process, false, false, false, 10, null, null, 42, steps, stepsPerYear, include)
    {
        public NanoQL.TimeGrid Grid() => TimeGrid();

        protected override PathPricer<MonteCarloPath> PathPricer() => throw new NotSupportedException();
    }
}
