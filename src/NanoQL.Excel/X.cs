// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>
/// The runtime the generated worksheet functions call (docs/excel.md): each call runs through <see cref="Run"/>, which
/// keys an object-returning call and presents its handle, or <see cref="RunVal"/>, which returns a value; their
/// arguments convert through this class's readers, a range where a function takes one value looping over its elements.
/// A failure becomes Excel's error for its code, its message kept by the calling cell for <c>nqlxlError</c>.
/// </summary>
public static partial class X
{
    /// <summary>The value a call returning nothing shows.</summary>
    public static readonly object Done = "OK";

    // The calls made, objects built and store hits, and handles shown off Excel's main thread, for nqlxlStats.
    private static long calls;
    private static long builds;
    private static long hits;
    private static long offMain;

    /// <summary>Gets or sets what the runtime reads from Excel: the calling cell, values, spills, the date system, RTD.</summary>
    internal static IXlHost Host { get; set; } = new ExcelHost();

    /// <summary>Gets or sets Excel's main thread, the managed one the add-in opened on; 0 before it opens.</summary>
    internal static int MainThread { get; set; }

    /// <summary>Gets whether an Excel value is no value at all: a missing argument or an empty cell.</summary>
    /// <param name="a">The value.</param>
    /// <returns>Whether it is missing or empty.</returns>
    public static bool IsMissing(object? a) => a is ExcelMissing || a is ExcelEmpty;

    /// <summary>Runs a call that returns a value, looping over the arguments the mask marks when a range is given.</summary>
    /// <param name="fn">The worksheet function's name.</param>
    /// <param name="args">The Excel arguments.</param>
    /// <param name="body">The call, on converted arguments.</param>
    /// <param name="documented">The codes the member documents for exceptions outside the C ABI's table, or null.</param>
    /// <param name="lift">The loop mask: a character per argument, '1' where a range loops; null for none.</param>
    /// <returns>The Excel result.</returns>
    public static object RunVal(string fn, object[] args, Func<object[], object?> body, Func<Exception, int>? documented = null, string? lift = null)
        => lift != null && LiftShape(args = Cells1(args, lift), lift, out int rows, out int cols)
            ? Lifted(fn, args, lift, rows, cols, e => RunValOne(fn, e, body, documented))
            : RunValOne(fn, args, body, documented);

    /// <summary>Runs a call that returns an object, behind its handle, looping as <see cref="RunVal"/> does.</summary>
    /// <param name="fn">The worksheet function's name.</param>
    /// <param name="args">The Excel arguments.</param>
    /// <param name="body">The call, on converted arguments.</param>
    /// <param name="documented">The codes the member documents for exceptions outside the C ABI's table, or null.</param>
    /// <param name="lift">The loop mask, or null.</param>
    /// <returns>The handle, or Excel's error.</returns>
    public static object Run(string fn, object[] args, Func<object[], object?> body, Func<Exception, int>? documented = null, string? lift = null)
        => lift != null && LiftShape(args = Cells1(args, lift), lift, out int rows, out int cols)
            ? Lifted(fn, args, lift, rows, cols, e => RunOne(fn, e, body, documented))
            : RunOne(fn, args, body, documented);

    /// <summary>
    /// The live handles, calls, builds and hits, the handles shown off Excel's main thread, the looped calls and their
    /// elements, and the names built, for nqlxlStats in a test build.
    /// </summary>
    /// <returns>The counts' text.</returns>
    public static string StatsText() => string.Create(
        CultureInfo.InvariantCulture,
        $"handles {ByHandle.Count} calls {Interlocked.Read(ref calls)} builds {Interlocked.Read(ref builds)} hits {Interlocked.Read(ref hits)} offmain {Interlocked.Read(ref offMain)} lifted {Interlocked.Read(ref liftedCalls)}/{Interlocked.Read(ref liftedElements)} named {Interlocked.Read(ref namedBuilds)}");

    /// <summary>
    /// Counts a handle shown off Excel's main thread, where Excel can refuse the RTD call that shows it.
    /// </summary>
    internal static void ShownOffMain() => Interlocked.Increment(ref offMain);

