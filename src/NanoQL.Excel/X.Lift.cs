// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>
/// Looping over a range (XLR15) and each failure's message by its cell (XLR4, XLD3): a range where a function takes one
/// value is looped over, element by element, the ranges paired by Excel's rule, each element the scalar call keyed as it
/// is; a failed element shows its error in its place and its message by its own cell of the spill.
/// </summary>
public static partial class X
{
    // Each failed call's message by its calling cell; a looped call's spill with its elements' messages by its anchor.
    private static readonly ConcurrentDictionary<string, string> Errors = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, XlSpill> Spills = new(StringComparer.Ordinal);

    [ThreadStatic]
    private static bool inLift;

    [ThreadStatic]
    private static string? liftMessage;

    private static long liftedCalls;
    private static long liftedElements;

    /// <summary>
    /// A cell's message (round 8's F8-1), Excel's own state deciding a spill: a cell Excel doesn't show as a spilled cell
    /// gives its own call's message; a spilled cell gives its element's when the spill shows the looped call's result
    /// element by element, and otherwise the formula's message, which names the first failed element and its place.
    /// </summary>
    /// <param name="r">The cell.</param>
    /// <returns>The message, or empty text.</returns>
    internal static string ErrorOf(ExcelReference r)
    {
        string? own = Errors.TryGetValue(KeyOf(r), out string? m) ? m : null;
        ExcelReference? p;
        try
        {
            p = Host.SpillParent(r);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            p = null;  // an Excel without dynamic arrays, whose ranges have no HasSpill
        }

        if (p == null || (p.RowFirst == r.RowFirst && p.ColumnFirst == r.ColumnFirst))
        {
            return own ?? string.Empty;
        }

        string pk = KeyOf(p);
        if (!Spills.TryGetValue(pk, out XlSpill? s))
        {
            return string.Empty;
        }

        if (Shows(s))
        {
            return s.Messages.TryGetValue((r.RowFirst - s.Row, r.ColumnFirst - s.Column), out string? em) ? em : string.Empty;
        }

        return Errors.TryGetValue(pk, out string? fm) ? fm : string.Empty;
    }

    /// <summary>A cell's own call's message, for the tests.</summary>
    /// <param name="r">The cell.</param>
    /// <returns>The message, or empty text.</returns>
    internal static string ErrorAt(ExcelReference r) => Errors.TryGetValue(KeyOf(r), out string? m) ? m : string.Empty;

    // A 1 x 1 range where the mask loops is its value, as in the single call's key (round 7's note).
    private static object[] Cells1(object[] args, string lift)
    {
        object[]? copy = null;
        for (int k = 0; k < args.Length && k < lift.Length; k++)
        {
            if (lift[k] == '1' && args[k] is object[,] { Length: 1 } m)
            {
                copy ??= (object[])args.Clone();
                copy[k] = m[0, 0];
            }
        }

        return copy ?? args;
    }

    // Whether a range given where the mask loops is more than one cell, and the loop's shape: the largest rows and columns.
    private static bool LiftShape(object[] args, string lift, out int rows, out int cols)
    {
        rows = cols = 1;
        bool any = false;
        if (inLift)
        {
            return false;
        }

        for (int k = 0; k < args.Length && k < lift.Length; k++)
        {
            if (lift[k] != '1' || args[k] is not object[,] m || m.Length <= 1)
            {
                continue;
            }

            any = true;
            rows = Math.Max(rows, m.GetLength(0));
            cols = Math.Max(cols, m.GetLength(1));
        }

        return any;
    }

    // The loop: each element the scalar call; a single row or column repeats, a position past a shorter range's end #N/A.
    private static object Lifted(string fn, object[] args, string lift, int rows, int cols, Func<object[], object> one)
    {
        Interlocked.Increment(ref liftedCalls);
        var o = new object[rows, cols];
        var messages = new List<(int I, int J, string Message)>();
        inLift = true;
        try
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    object[] e = (object[])args.Clone();
                    bool na = false;
                    for (int k = 0; k < args.Length && k < lift.Length; k++)
                    {
                        if (lift[k] != '1' || args[k] is not object[,] m)
                        {
                            continue;
                        }

                        if (m.Length == 1)
                        {
                            e[k] = m[0, 0];  // a 1 x 1 range is its value, as in the single call's key
                            continue;
                        }

                        int r = m.GetLength(0), c = m.GetLength(1), ii = r == 1 ? 0 : i, jj = c == 1 ? 0 : j;
                        if (ii >= r || jj >= c)
                        {
                            na = true;
                            break;
                        }

                        e[k] = m[ii, jj];
                    }

                    liftMessage = null;
                    object v = na ? ExcelError.ExcelErrorNA : one(e);
                    Interlocked.Increment(ref liftedElements);
                    if (v is object[,])
                    {
                        v = ExcelError.ExcelErrorValue;
                        liftMessage = $"{fn}: the element's result is a range; a looped call gives one value per element";
                    }

                    if (liftMessage != null)
                    {
                        messages.Add((i, j, liftMessage));
                    }

                    o[i, j] = v;
                }
            }
        }
        finally
        {
            inLift = false;
            liftMessage = null;
        }

        // The formula's cell gets the first failed element's message with its place, whether the looped call is the
        // formula's value or nested in it (F7-1); a looped call with no failed element leaves the cell's message (F9-1).
        if (Host.Caller() is ExcelReference anchor)
        {
            string key = KeyOf(anchor);
            var msgs = messages.ToDictionary(x => (x.I, x.J), x => $"{x.Message} (row {(x.I + 1).ToString(CultureInfo.InvariantCulture)}, column {(x.J + 1).ToString(CultureInfo.InvariantCulture)} of the spill)");
            if (messages.Count > 0)
            {
                Errors[key] = msgs[(messages[0].I, messages[0].J)];
            }

            Spills[key] = new XlSpill(anchor.SheetId, anchor.RowFirst, anchor.ColumnFirst, o, msgs);
        }

        KeepCall(fn, args, o);
        return o;
    }

    // Whether Excel shows the spill as the looped call returned it, element by element.
    private static bool Shows(XlSpill s)
    {
        int rows = s.Result.GetLength(0), cols = s.Result.GetLength(1);
        object shown;
        try
        {
            shown = Host.ValueOf(new ExcelReference(s.Row, s.Row + rows - 1, s.Column, s.Column + cols - 1, s.SheetId));
        }
        catch (XlCallException)
        {
            return false;
        }

        if (shown is not object[,] v)
        {
            return rows * cols == 1 && Same(shown, s.Result[0, 0]);
        }

        if (v.GetLength(0) != rows || v.GetLength(1) != cols)
        {
            return false;
        }

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (!Same(v[i, j], s.Result[i, j]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // A cell's value against an element's: numbers by bits, Excel's errors by code, an empty element as Excel shows it.
    private static bool Same(object shown, object element)
    {
        if (element is null or ExcelEmpty or ExcelMissing)
        {
            return shown is ExcelEmpty || shown is double z && z == 0;
        }

        if (element is int or long or float or decimal)
        {
            element = Convert.ToDouble(element, CultureInfo.InvariantCulture);
        }

        return element is double d ? shown is double dv && BitConverter.DoubleToInt64Bits(d) == BitConverter.DoubleToInt64Bits(dv) : Equals(shown, element);
    }

    private static string KeyOf(ExcelReference r) => $"{r.SheetId}!R{r.RowFirst.ToString(CultureInfo.InvariantCulture)}C{r.ColumnFirst.ToString(CultureInfo.InvariantCulture)}";

    private static string? CallerKey() => Host.Caller() is ExcelReference r ? KeyOf(r) : null;
}
