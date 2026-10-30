// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A multiple-resets swap convention resolved in a set (<see cref="ConventionSet.MultipleResetsSwap"/>): its record,
/// its base chain merged, and the builder it feeds, on its index built in the set.
/// </summary>
/// <remarks>
/// An option the record leaves out is the builder's: the set's <c>MakeMultipleResetsSwap</c> record, read through the
/// index's provider when the swap is built, or the index's own conventions.
/// </remarks>
public sealed class MultipleResetsSwapConvention
{
    internal MultipleResetsSwapConvention(ConventionSet set, MultipleResetsSwapConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public MultipleResetsSwapConventionRecord Record { get; }

    /// <summary>A multiple-resets swap builder on the record's index built in the set, with every option the record
    /// gives set.</summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwardingCurve">The index's forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The builder; it takes its other options' defaults from the set when it builds.</returns>
    /// <exception cref="ArgumentException">The record gives no index or resets per coupon, or a name it gives is
    /// unknown.</exception>
    public MakeMultipleResetsSwap Builder(Period tenor, YieldTermStructure? forwardingCurve = null)
    {
        MultipleResetsSwapConventionRecord r = Record;
        var b = new MakeMultipleResetsSwap(
            tenor,
            Ibor(Set, Required(r, nameof(r.Index), r.Index), r.IndexTenor, forwardingCurve),
            Required(r, nameof(r.ResetsPerCoupon), r.ResetsPerCoupon));
        b = r.SettlementDays is int settlementDays ? b.WithSettlementDays(settlementDays) : b;
        b = r.FixedLegFrequency is Frequency frequency ? b.WithFixedLegFrequency(frequency) : b;
        b = r.FixedLegDayCounter is string dayCounter ? b.WithFixedLegDayCount(Set.DayCounter(dayCounter)) : b;
        b = r.FixedLegConvention is BusinessDayConvention convention ? b.WithFixedLegConvention(convention) : b;
        return r.Averaging is RateAveragingType averaging ? b.WithAveragingMethod(averaging) : b;
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        if (Record.Index is not null)
        {
            _ = Ibor(Set, Record.Index, Record.IndexTenor, null);
        }

        _ = DayCounterOf(Set, Record.FixedLegDayCounter);
    }
}
