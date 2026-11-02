// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Office.Interop.Excel;
using Action = System.Action;
using Range = Microsoft.Office.Interop.Excel.Range;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// A worksheet through automation: formulas entered as Excel 365 enters them (<c>Formula2</c>, so a range spills), values
/// read as <c>Value2</c> (an error as its code), spills read whole, and every call retried while Excel is busy.
/// </summary>
internal static class Xl
{
    /// <summary>#VALUE!'s code.</summary>
    public const int Value = -2146826273;

    /// <summary>#REF!'s code.</summary>
    public const int Ref = -2146826265;

    /// <summary>#NUM!'s code.</summary>
    public const int Num = -2146826252;

    /// <summary>#N/A's code.</summary>
    public const int NA = -2146826246;

    /// <summary>#NAME?'s code.</summary>
    public const int Name = -2146826259;

    /// <summary>Enters a formula, as typed in the cell.</summary>
    /// <param name="ws">The worksheet.</param>
    /// <param name="cell">The cell's address.</param>
    /// <param name="formula">The formula, or a constant's text.</param>
    public static void Put(Worksheet ws, string cell, string formula) => Retry(() => Set(ws.Range[cell], "Formula2", formula));

    /// <summary>Enters cells' formulas in order.</summary>
    /// <param name="ws">The worksheet.</param>
    /// <param name="cells">Each cell's address and formula.</param>
    public static void Put(Worksheet ws, params (string Cell, string Formula)[] cells)
    {
        foreach ((string cell, string formula) in cells)
        {
            Put(ws, cell, formula);
        }
    }

    /// <summary>A cell's value: a number, text, a boolean, an error's code, or null for an empty cell.</summary>
    /// <param name="ws">The worksheet.</param>
    /// <param name="cell">The cell's address.</param>
    /// <returns>Its value.</returns>
    public static object? Get(Worksheet ws, string cell) => Retry(() => (object?)ws.Range[cell].Value2);

    /// <summary>A cell's text value.</summary>
    /// <param name="ws">The worksheet.</param>
    /// <param name="cell">The cell's address.</param>
    /// <returns>Its text, or the value's text when it isn't text.</returns>
    public static string Text(Worksheet ws, string cell) => Convert.ToString(Get(ws, cell), CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>A formula's whole spill, by rows: the cell's value alone when it doesn't spill.</summary>
    /// <param name="ws">The worksheet.</param>
    /// <param name="cell">The formula's cell.</param>
    /// <returns>The values, a row each.</returns>
    public static object?[][] Spill(Worksheet ws, string cell) => Retry(() =>
    {
        Range r = ws.Range[cell];
        if (!(bool)GetProp(r, "HasSpill")!)
        {
            return new[] { new[] { (object?)r.Value2 } };
        }

        return Rows(((Range)GetProp(r, "SpillingToRange")!).Value2);
    });

    /// <summary>A range's values, by rows.</summary>
    /// <param name="ws">The worksheet.</param>
    /// <param name="range">The range's address.</param>
    /// <returns>The values, a row each.</returns>
    public static object?[][] Range(Worksheet ws, string range) => Retry(() => Rows(ws.Range[range].Value2));

    /// <summary>A spill's first column.</summary>
    /// <param name="ws">The worksheet.</param>
    /// <param name="cell">The formula's cell.</param>
    /// <returns>The column's values.</returns>
    public static object?[] Column(Worksheet ws, string cell) => [.. Spill(ws, cell).Select(r => r[0])];

    /// <summary>Recalculates what changed, as Excel's automatic mode does.</summary>
    /// <param name="app">The Excel instance.</param>
    public static void Calc(Application app) => Retry(app.Calculate);

    /// <summary>A number's bits, as the plan's readings give them, or the value's text.</summary>
    /// <param name="v">The value.</param>
    /// <returns>Sixteen hexadecimal digits for a number.</returns>
    public static string Bits(object? v) => v is double d ? BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture) : $"{v}";

    /// <summary>A number's bits.</summary>
    /// <param name="d">The number.</param>
    /// <returns>Sixteen hexadecimal digits.</returns>
    public static string Bits(double d) => BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture);

    /// <summary>The first worksheet of a workbook.</summary>
    /// <param name="wb">The workbook.</param>
    /// <returns>Its first worksheet.</returns>
    public static Worksheet Sheet(Workbook wb) => Retry(() => (Worksheet)wb.Worksheets[1]);

    /// <summary>Runs an automation call, again while Excel rejects it as busy (RPC_E_CALL_REJECTED, RPC_E_SERVERCALL_RETRYLATER).</summary>
    /// <typeparam name="T">The call's result.</typeparam>
    /// <param name="f">The call.</param>
    /// <returns>Its result.</returns>
    public static T Retry<T>(Func<T> f)
    {
        for (int i = 0; ; i++)
        {
            try
            {
                return f();
            }
            catch (COMException e) when ((uint)e.HResult is 0x80010001 or 0x8001010A && i < 100)
            {
                Thread.Sleep(100);
            }
        }
    }

    /// <summary>Runs an automation call, again while Excel rejects it as busy.</summary>
    /// <param name="a">The call.</param>
    public static void Retry(Action a) => Retry(() =>
    {
        a();
        return 0;
    });

    /// <summary>A property Excel 365 added after the interop assembly's version, read late-bound.</summary>
    /// <param name="o">The object.</param>
    /// <param name="name">The property.</param>
    /// <returns>Its value.</returns>
    public static object? GetProp(object o, string name) => o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, null, CultureInfo.InvariantCulture);

    private static void Set(object o, string name, object value) => o.GetType().InvokeMember(name, BindingFlags.SetProperty, null, o, [value], CultureInfo.InvariantCulture);

    private static object?[][] Rows(object? value)
    {
        if (value is not object?[,] a)
        {
            return [[value]];
        }

        int r0 = a.GetLowerBound(0), c0 = a.GetLowerBound(1);
        return [.. Enumerable.Range(0, a.GetLength(0)).Select(i => Enumerable.Range(0, a.GetLength(1)).Select(j => a[r0 + i, c0 + j]).ToArray())];
    }
}
