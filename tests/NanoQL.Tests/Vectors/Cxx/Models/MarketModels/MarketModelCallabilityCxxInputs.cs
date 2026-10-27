// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's market-model callability specs, as tools/cxx/src/models/marketmodels/callability/
/// callabilityspec.hpp reads them, and the callability vectors' checks, as callability.cpp reads QuantLib's.
/// </summary>
internal static class MarketModelCallabilityCxxInputs
{
    /// <summary>The exercise value a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>A new exercise value.</returns>
    public static MarketModelExerciseValue ExerciseValueOf(JsonElement spec) =>
        spec.GetProperty("type").GetString() == "BermudanSwaptionExerciseValue"
            ? new BermudanSwaptionExerciseValue(
                Reals(spec.GetProperty("rateTimes")),
                [.. spec.GetProperty("payoffs").EnumerateArray().Select(p => (Payoff)new PlainVanillaPayoff((OptionType)p[0].GetInt32(), p[1].GetDouble()))])
            : new NothingExerciseValue(
                Reals(spec.GetProperty("rateTimes")),
                [.. spec.GetProperty("isExerciseTime").EnumerateArray().Select(x => x.GetBoolean())]);

    /// <summary>The node-data provider a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>A new provider.</returns>
    public static MarketModelNodeDataProvider ProviderOf(JsonElement spec) =>
        spec.GetProperty("type").GetString() == "TriggeredSwapExercise" ? ParametricOf(spec) : BasisSystemOf(spec);

    /// <summary>The basis system a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>A new basis system.</returns>
    public static MarketModelBasisSystem BasisSystemOf(JsonElement spec) =>
        spec.GetProperty("type").GetString() == "SwapBasisSystem"
            ? new SwapBasisSystem(Reals(spec.GetProperty("rateTimes")), Reals(spec.GetProperty("exerciseTimes")))
            : new SwapForwardBasisSystem(Reals(spec.GetProperty("rateTimes")), Reals(spec.GetProperty("exerciseTimes")));

    /// <summary>The parametric exercise a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>A new parametric exercise.</returns>
    public static MarketModelParametricExercise ParametricOf(JsonElement spec) =>
        new TriggeredSwapExercise(
            Reals(spec.GetProperty("rateTimes")), Reals(spec.GetProperty("exerciseTimes")), Reals(spec.GetProperty("strikes")));

    /// <summary>The strategy a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>A new strategy.</returns>
    public static ExerciseStrategy<CurveState> StrategyOf(JsonElement spec)
    {
        switch (spec.GetProperty("type").GetString())
        {
            case "SwapRateTrigger":
                return new SwapRateTrigger(
                    Reals(spec.GetProperty("rateTimes")), Reals(spec.GetProperty("swapTriggers")), Reals(spec.GetProperty("exerciseTimes")));
            case "LongstaffSchwartzExerciseStrategy":
                return new LongstaffSchwartzExerciseStrategy(
                    BasisSystemOf(spec.GetProperty("basisSystem")),
                    Jagged(spec.GetProperty("basisCoefficients")),
                    new EvolutionDescription(Reals(spec.GetProperty("rateTimes")), Reals(spec.GetProperty("evolutionTimes"))),
                    Integers(spec.GetProperty("numeraires")),
                    ExerciseValueOf(spec.GetProperty("exercise")),
                    ExerciseValueOf(spec.GetProperty("control")));
            default:
                return new ParametricExerciseAdapter(
                    ParametricOf(spec.GetProperty("exercise")), Jagged(spec.GetProperty("parameters")).AsSpan());
        }
    }

    /// <summary>The evolver a spec describes: [type, model, generator, numeraires, initialStep].</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>A new evolver.</returns>
    public static MarketModelEvolver EvolverOf(JsonElement spec)
    {
        MarketModel model = ModelOf(spec[1]);
        BrownianGeneratorFactory factory = FactoryOf(spec[2]);
        int[] numeraires = Integers(spec[3]);
        int initialStep = spec[4].GetInt32();
        return spec[0].GetString() switch
        {
            "LogNormalFwdRatePc" => new LogNormalFwdRatePc(model, factory, numeraires, initialStep),
            "LogNormalFwdRateEuler" => new LogNormalFwdRateEuler(model, factory, numeraires, initialStep),
            "LogNormalFwdRateIpc" => new LogNormalFwdRateIpc(model, factory, numeraires, initialStep),
            "LogNormalFwdRateBalland" => new LogNormalFwdRateBalland(model, factory, numeraires, initialStep),
            _ => new NormalFwdRatePc(model, factory, numeraires, initialStep),
        };
    }

