// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The internal extended-range double the Heston expansions and COS cumulants evaluate past κt's upper cut (the S-hes
/// plan's HR11): a double mantissa in [1, 2) times 2 to a long exponent, double's precision past its range. Its
/// arithmetic is double's within the range; e^{2000} and its reciprocal keep their exponents and multiply back to 1;
/// a non-finite input propagates. Read through reflection, the type being internal.
/// </summary>
public class ExtendedDoubleTests
{
    [Fact]
    public void WithinDoublesRangeTheArithmeticIsDoubles()
    {
        Assert.Equal(15.0, Value(Op("op_Multiply", Of(3.0), Of(5.0))));
        Assert.Equal(1.0 / 3.0, Value(Op("op_Division", Of(1.0), Of(3.0))));
        Assert.Equal(0.1 + 0.2, Value(Op("op_Addition", Of(0.1), Of(0.2))));
        Assert.Equal(1e300 - 1e299, Value(Op("op_Subtraction", Of(1e300), Of(1e299))));
        Assert.Equal(Math.Sqrt(2.0), Value(Fn("Sqrt", Of(2.0))));
        Assert.Equal(1.0, Mantissa(Of(1024.0)));
        Assert.Equal(10L, Exponent(Of(1024.0)));
    }

    [Fact]
    public void ThePastDoublesRangeKeepsItsExponent()
    {
        object big = Fn("Exp", Of(2000.0));
        object small = Fn("Exp", Of(-2000.0));

        // e^2000 = 2^2885.39: exponent 2885, mantissa 2^0.39
        Assert.Equal(2885L, Exponent(big));
        Assert.Equal(-2886L, Exponent(small));
        Assert.Equal(double.PositiveInfinity, Value(big));
        Assert.Equal(0.0, Value(small));
        Assert.Equal(1.0, Value(Op("op_Multiply", big, small)), 1e-13);
        Assert.Equal(2000.0, Value(Fn("Log", big)), 1e-12);

        // squaring past the range: (e^1000)^2 / e^2000
        object e1000 = Fn("Exp", Of(1000.0));
        Assert.Equal(1.0, Value(Op("op_Division", Op("op_Multiply", e1000, e1000), big)), 1e-13);
    }

    [Fact]
    public void NonFiniteInputsPropagate()
    {
        Assert.True(double.IsNaN(Value(Op("op_Addition", Of(double.NaN), Of(1.0)))));
        Assert.True(double.IsNaN(Value(Op("op_Multiply", Fn("Exp", Of(2000.0)), Of(double.NaN)))));
        Assert.Equal(double.PositiveInfinity, Value(Op("op_Multiply", Of(double.PositiveInfinity), Of(2.0))));
        Assert.True(double.IsNaN(Value(Fn("Sqrt", Of(-1.0)))));
        Assert.Equal(0.0, Value(Of(0.0)));
    }

    private static readonly Type XD = typeof(COSHestonEngine).Assembly.GetType("NanoQL.Numerics.ExtendedDouble", throwOnError: true)!;

    private static object Of(double x) => XD.GetMethod("Of", [typeof(double)])!.Invoke(null, [x])!;

    private static object Op(string name, object a, object b) => XD.GetMethod(name, [XD, XD])!.Invoke(null, [a, b])!;

    private static object Fn(string name, object a) => XD.GetMethod(name, BindingFlags.Public | BindingFlags.Static, [XD])!.Invoke(null, [a])!;

    private static double Value(object x) => (double)XD.GetProperty("Value")!.GetValue(x)!;

    private static double Mantissa(object x) => (double)XD.GetProperty("Mantissa")!.GetValue(x)!;

    private static long Exponent(object x) => (long)XD.GetProperty("Exponent")!.GetValue(x)!;
}
