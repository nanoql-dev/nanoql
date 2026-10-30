// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A CMS swap convention resolved in a set (<see cref="ConventionSet.Cms"/>): its record, its base chain merged, and
/// the builder it feeds, on its swap index and floating index built in the set.
/// </summary>
/// <remarks>
/// An option the record leaves out is the builder's: the set's <c>MakeCms</c> record, read through the swap index's
/// provider when the swap is built, or the indexes' own conventions.
/// </remarks>
public sealed class CmsConvention
{
    internal CmsConvention(ConventionSet set, CmsConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public CmsConventionRecord Record { get; }

    /// <summary>A CMS swap builder on the record's indexes built in the set, with every option the record gives
    /// set.</summary>
    /// <param name="swapTenor">The CMS swap's tenor.</param>
    /// <param name="forwardingCurve">The indexes' forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discountingCurve">The swap index's exogenous discount curve, or <see langword="null"/> for the
    /// forwarding curve.</param>
    /// <returns>The builder; it takes its other options' defaults from the set when it builds.</returns>
    /// <exception cref="ArgumentException">The record gives no swap index or its tenor, or a name it gives is
    /// unknown.</exception>
    public MakeCms Builder(Period swapTenor, YieldTermStructure? forwardingCurve = null, YieldTermStructure? discountingCurve = null)
    {
        CmsConventionRecord r = Record;
        SwapIndex swapIndex = SwapIndexOf(
            Set, Required(r, nameof(r.SwapIndex), r.SwapIndex), Required(r, nameof(r.SwapIndexTenor), r.SwapIndexTenor), forwardingCurve, discountingCurve);
        IborIndex? iborIndex = r.FloatingLegIndex is string floating ? Ibor(Set, floating, r.FloatingLegIndexTenor, forwardingCurve) : null;
        var b = new MakeCms(swapTenor, swapIndex, iborIndex);
        b = r.CmsLegTenor is Period cmsTenor ? b.WithCmsLegTenor(cmsTenor) : b;
        b = r.CmsLegCalendar is string cmsCalendar ? b.WithCmsLegCalendar(Set.Calendar(cmsCalendar)) : b;
        b = r.CmsLegConvention is BusinessDayConvention cmsConvention ? b.WithCmsLegConvention(cmsConvention) : b;
        b = r.CmsLegTerminationDateConvention is BusinessDayConvention cmsTermination ? b.WithCmsLegTerminationDateConvention(cmsTermination) : b;
        b = r.CmsLegRule is DateGenerationRule cmsRule ? b.WithCmsLegRule(cmsRule) : b;
        b = r.CmsLegEndOfMonth is bool cmsEndOfMonth ? b.WithCmsLegEndOfMonth(cmsEndOfMonth) : b;
        b = r.CmsLegDayCounter is string cmsDayCounter ? b.WithCmsLegDayCount(Set.DayCounter(cmsDayCounter)) : b;
        b = r.FloatingLegTenor is Period floatTenor ? b.WithFloatingLegTenor(floatTenor) : b;
        b = r.FloatingLegCalendar is string floatCalendar ? b.WithFloatingLegCalendar(Set.Calendar(floatCalendar)) : b;
        b = r.FloatingLegConvention is BusinessDayConvention floatConvention ? b.WithFloatingLegConvention(floatConvention) : b;
        b = r.FloatingLegTerminationDateConvention is BusinessDayConvention floatTermination
            ? b.WithFloatingLegTerminationDateConvention(floatTermination)
            : b;
        b = r.FloatingLegRule is DateGenerationRule floatRule ? b.WithFloatingLegRule(floatRule) : b;
        b = r.FloatingLegEndOfMonth is bool floatEndOfMonth ? b.WithFloatingLegEndOfMonth(floatEndOfMonth) : b;
        return r.FloatingLegDayCounter is string floatDayCounter ? b.WithFloatingLegDayCount(Set.DayCounter(floatDayCounter)) : b;
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        CmsConventionRecord r = Record;
        if (r.SwapIndex is not null)
        {
            _ = SwapIndexOf(Set, r.SwapIndex, r.SwapIndexTenor ?? new Period(10, TimeUnit.Years), null, null);
        }

        if (r.FloatingLegIndex is not null)
        {
            _ = Ibor(Set, r.FloatingLegIndex, r.FloatingLegIndexTenor, null);
        }

        _ = CalendarOf(Set, r.CmsLegCalendar);
        _ = CalendarOf(Set, r.FloatingLegCalendar);
        _ = DayCounterOf(Set, r.CmsLegDayCounter);
        _ = DayCounterOf(Set, r.FloatingLegDayCounter);
    }
}
