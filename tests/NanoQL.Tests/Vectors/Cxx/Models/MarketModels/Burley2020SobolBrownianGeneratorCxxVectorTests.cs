// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Burley-scrambled Sobol Brownian generator vectors,
/// <c>tests/vectors/cxx/models/marketmodels/browniangenerators/burley2020sobolbrowniangenerator.json</c>:
/// <see cref="Burley2020SobolBrownianGenerator"/>'s paths under each ordering on three (factors, steps) sets, and the
/// diagonal ordering on Joe-Kuo D7's and Kuo's direction integers, each path's weight and its steps' weights and
/// variates, and the same paths from <see cref="Burley2020SobolBrownianGeneratorFactory"/>'s generator.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Burley2020SobolBrownianGeneratorCxxVectorTests
{
    [Fact]
    public void Burley2020SobolBrownianGeneratorCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/browniangenerators/burley2020sobolbrowniangenerator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int factors = c.Int("factors"), steps = c.Int("steps");
            var ordering = (SobolBrownianGeneratorBaseOrdering)c.Int("ordering");
            long seed = c.Input("seed").GetInt64(), scrambleSeed = c.Input("scrambleSeed").GetInt64();
            var integers = (SobolRsgDirectionIntegers)c.Int("directionIntegers");
            MTBrownianGeneratorVectorTests.CheckPaths(
                check,
                c,
                new Burley2020SobolBrownianGenerator(factors, steps, ordering, seed, integers, scrambleSeed),
                new Burley2020SobolBrownianGeneratorFactory(ordering, seed, integers, scrambleSeed).Create(factors, steps));
        }

        check.Complete();
    }
}
