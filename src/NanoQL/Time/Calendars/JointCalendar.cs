// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2020 Piotr Siejda
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/jointcalendar.hpp ql/time/calendars/jointcalendar.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace NanoQL.Time;

/// <summary>A joint calendar.</summary>
/// <remarks>
/// <para>
/// Depending on the chosen rule, this calendar has a set of business days given by either the union or the
/// intersection of the sets of business days of the given calendars.
/// </para>
/// <para>
/// Two joint calendars are equal when they have the same name and the same added and removed holidays, and also the
/// same rule and the same component calendars, in order, each compared the same way. The name alone would not do: it
/// is built from the components' names, which don't show a component's added or removed holidays.
/// </para>
/// </remarks>
public sealed class JointCalendar : Calendar
{
    private readonly JointCalendarRule rule;
    private readonly ImmutableArray<Calendar> calendars;
    private readonly string name;

    /// <summary>Initializes a new instance of the <see cref="JointCalendar"/> class joining two calendars.</summary>
    /// <param name="c1">The first calendar.</param>
    /// <param name="c2">The second calendar.</param>
    /// <param name="r">The rule for joining them.</param>
    /// <exception cref="ArgumentNullException">A calendar is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public JointCalendar(Calendar c1, Calendar c2, JointCalendarRule r = JointCalendarRule.JoinHolidays)
        : this(r, [Required(c1), Required(c2)])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="JointCalendar"/> class joining three calendars.</summary>
    /// <param name="c1">The first calendar.</param>
    /// <param name="c2">The second calendar.</param>
    /// <param name="c3">The third calendar.</param>
    /// <param name="r">The rule for joining them.</param>
    /// <exception cref="ArgumentNullException">A calendar is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public JointCalendar(Calendar c1, Calendar c2, Calendar c3, JointCalendarRule r = JointCalendarRule.JoinHolidays)
        : this(r, [Required(c1), Required(c2), Required(c3)])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="JointCalendar"/> class joining four calendars.</summary>
    /// <param name="c1">The first calendar.</param>
    /// <param name="c2">The second calendar.</param>
    /// <param name="c3">The third calendar.</param>
    /// <param name="c4">The fourth calendar.</param>
    /// <param name="r">The rule for joining them.</param>
    /// <exception cref="ArgumentNullException">A calendar is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public JointCalendar(
        Calendar c1, Calendar c2, Calendar c3, Calendar c4, JointCalendarRule r = JointCalendarRule.JoinHolidays)
        : this(r, [Required(c1), Required(c2), Required(c3), Required(c4)])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="JointCalendar"/> class joining any number of calendars.</summary>
    /// <param name="cv">The calendars, in order; the sequence is copied.</param>
    /// <param name="r">The rule for joining them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cv"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="cv"/> is empty or holds a <see langword="null"/> calendar.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The rule isn't a member of the enum.</exception>
    public JointCalendar(IEnumerable<Calendar> cv, JointCalendarRule r = JointCalendarRule.JoinHolidays)
        : this(r, Copy(cv))
    {
    }

    private JointCalendar(JointCalendarRule r, ImmutableArray<Calendar> cv)
    {
        rule = r;
        calendars = cv;
        name = BuildName(r, cv);
    }

    /// <inheritdoc/>
    public override string Name => name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w)
    {
        switch (rule)
        {
            case JointCalendarRule.JoinHolidays:
                foreach (Calendar c in calendars)
                {
                    if (c.IsWeekend(w))
                    {
                        return true;
                    }
                }

                return false;
            case JointCalendarRule.JoinBusinessDays:
                foreach (Calendar c in calendars)
                {
                    if (!c.IsWeekend(w))
                    {
                        return false;
                    }
                }

                return true;
            default:
                throw new NanoQLException("unknown joint calendar rule");
        }
    }

    /// <summary>
    /// Whether the calendar has the same name and the same added and removed holidays as another, which must be a joint
    /// calendar with the same rule and the same component calendars, in order, each compared the same way.
    /// </summary>
    /// <param name="other">The other calendar.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public override bool Equals(Calendar? other) =>
        other is JointCalendar j
        && (ReferenceEquals(this, j)
            || (base.Equals(j) && rule == j.rule && calendars.SequenceEqual(j.calendars)));

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(base.GetHashCode());
        hash.Add(rule);
        foreach (Calendar c in calendars)
        {
            hash.Add(c);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date)
    {
        switch (rule)
        {
            case JointCalendarRule.JoinHolidays:
                foreach (Calendar c in calendars)
                {
                    if (c.IsHoliday(date))
                    {
                        return false;
                    }
                }

                return true;
            case JointCalendarRule.JoinBusinessDays:
                foreach (Calendar c in calendars)
                {
                    if (c.IsBusinessDay(date))
                    {
                        return true;
                    }
                }

                return false;
            default:
                throw new NanoQLException("unknown joint calendar rule");
        }
    }

    private static Calendar Required(Calendar c, [CallerArgumentExpression(nameof(c))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(c, paramName);
        return c;
    }

    private static ImmutableArray<Calendar> Copy(IEnumerable<Calendar> cv)
    {
        ArgumentNullException.ThrowIfNull(cv);
        ImmutableArray<Calendar> calendars = [.. cv];
        if (calendars.IsEmpty)
        {
            throw new ArgumentException("at least one calendar required", nameof(cv));
        }

        if (calendars.Any(static c => c is null))
        {
            throw new ArgumentException("no calendar implementation provided", nameof(cv));
        }

        return calendars;
    }

    // QuantLib's name(): the rule, then the calendars' names.
    private static string BuildName(JointCalendarRule r, ImmutableArray<Calendar> calendars)
    {
        var text = new StringBuilder();
        switch (r)
        {
            case JointCalendarRule.JoinHolidays:
                text.Append("JoinHolidays(");
                break;
            case JointCalendarRule.JoinBusinessDays:
                text.Append("JoinBusinessDays(");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(r), r, "unknown joint calendar rule");
        }

        text.Append(calendars[0].Name);
        for (int i = 1; i < calendars.Length; ++i)
        {
            text.Append(", ").Append(calendars[i].Name);
        }

        text.Append(')');
        return text.ToString();
    }
}
