// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="GMRES"/> on a lucky breakdown, and a zero restart count. QuantLib's loop stops when h(j+1, j) falls below
/// ε² (<c>gmres.cpp:101-102</c>) without rotating the column that found it, and solves with the columns before it
/// (<c>:128-131</c>). At the first step there are none: k is 0, <c>y[k-1]</c> writes before its array, and QuantLib's
/// process crashes on diag(2, 3) with b = (1, 0), an eigenvector. At a later step the dropped column turns a solved
/// system into a refusal: on diag(1, 1, 3, 3) with b = (1, 1, 1, 1), the second step's vector is exactly zero, and
/// QuantLib's <c>solve</c> throws "could not converge" where its <c>solveWithRestart(5)</c> returns the exact
/// solution. nanoql takes the breakdown as the method defines it, the Krylov space holding the exact solution: it
/// rotates the column and solves with it. <c>solveWithRestart</c>'s count bounds the restarts, <c>restart − 1</c> after
/// the first cycle (<c>:54</c>), and at 0 the bound wraps to none, so QuantLib can run forever where every positive
/// count ends; nanoql refuses 0 on entry, as QuantLib refuses a zero <c>maxIter</c>.
/// </summary>
public class GmresBreakdownTests
{
    [Fact]
    public void ABreakdownAtTheFirstStepSolvesTheSystem()
    {
        var a = new Matrix(2, 2, 0.0) { [0, 0] = 2.0, [1, 1] = 3.0 };
        var b = new Vector([1.0, 0.0]);

        GMRESResult result = new GMRES(x => a * x, 5, 1e-10).Solve(b);

        // QuantLib's process crashes here (exit 0xC0000005); its check is S2's inverse, the system's LU solve
        Vector exact = MatrixFunctions.Inverse(a) * b;
        Assert.Equal(exact.ToArray(), result.X.ToArray());
        Assert.Equal([1.0, 0.0], result.Errors);
    }

    [Fact]
    public void ABreakdownAtALaterStepSolvesWhatQuantLibsRestartedSolveSolves()
    {
        var a = new Matrix(4, 4, 0.0) { [0, 0] = 1.0, [1, 1] = 1.0, [2, 2] = 3.0, [3, 3] = 3.0 };
        var b = new Vector([1.0, 1.0, 1.0, 1.0]);

        GMRESResult result = new GMRES(x => a * x, 10, 1e-10).Solve(b);

        // QuantLib's solve throws "could not converge"; its solveWithRestart(5) returns this solution
        double[] quantLibRestarted = [1.0000000000000002, 1.0000000000000002, 0.3333333333333332, 0.3333333333333332];
        Assert.Equal(quantLibRestarted, result.X.ToArray(), (x, y) => Math.Abs(x - y) <= 1e-15);
        Assert.Equal(0.0, result.Errors[^1]);
    }

    [Fact]
    public void AZeroRestartCountIsRefused()
    {
        var a = new Matrix(3, 3, 0.0) { [0, 1] = 1.0, [1, 2] = 1.0, [2, 0] = 1.0 };
        var solver = new GMRES(x => a * x, 1, 1e-8);

        // QuantLib restarts without bound on this cyclic shift, which GMRES(1) can't solve, and never returns
        var e = Assert.Throws<ArgumentException>(() => solver.SolveWithRestart(0, new Vector([1.0, 0.0, 0.0])));
        Assert.Equal("restart", e.ParamName);
        Assert.StartsWith("restart must be greater than zero", e.Message, StringComparison.Ordinal);
    }
}
