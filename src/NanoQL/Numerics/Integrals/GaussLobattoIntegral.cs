// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gausslobattointegral.hpp ql/math/integrals/gausslobattointegral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The integral of a one-dimensional function by the adaptive Gauss-Lobatto formula: W. Gander and W. Gautschi,
/// "Adaptive Quadrature - Revisited", BIT 40(1):84-101, March 2000, after Gander's MATLAB <c>adaptlob</c>.
/// </summary>
/// <remarks>
/// The absolute tolerance the recursion stops at is the accuracy, or the smaller of it and the relative accuracy times
/// a 13-point Kronrod estimate of the integral, each over machine epsilon times the convergence estimate's ratio when
/// one is used. The maximum number of evaluations bounds the evaluations made before each recursive step.
/// </remarks>
public sealed class GaussLobattoIntegral : Integrator
{
    // QuantLib's Null<Real>(), which std::max reads as a number when no relative accuracy is given.
    private const double NullReal = 3.4028234663852886e38;

    private const double x1_ = 0.94288241569547971906;
    private const double x2_ = 0.64185334234578130578;
    private const double x3_ = 0.23638319966214988028;

    private static readonly double alpha_ = Math.Sqrt(2.0 / 3.0);
    private static readonly double beta_ = 1.0 / Math.Sqrt(5.0);

    private readonly double? relAccuracy;
    private readonly bool useConvergenceEstimate;

    /// <summary>Initializes a new instance of the <see cref="GaussLobattoIntegral"/> class.</summary>
    /// <param name="maxIterations">The maximum number of evaluations.</param>
    /// <param name="absAccuracy">The absolute accuracy, above machine epsilon.</param>
    /// <param name="relAccuracy">The relative accuracy, or <see langword="null"/> (QuantLib's default) for
    /// none.</param>
    /// <param name="useConvergenceEstimate">Whether the tolerance is scaled by the convergence estimate.</param>
    /// <exception cref="ArgumentException"><paramref name="absAccuracy"/> isn't above machine epsilon.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    public GaussLobattoIntegral(
        int maxIterations, double absAccuracy, double? relAccuracy = null, bool useConvergenceEstimate = true)
        : base(absAccuracy, maxIterations)
    {
        this.relAccuracy = relAccuracy;
        this.useConvergenceEstimate = useConvergenceEstimate;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The tolerance can't be computed from the relative accuracy, the maximum number of evaluations is reached, or an
    /// interval holds no more machine numbers.
    /// </exception>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        int evaluations = 0;
        double calcAbsTolerance = CalculateAbsTolerance(f, a, b, ref evaluations);

        evaluations += 2;
        double fa = f(a);
        double fb = f(b);
        double value = AdaptivGaussLobattoStep(f, a, b, fa, fb, calcAbsTolerance, ref evaluations);
        return new IntegrationResult(value, evaluations);
    }

    private double CalculateAbsTolerance(Func<double, double> f, double a, double b, ref int evaluations)
    {
        double relTol = Max(relAccuracy ?? NullReal, NumericLimits.QL_EPSILON);

        double m = (a + b) / 2;
        double h = (b - a) / 2;
        double y1 = f(a);
        double y3 = f(m - (alpha_ * h));
        double y5 = f(m - (beta_ * h));
        double y7 = f(m);
        double y9 = f(m + (beta_ * h));
        double y11 = f(m + (alpha_ * h));
        double y13 = f(b);

        double f1 = f(m - (x1_ * h));
        double f2 = f(m + (x1_ * h));
        double f3 = f(m - (x2_ * h));
        double f4 = f(m + (x2_ * h));
        double f5 = f(m - (x3_ * h));
        double f6 = f(m + (x3_ * h));

        double acc = h * ((0.0158271919734801831 * (y1 + y13))
                          + (0.0942738402188500455 * (f1 + f2))
                          + (0.1550719873365853963 * (y3 + y11))
                          + (0.1888215739601824544 * (f3 + f4))
                          + (0.1997734052268585268 * (y5 + y9))
                          + (0.2249264653333395270 * (f5 + f6))
                          + (0.2426110719014077338 * y7));

        evaluations += 13;
        if (acc == 0.0 && (f1 != 0.0 || f2 != 0.0 || f3 != 0.0 || f4 != 0.0 || f5 != 0.0 || f6 != 0.0))
        {
            throw new NanoQLException("can not calculate absolute accuracy from relative accuracy");
        }

        double r = 1.0;
        if (useConvergenceEstimate)
        {
            double integral2 = h / 6 * (y1 + y13 + (5 * (y5 + y9)));
            double integral1 = h / 1470 * ((77 * (y1 + y13)) + (432 * (y3 + y11)) + (625 * (y5 + y9)) + (672 * y7));

            if (Math.Abs(integral2 - acc) != 0.0)
            {
                r = Math.Abs(integral1 - acc) / Math.Abs(integral2 - acc);
            }

            if (r == 0.0 || r > 1.0)
            {
                r = 1.0;
            }
        }

        if (relAccuracy is not null)
        {
            return Min(AbsoluteAccuracy, acc * relTol) / (r * NumericLimits.QL_EPSILON);
        }
        else
        {
            return AbsoluteAccuracy / (r * NumericLimits.QL_EPSILON);
        }
    }

    private double AdaptivGaussLobattoStep(
        Func<double, double> f, double a, double b, double fa, double fb, double acc, ref int evaluations)
    {
        if (!(evaluations < MaxEvaluations))
        {
            throw new NanoQLException("max number of iterations reached");
        }

        double h = (b - a) / 2;
        double m = (a + b) / 2;

        double mll = m - (alpha_ * h);
        double ml = m - (beta_ * h);
        double mr = m + (beta_ * h);
        double mrr = m + (alpha_ * h);

        double fmll = f(mll);
        double fml = f(ml);
        double fm = f(m);
        double fmr = f(mr);
        double fmrr = f(mrr);
        evaluations += 5;

        double integral2 = h / 6 * (fa + fb + (5 * (fml + fmr)));
        double integral1 = h / 1470 * ((77 * (fa + fb)) + (432 * (fmll + fmrr)) + (625 * (fml + fmr)) + (672 * fm));

        // avoid 80 bit logic on x86 cpu
        double dist = acc + (integral1 - integral2);
        if (dist == acc || mll <= a || b <= mrr)
        {
            if (!(m > a && b > m))
            {
                throw new NanoQLException("Interval contains no more machine number");
            }

            return integral1;
        }
        else
        {
            return AdaptivGaussLobattoStep(f, a, mll, fa, fmll, acc, ref evaluations)
                   + AdaptivGaussLobattoStep(f, mll, ml, fmll, fml, acc, ref evaluations)
                   + AdaptivGaussLobattoStep(f, ml, m, fml, fm, acc, ref evaluations)
                   + AdaptivGaussLobattoStep(f, m, mr, fm, fmr, acc, ref evaluations)
                   + AdaptivGaussLobattoStep(f, mr, mrr, fmr, fmrr, acc, ref evaluations)
                   + AdaptivGaussLobattoStep(f, mrr, b, fmrr, fb, acc, ref evaluations);
        }
    }

    // std::max and std::min: the second only when strictly larger (smaller).
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;
}
