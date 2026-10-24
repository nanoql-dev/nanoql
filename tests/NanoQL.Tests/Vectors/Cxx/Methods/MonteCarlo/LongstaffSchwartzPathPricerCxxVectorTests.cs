// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's LongstaffSchwartzPathPricer vectors,
/// <c>tests/vectors/cxx/methods/montecarlo/longstaffschwartzpathpricer.json</c>: on fixed path sets, the calibration's
/// values, the regression's coefficients, each call of the post-processing hook, the pricing paths' values and the
/// exercise probability, on <see cref="AmericanPathPricer"/> over five bases, every time of the grid an exercise time.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LongstaffSchwartzPathPricerCxxVectorTests
{
    [Fact]
    public void LongstaffSchwartzPathPricerCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/montecarlo/longstaffschwartzpathpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var grid = new NanoQL.TimeGrid(c.Doubles("times"));
            JsonElement p = c.Input("payoff");
            var payoff = new PlainVanillaPayoff((OptionType)p.GetProperty("optionType").GetInt32(), p.GetProperty("strike").GetDouble());
            var early = new AmericanPathPricer(payoff, c.Int("polynomialOrder"), (LsmBasisSystemPolynomialType)c.Int("polynomialType"));
            var curve = new FlatForward(c.DateInput("referenceDate"), c.Double("rate"), new Actual365Fixed());
            var pricer = new Recording(grid, early, curve, [.. Enumerable.Range(1, grid.Size - 1).Select(i => grid[i])]);

            double[] calibration = [.. Paths(c, "calibrationPaths", grid).Select(pricer.Evaluate)];
            pricer.Calibrate();
            double[] prices = [.. Paths(c, "pricingPaths", grid).Select(pricer.Evaluate)];

            check.Output(c, "calibration", calibration);
            check.Output(c, "coefficients", pricer.CoefficientRows());
            check.Output(c, "hookSteps", pricer.Steps.ToArray());
            check.Output(c, "hookStates", pricer.States.ToArray());
            check.Output(c, "hookPrices", pricer.Prices.ToArray());
            check.Output(c, "hookExercises", pricer.Exercises.ToArray());
            check.Output(c, "prices", prices);
            check.Output(c, "exerciseProbability", pricer.ExerciseProbability());
        }

        check.Complete();
    }

    private static IEnumerable<MonteCarloPath> Paths(VectorCase c, string name, NanoQL.TimeGrid grid) =>
        c.Input(name).EnumerateArray().Select(e => new MonteCarloPath(grid, new Vector(EquityInputs.Doubles(e))));

    // The pricer with its protected coefficients and post-processing hook read, as the harness's class derived from
    // QuantLib's reads them.
    private sealed class Recording(
        NanoQL.TimeGrid times, AmericanPathPricer early, YieldTermStructure curve, IReadOnlyList<double> exerciseTimes)
        : LongstaffSchwartzPathPricer<MonteCarloPath, double>(times, early, curve, exerciseTimes)
    {
        public List<object?> Steps { get; } = [];

        public List<object?> States { get; } = [];

        public List<object?> Prices { get; } = [];

        public List<object?> Exercises { get; } = [];

        public object?[] CoefficientRows() => [.. Coefficients.Select(v => (object?)v.AsSpan().ToArray())];

        protected override void PostProcessing(
            int i, IReadOnlyList<double> state, IReadOnlyList<double> price, IReadOnlyList<double> exercise)
        {
            Steps.Add(i);
            States.Add(state.ToArray());
            Prices.Add(price.ToArray());
            Exercises.Add(exercise.ToArray());
        }
    }
}
