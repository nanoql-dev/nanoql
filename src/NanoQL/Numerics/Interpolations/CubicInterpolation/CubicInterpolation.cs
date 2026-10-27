// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2004, 2008, 2009, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009 Sylvain Bertrand
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2016 Nicholas Bertocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/cubicinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// Cubic interpolation between discrete points: on each interval, the cubic matching the values at its ends and the
/// first derivatives there, which the derivative approximation gives.
/// </summary>
/// <remarks>
/// <para>
/// Different first-derivative approximations are implemented, both local and non-local
/// (<see cref="CubicInterpolationDerivativeApprox"/>). Local schemes (fourth-order, parabolic, modified parabolic,
/// Fritsch–Butland, Akima, Kruger) use only the values near x<sub>i</sub> to calculate each derivative there; the
/// non-local spline schemes, with different boundary conditions, use all the values and solve a linear system. Local
/// schemes produce C¹ interpolants, the spline schemes C² ones.
/// </para>
/// <para>
/// Hyman's monotonicity-constrained filter is also implemented: it can be applied to every scheme to ensure that, in the
/// regions of local monotonicity of the input (three successive increasing or decreasing values), the interpolating
/// cubic remains monotonic. A cubic already monotonic is left unchanged. For the C² interpolants the filter ensures
/// local monotonicity at the expense of the second derivative, which is no longer continuous where it applies. Some
/// non-linear schemes (modified parabolic, Fritsch–Butland, Kruger) are locally monotonic as they stand; the others must
/// be filtered, at the expense of their linearity. See R. L. Dougherty, A. Edelman and J. M. Hyman,
/// "Nonnegativity-, Monotonicity-, or Convexity-Preserving Cubic Spline and Quintic Hermite Interpolation", Mathematics
/// of Computation, v. 52, n. 186, April 1989, pp. 471-494.
/// </para>
/// <para>
/// Not yet implemented in QuantLib: the fourth-order and modified parabolic schemes, and the periodic boundary
/// condition. Immutable: the coefficients are computed at construction, and
/// <see cref="UpdateLeftConditionValue"/> and <see cref="UpdateRightConditionValue"/> return a new interpolation, where
/// QuantLib's change the boundary value for a later <c>update()</c>. An extension point, as in QuantLib: the named
/// interpolations derive from it.
/// </para>
/// </remarks>
public class CubicInterpolation : Interpolation
{
    private readonly CubicInterpolationDerivativeApprox da;
    private readonly bool monotonic;
    private readonly CubicInterpolationBoundaryCondition leftType, rightType;
    private readonly double leftValue, rightValue;

    // P[i](x) = y[i] + a[i]*(x-x[i]) + b[i]*(x-x[i])^2 + c[i]*(x-x[i])^3
    private readonly ImmutableArray<double> primitiveConst, a, b, c;
    private readonly ImmutableArray<bool> monotonicityAdjustments;

    /// <summary>Initializes a new instance of the <see cref="CubicInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2, and at least 4 for Akima's approximation or a Lagrange condition.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <param name="da">The first-derivative approximation.</param>
    /// <param name="monotonic">Whether to apply Hyman's monotonicity filter.</param>
    /// <param name="leftCond">The left boundary condition, for the spline approximation.</param>
    /// <param name="leftConditionValue">The left condition's value, where it takes one.</param>
    /// <param name="rightCond">The right boundary condition, for the spline approximation.</param>
    /// <param name="rightConditionValue">The right condition's value, where it takes one.</param>
    /// <exception cref="ArgumentException">The sizes differ, or there are too few points.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">
    /// The scheme or a condition isn't implemented (<see cref="CubicInterpolationDerivativeApprox.FourthOrder"/> with
    /// more than 2 points, <see cref="CubicInterpolationBoundaryCondition.Periodic"/> under the spline), or the spline's
    /// system has a zero pivot, or a <see cref="CubicInterpolationBoundaryCondition.NotAKnot"/> end under the spline has
    /// only 2 points, where QuantLib reads past its arrays (a divergence: nanoql refuses it).
    /// </exception>
    public CubicInterpolation(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        CubicInterpolationDerivativeApprox da,
        bool monotonic,
        CubicInterpolationBoundaryCondition leftCond,
        double leftConditionValue,
        CubicInterpolationBoundaryCondition rightCond,
        double rightConditionValue)
        : base(x, y, Cubic.RequiredPoints)
    {
        EnumArguments.Check(da, nameof(da));
        EnumArguments.Check(leftCond, nameof(leftCond));
        EnumArguments.Check(rightCond, nameof(rightCond));
        CheckPoints(x.Length, da, leftCond, rightCond);
        this.da = da;
        this.monotonic = monotonic;
        leftType = leftCond;
        rightType = rightCond;
        leftValue = leftConditionValue;
        rightValue = rightConditionValue;
        (primitiveConst, a, b, c, monotonicityAdjustments) = Update();
    }

