// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/lbfgsb.hpp ql/math/optimization/lbfgsb.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.ArrayFunctions;
using static NanoQL.Numerics.MatrixFunctions;

namespace NanoQL.Numerics;

/// <summary>Limited-memory BFGS for bound-constrained optimization (L-BFGS-B).</summary>
/// <remarks>
/// <para>
/// Faithful implementation of the algorithm of Byrd, Lu, Nocedal and Zhu, "A Limited Memory Algorithm for Bound
/// Constrained Optimization", SIAM J. Sci. Comput. 16(5):1190-1208, 1995.
/// </para>
/// <para>
/// Coordinates whose bound is ±<see cref="double.MaxValue"/> are treated as unbounded, so with no active bounds the
/// method reduces to plain limited-memory BFGS. Explicit bounds may instead be passed to the constructor, overriding
/// the constraint.
/// </para>
/// <para>
/// Limited memory trades accuracy of the Hessian model for cost: it stores only the last <c>memory</c> correction
/// pairs, needing O(memory·n) storage and work per step instead of the O(n²) of dense BFGS, at the price of linear
/// rather than superlinear convergence. This typically results in a modest increase in iteration count.
/// </para>
/// <para>
/// Immutable: each minimization keeps its correction pairs and working arrays to itself. On the relative
/// function-reduction stop the problem's gradient norm is the projected gradient's at the point returned, where
/// QuantLib's keeps the previous iterate's (<c>Divergences/LBFGSBTests.cs</c>). The breakpoints of the Cauchy search
/// are sorted stably, so equal breakpoints keep QuantLib's order up to 32 variables (MSVC's insertion sort) and their
/// own beyond, where MSVC's introsort reorders them.
/// </para>
/// </remarks>
public sealed class LBFGSB : OptimizationMethod
{
    private const double INF = NumericLimits.QL_MAX_REAL;

    private readonly int m;
    private readonly double pgTol;
    private readonly double factr;
    private readonly Vector lowerBound;
    private readonly Vector upperBound;

