// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The 1-D solvers are values (docs/porting-playbook.md §2's <c>Solver1D</c> row): <c>SetMaxEvaluations</c>,
/// <c>SetLowerBound</c> and <c>SetUpperBound</c> return a modified copy of the solver's own type and leave the
/// receiver unchanged, where QuantLib's setters change the solver; and the delegate overloads, which mirror
/// QuantLib-Python's <c>solve</c>, give the struct functions' results bit for bit.
/// </summary>
public class Solver1DTests
{
    private static readonly Func<double, double> Square = x => (x * x) - 1.0;

    [Fact]
    public void SettersReturnAModifiedCopy()
    {
        var brent = new Brent();
        double root = brent.Solve(Square, 1e-10, 0.5, 0.01);

        Brent limited = brent.SetMaxEvaluations(3);
        Assert.NotSame(brent, limited);
        Assert.Throws<NanoQLException>(() => limited.Solve(Square, 1e-10, 0.5, 0.01));
        Assert.Equal(root, brent.Solve(Square, 1e-10, 0.5, 0.01));

        // A bracket below the copy's lower bound, or above its upper bound, is refused; each receiver takes it. A copy
        // keeps its receiver's settings, so the chained copy has both bounds.
        Brent lowerOnly = brent.SetLowerBound(0.25);
        Brent bounded = lowerOnly.SetUpperBound(1.75);
        Assert.Throws<ArgumentException>(() => bounded.Solve(Square, 1e-10, 0.5, 0.0, 1.5));
        Assert.Throws<ArgumentException>(() => bounded.Solve(Square, 1e-10, 0.5, 0.5, 2.0));
        Assert.Throws<ArgumentException>(() => lowerOnly.Solve(Square, 1e-10, 0.5, 0.0, 1.5));
        Assert.Equal(1.0, lowerOnly.Solve(Square, 1e-10, 0.75, 0.5, 2.0), 1e-10);
        Assert.Equal(1.0, brent.Solve(Square, 1e-10, 0.5, 0.0, 2.0), 1e-10);
        Assert.Equal(root, bounded.Solve(Square, 1e-10, 0.5, 0.01), 1e-10);

        // NewtonSafe's setters return a NewtonSafe.
        var newton = new NewtonSafe();
        NewtonSafe newtonLimited = newton.SetMaxEvaluations(2);
        Assert.Throws<NanoQLException>(() => newtonLimited.Solve(Square, x => 2.0 * x, 1e-10, 0.5, 0.01));
        Assert.Equal(1.0, newton.Solve(Square, x => 2.0 * x, 1e-10, 0.5, 0.01), 1e-10);
        Assert.IsType<NewtonSafe>(newton.SetLowerBound(0.0));

        Assert.Throws<ArgumentOutOfRangeException>(() => brent.SetMaxEvaluations(-1));
    }

    [Fact]
    public void DelegateOverloadsEqualStructFunctions()
    {
        var brent = new Brent();
        var newton = new NewtonSafe();
        var cubic = new Cubic();
        Func<double, double> f = cubic.Evaluate, df = cubic.Derivative;

        Assert.Equal(brent.Solve(cubic, 1e-10, 2.0, 0.1), brent.Solve(f, 1e-10, 2.0, 0.1));
        Assert.Equal(brent.Solve(cubic, 1e-12, 2.5, 2.0, 3.0), brent.Solve(f, 1e-12, 2.5, 2.0, 3.0));
        Assert.Equal(newton.Solve(cubic, 1e-10, -1.0, 1.0), newton.Solve(f, df, 1e-10, -1.0, 1.0));
        Assert.Equal(newton.Solve(cubic, 1e-12, 2.5, 2.0, 3.0), newton.Solve(f, df, 1e-12, 2.5, 2.0, 3.0));

        Assert.Throws<ArgumentNullException>(() => brent.Solve((Func<double, double>)null!, 1e-10, 2.0, 0.1));
        Assert.Throws<ArgumentNullException>(() => newton.Solve(f, null!, 1e-10, 2.0, 0.1));
    }

    // x³ − 2x − 5, Newton's own example, with its derivative.
    private readonly struct Cubic : IDerivativeSolver1DFunction
    {
        public double Evaluate(double x) => (x * x * x) - (2.0 * x) - 5.0;

        public double Derivative(double x) => (3.0 * x * x) - 2.0;
    }
}
