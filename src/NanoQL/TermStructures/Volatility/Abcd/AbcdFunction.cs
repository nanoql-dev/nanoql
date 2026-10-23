// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/abcd.hpp ql/termstructures/volatility/abcd.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// The abcd functional form for instantaneous volatility, f(T − t) = (a + b·(T − t))·e<sup>−c·(T − t)</sup> + d, in
/// Rebonato's notation.
/// </summary>
/// <remarks>
/// <para>
/// One of QuantLib's defects is fixed (the S-g1d plan's G1R6 (x)): QuantLib's <c>primitive</c> divides a sum by 4c³
/// whose terms cancel to O(c³) when c·T and c·S are small, so the covariance between 0 and 1 of the 1-year rate is out
/// by 2.4e-6 at c = 1e-4 and by 14% at c = 1e-6. Where QuantLib's form rounds by more than 1e-12 of the integral at the
/// last time it reaches, min(T, S), the primitive is the integral from 0 written without the cancellation, each term a
/// polynomial times a decaying exponential; elsewhere it is QuantLib's, bit for bit.
/// </para>
/// <para>QuantLib's polynomial branch for c = 0 is left out: the base's validation requires c &gt; 0.</para>
/// </remarks>
public sealed class AbcdFunction : AbcdMathFunction
{
    private const double CancellationTolerance = 1e-12;

    /// <summary>Initializes a new instance of the <see cref="AbcdFunction"/> class.</summary>
    /// <param name="a">a.</param>
    /// <param name="b">b.</param>
    /// <param name="c">c, positive.</param>
    /// <param name="d">d, non-negative.</param>
    /// <exception cref="ArgumentException">The coefficients fail <see cref="AbcdMathFunction.Validate"/>.</exception>
    public AbcdFunction(double a = -0.06, double b = 0.17, double c = 0.54, double d = 0.17)
        : base(a, b, c, d)
    {
    }

    /// <summary>The volatility function's maximum.</summary>
    /// <returns>The maximum.</returns>
    public double MaximumVolatility() => MaximumValue();

    /// <summary>The volatility function's value at time 0, f(0).</summary>
    /// <returns>f(0).</returns>
    public double ShortTermVolatility() => Evaluate(0.0);

    /// <summary>The volatility function's value at +∞, d.</summary>
    /// <returns>d.</returns>
    public double LongTermVolatility() => LongTermValue();

    /// <summary>The instantaneous covariance at time t between the T-fixing and S-fixing rates,
    /// f(T − t)·f(S − t).</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The first rate's fixing time.</param>
    /// <param name="S">The second rate's fixing time.</param>
    /// <returns>The covariance.</returns>
    public double Covariance(double t, double T, double S) => Evaluate(T - t) * Evaluate(S - t);

    /// <summary>The integral of the instantaneous covariance between t1 and t2 for the T-fixing and S-fixing rates,
    /// the part before the earlier fixing.</summary>
    /// <param name="t1">The lower bound.</param>
    /// <param name="t2">The upper bound.</param>
    /// <param name="T">The first rate's fixing time.</param>
    /// <param name="S">The second rate's fixing time.</param>
    /// <returns>The integrated covariance.</returns>
    /// <exception cref="ArgumentException"><paramref name="t1"/> is after <paramref name="t2"/>.</exception>
    public double Covariance(double t1, double t2, double T, double S)
    {
        if (!(t1 <= t2))
        {
            throw new ArgumentException(
                $"integrations bounds ({Utilities.DataFormattersFunctions.General(t1)},{Utilities.DataFormattersFunctions.General(t2)}) are in reverse order",
                nameof(t2));
        }

        double cutOff = Math.Min(S, T);
        if (t1 >= cutOff)
        {
            return 0.0;
        }
        else
        {
            cutOff = Math.Min(t2, cutOff);
            return Primitive(cutOff, T, S) - Primitive(t1, T, S);
        }
    }