    /// <summary>The constants of the primitive on each interval: the primitive at its left node.</summary>
    /// <returns>One per interval.</returns>
    public ImmutableArray<double> PrimitiveConstants() => primitiveConst;

    /// <summary>The first-order coefficients a<sub>i</sub> of the cubic on each interval.</summary>
    /// <returns>One per interval.</returns>
    public ImmutableArray<double> ACoefficients() => a;

    /// <summary>The second-order coefficients b<sub>i</sub> of the cubic on each interval.</summary>
    /// <returns>One per interval.</returns>
    public ImmutableArray<double> BCoefficients() => b;

    /// <summary>The third-order coefficients c<sub>i</sub> of the cubic on each interval.</summary>
    /// <returns>One per interval.</returns>
    public ImmutableArray<double> CCoefficients() => c;

    /// <summary>Whether Hyman's filter changed the first derivative at each node.</summary>
    /// <returns>One per node; all <see langword="false"/> for an interpolation that isn't monotonic.</returns>
    public ImmutableArray<bool> MonotonicityAdjustments() => monotonicityAdjustments;

    /// <summary>
    /// A cubic interpolation over the same points and settings with another left condition value: QuantLib's
    /// <c>updateLeftConditionValue</c> followed by <c>update()</c>.
    /// </summary>
    /// <param name="value">The left condition's value.</param>
    /// <returns>The new interpolation, which keeps this one's extrapolation setting; this one is unchanged.</returns>
    /// <exception cref="NanoQLException">The spline's system has a zero pivot.</exception>
    public CubicInterpolation UpdateLeftConditionValue(double value) =>
        Rebuild(new CubicInterpolation(X, Y, da, monotonic, leftType, value, rightType, rightValue));

    /// <summary>
    /// A cubic interpolation over the same points and settings with another right condition value: QuantLib's
    /// <c>updateRightConditionValue</c> followed by <c>update()</c>.
    /// </summary>
    /// <param name="value">The right condition's value.</param>
    /// <returns>The new interpolation, which keeps this one's extrapolation setting; this one is unchanged.</returns>
    /// <exception cref="NanoQLException">The spline's system has a zero pivot.</exception>
    public CubicInterpolation UpdateRightConditionValue(double value) =>
        Rebuild(new CubicInterpolation(X, Y, da, monotonic, leftType, leftValue, rightType, value));

    // The constructor's own checks: QuantLib's CubicInterpolationImpl refuses too few points for a Lagrange condition or
    // Akima's scheme before any computation. LogCubicInterpolation runs them on its inner cubic's behalf, in QuantLib's
    // order, before its own check of the y values.
    internal static void CheckPoints(
        int points,
        CubicInterpolationDerivativeApprox da,
        CubicInterpolationBoundaryCondition leftCondition,
        CubicInterpolationBoundaryCondition rightCondition)
    {
        if (leftCondition == CubicInterpolationBoundaryCondition.Lagrange
            || rightCondition == CubicInterpolationBoundaryCondition.Lagrange)
        {
            if (!(points >= 4))
            {
                throw new ArgumentException(
                    $"Lagrange boundary condition requires at least 4 points ({points} are given)", "x");
            }
        }

        if (da == CubicInterpolationDerivativeApprox.Akima)
        {
            if (!(points >= 4))
            {
                throw new ArgumentException($"Akima approximation requires at least 4 points ({points} are given)", "x");
            }
        }
    }

