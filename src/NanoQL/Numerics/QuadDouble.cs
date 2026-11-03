// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Numerics;

/// <summary>
/// A quad-double: an unevaluated sum X0 + X1 + X2 + X3 of four non-overlapping doubles, about 62 digits, for QuantLib's
/// Heston expressions below double-double's own floor. Its arithmetic is QD's, Hida, Li and Bailey's quad-double
/// library, the one <see cref="DoubleDouble"/> follows: the accurate addition, multiplication and long division,
/// Newton's square root and logarithm, and the exponential by argument reduction, a Taylor series and repeated
/// squaring. A non-finite input gives a non-finite leading part.
/// </summary>
internal readonly record struct QuadDouble(double X0, double X1, double X2, double X3) : IRealArithmetic<QuadDouble>
{
    // ln 2 to quad-double precision, 2 atanh(1/3) = 2 Σ 1/((2k + 1) 3^{2k+1}), summed in quad-double
    private static readonly QuadDouble Ln2 = ComputeLn2();

    // 1/3!, ..., 1/17!, each by exact divisions in quad-double
    private static readonly QuadDouble[] InverseFactorials = ComputeInverseFactorials();

    // 2^-209, QD's quad-double epsilon
    private const double Epsilon = 1.21543267145725e-63;

    /// <inheritdoc/>
    public double Value => X0;

    /// <inheritdoc/>
    public static QuadDouble Of(double x) => new(x, 0.0, 0.0, 0.0);

    /// <inheritdoc/>
    public static QuadDouble Sqrt(QuadDouble x)
    {
        // QD's: three Newton steps toward 1/sqrt(x) from the double's, then times x
        if (x.X0 == 0.0)
        {
            return Of(x.X0);
        }

        if (!(x.X0 > 0.0) || double.IsPositiveInfinity(x.X0))
        {
            return Of(Math.Sqrt(x.X0));
        }

        QuadDouble r = Of(1.0 / Math.Sqrt(x.X0));
        QuadDouble h = TimesPowerOfTwo(x, 0.5);
        for (int i = 0; i < 3; ++i)
        {
            r += (0.5 - (h * (r * r))) * r;
        }

        return r * x;
    }

    /// <inheritdoc/>
    public static QuadDouble Exp(QuadDouble x)
    {
        // QD's: x = m ln 2 + r, exp(r / 2^16) by its Taylor series, squared sixteen times, scaled by 2^m
        const double K = 65536.0;
        const double InverseK = 1.0 / K;
        if (double.IsNaN(x.X0))
        {
            return x;
        }

        if (x.X0 <= -709.0)
        {
            return Of(0.0);
        }

        if (x.X0 >= 709.0)
        {
            return Of(double.PositiveInfinity);
        }

        if (x.X0 == 0.0 && x.X1 == 0.0)
        {
            return Of(1.0);
        }

        double m = Math.Floor((x.X0 / Ln2.X0) + 0.5);
        QuadDouble r = TimesPowerOfTwo(x - (Ln2 * m), InverseK);
        QuadDouble p = r * r;
        QuadDouble s = r + TimesPowerOfTwo(p, 0.5);
        double threshold = InverseK * Epsilon;
        int i = 0;
        QuadDouble t;
        do
        {
            p *= r;
            t = p * InverseFactorials[i++];
            s += t;
        }
        while (Math.Abs(t.X0) > threshold && i < InverseFactorials.Length);

        for (int k = 0; k < 16; ++k)
        {
            s = TimesPowerOfTwo(s, 2.0) + (s * s);
        }

        s += 1.0;
        int e = (int)m;
        return new QuadDouble(Math.ScaleB(s.X0, e), Math.ScaleB(s.X1, e), Math.ScaleB(s.X2, e), Math.ScaleB(s.X3, e));
    }

    /// <inheritdoc/>
    public static QuadDouble Log(QuadDouble x)
    {
        // QD's: three Newton steps on the double logarithm, y + x e^{-y} - 1
        if (x.X0 == 1.0 && x.X1 == 0.0 && x.X2 == 0.0 && x.X3 == 0.0)
        {
            return Of(0.0);
        }

        if (!(x.X0 > 0.0) || double.IsPositiveInfinity(x.X0))
        {
            return Of(Math.Log(x.X0));
        }

        QuadDouble y = Of(Math.Log(x.X0));
        for (int i = 0; i < 3; ++i)
        {
            y = y + (x * Exp(-y)) - 1.0;
        }

        return y;
    }

    /// <inheritdoc/>
    public static QuadDouble Pow(QuadDouble x, int n)
    {
        // QD's npwr: repeated squaring
        if (n == 0)
        {
            return Of(1.0);
        }

        int count = Math.Abs(n);
        QuadDouble r = x;
        QuadDouble s = Of(1.0);
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
                    r *= r;
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
    public static QuadDouble Pow(QuadDouble x, double y)
    {
        if (y == 1.5)
        {
            return x * Sqrt(x);
        }

        return Exp(Log(x) * y);
    }

    /// <inheritdoc/>
    public static QuadDouble operator +(QuadDouble a, QuadDouble b)
    {
        // QD's IEEE-style addition: the two sums' parts merged by magnitude through a double-length accumulator
        if (!double.IsFinite(a.X0) || !double.IsFinite(b.X0))
        {
            return Of(a.X0 + b.X0);
        }

        Span<double> av = [a.X0, a.X1, a.X2, a.X3];
        Span<double> bv = [b.X0, b.X1, b.X2, b.X3];
        Span<double> x = [0.0, 0.0, 0.0, 0.0];
        int i = 0, j = 0, k = 0;
        double u, v;
        if (Math.Abs(av[i]) > Math.Abs(bv[j]))
        {
            u = av[i++];
        }
        else
        {
            u = bv[j++];
        }

        if (Math.Abs(av[i]) > Math.Abs(bv[j]))
        {
            v = av[i++];
        }
        else
        {
            v = bv[j++];
        }

        (u, v) = QuickTwoSum(u, v);
        while (k < 4)
        {
            if (i >= 4 && j >= 4)
            {
                x[k] = u;
                if (k < 3)
                {
                    x[++k] = v;
                }

                break;
            }

            double t;
            if (i >= 4)
            {
                t = bv[j++];
            }
            else if (j >= 4)
            {
                t = av[i++];
            }
            else if (Math.Abs(av[i]) > Math.Abs(bv[j]))
            {
                t = av[i++];
            }
            else
            {
                t = bv[j++];
            }

            double s = QuickThreeAccumulate(ref u, ref v, t);
            if (s != 0.0)
            {
                x[k++] = s;
            }
        }

        for (k = i; k < 4; ++k)
        {
            x[3] += av[k];
        }

        for (k = j; k < 4; ++k)
        {
            x[3] += bv[k];
        }

        return Renormalize(x[0], x[1], x[2], x[3]);
    }

    /// <inheritdoc/>
    public static QuadDouble operator +(QuadDouble a, double b) => a + Of(b);

    /// <inheritdoc/>
    public static QuadDouble operator +(double a, QuadDouble b) => Of(a) + b;

    /// <inheritdoc/>
    public static QuadDouble operator -(QuadDouble a, QuadDouble b) => a + (-b);

    /// <inheritdoc/>
    public static QuadDouble operator -(QuadDouble a, double b) => a + Of(-b);

    /// <inheritdoc/>
    public static QuadDouble operator -(double a, QuadDouble b) => Of(a) + (-b);

    /// <inheritdoc/>
    public static QuadDouble operator -(QuadDouble a) => new(-a.X0, -a.X1, -a.X2, -a.X3);

    /// <inheritdoc/>
    public static QuadDouble operator *(QuadDouble a, QuadDouble b)
    {
        // QD's accurate multiplication: the products through order eps^3 error-free, eps^4 in double, renormalized
        double p0 = a.X0 * b.X0;
        if (!double.IsFinite(p0))
        {
            return Of(p0);
        }

        (p0, double q0) = TwoProd(a.X0, b.X0);
        (double p1, double q1) = TwoProd(a.X0, b.X1);
        (double p2, double q2) = TwoProd(a.X1, b.X0);
        (double p3, double q3) = TwoProd(a.X0, b.X2);
        (double p4, double q4) = TwoProd(a.X1, b.X1);
        (double p5, double q5) = TwoProd(a.X2, b.X0);

        // start accumulation
        ThreeSum(ref p1, ref p2, ref q0);

        // six-three sum of p2, q1, q2, p3, p4, p5
        ThreeSum(ref p2, ref q1, ref q2);
        ThreeSum(ref p3, ref p4, ref p5);

        // (s0, s1, s2) = (p2, q1, q2) + (p3, p4, p5)
        (double s0, double t0) = TwoSum(p2, p3);
        (double s1, double t1) = TwoSum(q1, p4);
        double s2 = q2 + p5;
        (s1, t0) = TwoSum(s1, t0);
        s2 += t0 + t1;

        // order eps^3 terms
        (double p6, double q6) = TwoProd(a.X0, b.X3);
        (double p7, double q7) = TwoProd(a.X1, b.X2);
        (double p8, double q8) = TwoProd(a.X2, b.X1);
        (double p9, double q9) = TwoProd(a.X3, b.X0);

        // nine-two sum of q0, s1, q3, q4, q5, p6, p7, p8, p9
        (q0, q3) = TwoSum(q0, q3);
        (q4, q5) = TwoSum(q4, q5);
        (p6, p7) = TwoSum(p6, p7);
        (p8, p9) = TwoSum(p8, p9);
        (t0, t1) = TwoSum(q0, q4);
        t1 += q3 + q5;
        (double r0, double r1) = TwoSum(p6, p8);
        r1 += p7 + p9;
        (q3, q4) = TwoSum(t0, r0);
        q4 += t1 + r1;
        (t0, t1) = TwoSum(q3, s1);
        t1 += q4;

        // order eps^4 terms
        t1 += (a.X1 * b.X3) + (a.X2 * b.X2) + (a.X3 * b.X1) + q6 + q7 + q8 + q9 + s2;

        return Renormalize(p0, p1, s0, t0, t1);
    }

    /// <inheritdoc/>
    public static QuadDouble operator *(QuadDouble a, double b) => a * Of(b);

    /// <inheritdoc/>
    public static QuadDouble operator *(double a, QuadDouble b) => Of(a) * b;

    /// <inheritdoc/>
    public static QuadDouble operator /(QuadDouble a, QuadDouble b)
    {
        // QD's accurate division: five quotient digits by long division
        double q0 = a.X0 / b.X0;
        if (!double.IsFinite(q0) || b.X0 == 0.0)
        {
            return Of(q0);
        }

        QuadDouble r = a - (b * q0);
        double q1 = r.X0 / b.X0;
        r -= b * q1;
        double q2 = r.X0 / b.X0;
        r -= b * q2;
        double q3 = r.X0 / b.X0;
        r -= b * q3;
        double q4 = r.X0 / b.X0;
        return Renormalize(q0, q1, q2, q3, q4);
    }

    /// <inheritdoc/>
    public static QuadDouble operator /(QuadDouble a, double b) => a / Of(b);

    /// <inheritdoc/>
    public static QuadDouble operator /(double a, QuadDouble b) => Of(a) / b;

    // a·2^k for a power of two, exact
    private static QuadDouble TimesPowerOfTwo(QuadDouble a, double powerOfTwo) =>
        new(a.X0 * powerOfTwo, a.X1 * powerOfTwo, a.X2 * powerOfTwo, a.X3 * powerOfTwo);

    // QD's quick_three_accum: adds c to the double-length accumulator (a, b), returning the part that leaves it
    private static double QuickThreeAccumulate(ref double a, ref double b, double c)
    {
        double s;
        (s, b) = TwoSum(b, c);
        (s, a) = TwoSum(a, s);
        bool za = a != 0.0;
        bool zb = b != 0.0;
        if (za && zb)
        {
            return s;
        }

        if (!zb)
        {
            b = a;
            a = s;
        }
        else
        {
            a = s;
        }

        return 0.0;
    }

    // QD's three_sum: (a, b, c) to a + b + c with a the leading part
    private static void ThreeSum(ref double a, ref double b, ref double c)
    {
        (double t1, double t2) = TwoSum(a, b);
        (a, double t3) = TwoSum(c, t1);
        (b, c) = TwoSum(t2, t3);
    }

    // QD's renorm of four parts
    private static QuadDouble Renormalize(double c0, double c1, double c2, double c3)
    {
        if (double.IsInfinity(c0))
        {
            return new QuadDouble(c0, c1, c2, c3);
        }

        double s0, s1, s2 = 0.0, s3 = 0.0;
        (s0, c3) = QuickTwoSum(c2, c3);
        (s0, c2) = QuickTwoSum(c1, s0);
        (c0, c1) = QuickTwoSum(c0, s0);
        s0 = c0;
        s1 = c1;
        if (s1 != 0.0)
        {
            (s1, s2) = QuickTwoSum(s1, c2);
            if (s2 != 0.0)
            {
                (s2, s3) = QuickTwoSum(s2, c3);
            }
            else
            {
                (s1, s2) = QuickTwoSum(s1, c3);
            }
        }
        else
        {
            (s0, s1) = QuickTwoSum(s0, c2);
            if (s1 != 0.0)
            {
                (s1, s2) = QuickTwoSum(s1, c3);
            }
            else
            {
                (s0, s1) = QuickTwoSum(s0, c3);
            }
        }

        return new QuadDouble(s0, s1, s2, s3);
    }

    // QD's renorm of five parts into four
    private static QuadDouble Renormalize(double c0, double c1, double c2, double c3, double c4)
    {
        if (double.IsInfinity(c0))
        {
            return new QuadDouble(c0, c1, c2, c3);
        }

        double s0, s1, s2 = 0.0, s3 = 0.0;
        (s0, c4) = QuickTwoSum(c3, c4);
        (s0, c3) = QuickTwoSum(c2, s0);
        (s0, c2) = QuickTwoSum(c1, s0);
        (c0, c1) = QuickTwoSum(c0, s0);
        s0 = c0;
        s1 = c1;
        if (s1 != 0.0)
        {
            (s1, s2) = QuickTwoSum(s1, c2);
            if (s2 != 0.0)
            {
                (s2, s3) = QuickTwoSum(s2, c3);
                if (s3 != 0.0)
                {
                    s3 += c4;
                }
                else
                {
                    s2 += c4;
                }
            }
            else
            {
                (s1, s2) = QuickTwoSum(s1, c3);
                if (s2 != 0.0)
                {
                    (s2, s3) = QuickTwoSum(s2, c4);
                }
                else
                {
                    (s1, s2) = QuickTwoSum(s1, c4);
                }
            }
        }
        else
        {
            (s0, s1) = QuickTwoSum(s0, c2);
            if (s1 != 0.0)
            {
                (s1, s2) = QuickTwoSum(s1, c3);
                if (s2 != 0.0)
                {
                    (s2, s3) = QuickTwoSum(s2, c4);
                }
                else
                {
                    (s1, s2) = QuickTwoSum(s1, c4);
                }
            }
            else
            {
                (s0, s1) = QuickTwoSum(s0, c3);
                if (s1 != 0.0)
                {
                    (s1, s2) = QuickTwoSum(s1, c4);
                }
                else
                {
                    (s0, s1) = QuickTwoSum(s0, c4);
                }
            }
        }

        return new QuadDouble(s0, s1, s2, s3);
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

    private static QuadDouble ComputeLn2()
    {
        // 2 atanh(1/3): each term's power of 1/9 by an exact division, about a digit a term
        QuadDouble power = Of(1.0) / 3.0;
        QuadDouble sum = power;
        for (int k = 1; k < 80; ++k)
        {
            power /= 9.0;
            sum += power / ((2 * k) + 1);
        }

        return TimesPowerOfTwo(sum, 2.0);
    }

    private static QuadDouble[] ComputeInverseFactorials()
    {
        var result = new QuadDouble[15];
        QuadDouble inverse = Of(1.0) / 2.0;
        for (int n = 3; n < 3 + result.Length; ++n)
        {
            inverse /= n;
            result[n - 3] = inverse;
        }

        return result;
    }
}
