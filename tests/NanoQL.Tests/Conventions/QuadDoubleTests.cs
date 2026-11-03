// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using NanoQL.PricingEngines;
using Big = System.Numerics.BigInteger;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The internal quad-double arithmetic the Heston expansions and COS cumulants evaluate below double-double's floor: a
/// value is an unevaluated sum X0 + X1 + X2 + X3 of four doubles, about 62 digits. A sum or product whose exact value
/// four doubles hold is exact; division, the square root, the exponential and the logarithm are within 2^-190 relative
/// of the exact or 80-digit values; a non-finite input propagates. Read through reflection, the type being internal.
/// </summary>
public class QuadDoubleTests
{
    [Fact]
    public void SumsThatFourDoublesHoldAreExact()
    {
        // 1 + 2^-60 + 2^-120 + 2^-180, each step's exact sum in four parts
        object sum = Of(1.0);
        Rational exact = Rational.One;
        foreach (int k in new[] { -60, -120, -180 })
        {
            sum = Op("op_Addition", sum, Of(Math.ScaleB(1.0, k)));
            exact += Rational.Pow2(k);
            Assert.Equal(exact, Exact(sum));
        }

        // the doubles of a cancelling sum, 1e16 + 1 - 1e16 + 0.1 - 0.1
        object cancelling = Of(1e16);
        foreach (double x in new[] { 1.0, -1e16, 0.1, -0.1 })
        {
            cancelling = Op("op_Addition", cancelling, Of(x));
        }

        Assert.Equal(Rational.One, Exact(cancelling));
    }

    [Fact]
    public void ProductsThatFourDoublesHoldAreExact()
    {
        // (1 + 2^-30)^2 and its square, (1 + 2^-30)^4 = 1 + 2^-28 + 3·2^-59 + 2^-88 + 2^-120
        double x = 1.0 + Math.ScaleB(1.0, -30);
        object square = Op("op_Multiply", Of(x), Of(x));
        Rational exactX = Rational.Of(x);
        Assert.Equal(exactX * exactX, Exact(square));
        object fourth = Op("op_Multiply", square, square);
        Assert.Equal(exactX * exactX * exactX * exactX, Exact(fourth));

        foreach ((double a, double b) in new[] { (1.0 / 3.0, 3.0), (0.1, 0.1), (Math.PI, -Math.E) })
        {
            Assert.Equal(Rational.Of(a) * Rational.Of(b), Exact(Op("op_Multiply", Of(a), Of(b))));
        }
    }

    [Fact]
    public void DivisionSquareRootExponentialAndLogarithmHoldSixtyDigits()
    {
        // 1/3 against the exact fraction: |3 q - 1| below 2^-205
        Rational third = Exact(Op("op_Division", Of(1.0), Of(3.0)));
        Assert.True(Rational.Abs((3 * third) - Rational.One) < Rational.Pow2(-205));

        // sqrt(2): |s^2 - 2| below 2^-200
        Rational s = Exact(Fn("Sqrt", Of(2.0)));
        Assert.True(Rational.Abs((s * s) - new Rational(2)) < Rational.Pow2(-200));

        // e, e^-10, e^x - 1 at x the double nearest 1e-6 and ln 2 against 80-digit values
        AssertWithin("2.7182818284590452353602874713526624977572470936999595749669676277240766303535476", Fn("Exp", Of(1.0)));
        AssertWithin(
            "0.000045399929762484851535591515560550610237918088866564969259071305650999421614302282", Fn("Exp", Of(-10.0)));
        AssertWithin(
            "0.0000010000005000001666214563999156421266758003620551186288684249689003289548490",
            Op("op_Subtraction", Fn("Exp", Of(1e-6)), Of(1.0)),
            -150);
        AssertWithin("0.69314718055994530941723212145817656807550013436025525412068000949339362196969472", Fn("Log", Of(2.0)));
    }

    [Fact]
    public void NonFiniteInputsPropagate()
    {
        Assert.True(double.IsNaN(Lead(Op("op_Addition", Of(double.NaN), Of(1.0)))));
        Assert.True(double.IsNaN(Lead(Op("op_Multiply", Of(2.0), Of(double.NaN)))));
        Assert.False(double.IsFinite(Lead(Op("op_Multiply", Of(double.PositiveInfinity), Of(2.0)))));
        Assert.False(double.IsFinite(Lead(Op("op_Division", Of(1.0), Of(0.0)))));
        Assert.True(double.IsNaN(Lead(Fn("Sqrt", Of(-1.0)))));
        Assert.True(double.IsNaN(Lead(Fn("Exp", Of(double.NaN)))));
    }

    private static readonly Type QD = typeof(COSHestonEngine).Assembly.GetType("NanoQL.Numerics.QuadDouble", throwOnError: true)!;

    private static object Of(double x) => QD.GetMethod("Of", [typeof(double)])!.Invoke(null, [x])!;

    private static object Op(string name, object a, object b) => QD.GetMethod(name, [QD, QD])!.Invoke(null, [a, b])!;

    private static object Fn(string name, object a) => QD.GetMethod(name, BindingFlags.Public | BindingFlags.Static, [QD])!.Invoke(null, [a])!;

    private static double Lead(object x) => (double)QD.GetProperty("X0")!.GetValue(x)!;

    private static Rational Exact(object x) =>
        Rational.Of((double)QD.GetProperty("X0")!.GetValue(x)!) + Rational.Of((double)QD.GetProperty("X1")!.GetValue(x)!)
        + Rational.Of((double)QD.GetProperty("X2")!.GetValue(x)!) + Rational.Of((double)QD.GetProperty("X3")!.GetValue(x)!);

    // |x - v| below 2^power |v| for a decimal v
    private static void AssertWithin(string decimalValue, object x, int power = -190)
    {
        Rational v = Rational.Parse(decimalValue);
        Rational error = Rational.Abs(Exact(x) - v);
        Assert.True(error < Rational.Abs(v) * Rational.Pow2(power), $"{Lead(x)} against {decimalValue}");
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
