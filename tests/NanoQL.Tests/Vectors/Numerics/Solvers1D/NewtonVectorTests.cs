// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Newton vectors, <c>tests/vectors/math/solvers1d/newton.json</c>: <see cref="Newton"/>'s two solve
/// forms over delegates, as the wheel's take a Python function with a derivative, with the setters of
/// <see cref="Solver1D{TSelf, TFunction}"/>, on six polynomials and their derivatives
/// (<see cref="Solver1DPolynomials"/>), and the refusals: the root, the number of calls of the function and of its
/// derivative, and the argument of the function's last call.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NewtonVectorTests
{
    [Fact]
    public void NewtonVector()
    {
        var file = VectorFile.Load("math/solvers1d/newton");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var f = new Solver1DPolynomials.Counted(c.String("function"));
            Newton solver = Solver1DPolynomials.Configured(new Newton(), c);
            double accuracy = Number(c.Input("accuracy")), guess = Number(c.Input("guess"));
            check.Output(c, "root", () => Solver1DPolynomials.StepForm(c)
                ? solver.Solve(f.Evaluate, f.Derivative, accuracy, guess, Number(c.Input("step")))
                : solver.Solve(
                    f.Evaluate, f.Derivative, accuracy, guess, Number(c.Input("xMin")), Number(c.Input("xMax"))));
            check.Output(c, "calls", f.Calls);
            check.Output(c, "derivativeCalls", f.DerivativeCalls);
            check.Output(c, "lastArgument", f.Last);
        }

        check.Complete();
    }
}
