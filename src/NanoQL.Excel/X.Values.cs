// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using ExcelDna.Integration;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;
using Calendar = NanoQL.Time.Calendar;

namespace NanoQL.Excel;

/// <summary>The conversions in: each kind's Excel value to nanoql's, refusing what it can't read, naming the argument.</summary>
public static partial class X
{
    // A handle's text: a type name, '#', and the key's 16 hex digits, or 32 after a collision.
    private static readonly Regex HandleText = new(@"^[A-Za-z_][A-Za-z0-9_]*#[0-9A-F]{16}(?:[0-9A-F]{16})?$", RegexOptions.CultureInvariant);

    /// <summary>A number.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The number.</returns>
    public static double Dbl(object a, string p) => One(a) is double d ? d : throw Bad(p, "a number", a);

    /// <summary>A whole number in 32 bits.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The number.</returns>
    public static int Int(object a, string p)
        => One(a) is double d && d == Math.Floor(d) && d >= int.MinValue && d <= int.MaxValue ? (int)d : throw Bad(p, "a whole number", a);

    /// <summary>A whole number in 64 bits.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The number.</returns>
    public static long Long(object a, string p)
        => One(a) is double d && d == Math.Floor(d) && d >= long.MinValue && d < 9.2233720368547758e18 ? (long)d : throw Bad(p, "a whole number", a);

    /// <summary>TRUE or FALSE, or 1 or 0.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The boolean.</returns>
    public static bool Bool(object a, string p) => One(a) switch
    {
        bool b => b,
        double d when d is 0 or 1 => d == 1,
        _ => throw Bad(p, "TRUE or FALSE", a),
    };

    /// <summary>Text.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The text.</returns>
    public static string Str(object a, string p) => One(a) is string s ? s : throw Bad(p, "text", a);

    /// <summary>A date: a whole serial number, the 1904 system's shifted to QuantLib's; a time of day refused.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The date.</returns>
    public static Date Date(object a, string p) => new(Serial(a, p) + DateShift());

    /// <summary>A date's serial number as the workbook holds it: a whole number, never a time of day.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The serial number, in the workbook's own date system.</returns>
    internal static int Serial(object a, string p)
        => One(a) is double d && d == Math.Floor(d) && d >= 0 && d <= int.MaxValue - 1462 ? (int)d : throw Bad(p, "a date (a whole-number serial)", a);

    /// <summary>A period as text, <c>"6M"</c> or <c>"1Y6M"</c>.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The period.</returns>
    public static Period Per(object a, string p) => One(a) is string s ? PeriodParser.Parse(s) : throw Bad(p, "a period such as \"6M\"", a);

    /// <summary>An enum by its member's name, in any case.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The member.</returns>
    public static T Enum<T>(object a, string p)
        where T : struct, System.Enum
    {
        if (One(a) is string s && s.Length > 0 && char.IsLetter(s[0]) && System.Enum.TryParse(s, true, out T v))
        {
            return v;
        }

        throw Bad(p, $"one of {string.Join(", ", System.Enum.GetNames<T>())}", a);
    }

    /// <summary>
    /// An object by its handle; where a calendar or a day counter goes, text that isn't a handle's names one in the
    /// current convention set.
    /// </summary>
    /// <typeparam name="T">The type the argument takes.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The object.</returns>
    public static T Obj<T>(object a, string p)
    {
        if (One(a) is string s)
        {
            if (ByHandle.TryGetValue(s, out XlEntry? en))
            {
                return en.Value is T t ? t : throw new ArgumentException($"{p}: expected a {typeof(T).Name}, got a {en.Value!.GetType().Name}", p);
            }

            if (!HandleText.IsMatch(s) && (typeof(T) == typeof(Calendar) || typeof(T) == typeof(DayCounter)))
            {
                return (T)ByName(typeof(T), s, p);
            }

            throw new XlHandleException($"{p}: no object \"{s}\" in this Excel session; recalculate the cell that made it", p);
        }

        throw Bad(p, $"a handle to a {typeof(T).Name}", a);
    }

    /// <summary>A nullable number: an empty cell or a missing argument is none.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The number, or none.</returns>
    public static double? OptDbl(object a, string p) => IsMissing(One(a)) ? null : Dbl(a, p);

    /// <summary>A nullable whole number.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The number, or none.</returns>
    public static int? OptInt(object a, string p) => IsMissing(One(a)) ? null : Int(a, p);

    /// <summary>A nullable 64-bit whole number.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The number, or none.</returns>
    public static long? OptLong(object a, string p) => IsMissing(One(a)) ? null : Long(a, p);

    /// <summary>A nullable boolean.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The boolean, or none.</returns>
    public static bool? OptBool(object a, string p) => IsMissing(One(a)) ? null : Bool(a, p);

    /// <summary>A nullable date.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The date, or none.</returns>
    public static Date? OptDate(object a, string p) => IsMissing(One(a)) ? null : Date(a, p);

    /// <summary>A nullable period.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The period, or none.</returns>
    public static Period? OptPer(object a, string p) => IsMissing(One(a)) ? null : Per(a, p);

    /// <summary>A nullable enum.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The member, or none.</returns>
    public static T? OptEnum<T>(object a, string p)
        where T : struct, System.Enum => IsMissing(One(a)) ? null : Enum<T>(a, p);

    /// <summary>A nullable struct behind a handle.</summary>
    /// <typeparam name="T">The struct.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The value, or none.</returns>
    public static T? OptBoxed<T>(object a, string p)
        where T : struct => IsMissing(One(a)) ? null : Obj<T>(a, p);