    // What the cubic refuses for a point count alone, in QuantLib's order: templateImpl's minimum, the constructor's
    // checks, then nanoql's refusal of a not-a-knot spline end on two points, which reads the configuration and the
    // count only. A left Periodic end, which QuantLib's update refuses before it reaches the right end, keeps that
    // refusal, so the not-a-knot right end isn't refused here then. Cubic.CheckPoints and LogCubic.CheckPoints run it.
    internal static void CheckConstruction(
        int points,
        CubicInterpolationDerivativeApprox da,
        CubicInterpolationBoundaryCondition leftCondition,
        CubicInterpolationBoundaryCondition rightCondition)
    {
        CheckRequiredPoints(points, Cubic.RequiredPoints);
        CheckPoints(points, da, leftCondition, rightCondition);
        if (da == CubicInterpolationDerivativeApprox.Spline
            && (leftCondition == CubicInterpolationBoundaryCondition.NotAKnot
                || (leftCondition != CubicInterpolationBoundaryCondition.Periodic
                    && rightCondition == CubicInterpolationBoundaryCondition.NotAKnot)))
        {
            RequireNotAKnotPoints(points);
        }
    }

    /// <inheritdoc/>
    protected sealed override double ValueCore(double x)
    {
        int j = Locate(x);
        double dx = x - X[j];
        return Y[j] + (dx * (a[j] + (dx * (b[j] + (dx * c[j])))));
    }

    /// <inheritdoc/>
    protected sealed override double PrimitiveCore(double x)
    {
        int j = Locate(x);
        double dx = x - X[j];
        return primitiveConst[j]
            + (dx * (Y[j] + (dx * ((a[j] / 2.0) + (dx * ((b[j] / 3.0) + (dx * c[j] / 4.0)))))));
    }

    /// <inheritdoc/>
    protected sealed override double DerivativeCore(double x)
    {
        int j = Locate(x);
        double dx = x - X[j];
        return a[j] + (((2.0 * b[j]) + (3.0 * c[j] * dx)) * dx);
    }

    /// <inheritdoc/>
    protected sealed override double SecondDerivativeCore(double x)
    {
        int j = Locate(x);
        double dx = x - X[j];
        return (2.0 * b[j]) + (6.0 * c[j] * dx);
    }

    // A divergence: on 2 points QuantLib's not-a-knot rows read dx_[1] and dx_[n_-3], past the end of dx_
    // (cubicinterpolation.hpp:428-431,460-463), a result C++ leaves undefined. nanoql refuses it, in each end's own case,
    // so a left end QuantLib refuses first (Periodic) keeps QuantLib's refusal.
    private static void RequireNotAKnotPoints(int n)
    {
        if (n < 3)
        {
            throw new NanoQLException("the not-a-knot end condition needs at least 3 points");
        }
    }

    // std::min and std::max: the first argument unless the second compares smaller (larger).
    private static double Min(double x, double y) => y < x ? y : x;

    private static double Max(double x, double y) => x < y ? y : x;

    private static double CubicInterpolatingPolynomialDerivative(
        double a, double b, double c, double d, double u, double v, double w, double z, double x)
    {
        return (-((((a-c)*(b-c)*(c-x)*z-(a-d)*(b-d)*(d-x)*w)*(a-x+b-x)
                   +((a-c)*(b-c)*z-(a-d)*(b-d)*w)*(a-x)*(b-x))*(a-b)+
                  ((a-c)*(a-d)*v-(b-c)*(b-d)*u)*(c-d)*(c-x)*(d-x)
                  +((a-c)*(a-d)*(a-x)*v-(b-c)*(b-d)*(b-x)*u)
                  *(c-x+d-x)*(c-d)))/
            ((a-b)*(a-c)*(a-d)*(b-c)*(b-d)*(c-d));
    }

    private CubicInterpolation Rebuild(CubicInterpolation updated) =>
        (CubicInterpolation)updated.WithExtrapolation(AllowsExtrapolation);

