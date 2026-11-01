// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Schema;

namespace NanoQL.Native;

/// <summary>
/// Errors across the C ABI: each refusal kind's code (docs/bindings.md, "Errors"), and the calling thread's last refusal:
/// its code, message, parameter and .NET type, kept until the thread's next refusal.
/// </summary>
internal static unsafe partial class Rt
{
    [ThreadStatic]
    private static int lastCode;

    [ThreadStatic]
    private static byte* lastMessage;

    [ThreadStatic]
    private static byte* lastParam;

    [ThreadStatic]
    private static byte* lastType;

    /// <summary>Gets the calling thread's last refusal's code.</summary>
    public static int LastCode => lastCode;

    /// <summary>Gets the calling thread's last refusal's message.</summary>
    public static byte* LastMessage => lastMessage;

    /// <summary>Gets the calling thread's last refusal's parameter, or NULL.</summary>
    public static byte* LastParam => lastParam;

    /// <summary>Gets the calling thread's last refusal's .NET type.</summary>
    public static byte* LastType => lastType;

    /// <summary>The code of an exception's kind, recorded as the thread's last refusal.</summary>
    /// <param name="e">The exception.</param>
    /// <param name="documented">The code a member's documentation gives a kind outside the table, or 0.</param>
    /// <returns>The code.</returns>
    public static int Fail(Exception e, int documented = 0)
    {
        int code = documented != 0 ? documented : Code(e);
        string? param = e is ArgumentException a ? a.ParamName : (e as HandleException)?.Param;
        return SetError(code, e.Message, param, e.GetType().FullName);
    }

    /// <summary>An exception kind's code.</summary>
    /// <param name="e">The exception.</param>
    /// <returns>Its code; 99 for a kind outside the table, a defect to report.</returns>
    public static int Code(Exception e) => e switch
    {
        HandleException h => h.Code,
        NanoQLException => 1,
        ArgumentNullException => 3,
        ArgumentOutOfRangeException => 4,
        ArgumentException => 2,
        InvalidCastException => 6,
        KeyNotFoundException => 8,
        XmlSchemaValidationException or XmlException or FormatException => 13,
        IOException or UnauthorizedAccessException => 12,
        NotSupportedException => 7,
        InvalidOperationException => 5,
        OutOfMemoryException => 11,
        _ => 99,
    };

    /// <summary>Records the calling thread's last refusal.</summary>
    /// <param name="code">Its code.</param>
    /// <param name="message">Its message.</param>
    /// <param name="param">Its parameter, or null.</param>
    /// <param name="type">Its .NET type's name, or null.</param>
    /// <returns>The code.</returns>
    public static int SetError(int code, string message, string? param, string? type)
    {
        FreeLast();
        lastCode = code;
        lastMessage = NewStr(message);
        lastParam = NewStr(param);
        lastType = NewStr(type);
        return code;
    }

    private static void FreeLast()
    {
        NativeMemory.Free(lastMessage);
        NativeMemory.Free(lastParam);
        NativeMemory.Free(lastType);
        lastMessage = lastParam = lastType = null;
        lastCode = 0;
    }
}
