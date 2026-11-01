// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using NanoQL.Time;

namespace NanoQL.Native;

/// <summary>Values across the C ABI: strings (UTF-8, NUL-terminated), dates (QuantLib's serial, 0 none) and memory.</summary>
internal static unsafe partial class Rt
{
    /// <summary>A C string's .NET string; NULL is null.</summary>
    /// <param name="p">The UTF-8 string.</param>
    /// <returns>The string.</returns>
    public static string Str(byte* p) => p == null ? null! : Marshal.PtrToStringUTF8((nint)p)!;

    /// <summary>A string for the caller, freed with <c>nql_free</c>; null is NULL.</summary>
    /// <param name="s">The string.</param>
    /// <returns>Its UTF-8 copy.</returns>
    public static byte* NewStr(string? s)
    {
        if (s is null)
        {
            return null;
        }

        int n = Encoding.UTF8.GetByteCount(s);
        byte* p = (byte*)NativeMemory.Alloc((nuint)n + 1);
        Encoding.UTF8.GetBytes(s, new Span<byte>(p, n));
        p[n] = 0;
        return p;
    }

    /// <summary>A date from its serial; 0 is QuantLib's <c>Date()</c>.</summary>
    /// <param name="serial">The serial.</param>
    /// <returns>The date.</returns>
    public static Date Date(int serial) => serial == 0 ? default : new Date(serial);

    /// <summary>A nullable date from its serial; 0 is none.</summary>
    /// <param name="serial">The serial.</param>
    /// <returns>The date, or null.</returns>
    public static Date? OptDate(int serial) => serial == 0 ? null : new Date(serial);

    /// <summary>Memory for the caller, freed with <c>nql_free</c>: at least one element, so it is never NULL.</summary>
    /// <typeparam name="T">The element.</typeparam>
    /// <param name="n">The count.</param>
    /// <returns>The memory.</returns>
    public static T* Alloc<T>(int n)
        where T : unmanaged => (T*)NativeMemory.Alloc((nuint)Math.Max(n, 1), (nuint)sizeof(T));

    /// <summary>A C period's value.</summary>
    /// <param name="p">The period.</param>
    /// <returns>The value.</returns>
    public static Period ToPeriod(this NqlPeriod p) => new(p.Length, (TimeUnit)p.Units);

    /// <summary>A C complex number's value.</summary>
    /// <param name="c">The number.</param>
    /// <returns>The value.</returns>
    public static Complex ToComplex(this NqlComplex c) => new(c.Re, c.Im);
}