    // QuantLib's update(): the first-derivative approximation, Hyman's filter, then the coefficients.
    private (ImmutableArray<double> PrimitiveConst, ImmutableArray<double> A, ImmutableArray<double> B,
        ImmutableArray<double> C, ImmutableArray<bool> MonotonicityAdjustments) Update()
    {
        ReadOnlySpan<double> xs = X, ys = Y;
        int n = xs.Length;

        // QuantLib's mutable members tmp_, dx_ and S_, locals here.
        var tmp = new Vector(n);
        var dx = new double[n - 1];
        var S = new double[n - 1];

        for (int i = 0; i < n - 1; ++i)
        {
            dx[i] = xs[i + 1] - xs[i];
            S[i] = (ys[i + 1] - ys[i]) / dx[i];
        }

        // first derivative approximation
        if (da == CubicInterpolationDerivativeApprox.Spline)
        {
            var L = new TridiagonalOperator(n);
            for (int i = 1; i < n - 1; ++i)
            {
                L.SetMidRow(i, dx[i], 2.0 * (dx[i] + dx[i - 1]), dx[i - 1]);
                tmp[i] = 3.0 * ((dx[i] * S[i - 1]) + (dx[i - 1] * S[i]));
            }

            // left boundary condition
            switch (leftType)
            {
                case CubicInterpolationBoundaryCondition.NotAKnot:
                    RequireNotAKnotPoints(n);

                    // ignoring end condition value
                    L.SetFirstRow(dx[1] * (dx[1] + dx[0]), (dx[0] + dx[1]) * (dx[0] + dx[1]));
                    tmp[0] = (S[0] * dx[1] * ((2.0 * dx[1]) + (3.0 * dx[0]))) + (S[1] * dx[0] * dx[0]);
                    break;
                case CubicInterpolationBoundaryCondition.FirstDerivative:
                    L.SetFirstRow(1.0, 0.0);
                    tmp[0] = leftValue;
                    break;
                case CubicInterpolationBoundaryCondition.SecondDerivative:
                    L.SetFirstRow(2.0, 1.0);
                    tmp[0] = (3.0 * S[0]) - (leftValue * dx[0] / 2.0);
                    break;
                case CubicInterpolationBoundaryCondition.Periodic:
                    throw new NanoQLException("this end condition is not implemented yet");
                case CubicInterpolationBoundaryCondition.Lagrange:
                    L.SetFirstRow(1.0, 0.0);
                    tmp[0] = CubicInterpolatingPolynomialDerivative(
                        xs[0], xs[1], xs[2], xs[3], ys[0], ys[1], ys[2], ys[3], xs[0]);
                    break;
                default:
                    throw new NanoQLException("unknown end condition");
            }

            // right boundary condition
            switch (rightType)
            {
                case CubicInterpolationBoundaryCondition.NotAKnot:
                    RequireNotAKnotPoints(n);

                    // ignoring end condition value
                    L.SetLastRow(
                        -(dx[n - 2] + dx[n - 3]) * (dx[n - 2] + dx[n - 3]),
                        -dx[n - 3] * (dx[n - 3] + dx[n - 2]));
                    tmp[n - 1] = (-S[n - 3] * dx[n - 2] * dx[n - 2])
                        - (S[n - 2] * dx[n - 3] * ((3.0 * dx[n - 2]) + (2.0 * dx[n - 3])));
                    break;
                case CubicInterpolationBoundaryCondition.FirstDerivative:
                    L.SetLastRow(0.0, 1.0);
                    tmp[n - 1] = rightValue;
                    break;
                case CubicInterpolationBoundaryCondition.SecondDerivative:
                    L.SetLastRow(1.0, 2.0);
                    tmp[n - 1] = (3.0 * S[n - 2]) + (rightValue * dx[n - 2] / 2.0);
                    break;
                case CubicInterpolationBoundaryCondition.Periodic:
                    throw new NanoQLException("this end condition is not implemented yet");
                case CubicInterpolationBoundaryCondition.Lagrange:
                    L.SetLastRow(0.0, 1.0);
                    tmp[n - 1] = CubicInterpolatingPolynomialDerivative(
                        xs[n - 4], xs[n - 3], xs[n - 2], xs[n - 1], ys[n - 4], ys[n - 3], ys[n - 2], ys[n - 1], xs[n - 1]);
                    break;
                default:
                    throw new NanoQLException("unknown end condition");
            }

            // solve the system
            L.SolveFor(tmp, tmp);
        }
        else if (da == CubicInterpolationDerivativeApprox.SplineOM1
            || da == CubicInterpolationDerivativeApprox.SplineOM2)
        {
            OvershootingMinimization(da == CubicInterpolationDerivativeApprox.SplineOM2, xs, ys, dx, tmp);
        }
        else
        {
            // local schemes
            if (n == 2)
            {
                tmp[0] = tmp[1] = S[0];
            }
            else
            {
                LocalScheme(n, dx, S, tmp);
            }
        }

        var monotonicityAdjustments = new bool[n];

        // Hyman monotonicity constrained filter
        if (monotonic)
        {
            HymanFilter(n, dx, S, tmp, monotonicityAdjustments);
        }

        // cubic coefficients
        double[] a = new double[n - 1], b = new double[n - 1], c = new double[n - 1];
        for (int i = 0; i < n - 1; ++i)
        {
            a[i] = tmp[i];
            b[i] = ((3.0 * S[i]) - tmp[i + 1] - (2.0 * tmp[i])) / dx[i];
            c[i] = (tmp[i + 1] + tmp[i] - (2.0 * S[i])) / (dx[i] * dx[i]);
        }

        var primitiveConst = new double[n - 1];
        primitiveConst[0] = 0.0;
        for (int i = 1; i < n - 1; ++i)
        {
            primitiveConst[i] = primitiveConst[i - 1]
                + (dx[i - 1]
                   * (ys[i - 1] + (dx[i - 1]
                      * ((a[i - 1] / 2.0) + (dx[i - 1]
                         * ((b[i - 1] / 3.0) + (dx[i - 1] * c[i - 1] / 4.0)))))));
        }

        return (
            ImmutableCollectionsMarshal.AsImmutableArray(primitiveConst),
            ImmutableCollectionsMarshal.AsImmutableArray(a),
            ImmutableCollectionsMarshal.AsImmutableArray(b),
            ImmutableCollectionsMarshal.AsImmutableArray(c),
            ImmutableCollectionsMarshal.AsImmutableArray(monotonicityAdjustments));
    }