    /// <summary>Initializes a new instance of the <see cref="LBFGSB"/> class.</summary>
    /// <param name="memory">The number of stored correction pairs, positive.</param>
    /// <param name="pgTol">The convergence tolerance on the infinity norm of the projected gradient.</param>
    /// <param name="fTol">The iteration stops when the relative reduction of the objective falls below it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="memory"/> isn't positive.</exception>
    public LBFGSB(int memory = 10, double pgTol = 1e-8, double fTol = 1e7 * NumericLimits.QL_EPSILON)
    {
        if (!(memory > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(memory), memory, "memory must be positive");
        }

        m = memory;
        this.pgTol = pgTol;
        factr = fTol / NumericLimits.QL_EPSILON;
        lowerBound = new Vector(0);
        upperBound = new Vector(0);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LBFGSB"/> class with explicit box bounds, which override the bounds
    /// of the problem's constraint. Use ±<see cref="double.MaxValue"/> for unbounded coordinates.
    /// </summary>
    /// <param name="lowerBound">The lower bounds, which it copies.</param>
    /// <param name="upperBound">The upper bounds, which it copies.</param>
    /// <param name="memory">The number of stored correction pairs, positive.</param>
    /// <param name="pgTol">The convergence tolerance on the infinity norm of the projected gradient.</param>
    /// <param name="fTol">The iteration stops when the relative reduction of the objective falls below it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="memory"/> isn't positive.</exception>
    /// <exception cref="ArgumentException">The bounds have different sizes.</exception>
    public LBFGSB(
        ReadOnlySpan<double> lowerBound,
        ReadOnlySpan<double> upperBound,
        int memory = 10,
        double pgTol = 1e-8,
        double fTol = 1e7 * NumericLimits.QL_EPSILON)
        : this(memory, pgTol, fTol)
    {
        this.lowerBound = new Vector(lowerBound);
        this.upperBound = new Vector(upperBound);
        if (!(this.lowerBound.Size == this.upperBound.Size))
        {
            throw new ArgumentException("lower and upper bound sizes are inconsistent", nameof(upperBound));
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The bounds' size doesn't match the number of variables.</exception>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria)
    {
        EndCriteriaType ecType = EndCriteriaType.None;
        P.Reset();

        Vector x = P.CurrentValue.Clone();
        int n = x.Size;

        Vector lo = lowerBound.Empty ? P.Constraint.LowerBound(x) : lowerBound.Clone();
        Vector hi = upperBound.Empty ? P.Constraint.UpperBound(x) : upperBound.Clone();

        if (!(lo.Size == n && hi.Size == n))
        {
            throw new NanoQLException("bounds size does not match the number of variables");
        }

        // start from a feasible point
        for (int i = 0; i < n; ++i)
        {
            x[i] = Min(Max(x[i], lo[i]), hi[i]);
        }

        var g = new Vector(n);
        double f = P.ValueAndGradient(g, x);
        P.CurrentValue = x;
        P.FunctionValue = f;

        var S = new List<Vector>();
        var Y = new List<Vector>();
        double theta = 1.0;
        int iter = 0;
        double pgInf; // projected gradient ∞-norm; persists for final reporting

        while (true)
        {
            // infinity norm of the projected gradient
            pgInf = ProjectedGradientNorm(x, g, lo, hi);

            P.GradientNormValue = pgInf * pgInf;

            if (pgInf < pgTol)
            {
                ecType = EndCriteriaType.ZeroGradientNorm;
                break;
            }

            if (endCriteria.CheckZeroGradientNorm(pgInf))
            {
                ecType = EndCriteriaType.ZeroGradientNorm;
                break;
            }

            if (endCriteria.CheckMaxIterations(iter))
            {
                ecType = EndCriteriaType.MaxIterations;
                break;
            }

            // compact representation; drop the oldest pairs if the middle
            // matrix turns out numerically singular
            CompactRep rep;

            while (true)
            {
                try
                {
                    rep = BuildCompactRep(S, Y, theta);
                    break;
                }
                catch (ArgumentException)
                {
                    if (S.Count == 0)
                    {
                        rep = new CompactRep { Theta = theta };
                        break;
                    }

                    S.RemoveAt(0);
                    Y.RemoveAt(0);
                }
            }

            GeneralizedCauchyPoint(x, g, lo, hi, rep, out Vector xcp, out Vector c, out bool[] isFree);

            Vector xbar;
            try
            {
                xbar = SubspaceMinimization(x, g, xcp, c, lo, hi, rep, isFree);
            }
            catch (ArgumentException)
            {
                xbar = xcp.Clone(); // fall back to the Cauchy point
            }

            Vector d = xbar - x;
            double dphi0 = DotProduct(g, d);

            // fall back to the (always-descent) Cauchy direction, then to
            // the projected gradient, if the subspace step is not downhill
            if (Norm2(d) < NumericLimits.QL_EPSILON || dphi0 >= 0.0)
            {
                d = xcp - x;
                dphi0 = DotProduct(g, d);
            }

            if (Norm2(d) < NumericLimits.QL_EPSILON)
            {
                ecType = EndCriteriaType.StationaryPoint;
                break;
            }

            if (dphi0 >= 0.0)
            {
                for (int i = 0; i < n; ++i)
                {
                    d[i] = Min(Max(x[i] - g[i], lo[i]), hi[i]) - x[i];
                }

                dphi0 = DotProduct(g, d);
                if (dphi0 >= 0.0 || Norm2(d) < NumericLimits.QL_EPSILON)
                {
                    ecType = EndCriteriaType.StationaryPoint;
                    break;
                }
            }

            double stpMax = MaxFeasibleStep(x, d, lo, hi);
            if (stpMax < NumericLimits.QL_EPSILON)
            {
                ecType = EndCriteriaType.StationaryPoint;
                break;
            }

            var xt = new Vector(n);
            var gt = new Vector(n);
            double ft = 0.0;
            if (!LineSearchWolfe(P, x, d, f, g, stpMax, ref xt, ref ft, gt))
            {
                ecType = EndCriteriaType.StationaryFunctionValue;
                break;
            }

            // limited-memory update with the curvature safeguard
            Vector s = xt - x;
            Vector y = gt - g;
            double sy = DotProduct(s, y);
            double yy = DotProduct(y, y);

            if (yy > 0.0 && sy > NumericLimits.QL_EPSILON * yy)
            {
                S.Add(s);
                Y.Add(y);

                if (S.Count > m)
                {
                    S.RemoveAt(0);
                    Y.RemoveAt(0);
                }

                theta = yy / sy;
            }

            double fOld = f;
            x = xt;
            f = ft;
            g = gt;
            ++iter;

            P.CurrentValue = x;
            P.FunctionValue = f;

            // relative function-reduction stop (SciPy's factr criterion)
            double denom = Max(Max(Math.Abs(fOld), Math.Abs(f)), 1.0);
            if ((fOld - f) <= factr * NumericLimits.QL_EPSILON * denom)
            {
                // the projected gradient at the point returned, where QuantLib's reports the previous iterate's
                pgInf = ProjectedGradientNorm(x, g, lo, hi);
                ecType = EndCriteriaType.StationaryFunctionValue;
                break;
            }
        }

        P.CurrentValue = x;
        P.FunctionValue = f;
        P.GradientNormValue = pgInf * pgInf;
        return ecType;
    }

    // std::max and std::min as C++ defines them, (a < b) ? b : a and (b < a) ? b : a (QuantLib's semantics on NaN
    // and signed zeros).
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    // A bound is "absent" when it equals +/- DBL_MAX (the value
    // returned by the default Constraint and by NoConstraint).
    // The 0.5 factor guards against overflow in subsequent arithmetic
    // such as (x - bound) or (bound - x).
    private static bool NoUpper(double u) => u >= 0.5 * INF;

    private static bool NoLower(double l) => l <= -0.5 * INF;

    // infinity norm of the projected gradient P(x - g, l, u) - x
    private static double ProjectedGradientNorm(Vector x, Vector g, Vector lo, Vector hi)
    {
        double pgInf = 0.0;

        for (int i = 0; i < x.Size; ++i)
        {
            double proj = Min(Max(x[i] - g[i], lo[i]), hi[i]) - x[i];
            pgInf = Max(pgInf, Math.Abs(proj));
        }

        return pgInf;
    }

    private static CompactRep BuildCompactRep(List<Vector> S, List<Vector> Y, double theta)
    {
        var rep = new CompactRep
        {
            Theta = theta,
            Col = S.Count,
        };
        int col = rep.Col;

        if (col == 0)
        {
            return rep;
        }

        int n = S[0].Size;
        var W = new Matrix(n, 2 * col);

        for (int j = 0; j < col; ++j)
        {
            for (int i = 0; i < n; ++i)
            {
                W[i, j] = Y[j][i];
                W[i, col + j] = theta * S[j][i];
            }
        }

        // small Gram matrices S^T S and S^T Y
        Matrix StS = new(col, col), StY = new(col, col);

        for (int i = 0; i < col; ++i)
        {
            for (int j = 0; j < col; ++j)
            {
                StS[i, j] = DotProduct(S[i], S[j]);
                StY[i, j] = DotProduct(S[i], Y[j]);
            }
        }

        // middle matrix  [ -D  L^T ; L  theta S^T S ]  with
        // D = diag(s_i.y_i) and L the strictly lower part of S^T Y.
        var mid = new Matrix(2 * col, 2 * col, 0.0);

        for (int i = 0; i < col; ++i)
        {
            mid[i, i] = -StY[i, i]; // -D

            for (int j = 0; j < col; ++j)
            {
                if (i > j)
                {
                    mid[col + i, j] = StY[i, j]; // L
                }

                if (j > i)
                {
                    mid[i, col + j] = StY[j, i]; // L^T
                }

                mid[col + i, col + j] = theta * StS[i, j];
            }
        }

        rep.W = W;
        rep.M = Inverse(mid);
        return rep;
    }

    private static Vector WRow(Matrix W, int i)
    {
        var r = new Vector(W.Columns);

        for (int k = 0; k < r.Size; ++k)
        {
            r[k] = W[i, k];
        }

        return r;
    }

    // Generalized Cauchy point (Byrd et al. 1995, Algorithm CP).
    // Walks the breakpoints of the projected steepest-descent path
    // x(t) = P(x - t g, l, u) and locates the first local minimizer of
    // the quadratic model along it.  Returns the Cauchy point xcp, the
    // accumulated c = W^T (xcp - x) needed by the subspace step, and
    // the set of variables left free (not pinned at a bound).
    private static void GeneralizedCauchyPoint(
        Vector x,
        Vector g,
        Vector lo,
        Vector hi,
        CompactRep rep,
        out Vector xcp,
        out Vector cOut,
        out bool[] isFree)
    {
        int n = x.Size;
        int m2 = 2 * rep.Col;

        xcp = x.Clone();
        isFree = new bool[n];
        Array.Fill(isFree, true);

        Vector t = new(n), d = new(n, 0.0);
        var brk = new List<int>(); // indices with a strictly positive breakpoint
        for (int i = 0; i < n; ++i)
        {
            double ti;

            if (g[i] < 0.0)
            {
                ti = NoUpper(hi[i]) ? INF : (x[i] - hi[i]) / g[i];
            }
            else if (g[i] > 0.0)
            {
                ti = NoLower(lo[i]) ? INF : (x[i] - lo[i]) / g[i];
            }
            else
            {
                ti = INF;
            }

            t[i] = ti;

            if (ti <= 0.0)
            {
                // already sitting on the bound the gradient pushes into
                d[i] = 0.0;
                isFree[i] = false;
            }
            else
            {
                d[i] = -g[i];
                brk.Add(i);
            }
        }

        var c = new Vector(m2, 0.0);
        if (brk.Count == 0)
        {
            // every variable is pinned
            cOut = c;
            return;
        }

        // a stable sort: MSVC's insertion sort up to 32 elements
        brk = [.. brk.OrderBy(i => t[i], BreakpointComparer.Instance)];

        Vector p = (rep.Col > 0) ? (d * rep.W!) : new Vector(0);

        double fp = -DotProduct(d, d); // first derivative of the model, m'(0)
        double fpp = -rep.Theta * fp;  // second derivative, theta d^T d - ...

        if (rep.Col > 0)
        {
            fpp -= DotProduct(p, rep.M! * p);
        }

        double fppFloor = NumericLimits.QL_EPSILON * (fpp > 0.0 ? fpp : 1.0);

        double dtMin = (fpp > 0.0) ? -fp / fpp : 0.0;

        double tOld = 0.0;
        int ptr = 0;
        int b = brk[ptr];
        double tb = t[b];
        double dt = tb;
        bool exhausted = false;

        while (dtMin >= dt && tb < INF)
        {
            // pin variable b at the bound it has reached
            double xcpb = (d[b] > 0.0) ? hi[b] : (d[b] < 0.0 ? lo[b] : x[b]);
            xcp[b] = xcpb;
            isFree[b] = false;
            double zb = xcpb - x[b];
            double gb = g[b];

            if (rep.Col > 0)
            {
                c += dt * p;

                Vector wb = WRow(rep.W!, b);
                Vector Mc = rep.M! * c;
                Vector Mp = rep.M! * p;
                Vector Mwb = rep.M! * wb;

                fp += (dt * fpp) + (gb * gb) + (rep.Theta * gb * zb) - (gb * DotProduct(wb, Mc));
                fpp += (-rep.Theta * gb * gb) - (2.0 * gb * DotProduct(wb, Mp)) - (gb * gb * DotProduct(wb, Mwb));
                p += gb * wb;
            }
            else
            {
                fp += (dt * fpp) + (gb * gb) + (rep.Theta * gb * zb);
                fpp += -rep.Theta * gb * gb;
            }

            if (fpp < fppFloor)
            {
                fpp = fppFloor;
            }

            d[b] = 0.0;
            dtMin = (fpp > 0.0) ? -fp / fpp : 0.0;
            tOld = tb;

            if (++ptr >= brk.Count)
            {
                exhausted = true;
                break;
            }

            b = brk[ptr];
            tb = t[b];
            dt = tb - tOld;
        }

        // advance the still-free variables along the final segment
        if (exhausted)
        {
            dtMin = 0.0;
        }

        dtMin = Max(dtMin, 0.0);
        tOld += dtMin;

        for (int i = 0; i < n; ++i)
        {
            if (isFree[i])
            {
                xcp[i] = x[i] + (tOld * d[i]);
            }
        }

        if (rep.Col > 0)
        {
            c += dtMin * p;
        }

        cOut = c;
    }

    // Subspace minimization by the direct primal method (Byrd et al.
    // 1995, section 5.1).  Minimizes the quadratic model over the free
    // variables, holding the active ones at their Cauchy-point bound,
    // then truncates the step back into the box.
    private static Vector SubspaceMinimization(
        Vector x,
        Vector g,
        Vector xcp,
        Vector c,
        Vector lo,
        Vector hi,
        CompactRep rep,
        bool[] isFree)
    {
        int n = x.Size;
        var freeIdx = new List<int>();

        for (int i = 0; i < n; ++i)
        {
            if (isFree[i])
            {
                freeIdx.Add(i);
            }
        }

        int nf = freeIdx.Count;

        Vector xbar = xcp.Clone();
        if (nf == 0)
        {
            return xbar;
        }

        double theta = rep.Theta;
        int m2 = 2 * rep.Col;

        // reduced gradient of the model at the Cauchy point:
        //   r = [ g + theta (xcp - x) - W M c ]  restricted to free vars
        var WMc = new Vector(n, 0.0);
        if (rep.Col > 0)
        {
            WMc = rep.W! * (rep.M! * c);
        }

        var r = new Vector(nf);
        for (int k = 0; k < nf; ++k)
        {
            int i = freeIdx[k];
            r[k] = g[i] + (theta * (xcp[i] - x[i])) - WMc[i];
        }

        var dhat = new Vector(nf);
        if (rep.Col == 0)
        {
            for (int k = 0; k < nf; ++k)
            {
                dhat[k] = -r[k] / theta;
            }
        }
        else
        {
            // v = M (W_free^T r)
            var wtr = new Vector(m2, 0.0);

            for (int k = 0; k < nf; ++k)
            {
                int i = freeIdx[k];

                for (int j = 0; j < m2; ++j)
                {
                    wtr[j] += rep.W![i, j] * r[k];
                }
            }

            Vector v = rep.M! * wtr;

            // N = I - (1/theta) M (W_free^T W_free)
            var WftWf = new Matrix(m2, m2, 0.0);

            for (int k = 0; k < nf; ++k)
            {
                int i = freeIdx[k];

                for (int a = 0; a < m2; ++a)
                {
                    for (int bb = 0; bb < m2; ++bb)
                    {
                        WftWf[a, bb] += rep.W![i, a] * rep.W![i, bb];
                    }
                }
            }

            Matrix N = rep.M! * WftWf;
            N *= -1.0 / theta;

            for (int a = 0; a < m2; ++a)
            {
                N[a, a] += 1.0;
            }

            v = Inverse(N) * v;

            // dhat = -(1/theta) r - (1/theta^2) W_free v
            for (int k = 0; k < nf; ++k)
            {
                int i = freeIdx[k];
                double Wfv = 0.0;

                for (int j = 0; j < m2; ++j)
                {
                    Wfv += rep.W![i, j] * v[j];
                }

                dhat[k] = (-r[k] / theta) - (Wfv / (theta * theta));
            }
        }

        // truncate so that every free variable stays inside the box
        double alphaStar = 1.0;

        for (int k = 0; k < nf; ++k)
        {
            int i = freeIdx[k];

            if (dhat[k] > 0.0 && !NoUpper(hi[i]))
            {
                alphaStar = Min(alphaStar, (hi[i] - xcp[i]) / dhat[k]);
            }
            else if (dhat[k] < 0.0 && !NoLower(lo[i]))
            {
                alphaStar = Min(alphaStar, (lo[i] - xcp[i]) / dhat[k]);
            }
        }

        alphaStar = Max(alphaStar, 0.0);

        for (int k = 0; k < nf; ++k)
        {
            xbar[freeIdx[k]] = xcp[freeIdx[k]] + (alphaStar * dhat[k]);
        }

        return xbar;
    }

    // Largest feasible step length along d starting from x.
    private static double MaxFeasibleStep(Vector x, Vector d, Vector lo, Vector hi)
    {
        double stp = INF;

        for (int i = 0; i < x.Size; ++i)
        {
            if (d[i] > 0.0 && !NoUpper(hi[i]))
            {
                stp = Min(stp, (hi[i] - x[i]) / d[i]);
            }
            else if (d[i] < 0.0 && !NoLower(lo[i]))
            {
                stp = Min(stp, (lo[i] - x[i]) / d[i]);
            }
        }

        return stp;
    }

    // Line search enforcing the strong Wolfe conditions (Nocedal &
    // Wright, Algorithms 3.5/3.6), capped at the largest feasible step.
    // On success returns the step, trial point, value and gradient.
    private static bool LineSearchWolfe(
        ProblemState P,
        Vector x,
        Vector d,
        double f0,
        Vector g0,
        double stpMax,
        ref Vector xt,
        ref double ft,
        Vector gt)
    {
        const double c1 = 1e-4, c2 = 0.9;
        const int maxIter = 30;

        double dphi0 = DotProduct(g0, d);
        if (dphi0 >= 0.0)
        {
            return false; // not a descent direction
        }

        bool haveBest = false;
        double bestF = f0;
        Vector? bestX = null, bestG = null;

        Vector trialX = xt;
        double trialF = ft;

        // evaluate phi(a) and phi'(a), tracking the best decrease seen
        double Eval(double a)
        {
            trialX = x + (a * d);
            trialF = P.ValueAndGradient(gt, trialX);

            if (trialF < bestF)
            {
                haveBest = true;
                bestF = trialF;
                bestX = trialX.Clone();
                bestG = gt.Clone();
            }

            return DotProduct(gt, d);
        }

        double aLo = 0.0, aHi = 0.0, fLo = f0;
        bool bracketed = false;

        double aPrev = 0.0, fPrev = f0;
        double a = Min(1.0, stpMax);

        try
        {
            for (int i = 0; i < maxIter; ++i)
            {
                double dphi = Eval(a);

                if (trialF > f0 + (c1 * a * dphi0) || (i > 0 && trialF >= fPrev))
                {
                    aLo = aPrev;
                    fLo = fPrev;
                    aHi = a;
                    bracketed = true;
                    break;
                }

                if (Math.Abs(dphi) <= -c2 * dphi0)
                {
                    return true; // strong Wolfe satisfied
                }

                if (dphi >= 0.0)
                {
                    aLo = a;
                    fLo = trialF;
                    aHi = aPrev;
                    bracketed = true;
                    break;
                }

                aPrev = a;
                fPrev = trialF;

                if (a >= stpMax)
                {
                    break; // cannot expand further
                }

                a = Min(2.0 * a, stpMax);
            }

            if (bracketed)
            {
                for (int j = 0; j < maxIter; ++j)
                {
                    a = 0.5 * (aLo + aHi); // bisection
                    double dphi = Eval(a);

                    if (trialF > f0 + (c1 * a * dphi0) || trialF >= fLo)
                    {
                        aHi = a;
                    }
                    else
                    {
                        if (Math.Abs(dphi) <= -c2 * dphi0)
                        {
                            return true;
                        }

                        if (dphi * (aHi - aLo) >= 0.0)
                        {
                            aHi = aLo;
                        }

                        aLo = a;
                        fLo = trialF;
                    }

                    if (Math.Abs(aHi - aLo) < NumericLimits.QL_EPSILON * Max(1.0, Math.Abs(a)))
                    {
                        break;
                    }
                }
            }

            // strong Wolfe not reached: accept the best sufficient decrease
            if (haveBest)
            {
                trialX = bestX!;
                trialF = bestF;
                bestG!.AsSpan().CopyTo(gt.AsSpan());

                return true;
            }

            return false;
        }
        finally
        {
            xt = trialX;
            ft = trialF;
        }
    }

    // The breakpoints' order, QuantLib's operator< on the step lengths.
    private sealed class BreakpointComparer : IComparer<double>
    {
        public static readonly BreakpointComparer Instance = new();

        public int Compare(double a, double b) => a < b ? -1 : (b < a ? 1 : 0);
    }

    // Compact limited-memory representation of the BFGS Hessian
    // approximation  B = theta I - W M W^T   (Byrd, Lu, Nocedal & Zhu
    // 1995, eq. 3.5), built from the stored correction pairs.
    private struct CompactRep
    {
        public Matrix? W;   // n x 2col,   [ Y | theta S ]
        public Matrix? M;   // 2col x 2col, inverse of the middle matrix
        public double Theta;
        public int Col;

        public CompactRep()
        {
            Theta = 1.0;
        }
    }
}
