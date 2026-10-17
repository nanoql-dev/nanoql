// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/levenbergmarquardt.hpp ql/math/optimization/levenbergmarquardt.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The Levenberg–Marquardt optimization method, minimizing the sum of the squares of the cost function's values.</summary>
/// <remarks>
/// <para>
/// This implementation is based on MINPACK (<see href="http://www.netlib.org/minpack"/>,
/// <see href="http://www.netlib.org/cephes/linalg.tgz"/>). It has a built in finite-difference scheme to compute the
/// Jacobian, which is used by default. If <c>useCostFunctionsJacobian</c> is true the corresponding method in the
/// cost function of the problem is used instead. Note that the default implementation of the Jacobian in
/// <see cref="CostFunction"/> uses a central difference (order 2, but requiring more function evaluations) compared to
/// the forward difference implemented here (order 1).
/// </para>
/// <para>
/// A point that fails the constraint, or whose values aren't all finite, gets the value 1e10 for every function, so
/// the method steers away from it; its Jacobian there is the initial point's. Immutable: the problem the MINPACK
/// callbacks read, which QuantLib keeps in a member, is the minimization's own.
/// </para>
/// </remarks>
public sealed class LevenbergMarquardt : OptimizationMethod
{
    private readonly double epsfcn;
    private readonly double xtol;
    private readonly double gtol;
    private readonly bool useCostFunctionsJacobian;

