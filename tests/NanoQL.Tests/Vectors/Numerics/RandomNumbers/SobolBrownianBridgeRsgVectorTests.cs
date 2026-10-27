// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Sobol Brownian-bridge generators' vectors, <c>tests/vectors/math/randomnumbers/sobolbrownianbridgersg.json</c>:
/// <see cref="SobolBrownianBridgeRsg"/>'s and <see cref="Burley2020SobolBrownianBridgeRsg"/>'s dimension and first five
/// sequences on the diagonal ordering and JoeKuoD7's direction integers, at dimensions within the table, where the
/// Sobol seed is never read, the Burley generator's seeds QuantLib's defaults; each sequence whole up to 60 dimensions,
/// and above that its last six values and every 50th from the first.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SobolBrownianBridgeRsgVectorTests
{
    [Fact]
    public void SobolBrownianBridgeRsgVector()
    {
        var file = VectorFile.Load("math/randomnumbers/sobolbrownianbridgersg");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int last = c.Int("last");
            int stride = c.Int("stride");
            IRsg rsg = c.Inputs.TryGetProperty("scrambleSeed", out System.Text.Json.JsonElement scramble)
                ? new Burley2020SobolBrownianBridgeRsg(
                    c.Int("factors"),
                    c.Int("steps"),
                    SobolBrownianGeneratorBaseOrdering.Diagonal,
                    c.Input("seed").GetInt64(),
                    SobolRsgDirectionIntegers.JoeKuoD7,
                    scramble.GetInt64())
                : new SobolBrownianBridgeRsg(
                    c.Int("factors"), c.Int("steps"), SobolBrownianGeneratorBaseOrdering.Diagonal, c.Input("seed").GetInt64());
            check.Output(c, "dimension", rsg.Dimension);
            var samples = new List<Sample<double[]>>();
            for (int i = 0; i < c.Int("sequences"); i++)
            {
                Sample<ReadOnlyMemory<double>> s = rsg.NextSequence();
                samples.Add(new Sample<double[]>(s.Value.ToArray(), s.Weight));
            }

            check.Output(c, "weights", samples.Select(s => s.Weight).ToArray());
            if (c.Expected.TryGetProperty("sequences", out _))
            {
                check.Output(c, "sequences", samples.Select(s => s.Value).ToList());
            }
            else
            {
                check.Output(c, "lastValues", samples.Select(s => s.Value[^last..]).ToList());
                check.Output(c, "stridedValues", samples.Select(s => s.Value.Where((_, j) => j % stride == 0).ToArray()).ToList());
            }
        }

        check.Complete();
    }
}
