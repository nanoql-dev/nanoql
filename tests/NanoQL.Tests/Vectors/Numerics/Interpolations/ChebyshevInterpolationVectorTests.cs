// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="ChebyshevInterpolation"/>'s vectors, <c>tests/vectors/math/interpolations/chebyshevinterpolation.json</c>:
/// the nodes of both kinds, and interpolations of four named functions built from the function and from its values,
/// read at the nodes, near them, across [-1, 1] and outside the range, and the refusals of no node.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ChebyshevInterpolationVectorTests
{
    // tools/vectors/gen/math/interpolations/chebyshevinterpolation.py's FUNCTIONS, each operation as written there
    private static readonly Dictionary<string, Func<double, double>> Functions = new()
    {
        ["x x x - x"] = x => (x * x * x) - x,
        ["1/(1+25 x x)"] = x => 1.0 / (1.0 + (25.0 * x * x)),
        ["sqrt(x+1)"] = x => Math.Sqrt(x + 1.0),
        ["|x|"] = Math.Abs,
    };

    [Fact]
    public void ChebyshevInterpolationVector()
    {
        var file = VectorFile.Load("math/interpolations/chebyshevinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var kind = (ChebyshevInterpolationPointsType)c.Int("pointsType");
            if (c.Expected.TryGetProperty("nodes", out _))
            {
                check.Output(
                    c, "nodes", Enumerable.Range(1, 20).Select(n => (object?)ChebyshevInterpolation.Nodes(n, kind).ToArray()).ToArray());
                continue;
            }

            Func<ChebyshevInterpolation> interpolation = c.Inputs.TryGetProperty("function", out _)
                ? () => new ChebyshevInterpolation(c.Int("n"), Functions[c.String("function")], kind)
                : () => new ChebyshevInterpolation(Numbers(c.Input("values")), kind);
            (double X, bool Flag)[] points =
            [
                .. c.Input("points").EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetBoolean())),
            ];
            check.Output(c, "value", points.Select(p => Lazy(() => interpolation().Evaluate(p.X, p.Flag))).ToArray());
        }

        check.Complete();
    }
}
