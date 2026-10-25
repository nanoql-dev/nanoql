// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Sobol generator vectors,
/// <c>tests/vectors/cxx/models/marketmodels/browniangenerators/sobolbrowniangenerator.json</c>:
/// <see cref="SobolBrownianGeneratorBase.OrderedIndices"/> and <see cref="SobolBrownianGeneratorBase.Transform"/> under
/// each ordering on four (factors, steps) sets, the transform of seeded variates over three paths.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SobolBrownianGeneratorCxxVectorTests
{
    [Fact]
    public void SobolBrownianGeneratorCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/browniangenerators/sobolbrowniangenerator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var g = new SobolBrownianGenerator(
                c.Input("factors").GetInt32(), c.Input("steps").GetInt32(), (SobolBrownianGeneratorBaseOrdering)c.Input("ordering").GetInt32(), 1);
            check.Output(c, "orderedIndices", g.OrderedIndices.Select(row => (object?)row.ToArray()).ToArray());
            IReadOnlyList<double>[] variates = [.. c.Input("variates").EnumerateArray().Select(v => (IReadOnlyList<double>)[.. v.EnumerateArray().Select(x => x.GetDouble())])];
            check.Output(c, "transform", g.Transform(variates).Select(row => (object?)row.ToArray()).ToArray());
        }

        check.Complete();
    }
}