    // SplineOM1 and SplineOM2, which differ only in their matrix Q.
    private static void OvershootingMinimization(
        bool second, ReadOnlySpan<double> xs, ReadOnlySpan<double> ys, double[] dx, Vector tmp)
    {
        int n = xs.Length;
        var T = new Matrix(n - 2, n, 0.0);
        for (int i = 0; i < n - 2; ++i)
        {
            T[i, i] = dx[i] / 6.0;
            T[i, i + 1] = second ? (dx[i] + dx[i + 1]) / 3.0 : (dx[i + 1] + dx[i]) / 3.0;
            T[i, i + 2] = dx[i + 1] / 6.0;
        }

        var S = new Matrix(n - 2, n, 0.0);
        for (int i = 0; i < n - 2; ++i)
        {
            S[i, i] = 1.0 / dx[i];
            S[i, i + 1] = -((1.0 / dx[i + 1]) + (1.0 / dx[i]));
            S[i, i + 2] = 1.0 / dx[i + 1];
        }

        var Up = new Matrix(n, 2, 0.0);
        Up[0, 0] = 1;
        Up[n - 1, 1] = 1;
        var Us = new Matrix(n, n - 2, 0.0);
        for (int i = 0; i < n - 2; ++i)
        {
            Us[i + 1, i] = 1;
        }

        Matrix Z = Us * MatrixFunctions.Inverse(T * Us);
        var I = new Matrix(n, n, 0.0);
        for (int i = 0; i < n; ++i)
        {
            I[i, i] = 1;
        }

        Matrix V = (I - (Z * T)) * Up;
        Matrix W = Z * S;
        var Q = new Matrix(n, n, 0.0);
        if (!second)
        {
            Q[0, 0] = 1.0 / (n - 1) * dx[0] * dx[0] * dx[0];
            Q[0, 1] = 7.0 / 8 * 1.0 / (n - 1) * dx[0] * dx[0] * dx[0];
            for (int i = 1; i < n - 1; ++i)
            {
                Q[i, i - 1] = 7.0 / 8 * 1.0 / (n - 1) * dx[i - 1] * dx[i - 1] * dx[i - 1];
                Q[i, i] = (1.0 / (n - 1) * dx[i] * dx[i] * dx[i]) + (1.0 / (n - 1) * dx[i - 1] * dx[i - 1] * dx[i - 1]);
                Q[i, i + 1] = 7.0 / 8 * 1.0 / (n - 1) * dx[i] * dx[i] * dx[i];
            }

            Q[n - 1, n - 2] = 7.0 / 8 * 1.0 / (n - 1) * dx[n - 2] * dx[n - 2] * dx[n - 2];
            Q[n - 1, n - 1] = 1.0 / (n - 1) * dx[n - 2] * dx[n - 2] * dx[n - 2];
        }
        else
        {
            Q[0, 0] = 1.0 / (n - 1) * dx[0];
            Q[0, 1] = 1.0 / 2 * 1.0 / (n - 1) * dx[0];
            for (int i = 1; i < n - 1; ++i)
            {
                Q[i, i - 1] = 1.0 / 2 * 1.0 / (n - 1) * dx[i - 1];
                Q[i, i] = (1.0 / (n - 1) * dx[i]) + (1.0 / (n - 1) * dx[i - 1]);
                Q[i, i + 1] = 1.0 / 2 * 1.0 / (n - 1) * dx[i];
            }

            Q[n - 1, n - 2] = 1.0 / 2 * 1.0 / (n - 1) * dx[n - 2];
            Q[n - 1, n - 1] = 1.0 / (n - 1) * dx[n - 2];
        }

        Matrix J = (I - (V * MatrixFunctions.Inverse(MatrixFunctions.Transpose(V) * Q * V) * MatrixFunctions.Transpose(V) * Q)) * W;
        var Y = new Vector(n);
        for (int i = 0; i < n; ++i)
        {
            Y[i] = ys[i];
        }

        Vector D = J * Y;
        for (int i = 0; i < n - 1; ++i)
        {
            tmp[i] = ((Y[i + 1] - Y[i]) / dx[i]) - (((2.0 * D[i]) + D[i + 1]) * dx[i] / 6.0);
        }

        tmp[n - 1] = tmp[n - 2] + (D[n - 2] * dx[n - 2]) + ((D[n - 1] - D[n - 2]) * dx[n - 2] / 2.0);
    }