    /// <summary>Checks an exercise value vector file's cases: each stepped on its case's states.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckExerciseValueSteps(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModelExerciseValue value = ExerciseValueOf(c.Input("exerciseValue"));
            check.Output(c, "numberOfExercises", value.NumberOfExercises);
            check.Output(c, "possibleCashFlowTimes", value.PossibleCashFlowTimes());
            check.Output(c, "evolutionTimes", value.Evolution.EvolutionTimes);
            check.Output(c, "isExerciseTime", value.IsExerciseTime());
            var indices = new List<int>();
            var amounts = new List<double>();
            value.Reset();
            foreach (double[] f in c.Input("stepForwards").EnumerateArray().Select(Reals))
            {
                LMMCurveState cs = State(value.Evolution.RateTimes, f);
                value.NextStep(cs);
                MarketModelMultiProductCashFlow cf = value.Value(cs);
                indices.Add(cf.TimeIndex);
                amounts.Add(cf.Amount);
            }

            check.Output(c, "timeIndices", indices);
            check.Output(c, "amounts", amounts);
        }

        check.Complete();
    }

    /// <summary>
    /// Checks a node-data provider vector file's cases: each stepped on its case's states, its values at each exercise,
    /// and a parametric exercise's counts, guesses and decisions on them.
    /// </summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckProviderSteps(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModelNodeDataProvider provider = ProviderOf(c.Input("provider"));
            var parametric = provider as MarketModelParametricExercise;
            ImmutableArray<int> numberOfData = provider.NumberOfData();
            check.Output(c, "numberOfExercises", provider.NumberOfExercises);
            if (c.Expected.TryGetProperty("numberOfData", out _))
            {
                check.Output(c, "numberOfData", numberOfData);
            }

            check.Output(c, "evolutionTimes", provider.Evolution.EvolutionTimes);
            ImmutableArray<bool> isExercise = provider.IsExerciseTime();
            check.Output(c, "isExerciseTime", isExercise);
            if (parametric is not null)
            {
                check.Output(c, "numberOfVariables", parametric.NumberOfVariables());
                check.Output(c, "numberOfParameters", parametric.NumberOfParameters());
                check.Output(c, "guesses", Lazy(() => Guesses(parametric)));
            }

            var values = new List<double[]?>();
            var decisions = new List<bool?>();
            provider.Reset();
            int exercise = 0;
            ImmutableArray<double> rateTimes = provider.Evolution.RateTimes;
            double[][] forwards = [.. c.Input("stepForwards").EnumerateArray().Select(Reals)];
            for (int k = 0; k < forwards.Length; ++k)
            {
                LMMCurveState cs = State(rateTimes, forwards[k]);
                provider.NextStep(cs);
                if (isExercise[k])
                {
                    double[] v = new double[numberOfData[exercise]];
                    provider.Values(cs, v);
                    values.Add(v);
                    if (parametric is not null)
                    {
                        double[] g = new double[parametric.NumberOfParameters()[exercise]];
                        parametric.Guess(exercise, g);
                        decisions.Add(parametric.Exercise(exercise, g, v));
                    }

                    ++exercise;
                }
                else
                {
                    values.Add(null);
                    decisions.Add(null);
                }
            }

            check.Output(c, "values", values);
            if (parametric is not null)
            {
                check.Output(c, "decisions", decisions);
            }
        }

        check.Complete();
    }

    /// <summary>Checks a strategy vector file's cases: each stepped on its case's states, its decisions.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckStrategySteps(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            ExerciseStrategy<CurveState> strategy = StrategyOf(c.Input("strategy"));
            double[] rateTimes = Reals(c.Input("rateTimes"));
            ImmutableArray<double> relevant = strategy.RelevantTimes();
            ImmutableArray<double> exercises = strategy.ExerciseTimes();
            ImmutableArray<bool> isExercise = UtilitiesFunctions.IsInSubset(relevant.AsSpan(), exercises.AsSpan());
            check.Output(c, "exerciseTimes", exercises);
            check.Output(c, "relevantTimes", relevant);
            var decisions = new List<bool?>();
            strategy.Reset();
            double[][] forwards = [.. c.Input("stepForwards").EnumerateArray().Select(Reals)];
            for (int k = 0; k < forwards.Length; ++k)
            {
                LMMCurveState cs = State([.. rateTimes], forwards[k]);
                strategy.NextStep(cs);
                decisions.Add(isExercise[k] ? strategy.Exercise(cs) : null);
            }

            check.Output(c, "decisions", decisions);
        }

        check.Complete();
    }

    /// <summary>Checks a node-data collection vector file's cases: each case's node data.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckCollect(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            NodeData[][] data = Collect(c);
            check.Output(c, "exerciseValues", data.Select(r => r.Select(n => n.ExerciseValue).ToArray()).ToArray());
            check.Output(c, "cumulatedCashFlows", data.Select(r => r.Select(n => n.CumulatedCashFlows).ToArray()).ToArray());
            check.Output(c, "values", data.Select(r => r.Select(n => n.Values).ToArray()).ToArray());
            check.Output(c, "controlValues", data.Select(r => r.Select(n => n.ControlValue).ToArray()).ToArray());
            check.Output(c, "isValid", data.Select(r => r.Select(n => n.IsValid).ToArray()).ToArray());
        }

        check.Complete();
    }

    /// <summary>
    /// Checks a regression or optimisation vector file's cases: the collected data regressed, or optimised where the
    /// case gives a simplex, its estimate, coefficients or parameters, and row 0's cash flows after it.
    /// </summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckRegressionOrOptimization(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            NodeData[][] data = Collect(c);
            if (c.Inputs.TryGetProperty("simplexLambda", out JsonElement lambda))
            {
                JsonElement ec = c.Input("endCriteria");
                var criteria = new EndCriteria(ec[0].GetInt32(), ec[1].GetInt32(), ec[2].GetDouble(), ec[3].GetDouble(), ec[4].GetDouble());
                GenericEarlyExerciseOptimizationResults r = ParametricExerciseFunctions.GenericEarlyExerciseOptimization(
                    data, ParametricOf(c.Input("provider")), criteria, new Simplex(lambda.GetDouble()));
                check.Output(c, "estimate", r.BiasedEstimate);
                check.Output(c, "parameters", r.Parameters);
            }
            else
            {
                GenericLSRegressionResults r = GenericLSRegressionFunctions.GenericLongstaffSchwartzRegression(data);
                check.Output(c, "estimate", r.BiasedEstimate);
                check.Output(c, "coefficients", r.BasisCoefficients);
            }

            check.Output(c, "cumulatedCashFlows", data[0].Select(n => n.CumulatedCashFlows).ToArray());
        }

        check.Complete();
    }

    /// <summary>Checks an upper-bound engine vector file's cases: the engine's statistics over the outer paths.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckUpperBound(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var engine = new UpperBoundEngine(
                EvolverOf(c.Input("evolver")),
                [.. c.Input("innerEvolvers").EnumerateArray().Select(EvolverOf)],
                MarketModelProductCxxInputs.ProductOf(c.Input("underlying")),
                ExerciseValueOf(c.Input("rebate")),
                MarketModelProductCxxInputs.ProductOf(c.Input("hedge")),
                ExerciseValueOf(c.Input("hedgeRebate")),
                StrategyOf(c.Input("strategy")),
                c.Double("initialNumeraireValue"));
            var stats = new GeneralStatistics();
            engine.MultiplePathValues(stats, c.Input("outerPaths").GetInt32(), c.Input("innerPaths").GetInt32());
            check.Output(c, "samples", stats.Samples);
            check.Output(c, "weightSum", stats.WeightSum());
            check.Output(c, "mean", stats.Mean());
            check.Output(c, "errorEstimate", Lazy(() => stats.ErrorEstimate()));
        }

        check.Complete();
    }

    private static NodeData[][] Collect(VectorCase c) =>
        CollectNodeDataFunctions.CollectNodeData(
            EvolverOf(c.Input("evolver")),
            MarketModelProductCxxInputs.ProductOf(c.Input("product")),
            ProviderOf(c.Input("provider")),
            ExerciseValueOf(c.Input("rebate")),
            ExerciseValueOf(c.Input("control")),
            c.Input("paths").GetInt32());

    private static double[][] Guesses(MarketModelParametricExercise parametric)
    {
        var guesses = new double[parametric.NumberOfExercises][];
        for (int i = 0; i < guesses.Length; ++i)
        {
            guesses[i] = new double[parametric.NumberOfParameters()[i]];
            parametric.Guess(i, guesses[i]);
        }

        return guesses;
    }

    private static ImmutableArray<ImmutableArray<double>> Jagged(JsonElement rows) =>
        [.. rows.EnumerateArray().Select(r => ImmutableArray.Create(Reals(r)))];

    private static LMMCurveState State(ImmutableArray<double> rateTimes, double[] forwards)
    {
        var cs = new LMMCurveState(rateTimes.AsSpan());
        cs.SetOnForwardRates(forwards);
        return cs;
    }
}
