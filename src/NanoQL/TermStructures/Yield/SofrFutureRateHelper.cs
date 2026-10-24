// SPDX-FileCopyrightText: Copyright (C) 2018 Roy Zywina
// SPDX-FileCopyrightText: Copyright (C) 2019 Eisuke Tani
// SPDX-FileCopyrightText: Copyright (C) 2019, 2020 Eisuke Tani
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/overnightindexfutureratehelper.hpp ql/termstructures/yield/overnightindexfutureratehelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A rate helper on a CME SOFR future: overnight SOFR rates from the reference month's third Wednesday, inclusive, to
/// the third Wednesday a quarter later, exclusive, compounded; or over the reference month, averaged.
/// </summary>
/// <remarks>A reference period that starts in the past reads the index's stored fixings.</remarks>
public sealed class SofrFutureRateHelper : OvernightIndexFutureRateHelper
{
    /// <summary>Initializes a new instance of the <see cref="SofrFutureRateHelper"/> class.</summary>
    /// <param name="price">The future's price.</param>
    /// <param name="referenceMonth">The reference month.</param>
    /// <param name="referenceYear">The reference year.</param>
    /// <param name="referenceFreq">The reference period: <see cref="Frequency.Quarterly"/> (compounded) or
    /// <see cref="Frequency.Monthly"/> (averaged).</param>
    /// <param name="convexityAdjustment">The convexity adjustment.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">The frequency is neither monthly nor quarterly, QuantLib's "only monthly
    /// and quarterly SOFR futures accepted"; or a custom pillar is missing or outside the reference period.</exception>
    public SofrFutureRateHelper(
        double price,
        Month referenceMonth,
        int referenceYear,
        Frequency referenceFreq,
        double convexityAdjustment = 0.0,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null)
        : base(
            price,
            SofrStart(referenceMonth, referenceYear, referenceFreq),
            SofrEnd(referenceMonth, referenceYear, referenceFreq),
            new Sofr(),
            convexityAdjustment,
            referenceFreq == Frequency.Quarterly ? RateAveragingType.Compound : RateAveragingType.Simple,
            pillar,
            customPillarDate)
    {
        if (!(referenceFreq == Frequency.Quarterly || referenceFreq == Frequency.Monthly))
        {
            throw new ArgumentException("only monthly and quarterly SOFR futures accepted", nameof(referenceFreq));
        }
    }

    private static Date SofrStart(Month month, int year, Frequency freq) =>
        freq == Frequency.Monthly ? new Date(1, month, year) : Date.NthWeekday(3, Weekday.Wednesday, month, year);

    private static Date SofrEnd(Month month, int year, Frequency freq)
    {
        if (freq == Frequency.Monthly)
        {
            return Date.EndOfMonth(new Date(1, month, year)) + 1;
        }
        else
        {
            Date d = SofrStart(month, year, freq) + new Period(freq);
            return Date.NthWeekday(3, Weekday.Wednesday, d.Month, d.Year);
        }
    }
}
