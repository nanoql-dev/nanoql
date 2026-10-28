// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/filonintegral.hpp ql/math/integrals/filonintegral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Filon's sine and cosine integrals: the integral of f(x)·sin(tx) or f(x)·cos(tx) over [a, b] on a given even number
/// of intervals (Abramowitz and Stegun, pp. 890-891).
/// </summary>
/// <remarks>
/// <para>
/// Filon's coefficients α, β and γ of θ = t·h cancel as θ falls: QuantLib's closed forms lose 5.6e-11 relative at θ 0.1
/// and everything near 1e-4, and are NaN at θ 0 (t 0). Below |θ| 1.7 nanoql takes their Taylor series to 14 terms
/// (α = 2θ³/45 − 2θ⁵/315 + …, β = 2/3 + 2θ²/15 − …, γ = 4/3 − 2θ²/15 + …), within 3.3e-16 of a 50-digit evaluation
/// there, and Simpson's rule at θ 0; from 1.7, where they are within 1e-15 of it, QuantLib's closed forms, bit for bit.
/// A documented divergence (the S-core plan's CR7 (x)).
/// </para>
/// <para>
/// Zero intervals are refused (CR7 (xxviii)): QuantLib accepts them and divides by 0. QuantLib's integrator counts
/// none of its evaluations and sets no error estimate, so a result's number of evaluations is 0 and its error
/// <see langword="null"/>.
/// </para>
/// </remarks>
public sealed class FilonIntegral : Integrator
{
    // QuantLib's Null<Real>(), its absolute accuracy
    private const double NullReal = 3.4028234663852886e38;

    // below it the coefficients' series, from it QuantLib's closed forms
    private const double SeriesThreshold = 1.7;

    // α/θ³, β and γ as power series in θ², their first 14 coefficients
    private static readonly double[] AlphaSeries =
    [
        0.044444444444444446, -0.006349206349206349, 0.0004232804232804233, -1.7102239324461548e-05,
        4.698417396830095e-07, -9.39683479366019e-09, 1.4330684870505738e-10, -1.7239921648728706e-12,
        1.6792131476034455e-14, -1.3520234682797467e-16, 9.152158862201363e-19, -5.2826313778940044e-21,
        2.6311957054644465e-23, -1.1425787802885314e-25,
    ];

    private static readonly double[] BetaSeries =
    [
        0.6666666666666666, 0.13333333333333333, -0.0380952380952381, 0.003527336860670194, -0.00017957351290684624,
        5.92000592000592e-06, -1.3782024364034945e-07, 2.3952716140702444e-09, -3.2324853091366326e-11,
        3.4890317622427146e-13, -3.0826135076778226e-15, 2.2713994267099746e-17, -1.4175060864015577e-19,
        7.589987611916673e-22,
    ];

    private static readonly double[] GammaSeries =
    [
        1.3333333333333333, -0.13333333333333333, 0.004761904761904762, -8.818342151675486e-05,
        1.0020843354176687e-06, -7.708341041674375e-09, 4.282411689819097e-11, -1.7993326427811332e-13,
        5.918857377569517e-16, -1.565835285071301e-18, 3.403989750155002e-21, -6.189072273009094e-24,
        9.551037458347369e-27, -1.2667158432821444e-29,
    ];

    private readonly FilonIntegralType type;
    private readonly double t;
    private readonly int n;

    /// <summary>Initializes a new instance of the <see cref="FilonIntegral"/> class.</summary>
    /// <param name="type">The kind of integral, sine or cosine.</param>
    /// <param name="t">The frequency t.</param>
    /// <param name="intervals">The number of intervals, positive and even.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="intervals"/> isn't positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="intervals"/> is odd.</exception>
    public FilonIntegral(FilonIntegralType type, double t, int intervals)
        : base(NullReal, CheckedEvaluations(intervals))
    {
        if ((intervals & 1) != 0)
        {
            throw new ArgumentException("number of intervals must be even", nameof(intervals));
        }

        this.type = type;
        this.t = t;
        n = intervals / 2;
    }

    /// <inheritdoc/>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        double h = (b - a) / (2 * n);
        var x = new Vector((2 * n) + 1, a, h);

        double theta = t * h;
        (double alpha, double beta, double gamma) = Coefficients(theta);

        var v = new double[x.Size];
        for (int i = 0; i < v.Length; ++i)
        {
            v[i] = f(x[i]);
        }

        Func<double, double> f1, f2;
        switch (type)
        {
            case FilonIntegralType.Cosine:
                f1 = Math.Sin;
                f2 = Math.Cos;
                break;
            case FilonIntegralType.Sine:
                f1 = Math.Cos;
                f2 = Math.Sin;
                break;
            default:
                throw new NanoQLException("unknown integration type");
        }

        double c2n1 = 0.0;
        double c2n = (v[0] * f2(t * a)) - (0.5 * ((v[2 * n] * f2(t * b)) + (v[0] * f2(t * a))));

        for (int i = 1; i <= n; ++i)
        {
            c2n += v[2 * i] * f2(t * x[2 * i]);
            c2n1 += v[(2 * i) - 1] * f2(t * x[(2 * i) - 1]);
        }

        double value = h * ((alpha * ((v[2 * n] * f1(t * x[2 * n])) - (v[0] * f1(t * x[0])))
                             * ((type == FilonIntegralType.Cosine) ? 1.0 : -1.0))
                            + (beta * c2n) + (gamma * c2n1));
        return new IntegrationResult(value, 0);
    }

    // QuantLib's closed forms from |θ| 1.7, the series below
    private static (double Alpha, double Beta, double Gamma) Coefficients(double theta)
    {
        if (Math.Abs(theta) < SeriesThreshold)
        {
            double theta2 = theta * theta;
            return (theta * theta2 * Horner(AlphaSeries, theta2), Horner(BetaSeries, theta2), Horner(GammaSeries, theta2));
        }

        double thetaSquared = theta * theta;
        double theta3 = thetaSquared * theta;

        double alpha = (1 / theta) + (Math.Sin(2 * theta) / (2 * thetaSquared))
            - (2 * Squared(Math.Sin(theta)) / theta3);
        double beta = 2 * (((1 + Squared(Math.Cos(theta))) / thetaSquared) - (Math.Sin(2 * theta) / theta3));
        double gamma = 4 * ((Math.Sin(theta) / theta3) - (Math.Cos(theta) / thetaSquared));
        return (alpha, beta, gamma);
    }

    private static double Horner(double[] coefficients, double x)
    {
        double sum = 0.0;
        for (int i = coefficients.Length - 1; i >= 0; --i)
        {
            sum = (sum * x) + coefficients[i];
        }

        return sum;
    }

    private static double Squared(double x) => x * x;

    private static int CheckedEvaluations(int intervals)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervals);
        return intervals + 1;
    }
}
