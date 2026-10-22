// SPDX-FileCopyrightText: Copyright (C) 2026 SoftSolution srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/islamicholidays.hpp ql/time/calendars/islamicholidays.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Time;

/// <summary>Shared Islamic holiday date lookup tables (Eid al-Fitr, Eid al-Adha).</summary>
/// <remarks>
/// <para>
/// For calculating Eid al-Fitr and Eid al-Adha, there are two main approaches (calendars) used in practice, and they
/// sometimes overlap. Umm al-Qura calendar is used by Saudi Arabia (primary user) and some Gulf countries. Moon
/// sighting calendar is used by South Asia, Central Asia, Middle East and North Africa.
/// </para>
/// <para>
/// QuantLib's functions in namespace <c>MoonSightingMethod</c>; the method stays in the members' names. The tables hold
/// 2026 to 2040: outside them, no date is either feast.
/// </para>
/// </remarks>
public static class IslamicHolidaysFunctions
{
    private static readonly ImmutableHashSet<Date> EidAlFitr =
    [
        new(20, Month.March, 2026),
        new(10, Month.March, 2027),
        new(27, Month.February, 2028),
        new(15, Month.February, 2029),
        new(5, Month.February, 2030),
        new(25, Month.January, 2031),
        new(14, Month.January, 2032),
        new(2, Month.January, 2033),
        new(23, Month.December, 2033),
        new(12, Month.December, 2034),
        new(1, Month.December, 2035),
        new(19, Month.November, 2036),
        new(8, Month.November, 2037),
        new(29, Month.October, 2038),
        new(19, Month.October, 2039),
        new(7, Month.October, 2040),
    ];

    private static readonly ImmutableHashSet<Date> EidAlAdha =
    [
        new(27, Month.May, 2026),
        new(17, Month.May, 2027),
        new(5, Month.May, 2028),
        new(24, Month.April, 2029),
        new(13, Month.April, 2030),
        new(3, Month.April, 2031),
        new(22, Month.March, 2032),
        new(11, Month.March, 2033),
        new(28, Month.February, 2034),
        new(18, Month.February, 2035),
        new(7, Month.February, 2036),
        new(27, Month.January, 2037),
        new(16, Month.January, 2038),
        new(5, Month.January, 2039),
        new(26, Month.December, 2039),
        new(15, Month.December, 2040),
    ];

    /// <summary>Whether a date is Eid al-Fitr by the moon sighting method; QuantLib's
    /// <c>MoonSightingMethod::isEidAlFitr</c>.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> if the date is Eid al-Fitr.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public static bool IsEidAlFitrByMoonSighting(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return EidAlFitr.Contains(d);
    }

    /// <summary>Whether a date is Eid al-Adha by the moon sighting method; QuantLib's
    /// <c>MoonSightingMethod::isEidAlAdha</c>.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> if the date is Eid al-Adha.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    public static bool IsEidAlAdhaByMoonSighting(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return EidAlAdha.Contains(d);
    }
}
