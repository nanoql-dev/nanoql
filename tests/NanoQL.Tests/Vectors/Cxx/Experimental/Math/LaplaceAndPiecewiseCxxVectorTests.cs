// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Laplace, Moore-Penrose and piecewise vectors,
/// <c>tests/vectors/cxx/experimental/math/laplaceandpiecewise.json</c>: filled matrices and a three-dimensional grid,
/// tall and square pseudo-inverses, piecewise integrals on positive breaks and the piecewise function.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LaplaceAndPiecewiseCxxVectorTests
{
    [Fact]
    public void LaplaceAndPiecewiseCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/laplaceandpiecewise");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            if (c.Name.StartsWith("laplace grid", StringComparison.Ordinal))
            {
                double?[] values = [.. a[0].EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Null ? (double?)null : e.GetDouble())];
                double[][] grids = [.. a[1].EnumerateArray().Select(Numbers)];
                int[] dims = [.. grids.Select(g => g.Length)];
                int Index(IReadOnlyList<int> coordinates)
                {
                    int k = 0, stride = 1;
                    for (int d = 0; d < dims.Length; d++)
                    {
                        k += coordinates[d] * stride;
                        stride *= dims[d];
                    }

                    return k;
                }

                var l = new LaplaceInterpolation(x => values[Index(x)], grids, a[2].GetDouble(), a[3].GetInt32());
                var outputs = new List<double>();
                var coordinates = new int[dims.Length];
                for (int k = 0; k < values.Length; k++)
                {
                    outputs.Add(l.Value(coordinates));
                    for (int d = 0; d < dims.Length; d++)
                    {
                        if (++coordinates[d] < dims[d])
                        {
                            break;
                        }

                        coordinates[d] = 0;
                    }
                }

                check.Output(c, "values", outputs.ToArray());
            }
            else if (c.Name.StartsWith("laplace", StringComparison.Ordinal))
            {
                Matrix filled = LaplaceInterpolationFunctions.LaplaceInterpolation(
                    MatrixOf(a[0]), Numbers(a[1]), Numbers(a[2]), a[3].GetDouble(), a[4].GetInt32());
                check.Output(c, "filled", Flat(filled));
            }
            else if (c.Name.StartsWith("moore-penrose", StringComparison.Ordinal))
            {
                double? tol = a[1].ValueKind == JsonValueKind.Null ? null : a[1].GetDouble();
                check.Output(c, "inverse", Flat(MoorePenroseInverseFunctions.MoorePenroseInverse(MatrixOf(a[0]), tol)));
            }
            else if (c.Name.StartsWith("piecewise function", StringComparison.Ordinal))
            {
                double[] X = Numbers(a[0]), Y = Numbers(a[1]);
                check.Output(c, "values", Numbers(a[2]).Select(t => PiecewiseFunctionFunctions.Evaluate(X, Y, t)).ToArray());
            }
            else
            {
                double[] ip = Numbers(a[3]), fp = Numbers(a[5]);
                Integrator inner = a[2].GetString() == "segment"
                    ? new SegmentIntegral((int)ip[0])
                    : new SimpsonIntegral(ip[0], (int)ip[1]);
                var piecewise = new PiecewiseIntegral(inner, Numbers(a[0]), a[1].GetBoolean());
                Func<double, double> f;
                if (a[4].GetString() == "step")
                {
                    int breaks = (int)fp[0];
                    double[] X = fp[1..(1 + breaks)], Y = fp[(1 + breaks)..];
                    f = t => PiecewiseFunctionFunctions.Evaluate(X, Y, t);
                }
                else
                {
                    f = Math.Exp;
                }

                check.Output(c, "values", a[6].EnumerateArray().Select(ab => piecewise.Integrate(f, ab[0].GetDouble(), ab[1].GetDouble())).ToArray());
            }
        }

        check.Complete();
    }

    private static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];

    // The rows, a null element NaN, nanoql's missing value.
    private static Matrix MatrixOf(JsonElement rows)
    {
        double[][] r = [.. rows.EnumerateArray().Select(row => row.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.Null ? double.NaN : e.GetDouble()).ToArray())];
        var m = new Matrix(r.Length, r.Length == 0 ? 0 : r[0].Length);
        for (int i = 0; i < r.Length; i++)
        {
            for (int j = 0; j < r[i].Length; j++)
            {
                m[i, j] = r[i][j];
            }
        }

        return m;
    }

    private static double[] Flat(Matrix m)
    {
        var out_ = new List<double>();
        for (int i = 0; i < m.Rows; i++)
        {
            for (int j = 0; j < m.Columns; j++)
            {
                out_.Add(m[i, j]);
            }
        }

        return [.. out_];
    }
}
