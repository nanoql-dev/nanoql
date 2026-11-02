// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>
/// A batch of draws behind a handle (XLR2): the values (a column, or a draw per row for sequences), their weights and
/// the next snapshot, read through <c>nqlxlDrawsValues</c>, <c>nqlxlDrawsWeights</c> and <c>nqlxlDrawsNext</c>.
/// </summary>
/// <param name="values">The values: a <see cref="T:double[]"/>, or a <see cref="T:double[,]"/> by draws.</param>
/// <param name="weights">The weights.</param>
/// <param name="next">The next snapshot.</param>
public sealed class XlDraws(object values, double[] weights, XlSnapshot next)
{
    /// <summary>Gets the values.</summary>
    public object Values { get; } = values;

    /// <summary>Gets the weights.</summary>
    public IReadOnlyList<double> Weights { get; } = weights;

    /// <summary>Gets the next snapshot.</summary>
    public XlSnapshot Next { get; } = next;
}
