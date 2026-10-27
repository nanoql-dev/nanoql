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
public class MCDiscreteGeometricAPEngineCxxVectorTests
{
    [Fact]
    public void MCDiscreteGeometricAPEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/asian/mc_discr_geom_av_price");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("value", out _))
            {
                check.Output(c, "value", Lazy(() => new GeometricAPOPathPricer((OptionType)c.Int("optionType"), c.Double("strike"), c.Double("discount"), c.Double("runningAccumulator"), c.Int("pastFixings")).Evaluate(Path(c))));
            }
            else
            {
                GeneralizedBlackScholesProcess process = ProcessAt(c.Context().EvaluationDate);
                check.Output(c, "built", Lazy(() => c.String("traits") == "pseudorandom"
                    ? Build(new MakeMCDiscreteGeometricAPEngine<PseudoRandom, GeneralStatistics>(process, 42), c)
                    : Build(new MakeMCDiscreteGeometricAPEngine<LowDiscrepancy, GeneralStatistics>(process, 42), c)));
            }
        }

        check.Complete();
    }

    // the harness's order of the builder's calls: the steps and the control variate, then the samples and tolerance in
    // the order given
    private static bool Build<TRng>(MakeMCDiscreteGeometricAPEngine<TRng, GeneralStatistics> builder, VectorCase c)
        where TRng : IRngTraits
    {
        int? samples = c.NullableInt("samples");
        double? tolerance = c.Input("tolerance").ValueKind == JsonValueKind.Null ? null : c.Double("tolerance");
        MakeMCDiscreteGeometricAPEngine<TRng, GeneralStatistics> Samples(MakeMCDiscreteGeometricAPEngine<TRng, GeneralStatistics> b) => samples is int n ? b.WithSamples(n) : b;
        MakeMCDiscreteGeometricAPEngine<TRng, GeneralStatistics> Tolerance(MakeMCDiscreteGeometricAPEngine<TRng, GeneralStatistics> b) => tolerance is double x ? b.WithAbsoluteTolerance(x) : b;
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

    private static GeneralizedBlackScholesProcess ProcessAt(Date today)
    {
        DayCounter dc = new Actual365Fixed();
        return new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.01, dc), new FlatForward(today, 0.03, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.2, dc));
    }
}
