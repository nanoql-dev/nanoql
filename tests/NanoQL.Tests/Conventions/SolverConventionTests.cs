// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// §2.6's two routes for the bisection, false-position and secant solvers (the S-core plan's C4): each finds Brent's
/// root, within the accuracy asked, on functions of several shapes, bracketed and from a step.
/// </summary>
public class SolverConventionTests
{
    private static readonly (string Name, Func<double, double> F, double Guess)[] Functions =
    [
        ("x³ − 2", x => (x * x * x) - 2.0, 1.0),
        ("eˣ − 3", x => Math.Exp(x) - 3.0, 0.5),
        ("cos x − x", x => Math.Cos(x) - x, 0.5),
        ("ln x + x", x => Math.Log(x) + x, 0.4),
    ];

    [Fact]
    public void EachSolverFindsBrentsRoot()
    {
        const double accuracy = 1e-10;
        foreach ((string name, Func<double, double> f, double guess) in Functions)
        {
            double brent = new Brent().Solve(f, 1e-14, guess, 0.1, 3.0);
            double[] roots =
            [
                new Bisection().Solve(f, accuracy, guess, 0.1, 3.0),
                new FalsePosition().Solve(f, accuracy, guess, 0.1, 3.0),
                new Secant().Solve(f, accuracy, guess, 0.1, 3.0),
                new Bisection().Solve(f, accuracy, guess, 0.01),
                new FalsePosition().Solve(f, accuracy, guess, 0.01),
                new Secant().Solve(f, accuracy, guess, 0.01),
            ];
            foreach (double root in roots)
            {
                Assert.True(Math.Abs(root - brent) < 10 * accuracy, $"{name}: {root:R} against {brent:R}");
            }
        }
    }
}
