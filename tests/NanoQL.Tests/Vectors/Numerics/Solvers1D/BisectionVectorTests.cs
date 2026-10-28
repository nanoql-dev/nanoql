// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The bisection vectors, <c>tests/vectors/math/solvers1d/bisection.json</c>: <see cref="Bisection"/>'s two solve forms
/// over delegates, as the wheel's take a Python function, with the setters of <see cref="Solver1D{TSelf, TFunction}"/>,
/// on six polynomials (<see cref="Solver1DPolynomials"/>), and the refusals: the root, the number of calls of the
/// function and the argument of its last call.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BisectionVectorTests
{
    [Fact]
    public void BisectionVector()
    {
        var file = VectorFile.Load("math/solvers1d/bisection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var f = new Solver1DPolynomials.Counted(c.String("function"));
            Bisection solver = Solver1DPolynomials.Configured(new Bisection(), c);
            double accuracy = Number(c.Input("accuracy")), guess = Number(c.Input("guess"));
            check.Output(c, "root", () => Solver1DPolynomials.StepForm(c)
                ? solver.Solve(f.Evaluate, accuracy, guess, Number(c.Input("step")))
                : solver.Solve(f.Evaluate, accuracy, guess, Number(c.Input("xMin")), Number(c.Input("xMax"))));
            check.Output(c, "calls", f.Calls);
            check.Output(c, "lastArgument", f.Last);
        }

        check.Complete();
    }
}
