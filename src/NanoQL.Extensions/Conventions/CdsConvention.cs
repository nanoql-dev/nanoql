// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A credit default swap convention resolved in a set (<see cref="ConventionSet.Cds"/>): its record, its base chain
/// merged, and the builder and the two default probability helpers it feeds.
/// </summary>
/// <remarks>
/// An option the record leaves out is the set's <c>MakeCreditDefaultSwap</c> record's, or QuantLib's. The builder
/// takes no settlement days or pricing model, and its calendar only from its default record; the helpers take no cash
/// settlement days but the swap's three (the upfront helper's are its upfront date's): a record whose value for one of
/// them differs is refused.
/// </remarks>
public sealed class CdsConvention
{
    internal CdsConvention(ConventionSet set, CdsConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public CdsConventionRecord Record { get; }

    /// <summary>A CDS builder created in the set, with every option the record gives set.</summary>
    /// <param name="tenor">The tenor, from the trade date.</param>
    /// <param name="runningSpread">The running spread, in fractional units.</param>
    /// <returns>The builder; it takes its other options' defaults from the set when it builds.</returns>
    /// <exception cref="ArgumentException">A name the record gives is unknown, or it gives a value the builder can't
    /// take.</exception>
    public MakeCreditDefaultSwap Builder(Period tenor, double runningSpread)
    {
        const string route = nameof(MakeCreditDefaultSwap);
        CdsConventionRecord r = Record;
        Untaken(route, r, nameof(r.SettlementDays), r.SettlementDays);
        Untaken(route, r, nameof(r.PricingModel), r.PricingModel);
        Untaken(route, r, nameof(r.Calendar), CalendarOf(Set, r.Calendar), MakeCreditDefaultSwap.Defaults(Set).Calendar);

        MakeCreditDefaultSwap b;
        using (ConventionProvider.Use(Set))
        {
            b = new MakeCreditDefaultSwap(tenor, runningSpread);
        }

        b = r.CouponFrequency is Frequency frequency ? b.WithCouponTenor(new Period(frequency)) : b;
        b = r.Rule is DateGenerationRule rule ? b.WithDateGenerationRule(rule) : b;
        b = r.PaymentConvention is BusinessDayConvention convention ? b.WithConvention(convention) : b;
        b = r.DayCounter is string dayCounter ? b.WithDayCounter(Set.DayCounter(dayCounter)) : b;
        b = r.LastPeriodDayCounter is string lastPeriod ? b.WithLastPeriodDayCounter(Set.DayCounter(lastPeriod)) : b;
        b = r.SettlesAccrual is bool settles ? b.SettleAccrual(settles) : b;
        b = r.PaysAtDefaultTime is bool pays ? b.PayAtDefaultTime(pays) : b;
        b = r.RebatesAccrual is bool rebates ? b.RebateAccrual(rebates) : b;
        return r.CashSettlementDays is int cash ? b.WithCashSettlementDays(cash) : b;
    }

    /// <summary>A default probability helper on a running spread quoted.</summary>
    /// <param name="runningSpread">The running spread quoted, in fractional units.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="discountCurve">The discount curve, or <see langword="null"/> for none.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="startDate">The protection start, or <see langword="null"/> to deduce it.</param>
    /// <param name="tradeDate">The trade date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The helper.</returns>
    /// <exception cref="ArgumentException">The record gives no settlement days, a name it gives is unknown, or it gives
    /// a value the helper can't take.</exception>
    public SpreadCdsHelper SpreadHelper(
        double runningSpread,
        Period tenor,
        double recoveryRate,
        YieldTermStructure? discountCurve,
        EvaluationContext context,
        Date? startDate = null,
        Date? tradeDate = null)
    {
        CdsConventionRecord r = Record;
        CdsDefaults d = MakeCreditDefaultSwap.Defaults(Set);

        // the helper's swap settles its rebate on the swap's own three days
        Untaken(nameof(SpreadCdsHelper), r, nameof(r.CashSettlementDays), r.CashSettlementDays ?? d.CashSettlementDays, 3);
        return new SpreadCdsHelper(
            runningSpread,
            tenor,
            Required(r, nameof(r.SettlementDays), r.SettlementDays),
            CalendarOf(Set, r.Calendar) ?? d.Calendar,
            r.CouponFrequency ?? d.CouponTenor.Frequency(),
            r.PaymentConvention ?? d.Convention,
            r.Rule ?? d.Rule,
            DayCounterOf(Set, r.DayCounter) ?? d.DayCounter,
            recoveryRate,
            discountCurve,
            context,
            r.SettlesAccrual ?? d.SettlesAccrual,
            r.PaysAtDefaultTime ?? d.PaysAtDefaultTime,
            startDate,
            DayCounterOf(Set, r.LastPeriodDayCounter) ?? d.LastPeriodDayCounter,
            r.RebatesAccrual ?? d.RebatesAccrual,
            r.PricingModel ?? CreditDefaultSwapPricingModel.Midpoint,
            tradeDate);
    }

    /// <summary>A default probability helper on an upfront quoted, with a running spread.</summary>
    /// <param name="upfront">The upfront quoted, in fractional units.</param>
    /// <param name="runningSpread">The running spread, in fractional units.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="discountCurve">The discount curve, or <see langword="null"/> for none.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="startDate">The protection start, or <see langword="null"/> to deduce it.</param>
    /// <param name="tradeDate">The trade date, or <see langword="null"/> for the evaluation date.</param>
    /// <returns>The helper, its upfront settled after the record's cash settlement days.</returns>
    /// <exception cref="ArgumentException">The record gives no settlement days, or a name it gives is unknown.</exception>
    public UpfrontCdsHelper UpfrontHelper(
        double upfront,
        double runningSpread,
        Period tenor,
        double recoveryRate,
        YieldTermStructure? discountCurve,
        EvaluationContext context,
        Date? startDate = null,
        Date? tradeDate = null)
    {
        CdsConventionRecord r = Record;
        CdsDefaults d = MakeCreditDefaultSwap.Defaults(Set);
        return new UpfrontCdsHelper(
            upfront,
            runningSpread,
            tenor,
            Required(r, nameof(r.SettlementDays), r.SettlementDays),
            CalendarOf(Set, r.Calendar) ?? d.Calendar,
            r.CouponFrequency ?? d.CouponTenor.Frequency(),
            r.PaymentConvention ?? d.Convention,
            r.Rule ?? d.Rule,
            DayCounterOf(Set, r.DayCounter) ?? d.DayCounter,
            recoveryRate,
            discountCurve,
            context,
            r.CashSettlementDays ?? d.CashSettlementDays,
            r.SettlesAccrual ?? d.SettlesAccrual,
            r.PaysAtDefaultTime ?? d.PaysAtDefaultTime,
            startDate,
            DayCounterOf(Set, r.LastPeriodDayCounter) ?? d.LastPeriodDayCounter,
            r.RebatesAccrual ?? d.RebatesAccrual,
            r.PricingModel ?? CreditDefaultSwapPricingModel.Midpoint,
            tradeDate);
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        _ = CalendarOf(Set, Record.Calendar);
        _ = DayCounterOf(Set, Record.DayCounter);
        _ = DayCounterOf(Set, Record.LastPeriodDayCounter);
    }
}
