// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MatrixVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Matrix vectors, <c>tests/vectors/cxx/math/matrix.json</c>: <see cref="MatrixFunctions.Determinant"/>
/// and <see cref="MatrixFunctions.Inverse"/>, over Boost uBLAS's LU port, on the survey's matrix families, with the
/// refusals of a singular and a non-square matrix; and the <see cref="Matrix"/> members the wheel doesn't expose. It
/// prints how many real outputs are bit-identical to QuantLib's.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MatrixCxxVectorTests
{
    [Fact]
    public void MatrixCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrix");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "Matrix(Size, Size)")
            {
                check.Output(
                    c,
                    "dimensions",
                    c.Input("shapes").EnumerateArray()
                        .Select(e => new Matrix(e[0].GetInt32(), e[1].GetInt32()))
                        .Select(m => (object?)new object?[] { m.Rows, m.Columns })
                        .ToArray());
                continue;
            }

            if (c.Name == "outerProduct refusals")
            {
                check.Output(
                    c,
                    "outerProduct",
                    c.Input("pairs").EnumerateArray()
                        .Select(p => (object?)(Func<object?>)(() =>
                            Rows(MatrixFunctions.OuterProduct(Reals(p[0]), Reals(p[1])))))
                        .ToArray());
                continue;
            }

            Matrix a = FromSpec(c.Input("m"));
            if (!c.Name.StartsWith("members ", StringComparison.Ordinal))
            {
                check.Output(c, "determinant", () => MatrixFunctions.Determinant(a));
                check.Output(c, "inverse", () => Rows(MatrixFunctions.Inverse(a)));
                continue;
            }

            Matrix b = FromSpec(c.Input("b")), wrong = FromSpec(c.Input("wrong")), other = FromSpec(c.Input("other"));
            double s = c.Double("s");
            check.Output(c, "rows", a.Rows);
            check.Output(c, "columns", a.Columns);
            check.Output(c, "size1", a.Size1);
            check.Output(c, "size2", a.Size2);
            check.Output(c, "empty", a.Empty);
            check.Output(c, "diagonal", a.Diagonal());
            check.Output(c, "storage", Storage(a));
            check.Output(c, "row", Rows(a));
            check.Output(
                c,
                "at",
                Enumerable.Range(0, a.Rows + 1).Select(i => (object?)(Func<object?>)(() => a.At(i).ToArray())).ToArray());
            check.Output(c, "column", Enumerable.Range(0, a.Columns).Select(j => (object?)a.Column(j)).ToArray());
            check.Output(c, "negative", Rows(-a));
            check.Output(c, "quotient", Rows(a / s));
            check.Output(c, "multiplyAssign", InPlace(a, m => m *= s));
            check.Output(c, "divideAssign", InPlace(a, m => m /= s));
            check.Output(c, "addAssign", InPlace(a, m => m += b));
            check.Output(c, "subtractAssign", InPlace(a, m => m -= b));
            check.Output(c, "addAssignWrongSize", InPlace(a, m => m += wrong));
            check.Output(c, "subtractAssignWrongSize", InPlace(a, m => m -= wrong));
            Matrix first = a.Clone(), second = other.Clone();
            first.Swap(second);
            check.Output(c, "swap", new object?[] { Rows(first), Rows(second) });
            check.Output(c, "equalCopy", a == a.Clone());
            check.Output(c, "notEqualCopy", a != a.Clone());
            check.Output(c, "equalOther", a == b);
            check.Output(c, "notEqualOther", a != b);
            check.Output(c, "text", a.ToString());
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"cxx/math/matrix: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }

    // The elements in storage order, through the enumerator (QuantLib's begin() to end()).
    private static double[] Storage(Matrix m)
    {
        var elements = new List<double>();
        foreach (double x in m)
        {
            elements.Add(x);
        }

        return [.. elements];
    }

    private static Vector Reals(System.Text.Json.JsonElement e) => new([.. e.EnumerateArray().Select(x => x.GetDouble())]);

    // A compound assignment on a copy, which it changes in place and returns as its rows; or its throw.
    private static Func<object?> InPlace(Matrix m, Action<Matrix> assign) => () =>
    {
        Matrix copy = m.Clone();
        assign(copy);
        return Rows(copy);
    };
}
