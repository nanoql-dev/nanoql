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
public class MCDiscreteGeometricAPHestonEngineCxxVectorTests
{
    [Fact]
    public void MCDiscreteGeometricAPHestonEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/asian/mc_discr_geom_av_price_heston");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("value", out _))
            {
                check.Output(c, "value", Lazy(() => new GeometricAPOHestonPathPricer((OptionType)c.Int("optionType"), c.Double("strike"), c.Double("discount"), [.. c.Ints("fixingIndices")], c.Double("runningAccumulator"), c.Int("pastFixings")).Evaluate(MultiPathOf(c))));
            }
            else
            {
                HestonProcess process = ProcessAt(c.Context().EvaluationDate);
                check.Output(c, "built", Lazy(() => c.String("traits") == "pseudorandom"
                    ? Build(new MakeMCDiscreteGeometricAPHestonEngine<PseudoRandom, GeneralStatistics>(process, 42), c)
                    : Build(new MakeMCDiscreteGeometricAPHestonEngine<LowDiscrepancy, GeneralStatistics>(process, 42), c)));
            }
        }

        check.Complete();
    }

    // the harness's order of the builder's calls: the steps and the control variate, then the samples and tolerance in
    // the order given
    private static bool Build<TRng>(MakeMCDiscreteGeometricAPHestonEngine<TRng, GeneralStatistics> builder, VectorCase c)
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
        MakeMCDiscreteGeometricAPHestonEngine<TRng, GeneralStatistics> Samples(MakeMCDiscreteGeometricAPHestonEngine<TRng, GeneralStatistics> b) => samples is int n ? b.WithSamples(n) : b;
        MakeMCDiscreteGeometricAPHestonEngine<TRng, GeneralStatistics> Tolerance(MakeMCDiscreteGeometricAPHestonEngine<TRng, GeneralStatistics> b) => tolerance is double x ? b.WithAbsoluteTolerance(x) : b;
        builder = c.String("order") == "samples" ? Tolerance(Samples(builder)) : Samples(Tolerance(builder));
        return builder.ToEngine() is not null;
    }

    // the case's grid, 0 added where its times don't start there, and a value at each node
    private static MonteCarloPath Path(VectorCase c) => new(new NanoQL.TimeGrid(c.Doubles("times")), new Vector(c.Doubles("values")));

    private static MultiPath MultiPathOf(VectorCase c)
    {
        var grid = new NanoQL.TimeGrid(c.Doubles("times"));
        return new MultiPath(ImmutableArray.Create(new MonteCarloPath(grid, new Vector(c.Doubles("values"))), new MonteCarloPath(grid, new Vector(c.Doubles("variance")))));
    }

    private static HestonProcess ProcessAt(Date today)
    {
        DayCounter dc = new Actual365Fixed();
        return new HestonProcess(new FlatForward(today, 0.03, dc), new FlatForward(today, 0.01, dc), 100.0, 0.04, 1.5, 0.04, 0.3, -0.7);
    }
}
