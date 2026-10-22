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
// Partial: parseFormatted (not ported: a DateOnly's ParseExact does its job)

using NanoQL.Time;

namespace NanoQL.Utilities;

/// <summary>Parses dates.</summary>
/// <remarks>
/// <para>
/// QuantLib's <c>parseFormatted</c>, which runs Boost.Date_Time's format engine on a string, isn't ported: a date
/// parsed by <c>DateOnly.ParseExact</c> converts to a <see cref="Date"/>.
/// </para>
/// <para>
/// QuantLib's <c>parseISO</c> checks the length and the dashes and reads each field with <c>std::stoi</c>, which stops
/// at the first non-digit, so "2006-1a-15" parses as 15 January 2006 (<c>dataparsers.cpp:106-114</c>): here a field
/// that isn't all digits is refused with QuantLib's "invalid format", a documented divergence.
/// </para>
/// </remarks>
public static class DateParser
{
    /// <summary>Parses an ISO 8601 date, YYYY-MM-DD.</summary>
    /// <param name="str">The string.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="str"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The string isn't a date in that format.</exception>
    public static Date ParseISO(string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        if (!(str.Length == 10 && str[4] == '-' && str[7] == '-')
            || !AllDigits(str.AsSpan(0, 4)) || !AllDigits(str.AsSpan(5, 2)) || !AllDigits(str.AsSpan(8, 2)))
        {
            throw new ArgumentException("invalid format", nameof(str));
        }

        int year = Number(str.AsSpan(0, 4));
        var month = (Month)Number(str.AsSpan(5, 2));
        int day = Number(str.AsSpan(8, 2));

        return new Date(day, month, year);
    }

    private static bool AllDigits(ReadOnlySpan<char> s) => !s.ContainsAnyExceptInRange('0', '9');

    private static int Number(ReadOnlySpan<char> digits)
    {
        int n = 0;
        foreach (char c in digits)
        {
            n = (n * 10) + (c - '0');
        }

        return n;
    }
}
