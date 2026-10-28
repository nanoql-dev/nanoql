// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The central-limit generators' vectors, <c>tests/vectors/math/randomnumbers/centrallimitgaussianrng.json</c>:
/// <see cref="CLGaussianRng{TRng}"/> through its four named classes over the Mersenne Twister, Knuth, L'Ecuyer and
/// xoshiro256** generators, for two seeds, the draws and the last weight. Halfway, the draws continue from a copy
/// (<see cref="IRng.Clone"/>), whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CentralLimitGaussianRngVectorTests
{
    [Fact]
    public void CentralLimitGaussianRngVector()
    {
        var file = VectorFile.Load("math/randomnumbers/centrallimitgaussianrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int draws = c.Int("draws");
            long seed = c.Input("seed").GetInt64();
            IRng rng = c.String("composition") switch
            {
                "CentralLimitMersenneTwisterGaussianRng" => new CentralLimitMersenneTwisterGaussianRng(new MersenneTwisterUniformRng(seed)),
                "CentralLimitKnuthGaussianRng" => new CentralLimitKnuthGaussianRng(new KnuthUniformRng(seed)),
                "CentralLimitLecuyerGaussianRng" => new CentralLimitLecuyerGaussianRng(new LecuyerUniformRng(seed)),
                "CentralLimitXoshiro256StarStarGaussianRng" =>
                    new CentralLimitXoshiro256StarStarGaussianRng(new Xoshiro256StarStarUniformRng(seed)),
                string other => throw new InvalidOperationException($"no composition {other}"),
            };
            var next = new double[draws];
            Sample<double> sample = default;
            for (int i = 0; i < draws; i++)
            {
                if (i == draws / 2)
                {
                    rng = rng.Clone();
                }

                sample = rng.Next();
                next[i] = sample.Value;
            }

            check.Output(c, "next", next);
            check.Output(c, "lastWeight", sample.Weight);
        }

        check.Complete();
    }
}
