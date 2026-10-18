// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's finite-difference Newton-safe vectors,
/// <c>tests/vectors/cxx/math/solvers1d/finitedifferencenewtonsafe.json</c>: <see cref="FiniteDifferenceNewtonSafe"/>'s
/// two solve forms over delegates, with the setters of <see cref="Solver1D{TSelf, TFunction}"/>, on twelve functions
/// written as the harness writes them (<c>tools/cxx/src/math/solvers1d/finitedifferencenewtonsafe.cpp</c>), and the
/// refusals: the root, the number of calls of the function and the argument of its last call.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FiniteDifferenceNewtonSafeCxxVectorTests
{
    [Fact]
    public void FiniteDifferenceNewtonSafeCxxVector()
    {
        var file = VectorFile.Load("cxx/math/solvers1d/finitedifferencenewtonsafe");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var f = new Counted(Function(c.String("function")));
            FiniteDifferenceNewtonSafe solver = Configured(c.Input("solver"));
            double accuracy = c.Double("accuracy"), guess = c.Double("guess");
            check.Output(c, "root", () => c.String("form") == "step"
                ? solver.Solve(f.Evaluate, accuracy, guess, c.Double("step"))
                : solver.Solve(f.Evaluate, accuracy, guess, c.Double("xMin"), c.Double("xMax")));
            if (c.Expected.TryGetProperty("calls", out _))
            {
                check.Output(c, "calls", f.Calls);
                check.Output(c, "lastArgument", f.Last);
            }
        }

        check.Complete();
    }

    private static FiniteDifferenceNewtonSafe Configured(JsonElement spec)
    {
        var solver = new FiniteDifferenceNewtonSafe();
        if (CouponInputs.NullableInt(spec.GetProperty("maxEvaluations")) is int evaluations)
        {
            solver = solver.SetMaxEvaluations(evaluations);
        }

        if (CouponInputs.NullableReal(spec.GetProperty("lowerBound")) is double lower)
        {
            solver = solver.SetLowerBound(lower);
        }

        if (CouponInputs.NullableReal(spec.GetProperty("upperBound")) is double upper)
        {
            solver = solver.SetUpperBound(upper);
        }

        return solver;
    }

    private static Func<double, double> Function(string name) => name switch
    {
        "p1" => x => (x * x) - 1.0,
        "p2" => x => 1.0 - (x * x),
        "p3" => x => (x * x * x) - (2.0 * x) - 5.0,
        "p4" => x => (x - 0.3) * (x + 2.0) * (x - 4.0),
        "p5" => x => (x * x * x * x) - 10.0,
        "p6" => x => (0.5 * x * x * x) - (x * x) + 0.25,
        "cube" => x => (x - 1.0) * (x - 1.0) * (x - 1.0),
        "reciprocal" => x => (1.0 / x) - 2.0,
        "root" => x => Math.Sqrt(x) - 1.5,
        "lorentz" => x => (x / (1.0 + (x * x))) - 0.3,
        "quintic" => x => (x * x * x * x * x) - (3.0 * x) + 1.0,
        "flat" => x => (0.001 * (x - 7.0)) + ((x - 7.0) * (x - 7.0) * (x - 7.0)),
        _ => throw new InvalidOperationException($"unknown function {name}"),
    };

    /// <summary>A function counting its calls and keeping its last argument.</summary>
    /// <param name="f">The function.</param>
    private sealed class Counted(Func<double, double> f)
    {
        public int Calls { get; private set; }

        public double? Last { get; private set; }

        public double Evaluate(double x)
        {
            Calls++;
            Last = x;
            return f(x);
        }
    }
}