    /// <summary>The average volatility in [tMin, tMax] of the T-fixing rate, √(∫f²(T − u)du / (tMax − tMin)); the
    /// instantaneous volatility when the two are equal.</summary>
    /// <param name="tMin">The interval's start.</param>
    /// <param name="tMax">The interval's end.</param>
    /// <param name="T">The rate's fixing time.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException"><paramref name="tMax"/> is before <paramref name="tMin"/>.</exception>
    public double Volatility(double tMin, double tMax, double T)
    {
        if (tMax == tMin)
        {
            return InstantaneousVolatility(tMax, T);
        }

        if (!(tMax > tMin))
        {
            throw new ArgumentException("tMax must be > tMin", nameof(tMax));
        }

        return Math.Sqrt(Variance(tMin, tMax, T) / (tMax - tMin));
    }

    /// <summary>The variance between tMin and tMax of the T-fixing rate, ∫f²(T − u)du over the interval.</summary>
    /// <param name="tMin">The interval's start.</param>
    /// <param name="tMax">The interval's end.</param>
    /// <param name="T">The rate's fixing time.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="ArgumentException"><paramref name="tMin"/> is after <paramref name="tMax"/>.</exception>
    public double Variance(double tMin, double tMax, double T) => Covariance(tMin, tMax, T, T);

    /// <summary>The instantaneous volatility at time u of the T-fixing rate, f(T − u).</summary>
    /// <param name="u">The time.</param>
    /// <param name="T">The rate's fixing time.</param>
    /// <returns>The volatility.</returns>
    public double InstantaneousVolatility(double u, double T) => Math.Sqrt(InstantaneousVariance(u, T));

    /// <summary>The instantaneous variance at time u of the T-fixing rate, f(T − u)².</summary>
    /// <param name="u">The time.</param>
    /// <param name="T">The rate's fixing time.</param>
    /// <returns>The variance.</returns>
    public double InstantaneousVariance(double u, double T) => InstantaneousCovariance(u, T, T);

    /// <summary>The instantaneous covariance at time u between the T and S fixing rates, f(T − u)·f(S − u).</summary>
    /// <param name="u">The time.</param>
    /// <param name="T">The first rate's fixing time.</param>
    /// <param name="S">The second rate's fixing time.</param>
    /// <returns>The covariance.</returns>
    public double InstantaneousCovariance(double u, double T, double S) => Evaluate(T - u) * Evaluate(S - u);

    /// <summary>The indefinite integral at time t of the instantaneous covariance between the T-fixing and S-fixing
    /// rates, the integral from 0; 0 past either fixing.</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The first rate's fixing time.</param>
    /// <param name="S">The second rate's fixing time.</param>
    /// <returns>The primitive.</returns>
    public double Primitive(double t, double T, double S)
    {
        if (T < t || S < t)
        {
            return 0.0;
        }

        // QuantLib's form, unless it cancels at the last time a covariance reads (G1R6 (x))
        return Cancels(Math.Min(S, T), T, S) ? PrimitiveWithoutCancellation(t, T, S) : QuantLibPrimitive(t, T, S, out _);
    }

