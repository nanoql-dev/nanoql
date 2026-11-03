// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

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

/// <summary>
/// The C++ harness's Monte Carlo lookback vectors,
/// <c>tests/vectors/cxx/pricingengines/lookback/mclookbackengine.json</c>: the checks of
/// <see cref="MakeMCLookbackEngine{TOption, TRng, TStats}"/> for each of the four lookback options; and the fixed and
/// floating options' prices through <see cref="MCPRLookbackEngine"/> and <see cref="MCLDLookbackEngine"/>, within the
/// tolerance their file sets; the processes as <c>tools/cxx/src/pricingengines/lookback/mclookbackengine.cpp</c> builds
/// them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCLookbackEngineCxxVectorTests
{
    [Fact]
    public void MCLookbackEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/lookback/mclookbackengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("priced", out JsonElement p))
            {
                EvaluationContext context = c.Context();
                var priced = new Lazy<OneAssetOptionResults>(() => Priced(p, context));
                check.Output(c, "NPV", Lazy(() => priced.Value.NPV));
                if (c.Expected.TryGetProperty("errorEstimate", out _))
                {
                    check.Output(c, "errorEstimate", Lazy(() => priced.Value.ErrorEstimate));
                }

                continue;
            }

            GeneralizedBlackScholesProcess process = ProcessAt(c.Context().EvaluationDate);
            check.Output(c, "built", Lazy(() => c.String("instrument") switch
            {
                "fixed" => Built<ContinuousFixedLookbackOption>(c, process),
                "floating" => Built<ContinuousFloatingLookbackOption>(c, process),
                "partial fixed" => Built<ContinuousPartialFixedLookbackOption>(c, process),
                _ => Built<ContinuousPartialFloatingLookbackOption>(c, process),
            }));
        }

        check.Complete();
    }

    private static bool Built<TOption>(VectorCase c, GeneralizedBlackScholesProcess process)
        where TOption : OneAssetOption =>
        c.String("traits") == "pseudorandom"
            ? Build(new MakeMCLookbackEngine<TOption, PseudoRandom, GeneralStatistics>(process, 42), c)
            : Build(new MakeMCLookbackEngine<TOption, LowDiscrepancy, GeneralStatistics>(process, 42), c);

    // The harness's order of the builder's calls: the steps, then the samples and tolerance in the order given
    private static bool Build<TOption, TRng>(MakeMCLookbackEngine<TOption, TRng, GeneralStatistics> builder, VectorCase c)
        where TOption : OneAssetOption
        where TRng : IRngTraits
    {
        if (c.NullableInt("steps") is int steps)
        {
            builder = builder.WithSteps(steps);
        }

        if (c.NullableInt("stepsPerYear") is int perYear)
        {
            builder = builder.WithStepsPerYear(perYear);
        }

        int? samples = c.NullableInt("samples");
        double? tolerance = c.Input("tolerance").ValueKind == JsonValueKind.Null ? null : c.Double("tolerance");
        MakeMCLookbackEngine<TOption, TRng, GeneralStatistics> Samples(MakeMCLookbackEngine<TOption, TRng, GeneralStatistics> b) =>
            samples is int n ? b.WithSamples(n) : b;
        MakeMCLookbackEngine<TOption, TRng, GeneralStatistics> Tolerance(MakeMCLookbackEngine<TOption, TRng, GeneralStatistics> b) =>
            tolerance is double x ? b.WithAbsoluteTolerance(x) : b;
        builder = c.String("order") == "samples" ? Tolerance(Samples(builder)) : Samples(Tolerance(builder));
        return builder.ToEngine() is not null;
    }

    // The harness's priced call: the fixed option struck at 90 or the floating one, the extremum so far 100, on
    // testMonteCarloLookback's market, through MCPRLookbackEngine or MCLDLookbackEngine
    private static OneAssetOptionResults Priced(JsonElement p, EvaluationContext context)
    {
        Date today = context.EvaluationDate;
        DayCounter dc = new Actual360();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.0, dc), new FlatForward(today, 0.06, dc), new BlackConstantVol(today, new NullCalendar(), 0.1, dc));
        var type = (OptionType)p.GetProperty("optionType").GetInt32();
        var exercise = new EuropeanExercise(today + 360);
        int steps = p.GetProperty("steps").GetInt32();
        int samples = p.GetProperty("samples").GetInt32();
        bool antithetic = p.GetProperty("antithetic").GetBoolean();
        long seed = p.GetProperty("seed").GetInt64();
        bool pseudoRandom = p.GetProperty("traits").GetString() == "pseudorandom";
        if (p.GetProperty("instrument").GetString() == "fixed")
        {
            var fixedOption = new ContinuousFixedLookbackOption(100.0, new PlainVanillaPayoff(type, 90.0), exercise);
            return pseudoRandom
                ? MCPRLookbackEngine.Calculate(fixedOption, context, process, steps, null, false, antithetic, samples, null, null, seed)
                : MCLDLookbackEngine.Calculate(fixedOption, context, process, steps, null, false, antithetic, samples, null, null, seed);
        }

        var floating = new ContinuousFloatingLookbackOption(100.0, new FloatingTypePayoff(type), exercise);
        return pseudoRandom
            ? MCPRLookbackEngine.Calculate(floating, context, process, steps, null, false, antithetic, samples, null, null, seed)
            : MCLDLookbackEngine.Calculate(floating, context, process, steps, null, false, antithetic, samples, null, null, seed);
    }

    private static GeneralizedBlackScholesProcess ProcessAt(Date today)
    {
        DayCounter dc = new Actual365Fixed();
        return new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.01, dc), new FlatForward(today, 0.03, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.2, dc));
    }
}
