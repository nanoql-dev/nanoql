// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The TridiagonalOperator vectors, <c>tests/vectors/methods/finitedifferences/tridiagonaloperator.json</c>:
/// <see cref="TridiagonalOperator"/>'s constructor, row setters, <see cref="TridiagonalOperator.ApplyTo"/>,
/// <see cref="TridiagonalOperator.SolveFor(Vector)"/>, sum, difference and product by a scalar, with their refusals, on
/// 20 operators of 3 to 10 rows; and <see cref="TridiagonalOperator.Identity"/>. An operator is read, as the generator
/// reads it, through its products with three comb vectors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TridiagonalOperatorVectorTests
{
    [Fact]
    public void TridiagonalOperatorVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/tridiagonaloperator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "identity")
            {
                check.Output(c, "operator", c.Ints("sizes").Select(n => (object?)Read(TridiagonalOperator.Identity(n))).ToArray());
                continue;
            }

            double[] low = Numbers(c.Input("low")), mid = Numbers(c.Input("mid")), high = Numbers(c.Input("high"));
            double[] low2 = Numbers(c.Input("low2")), mid2 = Numbers(c.Input("mid2")), high2 = Numbers(c.Input("high2"));
            double[] u = Numbers(c.Input("u")), r = Numbers(c.Input("r"));
            JsonElement rows = c.Input("rows");
            double[] first = Numbers(rows.GetProperty("first")), midRow = Numbers(rows.GetProperty("mid"));
            double[] last = Numbers(rows.GetProperty("last")), midRows = Numbers(rows.GetProperty("midRows"));
            int row = c.Int("row"), n = mid.Length;
            double s = c.Double("s");

            TridiagonalOperator Make() => new(new Vector(low), new Vector(mid), new Vector(high));
            TridiagonalOperator op = Make(), other = new(new Vector(low2), new Vector(mid2), new Vector(high2));
            TridiagonalOperator afterRows = Make();
            afterRows.SetFirstRow(first[0], first[1]);
            afterRows.SetMidRow(row, midRow[0], midRow[1], midRow[2]);
            afterRows.SetLastRow(last[0], last[1]);
            TridiagonalOperator afterMidRows = Make();
            afterMidRows.SetMidRows(midRows[0], midRows[1], midRows[2]);
            var shorter = new TridiagonalOperator(new Vector(low2[..^1]), new Vector(mid2[..^1]), new Vector(high2[..^1]));

            check.Output(c, "size", op.Size);
            check.Output(c, "operator", Read(op));
            check.Output(c, "afterRowSetters", Read(afterRows));
            check.Output(c, "afterSetMidRows", Read(afterMidRows));
            check.Output(c, "applyTo", op.ApplyTo(new Vector(u)));
            check.Output(c, "solveFor", () => op.SolveFor(new Vector(r)));
            check.Output(c, "sum", Read(op + other));
            check.Output(c, "difference", Read(op - other));
            check.Output(c, "product", Read(op * s));
            check.Output(c, "lowWrongSize", () => new TridiagonalOperator(new Vector(low[..^1]), new Vector(mid), new Vector(high)).Size);
            check.Output(c, "highWrongSize", () => new TridiagonalOperator(new Vector(low), new Vector(mid), new Vector([.. high, 1.0])).Size);
            check.Output(c, "applyToWrongSize", () => op.ApplyTo(new Vector([.. u, 1.0])));
            check.Output(c, "solveForWrongSize", () => op.SolveFor(new Vector(r[..^1])));
            check.Output(c, "setMidRowFirst", () =>
            {
                Make().SetMidRow(0, 1.0, 2.0, 3.0);
                return null;
            });
            check.Output(c, "setMidRowLast", () =>
            {
                Make().SetMidRow(n - 1, 1.0, 2.0, 3.0);
                return null;
            });
            check.Output(c, "sumWrongSize", () => (op + shorter).Size);
        }

        check.Complete();
    }

    // The operator as its products with the three comb vectors, the k-th holding 1 where the index is k modulo 3:
    // element j of each is one of row j's coefficients.
    internal static object?[] Read(TridiagonalOperator op) =>
        [.. Enumerable.Range(0, 3).Select(k => (object?)op.ApplyTo(Comb(op.Size, k)))];

    private static Vector Comb(int n, int k)
    {
        var comb = new Vector(n);
        for (int j = 0; j < n; ++j)
        {
            comb[j] = j % 3 == k ? 1.0 : 0.0;
        }

        return comb;
    }
}
