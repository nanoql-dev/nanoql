// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/gaussiannoncentralchisquaredpolynomial.hpp ql/experimental/math/gaussiannoncentralchisquaredpolynomial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The orthogonal polynomial of the non-central χ² distribution with ν degrees of freedom and non-centrality λ, for
/// Gauss quadrature on that distribution: QuantLib's <c>GaussNonCentralChiSquaredPolynomial</c>.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib sums each ordinary moment's closed form in double and runs <see cref="MomentBasedGaussianPolynomial"/>'s
/// recursion in double; the map from ordinary moments to the recurrence is ill-conditioned (Gautschi), so at QuantLib's
/// own CLV cases its 14 nodes are 2.6e-2 and 0.61 relative off the exact ones, at λ 50 and 75 Boost refuses a negative
/// node and at λ 100 it doesn't return. Here the moments are exact, from the cumulants 2^(j−1)(j−1)!(ν + jλ) on ν and λ
/// as the binary fractions they are, QuantLib's recursion runs on those exact rationals, and α and β are rounded to
/// double once: the nodes are within a few ulps of the exact ones at every λ. QuantLib's recursion is a template on its
/// number type for this, tested on a multi-precision type; exact rationals are that precision's limit (the S-xrt plan's
/// XRR8, ruled 2026-10-28; <c>Divergences/NonCentralChiSquaredPolynomialTests.cs</c>). <see cref="Moment"/> is the
/// exact moment rounded once.
/// </para>
/// <para>
/// As QuantLib's, the moments stop at the 27th, so a rule takes at most 14 nodes. The recursion's values are memoised
/// as the base memoises its own, under a lock; a 14-node rule's numbers reach about 9,000 bits.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class GaussNonCentralChiSquaredPolynomial : MomentBasedGaussianPolynomial
{
    // QuantLib's NM: the moments its table holds.
    private const int MomentCount = 28;

    private readonly double nu;
    private readonly double lambda;
    private readonly object sync = new();
    private readonly List<ExactRational> moments = [];
    private readonly Dictionary<int, ExactRational> b = [];
    private readonly Dictionary<int, ExactRational> c = [];
    private readonly Dictionary<(int K, int I), ExactRational> z = [];

    /// <summary>Initializes a new instance of the <see cref="GaussNonCentralChiSquaredPolynomial"/> class.</summary>
    /// <param name="nu">The degrees of freedom ν.</param>
    /// <param name="lambda">The non-centrality λ.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="nu"/> or <paramref name="lambda"/> isn't finite, which has no exact moments; QuantLib's moments are
    /// NaN there and Boost refuses its weight.
    /// </exception>
    public GaussNonCentralChiSquaredPolynomial(double nu, double lambda)
    {
        if (!double.IsFinite(nu))
        {
            throw new ArgumentException("the degrees of freedom must be finite", nameof(nu));
        }

        if (!double.IsFinite(lambda))
        {
            throw new ArgumentException("the non-centrality must be finite", nameof(lambda));
        }

        this.nu = nu;
        this.lambda = lambda;
    }

    /// <summary>The weight function: the non-central χ² density, Boost's.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The density at <paramref name="x"/>.</returns>
    /// <exception cref="ArgumentException">The distribution's parameters or the point are outside Boost's domain.</exception>
    public override double W(double x) => NonCentralChiSquaredFunctions.Pdf(nu, lambda, x);

    /// <summary>The i-th ordinary moment, exact and rounded once.</summary>
    /// <param name="i">The moment's order.</param>
    /// <returns>The moment.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    /// <exception cref="NanoQLException">The order is past QuantLib's table, with its text.</exception>
    public override double Moment(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        lock (sync)
        {
            return ExactMoment(i).ToDouble();
        }
    }

    /// <summary>The recurrence's coefficient αᵢ, from the exact recursion, rounded once.</summary>
    /// <param name="i">The coefficient's index.</param>
    /// <returns>αᵢ.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    /// <exception cref="NanoQLException">The recursion needs a moment past QuantLib's table.</exception>
    public override double Alpha(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        lock (sync)
        {
            return ExactAlpha(i).ToDouble();
        }
    }

    /// <summary>The recurrence's coefficient βᵢ, β₀ being 1, from the exact recursion, rounded once.</summary>
    /// <param name="i">The coefficient's index.</param>
    /// <returns>βᵢ.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    /// <exception cref="NanoQLException">The recursion needs a moment past QuantLib's table.</exception>
    public override double Beta(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        lock (sync)
        {
            return ExactBeta(i).ToDouble();
        }
    }

    // The moments from the cumulants κⱼ = 2^(j−1)(j−1)!(ν + jλ): mₙ = Σⱼ C(n−1, j−1) κⱼ mₙ₋ⱼ.
    private ExactRational ExactMoment(int n)
    {
        if (!(n < MomentCount))
        {
            throw new NanoQLException($"only moments up to {MomentCount} are supported.");
        }

        if (moments.Count == 0)
        {
            moments.Add(ExactRational.One);
        }

        ExactRational exactNu = ExactRational.FromDouble(nu);
        ExactRational exactLambda = ExactRational.FromDouble(lambda);
        while (moments.Count <= n)
        {
            int m = moments.Count;
            ExactRational sum = ExactRational.Zero;
            System.Numerics.BigInteger binomial = System.Numerics.BigInteger.One; // C(m−1, j−1)
            System.Numerics.BigInteger factor = System.Numerics.BigInteger.One; // 2^(j−1)(j−1)!
            for (int j = 1; j <= m; ++j)
            {
                if (j > 1)
                {
                    binomial = binomial * (m - j + 1) / (j - 1);
                    factor *= 2 * (j - 1);
                }

                ExactRational kappa = new ExactRational(factor) * (exactNu + (new ExactRational(j) * exactLambda));
                sum += new ExactRational(binomial) * kappa * moments[m - j];
            }

            moments.Add(sum);
        }

        return moments[n];
    }

    // QuantLib's MomentBasedGaussianPolynomial recursion, on exact rationals.
    private ExactRational Z(int k, int i)
    {
        if (k == -1)
        {
            return ExactRational.Zero;
        }

        if (!z.TryGetValue((k, i), out ExactRational value))
        {
            value = k == 0
                ? ExactMoment(i)
                : Z(k - 1, i + 1) - (ExactAlpha(k - 1) * Z(k - 1, i)) - (ExactBeta(k - 1) * Z(k - 2, i));
            z[(k, i)] = value;
        }

        return value;
    }

    private ExactRational ExactAlpha(int u)
    {
        if (!b.TryGetValue(u, out ExactRational value))
        {
            value = u == 0
                ? ExactMoment(1)
                : -(Z(u - 1, u) / Z(u - 1, u - 1)) + (Z(u, u + 1) / Z(u, u));
            b[u] = value;
        }

        return value;
    }

    private ExactRational ExactBeta(int u)
    {
        if (u == 0)
        {
            return ExactRational.One;
        }

        if (!c.TryGetValue(u, out ExactRational value))
        {
            value = Z(u, u) / Z(u - 1, u - 1);
            c[u] = value;
        }

        return value;
    }

    // An exact rational, its denominator positive and coprime to its numerator.
    private readonly struct ExactRational
    {
        private readonly System.Numerics.BigInteger num;
        private readonly System.Numerics.BigInteger den;

        public ExactRational(System.Numerics.BigInteger n)
        {
            num = n;
            den = System.Numerics.BigInteger.One;
        }

        private ExactRational(System.Numerics.BigInteger n, System.Numerics.BigInteger d)
        {
            if (d.IsZero)
            {
                throw new NanoQLException("the moment recursion divides by zero");
            }

            if (d.Sign < 0)
            {
                n = -n;
                d = -d;
            }

            System.Numerics.BigInteger g = System.Numerics.BigInteger.GreatestCommonDivisor(n, d);
            if (!g.IsOne && !g.IsZero)
            {
                n /= g;
                d /= g;
            }

            num = n;
            den = d;
        }

        public static ExactRational Zero => new(System.Numerics.BigInteger.Zero);

        public static ExactRational One => new(System.Numerics.BigInteger.One);

        public static ExactRational operator +(ExactRational x, ExactRational y) =>
            new((x.num * y.den) + (y.num * x.den), x.den * y.den);

        public static ExactRational operator -(ExactRational x, ExactRational y) =>
            new((x.num * y.den) - (y.num * x.den), x.den * y.den);

        public static ExactRational operator -(ExactRational x) => new(-x.num, x.den);

        public static ExactRational operator *(ExactRational x, ExactRational y) => new(x.num * y.num, x.den * y.den);

        public static ExactRational operator /(ExactRational x, ExactRational y) => new(x.num * y.den, x.den * y.num);

        // A finite double as the binary fraction it is.
        public static ExactRational FromDouble(double x)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;
            if (exponent == 0)
            {
                exponent = 1;
            }
            else
            {
                mantissa |= 1L << 52;
            }

            exponent -= 1075;
            System.Numerics.BigInteger n = negative ? -mantissa : mantissa;
            return exponent >= 0
                ? new ExactRational(n << exponent, System.Numerics.BigInteger.One)
                : new ExactRational(n, System.Numerics.BigInteger.One << -exponent);
        }

        // The nearest double, ties to even.
        public double ToDouble()
        {
            if (num.IsZero)
            {
                return 0.0;
            }

            bool negative = num.Sign < 0;
            System.Numerics.BigInteger n = System.Numerics.BigInteger.Abs(num);
            System.Numerics.BigInteger d = den;

            // e, with 2^e ≤ n/d < 2^(e+1)
            long e = (long)n.GetBitLength() - (long)d.GetBitLength();
            bool atLeast = e >= 0 ? n >= (d << (int)e) : (n << (int)-e) >= d;
            if (!atLeast)
            {
                e -= 1;
            }

            if (e > 1023)
            {
                return negative ? double.NegativeInfinity : double.PositiveInfinity;
            }

            // the fraction's bits: 52 for a normal double, fewer for a subnormal one
            long f = Math.Min(52, e + 1074);
            if (f < -1)
            {
                return negative ? -0.0 : 0.0;
            }

            // q: the value at one bit more than the fraction's, truncated
            long s = f - e + 1;
            System.Numerics.BigInteger q = s >= 0
                ? System.Numerics.BigInteger.DivRem(n << (int)s, d, out System.Numerics.BigInteger rem)
                : System.Numerics.BigInteger.DivRem(n, d << (int)-s, out rem);
            System.Numerics.BigInteger m = q >> 1;
            if (!q.IsEven && (!rem.IsZero || !m.IsEven))
            {
                m += 1;
            }

            double result = Math.ScaleB((double)m, (int)(e - f));
            return negative ? -result : result;
        }
    }
}
