// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2009 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (2) 2009 Mark Joshi
// SPDX-FileCopyrightText: Copyright (2) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/utilities/dataparsers.hpp ql/utilities/dataparsers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Time;

namespace NanoQL.Utilities;

/// <summary>Parses periods such as "3M", "1Y6M" or "-2W".</summary>
/// <remarks>
/// Each period is a number, optionally signed, and a unit letter (D, W, M or Y, in either case); periods follow each
/// other and add up. QuantLib reads the number with <c>std::stoi</c> from the first sign or digit, so it skips text
/// before it and after it ("x5D" and "5xD" both parse as 5D, <c>dataparsers.cpp:77-81</c>): here a number whose text
/// isn't an optional sign and digits is refused, a documented divergence.
/// </remarks>
public static class PeriodParser
{
    private static readonly char[] UnitLetters = ['D', 'd', 'W', 'w', 'M', 'm', 'Y', 'y'];
    private static readonly char[] NumberStart = ['-', '+', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    /// <summary>Parses a string of one or more periods.</summary>
    /// <param name="str">The string.</param>
    /// <returns>The sum of the periods.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="str"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The string isn't a sequence of periods.</exception>
    public static Period Parse(string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        if (!(str.Length > 1))
        {
            throw new ArgumentException("period string length must be at least 2", nameof(str));
        }

        var subStrings = new List<string>();
        string reducedString = str;

        int reducedStringDim = 100000, maxIter = 0;
        while (reducedStringDim > 0)
        {
            // where no unit is left, QuantLib's npos + 1 wraps to 0, as -1 + 1 is 0 here
            int iPos = reducedString.IndexOfAny(UnitLetters);
            int subStringDim = iPos + 1;
            reducedStringDim = reducedString.Length - subStringDim;
            subStrings.Add(reducedString[..subStringDim]);
            reducedString = reducedString.Substring(iPos + 1, reducedStringDim);
            ++maxIter;
            if (!(maxIter < str.Length))
            {
                throw new ArgumentException($"unknown '{str}' unit", nameof(str));
            }
        }

        Period result = ParseOnePeriod(subStrings[0]);
        for (int i = 1; i < subStrings.Count; ++i)
        {
            result += ParseOnePeriod(subStrings[i]);
        }

        return result;
    }

    private static Period ParseOnePeriod(string str)
    {
        if (!(str.Length > 1))
        {
            throw new ArgumentException("single period require a string of at least 2 characters", nameof(str));
        }

        int iPos = str.IndexOfAny(UnitLetters);
        if (!(iPos == str.Length - 1))
        {
            throw new ArgumentException($"unknown '{str[^1..]}' unit", nameof(str));
        }

        TimeUnit units = char.ToUpperInvariant(str[iPos]) switch
        {
            'W' => TimeUnit.Weeks,
            'M' => TimeUnit.Months,
            'Y' => TimeUnit.Years,
            _ => TimeUnit.Days,
        };

        int nPos = str.IndexOfAny(NumberStart);
        if (!(nPos >= 0 && nPos < iPos))
        {
            throw new ArgumentException($"no numbers of {units} provided", nameof(str));
        }

        // the number is the whole text before the unit: an optional sign, then digits
        ReadOnlySpan<char> number = str.AsSpan(0, iPos);
        ReadOnlySpan<char> digits = number[0] is '+' or '-' ? number[1..] : number;
        if (digits.IsEmpty
            || digits.ContainsAnyExceptInRange('0', '9')
            || !int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n))
        {
            throw new ArgumentException($"unable to parse the number of units of {units} in '{str}'", nameof(str));
        }

        return new Period(n, units);
    }
}
