// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SobolBrownianGenerator vectors,
/// <c>tests/vectors/models/marketmodels/browniangenerators/sobolbrowniangenerator.json</c>:
/// <see cref="SobolBrownianGenerator"/>'s paths under each ordering on three (factors, steps) sets, and the diagonal
/// ordering on Joe-Kuo D7's and Kuo's direction integers, each path's weight and its steps' weights and variates, and
/// the same paths from <see cref="SobolBrownianGeneratorFactory"/>'s generator.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SobolBrownianGeneratorVectorTests
{
    [Fact]
    public void SobolBrownianGeneratorVector()
    {
        var file = VectorFile.Load("models/marketmodels/browniangenerators/sobolbrowniangenerator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("generator");
            int factors = spec.GetProperty("factors").GetInt32(), steps = spec.GetProperty("steps").GetInt32();
            var ordering = (SobolBrownianGeneratorBaseOrdering)spec.GetProperty("ordering").GetInt32();
            long seed = spec.GetProperty("seed").GetInt64();
            var integers = (SobolRsgDirectionIntegers)spec.GetProperty("directionIntegers").GetInt32();
            MTBrownianGeneratorVectorTests.CheckPaths(
                check,
                c,
                new SobolBrownianGenerator(factors, steps, ordering, seed, integers),
                new SobolBrownianGeneratorFactory(ordering, seed, integers).Create(factors, steps));
        }

        check.Complete();
    }
}