    /// <summary>Numbers from a range by rows, its trailing empty cells trimmed and an inner one refused.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The numbers.</returns>
    public static double[] Dbls(object a, string p) => Cells(a, p).Select(x => Dbl(x, p)).ToArray();

    /// <summary>Whole numbers from a range.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The numbers.</returns>
    public static int[] Ints(object a, string p) => Cells(a, p).Select(x => Int(x, p)).ToArray();

    /// <summary>64-bit whole numbers from a range.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The numbers.</returns>
    public static long[] Longs(object a, string p) => Cells(a, p).Select(x => Long(x, p)).ToArray();

    /// <summary>Booleans from a range.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The booleans.</returns>
    public static bool[] Bools(object a, string p) => Cells(a, p).Select(x => Bool(x, p)).ToArray();

    /// <summary>Dates from a range.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The dates.</returns>
    public static Date[] Dates(object a, string p) => Cells(a, p).Select(x => Date(x, p)).ToArray();

    /// <summary>Periods from a range.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The periods.</returns>
    public static Period[] Pers(object a, string p) => Cells(a, p).Select(x => Per(x, p)).ToArray();

    /// <summary>Texts from a range.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The texts.</returns>
    public static string[] Strs(object a, string p) => Cells(a, p).Select(x => Str(x, p)).ToArray();

    /// <summary>Enums from a range, each by name.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The members.</returns>
    public static T[] Enums<T>(object a, string p)
        where T : struct, System.Enum => Cells(a, p).Select(x => Enum<T>(x, p)).ToArray();

    /// <summary>Objects from a range of handles; calendars and day counters by name too.</summary>
    /// <typeparam name="T">The type each element takes.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The objects.</returns>
    public static T[] Objs<T>(object a, string p) => Cells(a, p).Select(x => Obj<T>(x, p)).ToArray();

    /// <summary>A matrix from a 2-D range by rows; a single cell is a 1 × 1 matrix.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The matrix.</returns>
    public static Matrix Mat(object a, string p)
    {
        if (a is not object[,] m)
        {
            m = new object[,] { { a } };
        }

        var r = new Matrix(m.GetLength(0), m.GetLength(1));
        for (int i = 0; i < m.GetLength(0); i++)
        {
            for (int j = 0; j < m.GetLength(1); j++)
            {
                r[i, j] = Dbl(Cell(m[i, j], p), p);
            }
        }

        return r;
    }

    /// <summary>A vector from a row or a column.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The vector.</returns>
    public static NanoQL.Numerics.Vector Vec(object a, string p) => new(Dbls(a, p));

    /// <summary>A refusal of an argument: what it expected and what it got.</summary>
    /// <param name="p">The argument's name.</param>
    /// <param name="what">What it takes.</param>
    /// <param name="a">What it got.</param>
    /// <returns>The exception.</returns>
    internal static ArgumentException Bad(string p, string what, object? a) => new($"{p}: expected {what}, got {Show(a)}", p);

    /// <summary>How a message shows an Excel value.</summary>
    /// <param name="a">The value.</param>
    /// <returns>Its text.</returns>
    internal static string Show(object? a) => a switch
    {
        null => "nothing",
        ExcelMissing => "a missing argument",
        ExcelEmpty => "an empty cell",
        ExcelError e => "the Excel error " + ErrorText(e),
        string s => $"text \"{s}\"",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        bool b => b ? "TRUE" : "FALSE",
        object[,] m => $"a range of {m.GetLength(0)} by {m.GetLength(1)}",
        _ => a.GetType().Name,
    };

    /// <summary>A 1 × 1 range is its value, as in a single call's key.</summary>
    /// <param name="a">The Excel value.</param>
    /// <returns>The value.</returns>
    internal static object One(object a) => a is object[,] { Length: 1 } m ? m[0, 0] : a;

    /// <summary>
    /// A range's cells by rows, its trailing empty cells trimmed and an inner one refused, so two parallel ranges can't
    /// fall out of step; an Excel error inside it refused, a returned none never read as none.
    /// </summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The cells.</returns>
    internal static List<object> Cells(object a, string p)
    {
        if (a is not object[,] m)
        {
            return IsMissing(a) ? [] : [Cell(a, p)];
        }

        var all = new List<object>(m.Length);
        foreach (object x in m)
        {
            all.Add(x);
        }

        int n = all.Count;
        while (n > 0 && IsMissing(all[n - 1]))
        {
            n--;
        }

        for (int i = 0; i < n; i++)
        {
            if (IsMissing(all[i]))
            {
                throw new ArgumentException($"{p}: an empty cell inside the range (its element {(i + 1).ToString(CultureInfo.InvariantCulture)}); only trailing empty cells are trimmed", p);
            }

            Cell(all[i], p);
        }

        return all.GetRange(0, n);
    }

    /// <summary>A cell inside a range: an Excel error refused.</summary>
    /// <param name="x">The cell's value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The value.</returns>
    internal static object Cell(object x, string p)
        => x is ExcelError e ? throw new ArgumentException($"{p}: the range holds the Excel error {ErrorText(e)}, which no argument takes", p) : x;

    private static string ErrorText(ExcelError e) => e switch
    {
        ExcelError.ExcelErrorNA => "#N/A",
        ExcelError.ExcelErrorValue => "#VALUE!",
        ExcelError.ExcelErrorRef => "#REF!",
        ExcelError.ExcelErrorNum => "#NUM!",
        ExcelError.ExcelErrorDiv0 => "#DIV/0!",
        ExcelError.ExcelErrorName => "#NAME?",
        ExcelError.ExcelErrorNull => "#NULL!",
        _ => e.ToString(),
    };
}
