// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The inverse cumulative sequence generators' vectors, <c>tests/vectors/math/randomnumbers/inversecumulativersg.json</c>:
/// <see cref="InverseCumulativeRsg{TUsg, TIc}"/> through its six named classes, over Mersenne Twister, Halton and Sobol
/// sequences, with the inverse cumulative's default and a given one; 50 draws each, the last weight and the dimension.
/// The Mersenne Twister ones are built from a <see cref="MersenneTwisterUniformRsg"/> made from a generator or from a
/// seed. Halfway, the draws continue from a copy (<see cref="IRsg.Clone"/>), whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InverseCumulativeRsgVectorTests
{
    [Fact]
    public void InverseCumulativeRsgVector()
    {
        var file = VectorFile.Load("math/randomnumbers/inversecumulativersg");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int draws = c.Int("draws");
            IRsg rsg = Make(c);
            var sequences = new double[draws][];
            Sample<ReadOnlyMemory<double>> sample = default;
            for (int i = 0; i < draws; i++)
            {
                if (i == draws / 2)
                {
                    rsg = rsg.Clone();
                }

                sample = rsg.NextSequence();
                sequences[i] = sample.Value.ToArray();
            }

            check.Output(c, "nextSequence", sequences);
            check.Output(c, "lastWeight", sample.Weight);
            check.Output(c, "dimension", rsg.Dimension);
        }

        check.Complete();
    }

    // The case's composition, as the generator builds it.
    private static IRsg Make(VectorCase c)
    {
        int dimension = c.Int("dimension");
        long seed = c.Input("seed").GetInt64();
        JsonElement ic = c.Input("inverseCumulative");
        bool given = ic.ValueKind != JsonValueKind.Null;
        double average = given ? ic.GetProperty("average").GetDouble() : 0.0;
        double sigma = given ? ic.GetProperty("sigma").GetDouble() : 1.0;
        MersenneTwisterUniformRsg MersenneTwister() => c.String("construction") == "seed"
            ? new MersenneTwisterUniformRsg(dimension, seed)
            : new MersenneTwisterUniformRsg(dimension, new MersenneTwisterUniformRng(seed));
        KnuthUniformRsg Knuth() => c.String("construction") == "seed"
            ? new KnuthUniformRsg(dimension, seed)
            : new KnuthUniformRsg(dimension, new KnuthUniformRng(seed));
        LecuyerUniformRsg Lecuyer() => c.String("construction") == "seed"
            ? new LecuyerUniformRsg(dimension, seed)
            : new LecuyerUniformRsg(dimension, new LecuyerUniformRng(seed));
        Xoshiro256StarStarUniformRsg Xoshiro() => c.String("construction") == "seed"
            ? new Xoshiro256StarStarUniformRsg(dimension, seed)
            : new Xoshiro256StarStarUniformRsg(dimension, new Xoshiro256StarStarUniformRng(seed));
        return c.String("composition") switch
        {
            "InvCumulativeMersenneTwisterGaussianRsg" => given
                ? new InvCumulativeMersenneTwisterGaussianRsg(MersenneTwister(), new InverseCumulativeNormal(average, sigma))
                : new InvCumulativeMersenneTwisterGaussianRsg(MersenneTwister()),
            "MoroInvCumulativeMersenneTwisterGaussianRsg" => given
                ? new MoroInvCumulativeMersenneTwisterGaussianRsg(MersenneTwister(), new MoroInverseCumulativeNormal(average, sigma))
                : new MoroInvCumulativeMersenneTwisterGaussianRsg(MersenneTwister()),
            "InvCumulativeHaltonGaussianRsg" => given
                ? new InvCumulativeHaltonGaussianRsg(new HaltonRsg(dimension, seed), new InverseCumulativeNormal(average, sigma))
                : new InvCumulativeHaltonGaussianRsg(new HaltonRsg(dimension, seed)),
            "MoroInvCumulativeHaltonGaussianRsg" => given
                ? new MoroInvCumulativeHaltonGaussianRsg(new HaltonRsg(dimension, seed), new MoroInverseCumulativeNormal(average, sigma))
                : new MoroInvCumulativeHaltonGaussianRsg(new HaltonRsg(dimension, seed)),
            "InvCumulativeSobolGaussianRsg" => given
                ? new InvCumulativeSobolGaussianRsg(new SobolRsg(dimension, seed), new InverseCumulativeNormal(average, sigma))
                : new InvCumulativeSobolGaussianRsg(new SobolRsg(dimension, seed)),
            "MoroInvCumulativeSobolGaussianRsg" => given
                ? new MoroInvCumulativeSobolGaussianRsg(new SobolRsg(dimension, seed), new MoroInverseCumulativeNormal(average, sigma))
                : new MoroInvCumulativeSobolGaussianRsg(new SobolRsg(dimension, seed)),
            "InvCumulativeKnuthGaussianRsg" => given
                ? new InvCumulativeKnuthGaussianRsg(Knuth(), new InverseCumulativeNormal(average, sigma))
                : new InvCumulativeKnuthGaussianRsg(Knuth()),
            "MoroInvCumulativeKnuthGaussianRsg" => given
                ? new MoroInvCumulativeKnuthGaussianRsg(Knuth(), new MoroInverseCumulativeNormal(average, sigma))
                : new MoroInvCumulativeKnuthGaussianRsg(Knuth()),
            "InvCumulativeLecuyerGaussianRsg" => given
                ? new InvCumulativeLecuyerGaussianRsg(Lecuyer(), new InverseCumulativeNormal(average, sigma))
                : new InvCumulativeLecuyerGaussianRsg(Lecuyer()),
            "MoroInvCumulativeLecuyerGaussianRsg" => given
                ? new MoroInvCumulativeLecuyerGaussianRsg(Lecuyer(), new MoroInverseCumulativeNormal(average, sigma))
                : new MoroInvCumulativeLecuyerGaussianRsg(Lecuyer()),
            "InvCumulativeXoshiro256StarStarGaussianRsg" => given
                ? new InvCumulativeXoshiro256StarStarGaussianRsg(Xoshiro(), new InverseCumulativeNormal(average, sigma))
                : new InvCumulativeXoshiro256StarStarGaussianRsg(Xoshiro()),
            "MoroInvCumulativeXoshiro256StarStarGaussianRsg" => given
                ? new MoroInvCumulativeXoshiro256StarStarGaussianRsg(Xoshiro(), new MoroInverseCumulativeNormal(average, sigma))
                : new MoroInvCumulativeXoshiro256StarStarGaussianRsg(Xoshiro()),
            string other => throw new InvalidOperationException($"no composition {other}"),
        };
    }
}
