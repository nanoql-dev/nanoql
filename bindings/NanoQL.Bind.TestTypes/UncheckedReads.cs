// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.TestTypes;

/// <summary>
/// Members that read an array or index one without a check, each past its end under one of the Python replayer's
/// widened values alone, so the sweep can be seen reding on each kind of them; and each one's checked twin, which
/// refuses that input instead.
/// </summary>
public static class UncheckedReads
{
    private static readonly double[] Table = [1.0, 2.0, 3.0];

    /// <summary>The first value, read past an empty array's end.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The first.</returns>
    public static double First(double[] values) => values[0];

    /// <summary>The first value, an empty array refused.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The first.</returns>
    /// <exception cref="ArgumentException"><paramref name="values"/> is empty.</exception>
    public static double FirstChecked(double[] values) =>
        values.Length > 0 ? values[0] : throw new ArgumentException("no value given", nameof(values));

    /// <summary>A non-empty array's second value, read past a one-element array's end.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The second, or 0 for an empty array.</returns>
    public static double Second(double[] values) => values.Length == 0 ? 0.0 : values[1];

    /// <summary>A non-empty array's second value, a one-element array refused.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The second, or 0 for an empty array.</returns>
    /// <exception cref="ArgumentException"><paramref name="values"/> holds one value.</exception>
    public static double SecondChecked(double[] values) =>
        values.Length == 1 ? throw new ArgumentException("one value given", nameof(values)) : Second(values);

    /// <summary>The values' sum, copied into a buffer of three element by element, written past it for more.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The sum.</returns>
    public static double IntoThree(double[] values)
    {
        double[] buffer = new double[3];
        for (int i = 0; i < values.Length; i++)
        {
            buffer[i] = values[i];
        }

        return buffer.Sum();
    }

    /// <summary>The values' sum through the buffer of three, more values refused.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The sum.</returns>
    /// <exception cref="ArgumentException"><paramref name="values"/> holds more than three values.</exception>
    public static double IntoThreeChecked(double[] values) =>
        values.Length <= 3
            ? IntoThree(values)
            : throw new ArgumentException("more than three values given", nameof(values));

    /// <summary>An element of a fixed array, an index past it refused and a negative one read.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is past the array.</exception>
    public static double BelowLength(int index) =>
        index < Table.Length ? Table[index] : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>An element of a fixed array, an index outside it refused.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the array.</exception>
    public static double BelowLengthChecked(int index) =>
        index >= 0 ? BelowLength(index) : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>An element of a fixed array, a negative index refused and one past it read.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public static double NonNegative(int index) =>
        index >= 0 ? Table[index] : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>An element of a fixed array, an index outside it refused.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the array.</exception>
    public static double NonNegativeChecked(int index) =>
        index < Table.Length ? NonNegative(index) : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Two arrays, read past their ends when both are empty.</summary>
    /// <param name="a">The first values.</param>
    /// <param name="b">The second values.</param>
    /// <returns>0, but for two empty arrays.</returns>
    public static double BothEmpty(double[] a, double[] b) => a.Length == 0 && b.Length == 0 ? a[0] : 0.0;

    /// <summary>Two arrays, both empty refused.</summary>
    /// <param name="a">The first values.</param>
    /// <param name="b">The second values.</param>
    /// <returns>0.</returns>
    /// <exception cref="ArgumentException">Both arrays are empty.</exception>
    public static double BothEmptyChecked(double[] a, double[] b) =>
        a.Length == 0 && b.Length == 0 ? throw new ArgumentException("no values given", nameof(a)) : 0.0;

    /// <summary>Two arrays, read past their ends when both hold one value.</summary>
    /// <param name="a">The first values.</param>
    /// <param name="b">The second values.</param>
    /// <returns>0, but for two one-element arrays.</returns>
    public static double BothOne(double[] a, double[] b) => a.Length == 1 && b.Length == 1 ? a[1] : 0.0;

    /// <summary>Two arrays, both of one value refused.</summary>
    /// <param name="a">The first values.</param>
    /// <param name="b">The second values.</param>
    /// <returns>0.</returns>
    /// <exception cref="ArgumentException">Both arrays hold one value.</exception>
    public static double BothOneChecked(double[] a, double[] b) =>
        a.Length == 1 && b.Length == 1 ? throw new ArgumentException("one value each given", nameof(a)) : 0.0;

    /// <summary>Two arrays, the fixed array read past its end at the first's length when both hold more than
    /// three.</summary>
    /// <param name="a">The first values.</param>
    /// <param name="b">The second values.</param>
    /// <returns>0, but for two arrays of more than three values.</returns>
    public static double BothMany(double[] a, double[] b) => a.Length > 3 && b.Length > 3 ? Table[a.Length] : 0.0;

    /// <summary>Two arrays, both of more than three values refused.</summary>
    /// <param name="a">The first values.</param>
    /// <param name="b">The second values.</param>
    /// <returns>0.</returns>
    /// <exception cref="ArgumentException">Both arrays hold more than three values.</exception>
    public static double BothManyChecked(double[] a, double[] b) =>
        a.Length > 3 && b.Length > 3
            ? throw new ArgumentException("more than three values each given", nameof(a))
            : 0.0;
}
