// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/coupons/swapspreadindex.hpp ql/experimental/coupons/swapspreadindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A swap-rate spread index: gearing₁ times one swap index's rate plus gearing₂ times another's.
/// </summary>
/// <remarks>
/// The two indexes must share their fixing days and calendar, currency, day counter, and fixed leg's tenor and
/// convention; the index takes the first one's tenor, fixing days, currency, calendar and day counter. Its name is
/// QuantLib's, "&lt;name₁&gt;(&lt;gearing₁&gt;) + &lt;name₂&gt;(&lt;gearing₂&gt;)", each gearing to four decimals (the
/// S-g1d plan's G1D8). It stores no fixings of its own: a past fixing is the two indexes' past fixings combined.
/// Immutable. Experimental, as in QuantLib.
/// </remarks>
[Experimental("NQL0001")]
public sealed class SwapSpreadIndex : InterestRateIndex
{
    private readonly string spreadName;

    /// <summary>Initializes a new instance of the <see cref="SwapSpreadIndex"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="swapIndex1">The first swap index.</param>
    /// <param name="swapIndex2">The second swap index.</param>
    /// <param name="gearing1">The first index's gearing.</param>
    /// <param name="gearing2">The second index's gearing.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The two indexes differ in their fixing days, fixing calendar, currency, day
    /// counter, or fixed leg's tenor or convention.</exception>
    public SwapSpreadIndex(string familyName, SwapIndex swapIndex1, SwapIndex swapIndex2, double gearing1 = 1.0, double gearing2 = -1.0)
        : base(
            familyName,
            First(swapIndex1).Tenor, // does not make sense, but we have to provide one
            swapIndex1.FixingDays,
            swapIndex1.Currency,
            swapIndex1.FixingCalendar,
            swapIndex1.DayCounter)
    {
        ArgumentNullException.ThrowIfNull(swapIndex2);
        SwapIndex1 = swapIndex1;
        SwapIndex2 = swapIndex2;
        Gearing1 = gearing1;
        Gearing2 = gearing2;
        spreadName = $"{swapIndex1.Name}({gearing1.ToString("F4", CultureInfo.InvariantCulture)}) + "
            + $"{swapIndex2.Name}({gearing2.ToString("F4", CultureInfo.InvariantCulture)})";

        if (!(swapIndex1.FixingDays == swapIndex2.FixingDays))
        {
            throw new ArgumentException(
                $"index1 fixing days ({swapIndex1.FixingDays})must be equal to index2 fixing days ({swapIndex2.FixingDays})", nameof(swapIndex2));
        }

        if (!(swapIndex1.FixingCalendar == swapIndex2.FixingCalendar))
        {
            throw new ArgumentException(
                $"index1 fixingCalendar ({swapIndex1.FixingCalendar.Name})must be equal to index2 fixingCalendar ({swapIndex2.FixingCalendar.Name})",
                nameof(swapIndex2));
        }

        if (!(swapIndex1.Currency == swapIndex2.Currency))
        {
            throw new ArgumentException(
                $"index1 currency ({CurrencyText(swapIndex1.Currency)})must be equal to index2 currency ({CurrencyText(swapIndex2.Currency)})",
                nameof(swapIndex2));
        }

        if (!(swapIndex1.DayCounter == swapIndex2.DayCounter))
        {
            throw new ArgumentException(
                $"index1 dayCounter ({swapIndex1.DayCounter.Name})must be equal to index2 dayCounter ({swapIndex2.DayCounter.Name})", nameof(swapIndex2));
        }

        // QuantLib's texts, their closing parentheses missing
        if (!(swapIndex1.FixedLegTenor == swapIndex2.FixedLegTenor))
        {
            throw new ArgumentException(
                $"index1 fixedLegTenor ({PeriodFunctions.ShortPeriod(swapIndex1.FixedLegTenor)})must be equal to index2 fixedLegTenor "
                    + $"({PeriodFunctions.ShortPeriod(swapIndex2.FixedLegTenor)}",
                nameof(swapIndex2));
        }

        if (!(swapIndex1.FixedLegConvention == swapIndex2.FixedLegConvention))
        {
            throw new ArgumentException(
                $"index1 fixedLegConvention ({BusinessDayConventionFunctions.ToString(swapIndex1.FixedLegConvention)})must be equal to index2 "
                    + $"fixedLegConvention ({BusinessDayConventionFunctions.ToString(swapIndex2.FixedLegConvention)}",
                nameof(swapIndex2));
        }
    }

    /// <summary>Gets the first swap index.</summary>
    public SwapIndex SwapIndex1 { get; }

    /// <summary>Gets the second swap index.</summary>
    public SwapIndex SwapIndex2 { get; }

    /// <summary>Gets the first index's gearing.</summary>
    public double Gearing1 { get; }

    /// <summary>Gets the second index's gearing.</summary>
    public double Gearing2 { get; }

    /// <inheritdoc/>
    public override string Name => spreadName;

    /// <summary>Gets a value indicating whether the index stores fixings of its own: <see langword="false"/>.</summary>
    public override bool AllowsNativeFixings => false;

    /// <summary>A spread index has no single maturity date.</summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>Never.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override Date MaturityDate(Date valueDate) => throw new NanoQLException("SwapSpreadIndex does not provide a single maturity date");

    /// <summary>The forecast fixing: each index's fixing, a historic one on the evaluation date included, geared and
    /// summed.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    public override double ForecastFixing(Date fixingDate, EvaluationContext context) =>
        // this also handles the case when one of indices has
        // a historic fixing on the evaluation date
        (Gearing1 * SwapIndex1.Fixing(fixingDate, context, false)) + (Gearing2 * SwapIndex2.Fixing(fixingDate, context, false));

    /// <summary>The past fixing: the two indexes' past fixings geared and summed, or none where either is
    /// missing.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing, or <see langword="null"/>.</returns>
    public override double? PastFixing(Date fixingDate, EvaluationContext context)
    {
        double? f1 = SwapIndex1.PastFixing(fixingDate, context);
        double? f2 = SwapIndex2.PastFixing(fixingDate, context);

        // if one of the fixings is missing we return null, indicating
        // a missing fixing for the spread index
        if (f1 is not double first || f2 is not double second)
        {
            return null;
        }
        else
        {
            return (Gearing1 * first) + (Gearing2 * second);
        }
    }

    private static SwapIndex First(SwapIndex swapIndex1)
    {
        ArgumentNullException.ThrowIfNull(swapIndex1);
        return swapIndex1;
    }

    // QuantLib's operator<< for a currency: its code, or "null currency".
    private static string CurrencyText(Currency? currency) => currency is null ? "null currency" : currency.Code;
}
