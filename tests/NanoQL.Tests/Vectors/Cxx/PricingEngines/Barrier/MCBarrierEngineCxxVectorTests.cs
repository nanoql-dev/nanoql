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
/// The C++ harness's Monte Carlo barrier vectors, <c>tests/vectors/cxx/pricingengines/barrier/mcbarrierengine.json</c>:
/// <see cref="BarrierPathPricer"/> and <see cref="BiasedBarrierPathPricer"/> on given paths, and the checks of
/// <see cref="MakeMCBarrierEngine{TRng, TStats}"/>; the process as
/// <c>tools/cxx/src/pricingengines/barrier/mcbarrierengine.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCBarrierEngineCxxVectorTests
{
    [Fact]
    public void MCBarrierEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/barrier/mcbarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessAt(context.EvaluationDate);
            if (c.Inputs.TryGetProperty("pricer", out JsonElement p))
            {
                var type = (BarrierType)p.GetProperty("barrierType").GetInt32();
                double barrier = p.GetProperty("barrier").GetDouble();
                double rebate = p.GetProperty("rebate").GetDouble();
                var optionType = (OptionType)p.GetProperty("optionType").GetInt32();
                double strike = p.GetProperty("strike").GetDouble();
                double[] discounts = Doubles(p.GetProperty("discounts"));
                var path = new MonteCarloPath(new TimeGrid(Doubles(p.GetProperty("times"))), new Vector(Doubles(p.GetProperty("values"))));
                PathPricer<MonteCarloPath> pricer = p.TryGetProperty("seed", out JsonElement seed)
                    ? new BarrierPathPricer(
                        type, barrier, rebate, optionType, strike, discounts, process,
                        new MersenneTwisterUniformRsg(path.Length - 1, seed.GetInt64()))
                    : new BiasedBarrierPathPricer(type, barrier, rebate, optionType, strike, discounts);
                check.Output(c, "price", pricer.Evaluate(path));
            }
            else
            {
                check.Output(c, "built", Lazy(() => Built(c, process)));
            }
        }

        check.Complete();
    }

    private static bool Built(VectorCase c, GeneralizedBlackScholesProcess process) =>
        c.String("traits") == "pseudorandom"
            ? Build(new MakeMCBarrierEngine<PseudoRandom, GeneralStatistics>(process, 42), c)
            : Build(new MakeMCBarrierEngine<LowDiscrepancy, GeneralStatistics>(process, 42), c);

    // The harness's order of the builder's calls: the steps, then the samples and tolerance in the order given
    private static bool Build<TRng>(MakeMCBarrierEngine<TRng, GeneralStatistics> builder, VectorCase c)
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
        MakeMCBarrierEngine<TRng, GeneralStatistics> Samples(MakeMCBarrierEngine<TRng, GeneralStatistics> b) =>
            samples is int n ? b.WithSamples(n) : b;
        MakeMCBarrierEngine<TRng, GeneralStatistics> Tolerance(MakeMCBarrierEngine<TRng, GeneralStatistics> b) =>
            tolerance is double x ? b.WithAbsoluteTolerance(x) : b;
        builder = c.String("order") == "samples" ? Tolerance(Samples(builder)) : Samples(Tolerance(builder));
        return builder.ToEngine() is not null;
    }

    private static GeneralizedBlackScholesProcess ProcessAt(Date today)
    {
        DayCounter dc = new Actual365Fixed();
        return new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.01, dc), new FlatForward(today, 0.03, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.2, dc));
    }
}
