// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using Calendar = NanoQL.Time.Calendar;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// A built object's observable values as text, to compare what two builds made: its public properties down to their
/// values, a double by its bits, a cash flow with its amount where it can be computed, a calendar by its name and
/// holidays over 2026 to 2032, a day counter, an index and a currency by name; term structures and pricers apart.
/// </summary>
internal static class Fingerprint
{
    private static readonly Date From = new(1, Month.January, 2026);
    private static readonly Date To = new(31, Month.December, 2032);

    /// <summary>The value's text.</summary>
    /// <param name="value">The value.</param>
    /// <param name="context">The context a cash flow's amount is computed in.</param>
    /// <returns>The text.</returns>
    public static string Of(object? value, EvaluationContext context)
    {
        var sb = new StringBuilder();
        Write(sb, value, context, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, object? value, EvaluationContext context, int depth, HashSet<object> seen)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                return;
            case double d:
                sb.Append(BitConverter.DoubleToInt64Bits(d));
                return;
            case string or bool or int or Enum or Date or Period:
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case Calendar c:
                sb.Append(c.Name).Append('[').Append(string.Join(",", c.HolidayList(From, To, false))).Append(']');
                return;
            case DayCounter dc:
                sb.Append(dc.Name);
                return;
            case FinancialIndex index:
                sb.Append(index.Name);
                return;
            case Currency currency:
                sb.Append(currency.Code);
                return;
            case TermStructure or EvaluationContext:
                sb.Append('-');
                return;
        }

        if (depth > 8 || !seen.Add(value))
        {
            sb.Append("...");
            return;
        }

        if (value is CashFlow cf)
        {
            sb.Append("amount=");
            try
            {
                sb.Append(BitConverter.DoubleToInt64Bits(cf.Amount(context)));
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException or InvalidOperationException)
            {
                sb.Append("n/a");
            }
        }

        if (value is IEnumerable items)
        {
            sb.Append('[');
            foreach (object? item in items)
            {
                Write(sb, item, context, depth + 1, seen);
                sb.Append(';');
            }

            sb.Append(']');
            return;
        }

        sb.Append(value.GetType().Name).Append('{');
        foreach (PropertyInfo p in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.Name is not ("Pricer" or "EqualityContract"))
            .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            object? x;
            try
            {
                x = p.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                x = "throws";
            }

            sb.Append(p.Name).Append('=');
            Write(sb, x, context, depth + 1, seen);
            sb.Append(',');
        }

        sb.Append('}');
    }
}
