// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Xml;

namespace NanoQL.Bind.TestTypes;

/// <summary>Each refusal kind, raised on request, so each status code can be checked.</summary>
public static class Refusals
{
    /// <summary>Throws the exception of a kind.</summary>
    /// <param name="kind">
    /// 1 <see cref="NanoQLException"/>, 2 <see cref="ArgumentException"/>, 3 <see cref="ArgumentNullException"/>, 4
    /// <see cref="ArgumentOutOfRangeException"/>, 5 <see cref="InvalidOperationException"/>, 6
    /// <see cref="InvalidCastException"/>, 7 <see cref="NotSupportedException"/>, 8 <see cref="KeyNotFoundException"/>,
    /// 11 <see cref="OutOfMemoryException"/>, 12 <see cref="FileNotFoundException"/>, 13 <see cref="XmlException"/>,
    /// 14 <see cref="UnauthorizedAccessException"/>, 15 <see cref="FormatException"/>, anything else a
    /// <see cref="NullReferenceException"/>, a defect's kind.
    /// </param>
    /// <returns>Never returns.</returns>
    public static double Throw(int kind) => throw (kind switch
    {
        1 => new NanoQLException("a QuantLib check"),
        2 => new ArgumentException("a bad argument", "x"),
        3 => new ArgumentNullException("x"),
        4 => new ArgumentOutOfRangeException("x", "out of range"),
        5 => new InvalidOperationException("an invalid operation"),
        6 => new InvalidCastException("a bad cast"),
        7 => new NotSupportedException("not supported"),
        8 => new KeyNotFoundException("no such name"),
        11 => new OutOfMemoryException("out of memory"),
        12 => new FileNotFoundException("no such file", "missing.xml"),
        13 => new XmlException("malformed"),
        14 => new UnauthorizedAccessException("denied"),
        15 => new FormatException("malformed"),
        _ => (Exception)new NullReferenceException("a defect"),
    });

    /// <summary>An element of a fixed array, its index unchecked.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The element.</returns>
    /// <exception cref="IndexOutOfRangeException">The index is outside the array.</exception>
    public static double DocumentedElement(int i) => new[] { 1.0, 2.0, 3.0 }[i];

    /// <summary>An element of a fixed array, its index unchecked and its refusal undocumented: a defect's kind.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The element.</returns>
    public static double UndocumentedElement(int i) => new[] { 1.0, 2.0, 3.0 }[i];
}
