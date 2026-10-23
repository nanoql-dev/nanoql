// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The glued mesher vectors, <c>tests/vectors/experimental/finitedifferences/glued1dmesher.json</c>: meshers meeting at
/// a common point, with a gap, with a one-point mesher, and the refused overlap; each one's size, locations, dplus and
/// dminus.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Glued1dMesherVectorTests
{
    [Fact]
    public void Glued1dMesherVector()
    {
        var file = VectorFile.Load("experimental/finitedifferences/glued1dmesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Fdm1dMesher left = FdInputs.Mesher1dOf(c.Input("left"), context);
            Fdm1dMesher right = FdInputs.Mesher1dOf(c.Input("right"), context);
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => new Glued1dMesher(left, right));
                continue;
            }

            var m = new Glued1dMesher(left, right);
            check.Output(c, "size", m.Size);
            check.Output(c, "locations", m.Locations());
            check.Output(c, "dplus", Enumerable.Range(0, m.Size).Select(i => FdInputs.Nullable(m.Dplus(i))).ToArray());
            check.Output(c, "dminus", Enumerable.Range(0, m.Size).Select(i => FdInputs.Nullable(m.Dminus(i))).ToArray());
        }

        check.Complete();
    }
}
