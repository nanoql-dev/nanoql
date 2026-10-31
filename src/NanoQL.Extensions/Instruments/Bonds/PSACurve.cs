// SPDX-FileCopyrightText: Copyright (C) 2008-2022 Andrea Maggiulli (a.maggiulli@gmail.com)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: src/QLNet/Instruments/Bonds/PSACurve.cs @ QLNet-v1.13.2
// Ported from QLNet and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Extensions.Instruments;

/// <summary>
/// The Public Securities Association's prepayment benchmark at a multiple of its speed: a CPR rising by 0.2% a month
/// from 0.2% in the first month to 6% in the thirtieth, and 6% from then on, times the multiplier.
/// </summary>
/// <remarks>
/// <para>
/// The rates follow SIFMA's standard formula (Uniform Practices, Chapter SF, SF-6:
/// <c>CPR = min{100, PSA/100 · 0.2 · max{1, min{MONTH, 30}}}</c>), MONTH "the accrual period during which the age of
/// the mortgage loans increases from MONTH – 1 to MONTH": the whole calendar months from the curve's start, plus one,
/// so that a date's MONTH is k + 1 from the start's k-month anniversary until the next. Three departures from QLNet
/// follow it. QLNet counts the month as the 30/360 Bond Basis days from the start over 30, plus one, which from a start
/// on the 29th to the 31st repeats a month at February's anniversary and skips the next, and counts a 31st a month
/// ahead from a start on the 1st; the CPR is capped at 100%, where QLNet's passes it above 1,666⅔% PSA and its SMM is
/// NaN; and every month before the first counts as the first ("MONTH equals 1 for all prior months"), where QLNet's
/// falls to 0 and below. Elsewhere the rates are QLNet's, bit for bit.
/// </para>
/// <para>The SMM is the CPR's monthly equivalent, <c>1 − (1 − CPR)^(1/12)</c> (SF-5).</para>
/// </remarks>
public sealed class PSACurve : IPrepayModel
{
    private readonly Date startDate;
    private readonly double multiplier;

    /// <summary>Initializes a new instance of the <see cref="PSACurve"/> class.</summary>
    /// <param name="startDate">The date the curve's first month starts.</param>
    /// <param name="multiplier">The speed as a multiple of the benchmark's: 1 for 100% PSA, 1.5 for 150%.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="multiplier"/> is negative or not finite.
    /// </exception>
    public PSACurve(Date startDate, double multiplier = 1.0)
    {
        if (!(multiplier >= 0.0) || double.IsPositiveInfinity(multiplier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier), multiplier, "a PSA speed's multiplier is a finite number, 0 or more");
        }

        this.startDate = startDate;
        this.multiplier = multiplier;
    }

    /// <inheritdoc/>
    public double GetCPR(Date valDate)
    {
        // SF-6's MONTH, the calendar months from the start plus one; SIFMA's floor, a month before the curve's first
        // counting as the first.
        int month = Math.Max(1, MonthsFrom(startDate, valDate) + 1);
        double cpr = (month <= 30 ? 0.06 * (month / 30d) : 0.06) * multiplier;

        // SIFMA's cap (SF-6): a CPR of at most 100%.
        return Math.Min(cpr, 1.0);
    }

    /// <inheritdoc/>
    public double GetSMM(Date valDate) => 1 - Math.Pow(1 - GetCPR(valDate), 1 / 12d);

    /// <summary>
    /// The whole calendar months from <paramref name="start"/> to <paramref name="d"/>: the largest k with the start's
    /// k-month anniversary, by <see cref="Date"/>'s arithmetic (a day past a month's end its last day), on or before
    /// the date; negative before the start.
    /// </summary>
    private static int MonthsFrom(Date start, Date d)
    {
        int months = (12 * (d.Year - start.Year)) + (d.Month - start.Month);
        if (d.DayOfMonth < Math.Min(start.DayOfMonth, Date.EndOfMonth(d).DayOfMonth))
        {
            months--;
        }

        return months;
    }
}
