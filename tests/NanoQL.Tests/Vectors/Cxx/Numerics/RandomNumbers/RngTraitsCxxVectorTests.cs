// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's random-number traits vectors, <c>tests/vectors/cxx/math/randomnumbers/rngtraits.json</c>:
/// <see cref="PseudoRandom"/>, <see cref="PoissonPseudoRandom"/>, <see cref="LowDiscrepancy"/> and
/// <see cref="GenericLowDiscrepancy{TUrsg, TIc}"/> over <see cref="HaltonRsg"/>, which the wheel doesn't expose: their
/// sequence generators' draws, last sequence, last weight and dimension, with the inverse cumulative's default
/// (through <see cref="IRngTraits"/>, as a Monte Carlo engine calls them) and with a given one (QuantLib's
/// <c>icInstance</c>, the typed overload here); <c>AllowsErrorEstimate</c>; and dimension 0. Halfway, the draws continue
/// from a copy (<see cref="IRsg.Clone"/>), whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RngTraitsCxxVectorTests
{
    private const string Halton = "GenericLowDiscrepancy<HaltonRsg,InverseCumulativeNormal>";

    [Fact]
    public void RngTraitsCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/rngtraits");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "allowsErrorEstimate")
            {
                check.Output(c, "PseudoRandom", Allows<PseudoRandom>());
                check.Output(c, "PoissonPseudoRandom", Allows<PoissonPseudoRandom>());
                check.Output(c, "LowDiscrepancy", Allows<LowDiscrepancy>());
                check.Output(c, Halton, Allows<GenericLowDiscrepancy<HaltonRsg, InverseCumulativeNormal>>());
                continue;
            }

            int dimension = c.Int("dimension");
            long seed = c.Input("seed").GetInt64();
            if (dimension == 0)
            {
                check.Output(c, "PseudoRandom", (Func<object?>)(() => PseudoRandom.MakeSequenceGenerator(0, seed).Dimension));
                check.Output(c, "LowDiscrepancy", (Func<object?>)(() => LowDiscrepancy.MakeSequenceGenerator(0, seed).Dimension));
                continue;
            }

            IRsg rsg = Make(c.String("traits"), dimension, seed, c.Input("inverseCumulative"));
            int draws = c.Int("draws");
            var sequences = new double[draws][];
            for (int i = 0; i < draws; i++)
            {
                if (i == draws / 2)
                {
                    rsg = rsg.Clone();
                }

                sequences[i] = rsg.NextSequence().Value.ToArray();
            }

            check.Output(c, "nextSequence", sequences);
            check.Output(c, "dimension", rsg.Dimension);
            if (c.Expected.TryGetProperty("lastSequence", out _))
            {
                Sample<ReadOnlyMemory<double>> last = rsg.LastSequence();
                check.Output(c, "lastSequence", last.Value.ToArray());
                check.Output(c, "lastWeight", last.Weight);
            }
        }

        check.Complete();
    }

    private static bool Allows<TTraits>()
        where TTraits : struct, IRngTraits => TTraits.AllowsErrorEstimate;

    // A Monte Carlo engine's call: the traits' type-erased sequence generator.
    private static IRsg Erased<TTraits>(int dimension, long seed)
        where TTraits : struct, IRngTraits => TTraits.MakeSequenceGenerator(dimension, seed);

    private static IRsg Make(string traits, int dimension, long seed, JsonElement ic)
    {
        if (ic.ValueKind == JsonValueKind.Null)
        {
            return traits switch
            {
                "PseudoRandom" => Erased<PseudoRandom>(dimension, seed),
                "PoissonPseudoRandom" => Erased<PoissonPseudoRandom>(dimension, seed),
                "LowDiscrepancy" => Erased<LowDiscrepancy>(dimension, seed),
                Halton => GenericLowDiscrepancy<HaltonRsg, InverseCumulativeNormal>.MakeSequenceGenerator(dimension, seed),
                _ => throw new InvalidOperationException($"no traits {traits}"),
            };
        }

        return traits switch
        {
            "PseudoRandom" => PseudoRandom.MakeSequenceGenerator(dimension, seed, Normal(ic)),
            "PoissonPseudoRandom" => PoissonPseudoRandom.MakeSequenceGenerator(
                dimension, seed, new InverseCumulativePoisson(ic.GetProperty("lambda").GetDouble())),
            "LowDiscrepancy" => LowDiscrepancy.MakeSequenceGenerator(dimension, seed, Normal(ic)),
            Halton => GenericLowDiscrepancy<HaltonRsg, InverseCumulativeNormal>.MakeSequenceGenerator(dimension, seed, Normal(ic)),
            _ => throw new InvalidOperationException($"no traits {traits}"),
        };
    }

    private static InverseCumulativeNormal Normal(JsonElement ic) =>
        new(ic.GetProperty("average").GetDouble(), ic.GetProperty("sigma").GetDouble());
}
