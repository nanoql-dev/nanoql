// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Checks the cases of the ASX and IMM date functions' vector files (<c>tools/vectors/gen/time/asx.py</c>'s functions,
/// which <c>time/imm.py</c> shares).
/// </summary>
/// <remarks>
/// A case's <c>"kind"</c> is <c>"dates"</c> (a list of dates, each date's check on both cycles, next date and next
/// code), <c>"codes"</c> (a list of strings and four reference dates: each string's check on both cycles, its date
/// with no reference date and at each, its next date and next code), <c>"code"</c> (a list of dates, each one's code)
/// or <c>"today"</c> (the next date and code with no date, from the context's evaluation date).
/// </remarks>
internal static class DateFunctionInputs
{
    /// <summary>Compares a case's outputs.</summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="f">The date module's functions.</param>
    public static void Check(VectorAssert check, VectorCase c, Functions f)
    {
        EvaluationContext context = c.Context();
        switch (c.String("kind"))
        {
            case "dates":
            {
                Date[] dates = c.DateInputs("dates");
                check.Output(c, "isDateMain", dates.Select(d => f.IsDate(d, true)).ToArray());
                check.Output(c, "isDateAny", dates.Select(d => f.IsDate(d, false)).ToArray());
                check.Output(c, "nextDateMain", Each(dates, d => f.NextDate(context, d, true)));
                check.Output(c, "nextDateAny", Each(dates, d => f.NextDate(context, d, false)));
                check.Output(c, "nextCodeMain", Each(dates, d => f.NextCode(context, d, true)));
                check.Output(c, "nextCodeAny", Each(dates, d => f.NextCode(context, d, false)));
                break;
            }

            case "codes":
            {
                string[] codes = [.. c.Input("codes").EnumerateArray().Select(e => e.GetString()!)];
                Date[] refs = c.DateInputs("referenceDates");
                check.Output(c, "isCodeMain", codes.Select(s => f.IsCode(s, true)).ToArray());
                check.Output(c, "isCodeAny", codes.Select(s => f.IsCode(s, false)).ToArray());
                check.Output(c, "date", Each(codes, s => f.DateOfCode(s, context, null)));
                check.Output(c, "dateAt", codes.Select(s => Each(refs, r => f.DateOfCode(s, context, r))).ToArray());
                check.Output(c, "nextDateMain", Each(codes, s => f.NextDateOfCode(s, context, true, null)));
                check.Output(c, "nextDateAny", Each(codes, s => f.NextDateOfCode(s, context, false, refs[0])));
                check.Output(c, "nextCodeMain", Each(codes, s => f.NextCodeOfCode(s, context, true, refs[2])));
                check.Output(c, "nextCodeAny", Each(codes, s => f.NextCodeOfCode(s, context, false, null)));
                break;
            }

            case "code":
                check.Output(c, "code", Each(c.DateInputs("dates"), d => f.Code(d)));
                break;
            case "today":
                check.Output(c, "nextDateMain", f.NextDate(context, null, true));
                check.Output(c, "nextDateAny", f.NextDate(context, null, false));
                check.Output(c, "nextCodeMain", f.NextCode(context, null, true));
                check.Output(c, "nextCodeAny", f.NextCode(context, null, false));
                break;
            default:
                Assert.Fail($"unexpected case kind in '{c.Name}'");
                break;
        }
    }

    private static Func<object?>[] Each<T, TOut>(IEnumerable<T> xs, Func<T, TOut> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];

    /// <summary>A date module's functions: <see cref="ASX"/>'s or <see cref="IMM"/>'s.</summary>
    /// <param name="IsDate">Whether a date is a date of the module, on the main cycle or not.</param>
    /// <param name="IsCode">Whether a string is a code, on the main cycle or not.</param>
    /// <param name="Code">A date's code.</param>
    /// <param name="DateOfCode">A code's date, from a reference date or the context's evaluation date.</param>
    /// <param name="NextDate">The next date after a date or the context's evaluation date.</param>
    /// <param name="NextDateOfCode">The next date after a code's date.</param>
    /// <param name="NextCode">The next code after a date or the context's evaluation date.</param>
    /// <param name="NextCodeOfCode">The next code after a code's date.</param>
    internal sealed record Functions(
        Func<Date, bool, bool> IsDate,
        Func<string, bool, bool> IsCode,
        Func<Date, string> Code,
        Func<string, EvaluationContext, Date?, Date> DateOfCode,
        Func<EvaluationContext, Date?, bool, Date> NextDate,
        Func<string, EvaluationContext, bool, Date?, Date> NextDateOfCode,
        Func<EvaluationContext, Date?, bool, string> NextCode,
        Func<string, EvaluationContext, bool, Date?, string> NextCodeOfCode);
}
