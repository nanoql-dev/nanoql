// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Numerics;

/// <summary>
/// A double-double: an unevaluated sum of two doubles, <see cref="Hi"/> + <see cref="Lo"/> with |Lo| at most half an
/// ulp of Hi, about 31 decimal digits, in which QuantLib's own expressions evaluate where their double arithmetic
/// cancels.
/// </summary>
/// <param name="Hi">The leading double, the value rounded to a double.</param>
/// <param name="Lo">The trailing double.</param>
/// <remarks>
/// <para>
/// Sums and products are Dekker's and Knuth's error-free transformations, the product's through
/// <see cref="Math.FusedMultiplyAdd"/>; division, the square root, the exponential and the logarithm follow Bailey,
/// Li and Hida's QD library ("double-double (approximately 31-digit precision)"), each accurate to about 2⁻¹⁰⁴
/// relative. A non-finite input gives a non-finite <see cref="Hi"/>, its trailing part not read.
/// </para>
/// </remarks>
internal readonly record struct DoubleDouble(double Hi, double Lo) : IRealArithmetic<DoubleDouble>
{
    // ln 2 as a double-double: 0x1.62e42fefa39efp-1 + 0x1.abc9e3b39803fp-56
    private static readonly DoubleDouble Ln2 = new(0.6931471805599453, 2.3190468138462996e-17);

    // 1/3!, ..., 1/9!, each the quotient's double and its remainder over the factorial
    private static readonly DoubleDouble[] InverseFactorials = [.. Enumerable.Range(3, 7).Select(InverseFactorial)];

    /// <inheritdoc/>
    public double Value => Hi;

    /// <inheritdoc/>
    public static DoubleDouble Of(double x) => new(x, 0.0);

    /// <inheritdoc/>
    public static DoubleDouble Sqrt(DoubleDouble x)
    {
        // QD's: one Newton step on the double square root
        if (x.Hi == 0.0)
        {
            return new DoubleDouble(x.Hi, 0.0);
        }

        if (!(x.Hi > 0.0) || double.IsPositiveInfinity(x.Hi))
        {
            return new DoubleDouble(Math.Sqrt(x.Hi), 0.0);
        }

        double inverse = 1.0 / Math.Sqrt(x.Hi);
        double ax = x.Hi * inverse;
        return Of(ax) + ((x - Square(ax)).Hi * (inverse * 0.5));
    }

    /// <inheritdoc/>
    public static DoubleDouble Exp(DoubleDouble x)
    {
        // QD's: x = m ln 2 + r, exp(r / 512) by its Taylor series, squared nine times, scaled by 2^m
        const double K = 512.0;
        const double InverseK = 1.0 / K;
        if (double.IsNaN(x.Hi))
        {
            return x;
        }

        if (x.Hi <= -709.0)
        {
            return Of(0.0);
        }

        if (x.Hi >= 709.0)
        {
            return Of(double.PositiveInfinity);
        }

        if (x.Hi == 0.0)
        {
            return Of(1.0);
        }

        double m = Math.Floor((x.Hi / Ln2.Hi) + 0.5);
        DoubleDouble r = TimesPowerOfTwo(x - (Ln2 * m), InverseK);
        DoubleDouble p = Square(r);
        DoubleDouble s = r + TimesPowerOfTwo(p, 0.5);
        p *= r;
        DoubleDouble t = p * InverseFactorials[0];
        int i = 0;
        do
        {
            s += t;
            p *= r;
            ++i;
            t = p * InverseFactorials[i];
        }
        while (Math.Abs(t.Hi) > InverseK * 4.93038065763132e-32 && i < 5);

        s += t;
        for (int k = 0; k < 9; ++k)
        {
            s = TimesPowerOfTwo(s, 2.0) + Square(s);
        }

        s += 1.0;
        return new DoubleDouble(Math.ScaleB(s.Hi, (int)m), Math.ScaleB(s.Lo, (int)m));
    }

    /// <inheritdoc/>
    public static DoubleDouble Log(DoubleDouble x)
    {
        // QD's: one Newton step on the double logarithm, x + a e^{-x} - 1
        if (x.Hi == 1.0 && x.Lo == 0.0)
        {
            return Of(0.0);
        }

        if (!(x.Hi > 0.0) || double.IsPositiveInfinity(x.Hi))
        {
            return Of(Math.Log(x.Hi));
        }

        DoubleDouble y = Of(Math.Log(x.Hi));
        return y + (x * Exp(-y)) - 1.0;
    }

    /// <inheritdoc/>
    public static DoubleDouble Pow(DoubleDouble x, int n)
    {
        // QD's npwr: repeated squaring
        if (n == 0)
        {
            return Of(1.0);
        }

        int count = Math.Abs(n);
        DoubleDouble r = x;
        DoubleDouble s = Of(1.0);
        if (count > 1)
        {
            while (count > 0)
            {
                if (count % 2 == 1)
                {
                    s *= r;
                }

                count /= 2;
                if (count > 0)
                {
                    r = Square(r);
                }
            }
        }
        else
        {
            s = r;
        }

        return n < 0 ? 1.0 / s : s;
    }

    /// <inheritdoc/>
    public static DoubleDouble Pow(DoubleDouble x, double y)
    {
        if (y == 1.5)
        {
            return x * Sqrt(x);
        }

        return Exp(Log(x) * y);
    }

    /// <inheritdoc/>
    public static DoubleDouble operator +(DoubleDouble a, DoubleDouble b)
    {
        // QD's IEEE-style addition: both parts summed exactly, then renormalized
        (double s1, double s2) = TwoSum(a.Hi, b.Hi);
        if (!double.IsFinite(s1))
        {
            return new DoubleDouble(s1, 0.0);
        }

        (double t1, double t2) = TwoSum(a.Lo, b.Lo);
        s2 += t1;
        (s1, s2) = QuickTwoSum(s1, s2);
        s2 += t2;
        (s1, s2) = QuickTwoSum(s1, s2);
        return new DoubleDouble(s1, s2);
    }

    /// <inheritdoc/>
    public static DoubleDouble operator +(DoubleDouble a, double b) => a + Of(b);

    /// <inheritdoc/>
    public static DoubleDouble operator +(double a, DoubleDouble b) => Of(a) + b;

    /// <inheritdoc/>
    public static DoubleDouble operator -(DoubleDouble a, DoubleDouble b) => a + (-b);

    /// <inheritdoc/>
    public static DoubleDouble operator -(DoubleDouble a, double b) => a + Of(-b);

    /// <inheritdoc/>
    public static DoubleDouble operator -(double a, DoubleDouble b) => Of(a) + (-b);

    /// <inheritdoc/>
    public static DoubleDouble operator -(DoubleDouble a) => new(-a.Hi, -a.Lo);

    /// <inheritdoc/>
    public static DoubleDouble operator *(DoubleDouble a, DoubleDouble b)
    {
        (double p1, double p2) = TwoProd(a.Hi, b.Hi);
        if (!double.IsFinite(p1))
        {
            return new DoubleDouble(p1, 0.0);
        }

        p2 += (a.Hi * b.Lo) + (a.Lo * b.Hi);
        (p1, p2) = QuickTwoSum(p1, p2);
        return new DoubleDouble(p1, p2);
    }

    /// <inheritdoc/>
    public static DoubleDouble operator *(DoubleDouble a, double b) => a * Of(b);

    /// <inheritdoc/>
    public static DoubleDouble operator *(double a, DoubleDouble b) => Of(a) * b;

    /// <inheritdoc/>
    public static DoubleDouble operator /(DoubleDouble a, DoubleDouble b)
    {
        // QD's accurate division: three quotient digits
        double q1 = a.Hi / b.Hi;
        if (!double.IsFinite(q1) || b.Hi == 0.0)
        {
            return new DoubleDouble(q1, 0.0);
        }

        DoubleDouble r = a - (q1 * b);
        double q2 = r.Hi / b.Hi;
        r -= q2 * b;
        double q3 = r.Hi / b.Hi;
        (q1, q2) = QuickTwoSum(q1, q2);
        return new DoubleDouble(q1, q2) + q3;
    }

    /// <inheritdoc/>
    public static DoubleDouble operator /(DoubleDouble a, double b) => a / Of(b);

    /// <inheritdoc/>
    public static DoubleDouble operator /(double a, DoubleDouble b) => Of(a) / b;

    // a·2^k for a power of two, exact
    private static DoubleDouble TimesPowerOfTwo(DoubleDouble a, double powerOfTwo) => new(a.Hi * powerOfTwo, a.Lo * powerOfTwo);

    private static DoubleDouble Square(DoubleDouble a) => a * a;

    private static DoubleDouble Square(double a)
    {
        (double p, double e) = TwoProd(a, a);
        return new DoubleDouble(p, e);
    }

    // Knuth's two-sum: s + e = a + b exactly
    private static (double S, double E) TwoSum(double a, double b)
    {
        double s = a + b;
        double bb = s - a;
        double e = (a - (s - bb)) + (b - bb);
        return (s, e);
    }

    // Dekker's fast two-sum, |a| at least |b|
    private static (double S, double E) QuickTwoSum(double a, double b)
    {
        double s = a + b;
        double e = b - (s - a);
        return (s, e);
    }

    // the error-free product through a fused multiply-add
    private static (double P, double E) TwoProd(double a, double b)
    {
        double p = a * b;
        double e = Math.FusedMultiplyAdd(a, b, -p);
        return (p, e);
    }

    private static DoubleDouble InverseFactorial(int n)
    {
        double factorial = 1.0;
        for (int k = 2; k <= n; ++k)
        {
            factorial *= k;
        }

        double hi = 1.0 / factorial;
        double lo = -Math.FusedMultiplyAdd(hi, factorial, -1.0) / factorial;
        return new DoubleDouble(hi, lo);
    }
}