    /// <summary>
    /// The C ABI's error code for an exception: the runtime's own kinds first (an unknown handle 9, though it derives
    /// from <see cref="ArgumentException"/>; a failed settings load, a family's refusal 2), then the code the member
    /// documents, then the runtime's copy of the C ABI's table.
    /// </summary>
    /// <param name="e">The exception.</param>
    /// <param name="documented">The member's documented codes, or null.</param>
    /// <returns>The code.</returns>
    internal static int BirCode(Exception e, Func<Exception, int>? documented = null)
    {
        if (e is XlHandleException)
        {
            return 9;
        }

        if (e is XlSettingsException)
        {
            return 2;
        }

        int d = documented?.Invoke(e) ?? 0;
        return d != 0 ? d : CopyOfRtCode(e);
    }

    /// <summary>The C ABI's code for an exception, as the native host's table gives it; a test checks the copy.</summary>
    /// <param name="e">The exception.</param>
    /// <returns>The code.</returns>
    internal static int CopyOfRtCode(Exception e) => e switch
    {
        NanoQLException => 1,
        ArgumentNullException => 3,
        ArgumentOutOfRangeException => 4,
        ArgumentException => 2,
        InvalidCastException => 6,
        KeyNotFoundException => 8,
        System.Xml.Schema.XmlSchemaValidationException or System.Xml.XmlException or FormatException => 13,
        IOException or UnauthorizedAccessException => 12,
        NotSupportedException => 7,
        InvalidOperationException => 5,
        OutOfMemoryException => 11,
        _ => 99,
    };

    /// <summary>
    /// The Excel error for a code: #VALUE! for refused arguments, kinds, casts, names, files and features; #REF! for a
    /// handle this session doesn't hold; #NUM! for QuantLib's own checks, failed calculations, memory and anything
    /// else.
    /// </summary>
    /// <param name="code">The C ABI's error code.</param>
    /// <returns>The error.</returns>
    public static ExcelError ExcelOf(int code) => code switch
    {
        2 or 3 or 4 or 6 or 7 or 8 or 10 or 12 or 13 => ExcelError.ExcelErrorValue,
        9 => ExcelError.ExcelErrorRef,
        _ => ExcelError.ExcelErrorNum,
    };

    private static object RunValOne(string fn, object[] args, Func<object[], object?> body, Func<Exception, int>? documented)
    {
        Interlocked.Increment(ref calls);
        try
        {
            if (Array.Find(args, a => a is ExcelError) is ExcelError passed)
            {
                // Excel's own errors pass through, as built-in functions pass them.
                KeepCall(fn, args, new XlPassed(passed));
                return passed;
            }

            object? raw = body(args);
            object r = Present(fn, args, raw);
            KeepCall(fn, args, r);
            return r;
        }
        catch (Exception e)
        {
            return Fail(fn, args, e, documented);
        }
    }

    private static object RunOne(string fn, object[] args, Func<object[], object?> body, Func<Exception, int>? documented)
    {
        Interlocked.Increment(ref calls);
        try
        {
            if (Array.Find(args, a => a is ExcelError) is ExcelError passed)
            {
                KeepCall(fn, args, new XlPassed(passed));
                return passed;
            }

            object r = Wrap(fn, args, body);
            KeepCall(fn, args, r);
            return r;
        }
        catch (Exception e)
        {
            return Fail(fn, args, e, documented);
        }
    }

    // A failed call: its message kept by the calling cell (or the looped element's place), its Excel error returned.
    private static ExcelError Fail(string fn, object[] args, Exception e, Func<Exception, int>? documented)
    {
        if (e is System.Reflection.TargetInvocationException { InnerException: { } inner })
        {
            e = inner;
        }

        string msg = $"{fn}: {e.GetType().Name}: {e.Message}" + (e is ArgumentException { ParamName: { } pn } && !e.Message.Contains(pn, StringComparison.Ordinal) ? $" (parameter {pn})" : string.Empty);
        ExcelError code = ExcelOf(BirCode(e, documented));
        if (inLift)
        {
            liftMessage = msg;
        }
        else if (CallerKey() is { } caller)
        {
            Errors[caller] = msg;
        }

        KeepCall(fn, args, new XlFailure(msg, code));
        return code;
    }
}