    /// <summary>Initializes a new instance of the <see cref="LevenbergMarquardt"/> class.</summary>
    /// <param name="epsfcn">The functions' relative errors, which set the forward-difference step.</param>
    /// <param name="xtol">The relative error desired in the solution, non-negative.</param>
    /// <param name="gtol">The orthogonality desired between the values and the Jacobian's columns, non-negative.</param>
    /// <param name="useCostFunctionsJacobian">Whether to use the cost function's Jacobian rather than forward differences.</param>
    public LevenbergMarquardt(
        double epsfcn = 1.0e-8, double xtol = 1.0e-8, double gtol = 1.0e-8, bool useCostFunctionsJacobian = false)
    {
        this.epsfcn = epsfcn;
        this.xtol = xtol;
        this.gtol = gtol;
        this.useCostFunctionsJacobian = useCostFunctionsJacobian;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// There are no variables, fewer functions than variables, a negative tolerance, no evaluations allowed, or MINPACK
    /// refuses its input, finds its tolerances too small or returns an unknown result.
    /// </exception>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria)
    {
        P.Reset();
        Vector initX = P.CurrentValue;
        Vector initCostValues = P.CostFunction.Values(initX);
        int m = initCostValues.Size;
        int n = initX.Size;
        var initJacobian = new Matrix();
        if (useCostFunctionsJacobian)
        {
            initJacobian = new Matrix(m, n);
            P.CostFunction.Jacobian(initJacobian, initX);
        }

        Vector xx = initX.Clone();
        var fvec = new double[m];
        var diag = new double[n];
        int mode = 1;
        // magic number recommended by the documentation
        double factor = 100;
        // lmdif() evaluates cost function n+1 times for each iteration (technically, 2n+1
        // times if useCostFunctionsJacobian is true, but lmdif() doesn't account for that)
        int maxfev = endCriteria.MaxIterations * (n + 1);
        int nprint = 0;
        int info = 0;
        int nfev = 0;
        var fjac = new double[m * n];
        int ldfjac = m;
        var ipvt = new int[n];
        var qtf = new double[n];
        var wa1 = new double[n];
        var wa2 = new double[n];
        var wa3 = new double[n];
        var wa4 = new double[m];
        // requirements; check here to get more detailed error messages.
        if (!(n > 0))
        {
            throw new NanoQLException("no variables given");
        }

        if (!(m >= n))
        {
            throw new NanoQLException($"less functions ({m}) than available variables ({n})");
        }

        if (!(endCriteria.FunctionEpsilon >= 0.0))
        {
            throw new NanoQLException("negative f tolerance");
        }

        if (!(xtol >= 0.0))
        {
            throw new NanoQLException("negative x tolerance");
        }

        if (!(gtol >= 0.0))
        {
            throw new NanoQLException("negative g tolerance");
        }

        if (!(maxfev > 0))
        {
            throw new NanoQLException("null number of evaluations");
        }

        // call lmdif to minimize the sum of the squares of m functions
        // in n variables by the Levenberg-Marquardt algorithm.
        // The callbacks ignore MINPACK's iflag, as QuantLib's do.
        LmdifFunctions.LmdifCostFunction lmdifCostFunction =
            (int fm, int fn, Span<double> x, Span<double> values, ref int iflag) =>
                Fcn(P, initCostValues, fn, x, values);
        LmdifFunctions.LmdifCostFunction? lmdifJacFunction = useCostFunctionsJacobian
            ? (int jm, int jn, Span<double> x, Span<double> jacobian, ref int iflag) =>
                JacFcn(P, initJacobian, jm, jn, x, jacobian)
            : null;
        LmdifFunctions.Lmdif(
            m,
            n,
            xx.AsSpan(),
            fvec,
            endCriteria.FunctionEpsilon,
            xtol,
            gtol,
            maxfev,
            epsfcn,
            diag,
            mode,
            factor,
            nprint,
            ref info,
            ref nfev,
            fjac,
            ldfjac,
            ipvt,
            qtf,
            wa1,
            wa2,
            wa3,
            wa4,
            lmdifCostFunction,
            lmdifJacFunction);

        // check requirements & endCriteria evaluation
        if (!(info != 0))
        {
            throw new NanoQLException("MINPACK: improper input parameters");
        }

        if (!(info != 7))
        {
            throw new NanoQLException(
                "MINPACK: xtol is too small. no further improvement in the approximate solution x is possible.");
        }

        if (!(info != 8))
        {
            throw new NanoQLException(
                "MINPACK: gtol is too small. fvec is orthogonal to the columns of the jacobian to machine precision.");
        }

        EndCriteriaType ecType;
        switch (info)
        {
            case 1:
            case 2:
            case 3:
            case 4:
                // 2 and 3 should be StationaryPoint, 4 a new gradient-related value,
                // but we keep StationaryFunctionValue for backwards compatibility.
                ecType = EndCriteriaType.StationaryFunctionValue;
                break;
            case 5:
                ecType = EndCriteriaType.MaxIterations;
                break;
            case 6:
                ecType = EndCriteriaType.FunctionEpsilonTooSmall;
                break;
            default:
                throw new NanoQLException($"unknown MINPACK result: {info}");
        }

        // set problem
        P.CurrentValue = xx;
        P.FunctionValue = P.CostFunction.Value(P.CurrentValue);

        return ecType;
    }

    // QuantLib's fcn: the problem's values at x, counted, or 1e10 for every function where x fails the constraint or a
    // value isn't finite.
    private static void Fcn(ProblemState P, Vector initCostValues, int n, Span<double> x, Span<double> fvec)
    {
        var xt = new Vector(n);
        x[..n].CopyTo(xt.AsSpan());
        if (P.Constraint.Test(xt))
        {
            Vector tmp = P.Values(xt);
            bool valid = true;
            foreach (double i in tmp)
            {
                if (!double.IsFinite(i))
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                tmp.AsSpan().CopyTo(fvec);
                return;
            }
        }

        // Constraint violated or evaluation produced non-finite values:
        // return a large, uniform penalty so the optimizer steers away.
        // A fixed constant is used instead of the initial cost values because
        // the latter can be very small (even zero) when the starting point is
        // near-optimal, which would fail to deter the optimizer from exploring
        // infeasible regions.
        fvec[..initCostValues.Size].Fill(1.0e10);
    }

    // QuantLib's jacFcn: the cost function's Jacobian at x, by columns, or the initial point's where x fails the
    // constraint or an element isn't finite.
    private static void JacFcn(ProblemState P, Matrix initJacobian, int m, int n, Span<double> x, Span<double> fjac)
    {
        var xt = new Vector(n);
        x[..n].CopyTo(xt.AsSpan());
        if (P.Constraint.Test(xt))
        {
            var tmp = new Matrix(m, n);
            P.CostFunction.Jacobian(tmp, xt);
            bool valid = true;
            for (int i = 0; i < tmp.Rows && valid; i++)
            {
                foreach (double it in tmp.Row(i))
                {
                    if (!double.IsFinite(it))
                    {
                        valid = false;
                        break;
                    }
                }
            }

            if (valid)
            {
                CopyByRows(MatrixFunctions.Transpose(tmp), fjac);
                return;
            }
        }

        // Constraint violated or Jacobian produced non-finite values:
        // return initial Jacobian so the optimizer doesn't diverge
        CopyByRows(MatrixFunctions.Transpose(initJacobian), fjac);
    }

    // std::copy over a Matrix's begin() and end(): its elements, row by row.
    private static void CopyByRows(Matrix source, Span<double> destination)
    {
        int at = 0;
        for (int i = 0; i < source.Rows; i++)
        {
            Span<double> row = source.Row(i);
            row.CopyTo(destination[at..]);
            at += row.Length;
        }
    }
}