    // QuantLib's primitive; the sum of its numerator's terms' sizes beside it.
    private double QuantLibPrimitive(double t, double T, double S, out double size)
    {
        double a = A, b = B, c = C, d = D;
        double k1 = Math.Exp(c * t), k2 = Math.Exp(c * S), k3 = Math.Exp(c * T);

        size = (b * b * (1 + (2 * c * c * Math.Abs(S * T)) + (c * Math.Abs(S + T))
                    + (k1 * k1 * (1 + (c * Math.Abs(S + T - (2 * t))) + (2 * c * c * Math.Abs((S - t) * (T - t)))))))
            + (2 * c * c * ((2 * Math.Abs(d * a) * (k2 + k3) * (k1 + 1)) + (a * a * ((k1 * k1) + 1)) + (2 * c * d * d * k2 * k3 * Math.Abs(t))))
            + (2 * Math.Abs(b) * c * ((Math.Abs(a) * (1 + (c * Math.Abs(S + T)) + (k1 * k1 * (1 + (c * Math.Abs(S + T - (2 * t)))))))
                + (2 * d * ((k3 * (1 + (c * Math.Abs(S)))) + (k2 * (1 + (c * Math.Abs(T)))) + (k1 * k3 * (1 + (c * Math.Abs(S - t))))
                    + (k1 * k2 * (1 + (c * Math.Abs(T - t))))))));

        return ((b * b * (-1 - (2 * c * c * S * T) - (c * (S + T))
                    + (k1 * k1 * (1 + (c * (S + T - (2 * t))) + (2 * c * c * (S - t) * (T - t))))))
                + (2 * c * c * ((2 * d * a * (k2 + k3) * (k1 - 1))
                    + (a * a * ((k1 * k1) - 1)) + (2 * c * d * d * k2 * k3 * t)))
                + (2 * b * c * ((a * (-1 - (c * (S + T)) + (k1 * k1 * (1 + (c * (S + T - (2 * t)))))))
                    - (2 * d * ((k3 * (1 + (c * S))) + (k2 * (1 + (c * T)))
                        - (k1 * k3 * (1 + (c * (S - t))))
                        - (k1 * k2 * (1 + (c * (T - t)))))))))
            / (4 * c * c * c * k2 * k3);
    }

    // Whether QuantLib's form rounds the primitive at t by more than the tolerance, relative.
    private bool Cancels(double t, double T, double S)
    {
        double value = QuantLibPrimitive(t, T, S, out double size);
        double c = C;
        double scale = 4 * c * c * c * Math.Exp(c * S) * Math.Exp(c * T);
        if (!(double.IsFinite(value) && double.IsFinite(size) && value != 0.0))
        {
            return true;
        }

        return NumericLimits.QL_EPSILON * size / scale > CancellationTolerance * Math.Abs(value);
    }

    // The integral from 0 to t of f(T − u)·f(S − u), with v = t − u: f(T − u) = (αT + b·v)·e^(−c·x)·e^(−c·v) + d for
    // x = T − t and αT = a + b·x, so each term is a polynomial in v times e^(−κv), integrated as t^(n+1)·hₙ(−κt).
    private double PrimitiveWithoutCancellation(double t, double T, double S)
    {
        double a = A, b = B, c = C, d = D;
        double x = T - t, y = S - t;
        double alphaT = a + (b * x), alphaS = a + (b * y);
        double j0c = t * H(0, -c * t), j1c = t * t * H(1, -c * t);
        double j02c = t * H(0, -2 * c * t), j12c = t * t * H(1, -2 * c * t), j22c = t * t * t * H(2, -2 * c * t);
        return (Math.Exp(-c * (x + y)) * ((alphaT * alphaS * j02c) + (b * (alphaT + alphaS) * j12c) + (b * b * j22c)))
            + (d * Math.Exp(-c * x) * ((alphaT * j0c) + (b * j1c)))
            + (d * Math.Exp(-c * y) * ((alphaS * j0c) + (b * j1c)))
            + (d * d * t);
    }

    // hₙ(z), the integral over [0, 1] of sⁿ·e^(z·s): its series Σ zᵏ/(k!·(k + n + 1)) where |z| ≤ 1, else the closed
    // form, which cancels by a few bits at most there.
    private static double H(int n, double z)
    {
        if (Math.Abs(z) <= 1.0)
        {
            double sum = 0.0, term = 1.0;
            for (int k = 0; k < 40; k++)
            {
                double add = term / (k + n + 1);
                sum += add;
                if (Math.Abs(add) < 1e-18 * Math.Abs(sum))
                {
                    break;
                }

                term *= z / (k + 1);
            }

            return sum;
        }

        double e = Math.Exp(z);
        return n switch
        {
            0 => (e - 1.0) / z,
            1 => ((e * (z - 1.0)) + 1.0) / (z * z),
            _ => ((e * ((z * z) - (2.0 * z) + 2.0)) - 2.0) / (z * z * z),
        };
    }
}
