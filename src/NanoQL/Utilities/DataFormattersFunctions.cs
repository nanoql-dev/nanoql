// SPDX-FileCopyrightText: Copyright (C) 2005, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/utilities/dataformatters.hpp ql/utilities/dataformatters.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using System.Numerics;
using System.Text;

namespace NanoQL.Utilities;

/// <summary>QuantLib's output manipulators (<c>dataformatters.hpp</c>), returning the text they would write.</summary>
/// <remarks>
/// Where QuantLib streams a number with the stream's defaults, the text follows a C++ stream: six significant digits,
/// trailing zeros removed, an exponent of at least two digits (<c>1e-07</c>), and exact ties rounded half to even. A
/// <see cref="bool"/> is written <c>True</c> or <c>False</c>, where a C++ stream writes <c>1</c> or <c>0</c>; QuantLib
/// streams no boolean through these functions.
/// </remarks>
public static class DataFormattersFunctions
{
    /// <summary>A value, or "null" for a missing one.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns>The text.</returns>
    public static string CheckNull<T>(T? value)
        where T : struct => value is T v ? StreamText(v) : "null";

    /// <summary>A natural number as an ordinal: 1st, 2nd, 3rd, 4th, 11th, 12th, 13th, 21st, ...</summary>
    /// <param name="n">The number.</param>
    /// <returns>The text.</returns>
    /// <remarks>As in QuantLib, only 11, 12 and 13 are special-cased, so 111 gives "111st".</remarks>
    public static string Ordinal(int n)
    {
        string suffix = n is 11 or 12 or 13 ? "th" : (n % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th",
        };
        return n.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    /// <summary>An integer as an odd number times a power of two, e.g. "3*2^2" for 12.</summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="n">The number, or <see langword="null"/>.</param>
    /// <returns>The text, or "null".</returns>
    public static string PowerOfTwo<T>(T? n)
        where T : struct, IBinaryInteger<T>
    {
        if (n is not T value)
        {
            return "null";
        }

        int power = 0;
        if (value != T.Zero)
        {
            while (T.IsEvenInteger(value))
            {
                power++;
                value >>= 1;
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value}*2^{power}");
    }

    /// <summary>A real as a percentage with six decimals, e.g. "3.250000 %".</summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns>The text, or "null".</returns>
    /// <remarks>
    /// As in QuantLib, the percentage is the product <c>value * 100.0</c>, so a value whose product overflows is
    /// written as infinite; a NaN or infinite percentage is written as a C++ stream writes it, "nan", "inf" or "-inf".
    /// </remarks>
    public static string Percent(double? value)
    {
        if (value is not double v)
        {
            return "null";
        }

        double percentage = v * 100.0;
        string text = double.IsFinite(percentage)
            ? percentage.ToString("F6", CultureInfo.InvariantCulture)
            : General(percentage);
        return text + " %";
    }

    /// <summary>A rate or spread as a percentage.</summary>
    /// <param name="r">The rate, or <see langword="null"/>.</param>
    /// <returns>The text, or "null".</returns>
    public static string Rate(double? r) => Percent(r);

    /// <summary>A volatility as a percentage.</summary>
    /// <param name="v">The volatility, or <see langword="null"/>.</param>
    /// <returns>The text, or "null".</returns>
    public static string Volatility(double? v) => Percent(v);

    /// <summary>A sequence as space-separated values in parentheses, e.g. "( 1 2.5 3 )".</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="c">The sequence.</param>
    /// <returns>The text.</returns>
    public static string Sequence<T>(IEnumerable<T> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var text = new StringBuilder("( ");
        foreach (T item in c)
        {
            text.Append(StreamText(item)).Append(' ');
        }

        return text.Append(')').ToString();
    }

    private static string StreamText(object? value) => value switch
    {
        double d => General(d),
        float f => General(f),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? string.Empty,
    };

    // A C++ stream's default floating-point output: %g with six significant digits. Also the element text of
    // Vector's and Matrix's ToString, QuantLib's operator<< for Array and Matrix.
    internal static string General(double x)
    {
        if (double.IsNaN(x))
        {
            return "nan";
        }

        if (double.IsInfinity(x))
        {
            return x > 0 ? "inf" : "-inf";
        }

        if (x == 0)
        {
            return double.IsNegative(x) ? "-0" : "0";
        }

        string scientific = x.ToString("E5", CultureInfo.InvariantCulture);
        int e = scientific.IndexOf('E', StringComparison.Ordinal);
        int exponent = int.Parse(scientific[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        if (exponent < -4 || exponent >= 6)
        {
            string sign = exponent < 0 ? "-" : "+";
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{TrimZeros(scientific[..e])}e{sign}{Math.Abs(exponent):00}");
        }

        return TrimZeros(x.ToString("F" + (5 - exponent).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
    }

    private static string TrimZeros(string number) =>
        number.Contains('.', StringComparison.Ordinal) ? number.TrimEnd('0').TrimEnd('.') : number;
}