    // Also the finite-difference Asian condition's held-buffer spline (FdmArithmeticAverageCondition).
    internal static void HymanFilter(int n, double[] dx, double[] S, Vector tmp, bool[] monotonicityAdjustments)
    {
        double correction;
        double pm, pu, pd, M;
        for (int i = 0; i < n; ++i)
        {
            if (i == 0)
            {
                if (tmp[i] * S[0] > 0.0)
                {
                    correction = tmp[i] / Math.Abs(tmp[i]) * Min(Math.Abs(tmp[i]), Math.Abs(3.0 * S[0]));
                }
                else
                {
                    correction = 0.0;
                }

                if (correction != tmp[i])
                {
                    tmp[i] = correction;
                    monotonicityAdjustments[i] = true;
                }
            }
            else if (i == n - 1)
            {
                if (tmp[i] * S[n - 2] > 0.0)
                {
                    correction = tmp[i] / Math.Abs(tmp[i]) * Min(Math.Abs(tmp[i]), Math.Abs(3.0 * S[n - 2]));
                }
                else
                {
                    correction = 0.0;
                }

                if (correction != tmp[i])
                {
                    tmp[i] = correction;
                    monotonicityAdjustments[i] = true;
                }
            }
            else
            {
                pm = ((S[i - 1] * dx[i]) + (S[i] * dx[i - 1])) / (dx[i - 1] + dx[i]);

                // std::min of an initializer list: the leftmost smallest.
                M = 3.0 * Min(Min(Math.Abs(S[i - 1]), Math.Abs(S[i])), Math.Abs(pm));
                if (i > 1)
                {
                    if ((S[i - 1] - S[i - 2]) * (S[i] - S[i - 1]) > 0.0)
                    {
                        pd = ((S[i - 1] * ((2.0 * dx[i - 1]) + dx[i - 2])) - (S[i - 2] * dx[i - 1])) / (dx[i - 2] + dx[i - 1]);
                        if (pm * pd > 0.0 && pm * (S[i - 1] - S[i - 2]) > 0.0)
                        {
                            M = Max(M, 1.5 * Min(Math.Abs(pm), Math.Abs(pd)));
                        }
                    }
                }

                if (i < n - 2)
                {
                    if ((S[i] - S[i - 1]) * (S[i + 1] - S[i]) > 0.0)
                    {
                        pu = ((S[i] * ((2.0 * dx[i]) + dx[i + 1])) - (S[i + 1] * dx[i])) / (dx[i] + dx[i + 1]);
                        if (pm * pu > 0.0 && -pm * (S[i] - S[i - 1]) > 0.0)
                        {
                            M = Max(M, 1.5 * Min(Math.Abs(pm), Math.Abs(pu)));
                        }
                    }
                }

                if (tmp[i] * pm > 0.0)
                {
                    correction = tmp[i] / Math.Abs(tmp[i]) * Min(Math.Abs(tmp[i]), M);
                }
                else
                {
                    correction = 0.0;
                }

                if (correction != tmp[i])
                {
                    tmp[i] = correction;
                    monotonicityAdjustments[i] = true;
                }
            }
        }
    }

