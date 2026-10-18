// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Harness;

/// <summary>
/// The index through which <see cref="VectorCase.Context"/> stores a case's fixings: named by a key of the case's
/// <c>inputs.settings.fixings</c>, it accepts every date. The store keys histories by name, as QuantLib does, so the
/// case's own index of that name reads them.
/// </summary>
/// <param name="name">The name, as QuantLib formed it for the case's index.</param>
internal sealed class VectorFixingIndex(string name) : FinancialIndex
{
    /// <inheritdoc/>
    public override string Name { get; } = name;

    /// <inheritdoc/>
    public override Calendar FixingCalendar { get; } = new NullCalendar();

    /// <inheritdoc/>
    public override bool IsValidFixingDate(Date fixingDate) => true;

    /// <inheritdoc/>
    public override double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false) =>
        throw new InvalidOperationException("the index of a case's fixings forecasts nothing");
}
