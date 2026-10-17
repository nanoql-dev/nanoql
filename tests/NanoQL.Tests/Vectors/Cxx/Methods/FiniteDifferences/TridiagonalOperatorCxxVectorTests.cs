// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's TridiagonalOperator vectors, <c>tests/vectors/cxx/methods/finitedifferences/tridiagonaloperator.json</c>:
/// <see cref="TridiagonalOperator"/>'s members the wheel doesn't expose (<see cref="TridiagonalOperator.SOR"/>, the
/// in-place <see cref="TridiagonalOperator.SolveFor(Vector, Vector)"/>, <see cref="TridiagonalOperator.IsTimeDependent"/>,
/// <see cref="TridiagonalOperator.SetTime"/> through a time setter, the diagonals' accessors,
/// <see cref="TridiagonalOperator.Swap"/>, the unary operators, the scalar-first product and the quotient, the size
/// constructor), with the refusals. Its time setter is the harness's own, which <see cref="ScaleByTime"/> repeats.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TridiagonalOperatorCxxVectorTests
{
    [Fact]
    public void TridiagonalOperatorCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/tridiagonaloperator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "size constructor and identity")
            {
                check.Output(
                    c,
                    "sizeConstructor",
                    c.Ints("sizes").Select(n => (object?)(Func<object?>)(() => Sizes(new TridiagonalOperator(n)))).ToArray());
                check.Output(c, "identityZero", () => TridiagonalOperator.Identity(0));
                check.Output(c, "identityOne", Diagonals(TridiagonalOperator.Identity(1)));
                continue;
            }

            if (c.Inputs.TryGetProperty("method", out JsonElement method))
            {
                JsonElement[] p = [.. c.Input("params").EnumerateArray()];
                TridiagonalOperator refused = FromSpec(p[0]);
                var rhs = new Vector(Numbers(p[1]));
                Func<object?> result = method.GetString() switch
                {
                    "TridiagonalOperator::solveFor(const Array&)" => () => refused.SolveFor(rhs),
                    "TridiagonalOperator::solveFor(const Array&,Array&)" => () =>
                    {
                        var into = new Vector(rhs.Size, 0.0);
                        refused.SolveFor(rhs, into);
                        return into;
                    },
                    "TridiagonalOperator::SOR" => () => refused.SOR(rhs, Number(p[2])),
                    _ => throw new InvalidOperationException($"{c.Name}: unknown method {method}"),
                };
                check.Output(c, "result", result);
                continue;
            }

            TridiagonalOperator op = FromSpec(c.Input("operator"));
            var x = new Vector(Numbers(c.Input("rhs")));
            double s = c.Double("s");
            check.Output(c, "isTimeDependent", op.IsTimeDependent);
            check.Output(c, "lowerDiagonal", op.LowerDiagonal());
            check.Output(c, "diagonal", op.Diagonal());
            check.Output(c, "upperDiagonal", op.UpperDiagonal());
            check.Output(
                c,
                "sor",
                Numbers(c.Input("tolerances")).Select(tol => (object?)(Func<object?>)(() => op.SOR(x, tol))).ToArray());
            check.Output(c, "solveForInto", () =>
            {
                var into = new Vector(x.Size, 0.0);
                op.SolveFor(x, into);
                return into;
            });
            check.Output(c, "solveForInPlace", () =>
            {
                Vector same = x.Clone();
                op.SolveFor(same, same);
                return same;
            });
            TridiagonalOperator timed = FromSpec(c.Input("operator"));
            var setTime = new List<object?>();
            foreach (double t in Numbers(c.Input("times")))
            {
                timed.SetTime(t);
                setTime.Add(Diagonals(timed));
            }

            check.Output(c, "setTime", setTime);
            TridiagonalOperator first = FromSpec(c.Input("operator")), second = FromSpec(c.Input("partner"));
            first.Swap(second);
            check.Output(c, "swapSizes", new object?[] { first.Size, second.Size });
            check.Output(c, "swapFirst", Diagonals(first));
            check.Output(c, "swapSecond", Diagonals(second));
            check.Output(c, "swapTimeDependent", new object?[] { first.IsTimeDependent, second.IsTimeDependent });
            Result(check, c, "positive", +op);
            Result(check, c, "negative", -op);
            Result(check, c, "scaledLeft", s * op);
            Result(check, c, "quotient", op / s);
        }

        check.Complete();
    }

    private static void Result(VectorAssert check, VectorCase c, string name, TridiagonalOperator op)
    {
        check.Output(c, name, Diagonals(op));
        check.Output(c, name + "TimeDependent", op.IsTimeDependent);
    }

    private static object?[] Diagonals(TridiagonalOperator op) => [op.LowerDiagonal(), op.Diagonal(), op.UpperDiagonal()];

    private static object?[] Sizes(TridiagonalOperator op) =>
        [op.Size, op.LowerDiagonal().Size, op.Diagonal().Size, op.UpperDiagonal().Size];

    // The harness's operator spec: {"low", "mid", "high"}, with "timeSetter": "scaleByTime", or {"size": 0}.
    private static TridiagonalOperator FromSpec(JsonElement spec)
    {
        if (spec.TryGetProperty("size", out JsonElement size))
        {
            return new TridiagonalOperator(size.GetInt32());
        }

        Vector low = new(Numbers(spec.GetProperty("low"))), mid = new(Numbers(spec.GetProperty("mid")));
        Vector high = new(Numbers(spec.GetProperty("high")));
        return spec.TryGetProperty("timeSetter", out _)
            ? new TimeDependentOperator(low, mid, high)
            : new TridiagonalOperator(low, mid, high);
    }

    // The harness's time setter: every element of the three diagonals multiplied by the time, through the accessors
    // and the row setters, so each SetTime(t) scales the operator's current coefficients by t.
    private sealed class ScaleByTime : TridiagonalOperatorTimeSetter
    {
        public override void SetTime(double t, TridiagonalOperator L)
        {
            Vector low = L.LowerDiagonal(), mid = L.Diagonal(), high = L.UpperDiagonal();
            int n = L.Size;
            L.SetFirstRow(mid[0] * t, high[0] * t);
            for (int i = 1; i <= n - 2; ++i)
            {
                L.SetMidRow(i, low[i - 1] * t, mid[i] * t, high[i] * t);
            }

            L.SetLastRow(low[n - 2] * t, mid[n - 1] * t);
        }
    }

    // An operator whose time setter is ScaleByTime: a derived class sets the protected time setter.
    private sealed class TimeDependentOperator : TridiagonalOperator
    {
        public TimeDependentOperator(Vector low, Vector mid, Vector high)
            : base(low, mid, high)
        {
            timeSetter = new ScaleByTime();
        }
    }
}
