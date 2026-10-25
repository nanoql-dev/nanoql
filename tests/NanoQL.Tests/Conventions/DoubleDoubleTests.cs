// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using NanoQL.PricingEngines;
using Big = System.Numerics.BigInteger;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The internal double-double arithmetic the Heston expansions and COS cumulants evaluate below their cuts (the S-hes
/// plan's HR6): a value is an unevaluated sum Hi + Lo of two doubles, about 31 digits. The sum and product are
/// error-free transformations, Hi + Lo exactly the exact sum or product of two doubles; division, the square root and
/// the exponential are within 2^-100 relative of the exact or 50-digit values; a non-finite input propagates. Read
/// through reflection, the type being internal.
/// </summary>
public class DoubleDoubleTests
{
    [Fact]
    public void TheSumOfTwoDoublesIsExact()
    {
        foreach ((double a, double b) in new[] { (1e16, 1.0), (1.0, Math.ScaleB(1.0, -60)), (0.1, 0.2), (-3.0, 1e-300) })
        {
            (double hi, double lo) = Parts(Op("op_Addition", Of(a), Of(b)));
            Assert.Equal(Exact(a) + Exact(b), Exact(hi) + Exact(lo));
            Assert.Equal(hi, a + b);
        }
    }

    [Fact]
    public void TheProductOfTwoDoublesIsExact()
    {
        double x = 1.0 + Math.ScaleB(1.0, -30);
        foreach ((double a, double b) in new[] { (x, x), (1.0 / 3.0, 3.0), (0.1, 0.1), (Math.PI, -Math.E) })
        {
            (double hi, double lo) = Parts(Op("op_Multiply", Of(a), Of(b)));
            Assert.Equal(Exact(a) * Exact(b), Exact(hi) + Exact(lo));
            Assert.Equal(hi, a * b);
        }

        // (1 + 2^-30)^2 = 1 + 2^-29 + 2^-60, the double product dropping 2^-60
        Assert.Equal((1.0 + Math.ScaleB(1.0, -29), Math.ScaleB(1.0, -60)), Parts(Op("op_Multiply", Of(x), Of(x))));
    }

    [Fact]
    public void DivisionSquareRootAndExponentialHoldThirtyDigits()
    {
        // 1/3 against the exact fraction: |3 (Hi + Lo) - 1| below 2^-100
        (double hi, double lo) = Parts(Op("op_Division", Of(1.0), Of(3.0)));
        Assert.True(Rational.Abs((3 * (Exact(hi) + Exact(lo))) - Rational.One) < Rational.Pow2(-100));

        // sqrt(2): |(Hi + Lo)^2 - 2| below 2 * 2^-100 * 2
        (hi, lo) = Parts(Fn("Sqrt", Of(2.0)));
        Rational s = Exact(hi) + Exact(lo);
        Assert.True(Rational.Abs((s * s) - new Rational(2)) < Rational.Pow2(-98));

        // e and e^-10 against 50-digit values
        (hi, lo) = Parts(Fn("Exp", Of(1.0)));
        AssertWithin("2.71828182845904523536028747135266249775724709369995", hi, lo);
        (hi, lo) = Parts(Fn("Exp", Of(-10.0)));
        AssertWithin("0.000045399929762484851535591515560550610237918088866565", hi, lo);
    }

    [Fact]
    public void NonFiniteInputsPropagate()
    {
        Assert.True(double.IsNaN(Parts(Op("op_Addition", Of(double.NaN), Of(1.0))).Hi));
        Assert.True(double.IsNaN(Parts(Op("op_Multiply", Of(2.0), Of(double.NaN))).Hi));
        Assert.False(double.IsFinite(Parts(Op("op_Multiply", Of(double.PositiveInfinity), Of(2.0))).Hi));
        Assert.False(double.IsFinite(Parts(Op("op_Division", Of(1.0), Of(0.0))).Hi));
        Assert.True(double.IsNaN(Parts(Fn("Sqrt", Of(-1.0))).Hi));
    }

    private static readonly Type DD = typeof(COSHestonEngine).Assembly.GetType("NanoQL.Numerics.DoubleDouble", throwOnError: true)!;

    private static object Of(double x) => DD.GetMethod("Of", [typeof(double)])!.Invoke(null, [x])!;

    private static object Op(string name, object a, object b) => DD.GetMethod(name, [DD, DD])!.Invoke(null, [a, b])!;

    private static object Fn(string name, object a) => DD.GetMethod(name, BindingFlags.Public | BindingFlags.Static, [DD])!.Invoke(null, [a])!;

    private static (double Hi, double Lo) Parts(object x) =>
        ((double)DD.GetProperty("Hi")!.GetValue(x)!, (double)DD.GetProperty("Lo")!.GetValue(x)!);

    private static Rational Exact(double x) => Rational.Of(x);

    // |Hi + Lo - v| below 2^-100 |v| for a decimal v
    private static void AssertWithin(string decimalValue, double hi, double lo)
    {
        Rational v = Rational.Parse(decimalValue);
        Rational error = Rational.Abs(Exact(hi) + Exact(lo) - v);
        Assert.True(error < Rational.Abs(v) * Rational.Pow2(-100), $"{hi} + {lo} against {decimalValue}");
    }

    // an exact rational p / q, q > 0
    private readonly record struct Rational(Big P, Big Q)
    {
        public Rational(Big p)
            : this(p, Big.One)
        {
        }

        public static Rational One => new(Big.One);

        public static Rational Of(double x)
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
            Big p = negative ? -mantissa : mantissa;
            return exponent >= 0 ? new Rational(p << exponent) : Reduce(p, Big.One << -exponent);
        }

        public static Rational Parse(string s)
        {
            int dot = s.IndexOf('.', StringComparison.Ordinal);
            string digits = s.Remove(dot, 1);
            return Reduce(Big.Parse(digits, CultureInfo.InvariantCulture), Big.Pow(10, s.Length - dot - 1));
        }

        public static Rational Pow2(int n) => n >= 0 ? new Rational(Big.One << n) : new Rational(Big.One, Big.One << -n);

        public static Rational Abs(Rational a) => new(Big.Abs(a.P), a.Q);

        public static Rational operator +(Rational a, Rational b) => Reduce((a.P * b.Q) + (b.P * a.Q), a.Q * b.Q);

        public static Rational operator -(Rational a, Rational b) => Reduce((a.P * b.Q) - (b.P * a.Q), a.Q * b.Q);

        public static Rational operator *(Rational a, Rational b) => Reduce(a.P * b.P, a.Q * b.Q);

        public static Rational operator *(int a, Rational b) => Reduce(a * b.P, b.Q);

        public static bool operator <(Rational a, Rational b) => a.P * b.Q < b.P * a.Q;

        public static bool operator >(Rational a, Rational b) => a.P * b.Q > b.P * a.Q;

        private static Rational Reduce(Big p, Big q)
        {
            Big g = Big.GreatestCommonDivisor(p, q);
            return g.IsZero ? new Rational(Big.Zero, Big.One) : new Rational(p / g, q / g);
        }
    }
}
