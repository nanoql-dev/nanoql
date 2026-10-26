// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Halley vectors, <c>tests/vectors/cxx/math/solvers1d/halley.json</c>: <see cref="Halley"/>'s roots
/// of polynomials and exponentials from guesses and in brackets, its fallback to Newton-safe, and too few evaluations
/// (refused).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HalleyCxxVectorTests
{
    [Fact]
    public void HalleyCxxVector()
    {
        var file = VectorFile.Load("cxx/math/solvers1d/halley");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement fn = c.Input("function");
            var f = new Function(fn[0].GetString() == "polynomial", [.. fn.EnumerateArray().Skip(1).Select(e => e.GetDouble())]);
            Halley solver = new Halley().SetMaxEvaluations(c.Int("maxEvaluations"));
            double accuracy = c.Double("accuracy"), guess = c.Double("guess");
            JsonElement bracket = c.Input("bracket");
            check.Output(c, "root", () => bracket.ValueKind == JsonValueKind.Null
                ? solver.Solve(f, accuracy, guess, c.Double("step"))
                : solver.Solve(f, accuracy, guess, bracket[0].GetDouble(), bracket[1].GetDouble()));
        }

        check.Complete();
    }

    // The harness's function: Σ c_k x^k, each power multiplied up from 1, or a·exp(bx) − c, with its two derivatives.
    private readonly struct Function(bool polynomial, double[] c) : ISecondDerivativeSolver1DFunction
    {
        public double Evaluate(double x)
        {
            if (!polynomial)
            {
                return (c[0] * Math.Exp(c[1] * x)) - c[2];
            }

            double s = 0.0, p = 1.0;
            foreach (double ck in c)
            {
                s += ck * p;
                p *= x;
            }

            return s;
        }

        public double Derivative(double x)
        {
            if (!polynomial)
            {
                return c[0] * c[1] * Math.Exp(c[1] * x);
            }

            double s = 0.0, p = 1.0;
            for (int k = 1; k < c.Length; ++k)
            {
                s += k * c[k] * p;
                p *= x;
            }

            return s;
        }

        public double SecondDerivative(double x)
        {
            if (!polynomial)
            {
                return c[0] * c[1] * c[1] * Math.Exp(c[1] * x);
            }

            double s = 0.0, p = 1.0;
            for (int k = 2; k < c.Length; ++k)
            {
                s += k * (k - 1) * c[k] * p;
                p *= x;
            }

            return s;
        }
    }
}
