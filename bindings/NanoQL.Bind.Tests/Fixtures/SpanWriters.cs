// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>Members writing into their argument through a span, each a shape the IL scan follows, and one reading.</summary>
public static class SpanWriters
{
    /// <summary>Through a span held in a local.</summary>
    /// <param name="v">The vector, written.</param>
    public static void ThroughLocal(Vector v)
    {
        Span<double> s = v.AsSpan();
        s[0] = 2;
    }

    /// <summary>A span's fill.</summary>
    /// <param name="v">The vector, written.</param>
    public static void Fill(Vector v) => v.AsSpan().Fill(3);

    /// <summary>An array copied into the span.</summary>
    /// <param name="v">The vector, written.</param>
    public static void CopyInto(Vector v) => new double[] { 4 }.CopyTo(v.AsSpan());

    /// <summary>A helper writing the span it takes.</summary>
    /// <param name="v">The vector, written.</param>
    public static void ThroughHelper(Vector v) => FillSpan(v.AsSpan());

    /// <summary>A matrix's row, written.</summary>
    /// <param name="m">The matrix, written.</param>
    public static void Row(Matrix m) => m.Row(0)[0] = 6;

    /// <summary>A read through the span.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>Its first element.</returns>
    public static double Reader(Vector v) => v.AsSpan()[0];

    private static void FillSpan(Span<double> s) => s[0] = 5;
}
