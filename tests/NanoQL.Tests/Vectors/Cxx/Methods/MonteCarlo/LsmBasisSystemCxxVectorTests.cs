// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's LsmBasisSystem vectors, <c>tests/vectors/cxx/methods/montecarlo/lsmbasissystem.json</c>: the
/// one-factor basis systems of every polynomial type and the multi-factor systems, each function's values at points,
/// and the refusals of an unknown type and of no factors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LsmBasisSystemCxxVectorTests
{
    [Fact]
    public void LsmBasisSystemCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/montecarlo/lsmbasissystem");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var type = (LsmBasisSystemPolynomialType)c.Int("polynomialType");
            int order = c.Int("order");
            if (c.Inputs.TryGetProperty("xs", out JsonElement xs))
            {
                double[] points = EquityInputs.Doubles(xs);
                check.Output(c, "values", () =>
                {
                    ImmutableArray<Func<double, double>> basis = LsmBasisSystem.PathBasisSystem(order, type);
                    return basis.Select(f => (object?)points.Select(f).ToArray()).ToArray();
                });
            }
            else
            {
                Vector[] points = [.. c.Input("points").EnumerateArray().Select(p => new Vector(EquityInputs.Doubles(p)))];
                check.Output(c, "values", () =>
                {
                    ImmutableArray<Func<Vector, double>> basis = LsmBasisSystem.MultiPathBasisSystem(c.Int("dim"), order, type);
                    return basis.Select(f => (object?)points.Select(f).ToArray()).ToArray();
                });
            }
        }

        check.Complete();
    }
}
