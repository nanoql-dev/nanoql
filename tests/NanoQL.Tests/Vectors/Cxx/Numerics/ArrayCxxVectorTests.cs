// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Array vectors, <c>tests/vectors/cxx/math/array.json</c>: the <see cref="Vector"/> members and
/// <see cref="ArrayFunctions"/> the wheel doesn't expose (the size and the value-and-increment constructors,
/// <see cref="ArrayFunctions.Norm2"/> and the element functions, <see cref="Vector.Resize"/>, <see cref="Vector.Front"/>,
/// <see cref="Vector.Back"/>, <see cref="Vector.At"/>, <see cref="Vector.Empty"/>, the compound assignments, the
/// scalar-first operators, the unary <c>+</c> and <see cref="Vector.Swap"/>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ArrayCxxVectorTests
{
    [Fact]
    public void ArrayCxxVector()
    {
        var file = VectorFile.Load("cxx/math/array");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "constructors")
            {
                check.Output(c, "sizeConstructor", c.Ints("sizes").Select(n => (object?)new Vector(n).Size).ToArray());
                check.Output(
                    c,
                    "valueIncrementConstructor",
                    c.Input("constructed").EnumerateArray()
                        .Select(e => (object?)new Vector(e[0].GetInt32(), Number(e[1]), Number(e[2])))
                        .ToArray());
                continue;
            }

            double[] x = Numbers(c.Input("x"));
            int[] resizes = c.Ints("resizes");
            Vector a = new(x);
            check.Output(c, "norm2", ArrayFunctions.Norm2(a));
            check.Output(c, "empty", a.Empty);
            check.Output(c, "resizeSize", resizes.Select(m => (object?)Resized(x, m).Size).ToArray());
            check.Output(
                c,
                "resizeKept",
                resizes.Select(m => (object?)Resized(x, m).AsSpan()[..Math.Min(m, x.Length)].ToArray()).ToArray());
            if (c.Name == "empty")
            {
                check.Output(c, "at", new object?[] { (Func<object?>)(() => a.At(0)) });
                continue;
            }

            double[] y = Numbers(c.Input("y")), z = Numbers(c.Input("z"));
            double s = Number(c.Input("s")), p = Number(c.Input("p"));
            check.Output(c, "abs", ArrayFunctions.Abs(a));
            check.Output(c, "sqrt", ArrayFunctions.Sqrt(a));
            check.Output(c, "log", ArrayFunctions.Log(a));
            check.Output(c, "exp", ArrayFunctions.Exp(a));
            check.Output(c, "pow", ArrayFunctions.Pow(a, p));
            check.Output(c, "front", a.Front());
            check.Output(c, "back", a.Back());
            check.Output(c, "at", c.Ints("indices").Select(i => (object?)(Func<object?>)(() => a.At(i))).ToArray());
            check.Output(c, "addAssign", InPlace(x, v => v += new Vector(y)));
            check.Output(c, "addAssignScalar", InPlace(x, v => v += s));
            check.Output(c, "subtractAssign", InPlace(x, v => v -= new Vector(y)));
            check.Output(c, "subtractAssignScalar", InPlace(x, v => v -= s));
            check.Output(c, "multiplyAssign", InPlace(x, v => v *= new Vector(y)));
            check.Output(c, "multiplyAssignScalar", InPlace(x, v => v *= s));
            check.Output(c, "divideAssign", InPlace(x, v => v /= new Vector(y)));
            check.Output(c, "divideAssignScalar", InPlace(x, v => v /= s));
            check.Output(c, "addAssignOtherSize", InPlace(x, v => v += new Vector(z)));
            check.Output(c, "subtractAssignOtherSize", InPlace(x, v => v -= new Vector(z)));
            check.Output(c, "multiplyAssignOtherSize", InPlace(x, v => v *= new Vector(z)));
            check.Output(c, "divideAssignOtherSize", InPlace(x, v => v /= new Vector(z)));
            check.Output(c, "scalarSum", s + a);
            check.Output(c, "scalarDifference", s - a);
            check.Output(c, "scalarProduct", s * a);
            check.Output(c, "scalarQuotient", s / a);
            check.Output(c, "positive", +a);
            Vector first = new(x), second = new(z);
            first.Swap(second);
            check.Output(c, "swap", new object?[] { first, second });
        }

        check.Complete();
    }

    private static Vector Resized(double[] x, int n)
    {
        var v = new Vector(x);
        v.Resize(n);
        return v;
    }

    // A compound assignment on a fresh vector, which it changes in place and returns; or its throw.
    private static Func<object?> InPlace(double[] x, Action<Vector> assign) => () =>
    {
        var v = new Vector(x);
        assign(v);
        return v;
    };
}
