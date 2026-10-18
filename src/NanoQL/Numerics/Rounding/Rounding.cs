// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/rounding.hpp ql/math/rounding.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// A basic rounding class that supports rounding to up to 16 decimal places, by one of the methods of
/// <see cref="RoundingType"/>.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. QuantLib documents the precision's range as [0, 16] and doesn't check it, leaving other values
/// undefined; nanoql refuses them when the rounding is built.
/// </para>
/// <para>
/// A rounding built with <see cref="Rounding()"/>, QuantLib's default construction, doesn't round and has no precision
/// or rounding digit: <see cref="Precision"/> and <see cref="RoundingDigit"/> refuse it, where QuantLib reads two
/// uninitialized members.
/// </para>
/// </remarks>
public class Rounding
{
    // QuantLib's fast_pow10 table, for the precisions it supports.
    private static readonly double[] Pow10 =
    [
        1.0E0, 1.0E1, 1.0E2, 1.0E3, 1.0E4, 1.0E5,
        1.0E6, 1.0E7, 1.0E8, 1.0E9, 1.0E10, 1.0E11,
        1.0E12, 1.0E13, 1.0E14, 1.0E15, 1.0E16,
    ];

    private readonly int? precision;
    private readonly int? digit;

    /// <summary>Initializes a new instance of the <see cref="Rounding"/> class that doesn't round.</summary>
    public Rounding()
    {
        Type = RoundingType.None;
    }

    /// <summary>Initializes a new instance of the <see cref="Rounding"/> class.</summary>
    /// <param name="precision">The number of decimal places kept, in [0, 16].</param>
    /// <param name="type">The rounding method.</param>
    /// <param name="digit">
    /// The rounding digit: the first decimal place past the precision rounds up when it is at least this digit, for
    /// the methods that use it. It isn't checked, as in QuantLib: 0 always rounds up and 10 never does.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The precision is outside [0, 16], or the method isn't a member of <see cref="RoundingType"/>.
    /// </exception>
    public Rounding(int precision, RoundingType type = RoundingType.Closest, int digit = 5)
    {
        CheckPrecision(precision, nameof(precision));
        EnumArguments.Check(type, nameof(type));
        this.precision = precision;
        Type = type;
        this.digit = digit;
    }

    /// <summary>Gets the rounding method.</summary>
    public RoundingType Type { get; }

    /// <summary>The number of decimal places kept.</summary>
    /// <returns>The precision.</returns>
    /// <exception cref="NanoQLException">The rounding was built by <see cref="Rounding()"/>.</exception>
    public int Precision() => precision ?? throw new NanoQLException("no precision for a default rounding");

    /// <summary>The rounding digit.</summary>
    /// <returns>The digit.</returns>
    /// <exception cref="NanoQLException">The rounding was built by <see cref="Rounding()"/>.</exception>
    public int RoundingDigit() => digit ?? throw new NanoQLException("no rounding digit for a default rounding");

    /// <summary>Rounds a value: QuantLib's <c>operator()</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The rounded value. NaN and the infinities come back unchanged, and so does every value under
    /// <see cref="RoundingType.None"/>; a value whose scaled magnitude overflows comes back infinite, as in QuantLib.
    /// </returns>
    public double Evaluate(double value)
    {
        if (Type == RoundingType.None)
        {
            return value;
        }

        // Only a rounding of a method other than None reaches here, and every such rounding has a precision.
        double mult = Pow10[precision!.Value];
        bool neg = value < 0.0;
        double lvalue = Math.Abs(value) * mult;
        double modVal = Fraction(lvalue);
        lvalue -= modVal;
        double threshold = digit!.Value / 10.0;
        switch (Type)
        {
            case RoundingType.Down:
                break;
            case RoundingType.Up:
                if (modVal != 0.0)
                {
                    lvalue += 1.0;
                }

                break;
            case RoundingType.Closest:
                if (modVal >= threshold)
                {
                    lvalue += 1.0;
                }

                break;
            case RoundingType.Floor:
                if (!neg && modVal >= threshold)
                {
                    lvalue += 1.0;
                }

                break;
            case RoundingType.Ceiling:
                if (neg && modVal >= threshold)
                {
                    lvalue += 1.0;
                }

                break;
        }

        return neg ? -(lvalue / mult) : lvalue / mult;
    }

    /// <summary>Refuses a rounding precision outside [0, 16], QuantLib's documented range.</summary>
    /// <param name="precision">The precision.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The precision is outside [0, 16].</exception>
    internal static void CheckPrecision(int precision, string paramName)
    {
        if (precision is < 0 or > 16)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                precision,
                string.Create(CultureInfo.InvariantCulture, $"rounding precision ({precision}) must be between 0 and 16"));
        }
    }

    // std::modf's fractional part: x - trunc(x), which is exact, with x's sign, and ±0 for ±∞, whose integral part
    // takes all of x. NaN gives NaN.
    private static double Fraction(double x) =>
        double.IsInfinity(x) ? Math.CopySign(0.0, x) : Math.CopySign(x - Math.Truncate(x), x);
}
