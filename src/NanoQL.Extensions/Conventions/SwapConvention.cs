// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A swap convention resolved in a set (<see cref="ConventionSet.Swap"/>): its record, its base chain merged, and the
/// builder and rate helper it feeds, on its index built in the set.
/// </summary>
/// <remarks>
/// An option the record leaves out is the builder's: the set's <c>MakeVanillaSwap.</c> record for the index's
/// currency, read through the index's provider when the swap is built, or the index's own conventions. The rate helper
/// takes one calendar and one end-of-month flag for both legs and one convention for each leg's two, and leaves the
/// rules, the floating leg's tenor and day counter, the payment convention and the settlement calendar to the
/// builder: a record whose value for one of them differs from the builder's is refused.
/// </remarks>
public sealed class SwapConvention
{
    private const string Helper = nameof(SwapRateHelper);

    internal SwapConvention(ConventionSet set, SwapConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public SwapConventionRecord Record { get; }

    /// <summary>A swap builder on the record's index built in the set, with every option the record gives set.</summary>
    /// <param name="swapTenor">The swap's tenor.</param>
    /// <param name="forwardingCurve">The index's forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The builder; it takes its other options' defaults from the set when it builds.</returns>
    /// <exception cref="ArgumentException">The record gives no index, or a name it gives is unknown.</exception>
    public MakeVanillaSwap Builder(Period swapTenor, YieldTermStructure? forwardingCurve = null)
    {
        SwapConventionRecord r = Record;
        var b = new MakeVanillaSwap(swapTenor, Index(forwardingCurve));
        if (r.SettlementDays is int settlementDays)
        {
            b = b.WithSettlementDays(settlementDays);
        }

        if (r.SettlementCalendar is string settlementCalendar)
        {
            b = b.WithSettlementCalendar(Set.Calendar(settlementCalendar));
        }

        b = r.FixedLegTenor is Period fixedTenor ? b.WithFixedLegTenor(fixedTenor) : b;
        b = r.FixedLegCalendar is string fixedCalendar ? b.WithFixedLegCalendar(Set.Calendar(fixedCalendar)) : b;
        b = r.FixedLegConvention is BusinessDayConvention fixedConvention ? b.WithFixedLegConvention(fixedConvention) : b;
        b = r.FixedLegTerminationDateConvention is BusinessDayConvention fixedTermination
            ? b.WithFixedLegTerminationDateConvention(fixedTermination)
            : b;
        b = r.FixedLegRule is DateGenerationRule fixedRule ? b.WithFixedLegRule(fixedRule) : b;
        b = r.FixedLegEndOfMonth is bool fixedEndOfMonth ? b.WithFixedLegEndOfMonth(fixedEndOfMonth) : b;
        b = r.FixedLegDayCounter is string fixedDayCounter ? b.WithFixedLegDayCount(Set.DayCounter(fixedDayCounter)) : b;
        b = r.FloatingLegTenor is Period floatTenor ? b.WithFloatingLegTenor(floatTenor) : b;
        b = r.FloatingLegCalendar is string floatCalendar ? b.WithFloatingLegCalendar(Set.Calendar(floatCalendar)) : b;
        b = r.FloatingLegConvention is BusinessDayConvention floatConvention ? b.WithFloatingLegConvention(floatConvention) : b;
        b = r.FloatingLegTerminationDateConvention is BusinessDayConvention floatTermination
            ? b.WithFloatingLegTerminationDateConvention(floatTermination)
            : b;
        b = r.FloatingLegRule is DateGenerationRule floatRule ? b.WithFloatingLegRule(floatRule) : b;
        b = r.FloatingLegEndOfMonth is bool floatEndOfMonth ? b.WithFloatingLegEndOfMonth(floatEndOfMonth) : b;
        b = r.FloatingLegDayCounter is string floatDayCounter ? b.WithFloatingLegDayCount(Set.DayCounter(floatDayCounter)) : b;
        return r.PaymentConvention is BusinessDayConvention payment ? b.WithPaymentConvention(payment) : b;
    }

    /// <summary>
    /// A swap rate helper on the record's index built in the set, through <see cref="SwapRateHelper"/>'s
    /// explicit-conventions constructor.
    /// </summary>
    /// <param name="rate">The swap rate quoted.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountingCurve">The exogenous discount curve, or <see langword="null"/> for the bootstrapped
    /// curve.</param>
    /// <param name="forwardingCurve">The index's forwarding curve, or <see langword="null"/> for the bootstrapped
    /// curve.</param>
    /// <param name="forwardStart">The forward start.</param>
    /// <returns>The helper.</returns>
    /// <exception cref="ArgumentException">The record gives no index, a name it gives is unknown, or it gives a value
    /// the helper can't take.</exception>
    public SwapRateHelper RateHelper(
        double rate,
        Period tenor,
        EvaluationContext context,
        YieldTermStructure? discountingCurve = null,
        YieldTermStructure? forwardingCurve = null,
        Period forwardStart = default)
    {
        SwapConventionRecord r = Record;
        IborIndex index = Index(forwardingCurve);
        VanillaSwapDefaults d = MakeVanillaSwap.Defaults(index.Currency, Set);

        // the helper's one calendar, end of month and floating convention, each taken where the record's two agree
        Calendar calendar = Tied(
            Helper,
            r,
            nameof(r.FixedLegCalendar),
            CalendarOf(Set, r.FixedLegCalendar) ?? index.FixingCalendar,
            nameof(r.FloatingLegCalendar),
            CalendarOf(Set, r.FloatingLegCalendar) ?? index.FixingCalendar);
        bool endOfMonth = Tied(
            Helper,
            r,
            nameof(r.FixedLegEndOfMonth),
            r.FixedLegEndOfMonth ?? d.FixedLegEndOfMonth,
            nameof(r.FloatingLegEndOfMonth),
            r.FloatingLegEndOfMonth ?? d.FloatingLegEndOfMonth);
        BusinessDayConvention fixedConvention = Tied(
            Helper,
            r,
            nameof(r.FixedLegConvention),
            r.FixedLegConvention ?? d.FixedLegConvention,
            nameof(r.FixedLegTerminationDateConvention),
            r.FixedLegTerminationDateConvention ?? d.FixedLegTerminationDateConvention);
        BusinessDayConvention? floatConvention = r.FloatingLegConvention is null && r.FloatingLegTerminationDateConvention is null
            ? null
            : Tied(
                Helper,
                r,
                nameof(r.FloatingLegConvention),
                r.FloatingLegConvention ?? index.BusinessDayConvention,
                nameof(r.FloatingLegTerminationDateConvention),
                r.FloatingLegTerminationDateConvention ?? index.BusinessDayConvention);

        // the fixed leg's tenor, the record's or the builder's default at the tenor, which the helper takes as a frequency
        Period fixedTenor = r.FixedLegTenor
            ?? (d.FixedLegTenorThreshold is { } threshold && threshold.Holds(tenor) ? threshold.FixedLegTenor : d.FixedLegTenor)
            ?? throw new NanoQLException($"unknown fixed leg default tenor for {index.Currency?.Code}");
        Frequency fixedFrequency = fixedTenor.Frequency();
        if (fixedFrequency is Frequency.OtherFrequency or Frequency.NoFrequency || new Period(fixedFrequency) != fixedTenor)
        {
            throw Refusal(Helper, r, nameof(r.FixedLegTenor), $"it takes the fixed leg's tenor as a frequency, which {fixedTenor} isn't");
        }

        // what the helper leaves to the builder, refused where the record gives another value
        Untaken(Helper, r, nameof(r.FixedLegRule), r.FixedLegRule, d.FixedLegRule);
        Untaken(Helper, r, nameof(r.FloatingLegRule), r.FloatingLegRule, d.FloatingLegRule);
        Untaken(Helper, r, nameof(r.FloatingLegTenor), r.FloatingLegTenor, index.Tenor);
        Untaken(Helper, r, nameof(r.FloatingLegDayCounter), DayCounterOf(Set, r.FloatingLegDayCounter), index.DayCounter);
        Untaken(Helper, r, nameof(r.PaymentConvention), r.PaymentConvention, floatConvention ?? index.BusinessDayConvention);
        Untaken(Helper, r, nameof(r.SettlementCalendar), CalendarOf(Set, r.SettlementCalendar), calendar);

        return new SwapRateHelper(
            rate,
            tenor,
            calendar,
            fixedFrequency,
            fixedConvention,
            DayCounterOf(Set, r.FixedLegDayCounter),
            index,
            context,
            fwdStart: forwardStart,
            discountingCurve: discountingCurve,
            settlementDays: r.SettlementDays,
            endOfMonth: endOfMonth,
            floatConvention: floatConvention);
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        SwapConventionRecord r = Record;
        if (r.Index is not null)
        {
            _ = Index(null);
        }

        foreach (string? calendar in new[] { r.SettlementCalendar, r.FixedLegCalendar, r.FloatingLegCalendar })
        {
            _ = CalendarOf(Set, calendar);
        }

        _ = DayCounterOf(Set, r.FixedLegDayCounter);
        _ = DayCounterOf(Set, r.FloatingLegDayCounter);
    }

    private IborIndex Index(YieldTermStructure? h) =>
        Ibor(Set, Required(Record, nameof(Record.Index), Record.Index), Record.IndexTenor, h);
}