    private void LocalScheme(int n, double[] dx, double[] S, Vector tmp)
    {
        switch (da)
        {
            case CubicInterpolationDerivativeApprox.FourthOrder:
                throw new NanoQLException("FourthOrder not implemented yet");
            case CubicInterpolationDerivativeApprox.Parabolic:
                // intermediate points
                for (int i = 1; i < n - 1; ++i)
                {
                    tmp[i] = ((dx[i - 1] * S[i]) + (dx[i] * S[i - 1])) / (dx[i] + dx[i - 1]);
                }

                // end points
                tmp[0] = ((((2.0 * dx[0]) + dx[1]) * S[0]) - (dx[0] * S[1])) / (dx[0] + dx[1]);
                tmp[n - 1] = ((((2.0 * dx[n - 2]) + dx[n - 3]) * S[n - 2]) - (dx[n - 2] * S[n - 3])) / (dx[n - 2] + dx[n - 3]);
                break;
            case CubicInterpolationDerivativeApprox.FritschButland:
                // intermediate points
                for (int i = 1; i < n - 1; ++i)
                {
                    double Smin = Min(S[i - 1], S[i]);
                    double Smax = Max(S[i - 1], S[i]);
                    if (Smax + (2.0 * Smin) == 0)
                    {
                        if (Smin * Smax < 0)
                        {
                            tmp[i] = NumericLimits.QL_MIN_REAL;
                        }
                        else if (Smin * Smax == 0)
                        {
                            tmp[i] = 0;
                        }
                        else
                        {
                            tmp[i] = NumericLimits.QL_MAX_REAL;
                        }
                    }
                    else
                    {
                        tmp[i] = 3.0 * Smin * Smax / (Smax + (2.0 * Smin));
                    }
                }

                // end points
                tmp[0] = ((((2.0 * dx[0]) + dx[1]) * S[0]) - (dx[0] * S[1])) / (dx[0] + dx[1]);
                tmp[n - 1] = ((((2.0 * dx[n - 2]) + dx[n - 3]) * S[n - 2]) - (dx[n - 2] * S[n - 3])) / (dx[n - 2] + dx[n - 3]);
                break;
            case CubicInterpolationDerivativeApprox.Akima:
                tmp[0] = ((Math.Abs(S[1] - S[0]) * 2 * S[0] * S[1]) + (Math.Abs((2 * S[0] * S[1]) - (4 * S[0] * S[0] * S[1])) * S[0]))
                    / (Math.Abs(S[1] - S[0]) + Math.Abs((2 * S[0] * S[1]) - (4 * S[0] * S[0] * S[1])));
                tmp[1] = ((Math.Abs(S[2] - S[1]) * S[0]) + (Math.Abs(S[0] - (2 * S[0] * S[1])) * S[1]))
                    / (Math.Abs(S[2] - S[1]) + Math.Abs(S[0] - (2 * S[0] * S[1])));
                for (int i = 2; i < n - 2; ++i)
                {
                    if ((S[i - 2] == S[i - 1]) && (S[i] != S[i + 1]))
                    {
                        tmp[i] = S[i - 1];
                    }
                    else if ((S[i - 2] != S[i - 1]) && (S[i] == S[i + 1]))
                    {
                        tmp[i] = S[i];
                    }
                    else if (S[i] == S[i - 1])
                    {
                        tmp[i] = S[i];
                    }
                    else if ((S[i - 2] == S[i - 1]) && (S[i - 1] != S[i]) && (S[i] == S[i + 1]))
                    {
                        tmp[i] = (S[i - 1] + S[i]) / 2.0;
                    }
                    else
                    {
                        tmp[i] = ((Math.Abs(S[i + 1] - S[i]) * S[i - 1]) + (Math.Abs(S[i - 1] - S[i - 2]) * S[i]))
                            / (Math.Abs(S[i + 1] - S[i]) + Math.Abs(S[i - 1] - S[i - 2]));
                    }
                }

                tmp[n - 2] = ((Math.Abs((2 * S[n - 2] * S[n - 3]) - S[n - 2]) * S[n - 3]) + (Math.Abs(S[n - 3] - S[n - 4]) * S[n - 2]))
                    / (Math.Abs((2 * S[n - 2] * S[n - 3]) - S[n - 2]) + Math.Abs(S[n - 3] - S[n - 4]));
                tmp[n - 1] = ((Math.Abs((4 * S[n - 2] * S[n - 2] * S[n - 3]) - (2 * S[n - 2] * S[n - 3])) * S[n - 2])
                        + (Math.Abs(S[n - 2] - S[n - 3]) * 2 * S[n - 2] * S[n - 3]))
                    / (Math.Abs((4 * S[n - 2] * S[n - 2] * S[n - 3]) - (2 * S[n - 2] * S[n - 3])) + Math.Abs(S[n - 2] - S[n - 3]));
                break;
            case CubicInterpolationDerivativeApprox.Kruger:
                // intermediate points
                for (int i = 1; i < n - 1; ++i)
                {
                    if (S[i - 1] * S[i] < 0.0)
                    {
                        // slope changes sign at point
                        tmp[i] = 0.0;
                    }
                    else
                    {
                        // slope will be between the slopes of the adjacent straight lines and should approach zero if
                        // the slope of either line approaches zero
                        tmp[i] = 2.0 / ((1.0 / S[i - 1]) + (1.0 / S[i]));
                    }
                }

                // end points
                tmp[0] = ((3.0 * S[0]) - tmp[1]) / 2.0;
                tmp[n - 1] = ((3.0 * S[n - 2]) - tmp[n - 2]) / 2.0;
                break;
            case CubicInterpolationDerivativeApprox.Harmonic:
                // intermediate points
                for (int i = 1; i < n - 1; ++i)
                {
                    double w1 = (2 * dx[i]) + dx[i - 1];
                    double w2 = dx[i] + (2 * dx[i - 1]);
                    if (S[i - 1] * S[i] <= 0.0)
                    {
                        // slope changes sign at point
                        tmp[i] = 0.0;
                    }
                    else
                    {
                        // weighted harmonic mean of S[i] and S[i-1] if they have the same sign; otherwise 0
                        tmp[i] = (w1 + w2) / ((w1 / S[i - 1]) + (w2 / S[i]));
                    }
                }

                // end points [0]
                tmp[0] = ((((2 * dx[0]) + dx[1]) * S[0]) - (dx[0] * S[1])) / (dx[1] + dx[0]);
                if (tmp[0] * S[0] < 0.0)
                {
                    tmp[0] = 0;
                }
                else if (S[0] * S[1] < 0)
                {
                    if (Math.Abs(tmp[0]) > Math.Abs(3 * S[0]))
                    {
                        tmp[0] = 3 * S[0];
                    }
                }

                // end points [n-1]
                tmp[n - 1] = ((((2 * dx[n - 2]) + dx[n - 3]) * S[n - 2]) - (dx[n - 2] * S[n - 3])) / (dx[n - 3] + dx[n - 2]);
                if (tmp[n - 1] * S[n - 2] < 0.0)
                {
                    tmp[n - 1] = 0;
                }
                else if (S[n - 2] * S[n - 3] < 0)
                {
                    if (Math.Abs(tmp[n - 1]) > Math.Abs(3 * S[n - 2]))
                    {
                        tmp[n - 1] = 3 * S[n - 2];
                    }
                }

                break;
            default:
                throw new NanoQLException("unknown scheme");
        }
    }
}
