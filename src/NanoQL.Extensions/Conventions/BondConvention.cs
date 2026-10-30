// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A government bond market's convention resolved in a set (<see cref="ConventionSet.Bond"/>): its record, its base
/// chain merged, and the fixed-rate bond and bond helper it feeds; the trade's terms are arguments.
/// </summary>
/// <remarks>
/// <para>
/// A field the record leaves out is the set's <c>FixedRateBond</c> record's, which mirrors QuantLib's constructors'
/// defaults, or theirs: payment Following, redemption 100, ex-coupon dates unadjusted without end of month, a clean
/// price. The schedule is <see cref="MakeSchedule"/>'s from the issue date to the maturity, its options the record's.
/// The bond takes no price type, so a record giving one is refused there.
/// </para>
/// <para>
/// A record whose chain reaches <see cref="BTP"/>'s preset record configures that class, which rounds its accrued
/// amount: build <see cref="BTP"/> in the set for it, which the routes here refuse.
/// </para>
/// </remarks>
public sealed class BondConvention
{
    private readonly BondConventionRecord? defaults;

    internal BondConvention(ConventionSet set, BondConventionRecord record, BondConventionRecord? defaults, string? preset)
    {
        Set = set;
        Record = record;
        this.defaults = defaults;
        Preset = preset;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public BondConventionRecord Record { get; }

    // The coded bond preset the record's chain reaches, or null.
    private string? Preset { get; }

    /// <summary>A fixed-rate bond from the issue date to the maturity on the record's conventions.</summary>
    /// <param name="faceAmount">The face amount.</param>
    /// <param name="issueDate">The issue date, the schedule's first.</param>
    /// <param name="maturityDate">The maturity date, the schedule's last.</param>
    /// <param name="coupons">The coupon rates.</param>
    /// <returns>The bond.</returns>
    /// <exception cref="ArgumentException">The record leaves out a field the bond needs, a name it gives is unknown, or it
    /// gives a value the bond can't take.</exception>
    public FixedRateBond Bond(double faceAmount, Date issueDate, Date maturityDate, IEnumerable<double> coupons)
    {
        const string route = nameof(FixedRateBond);
        BondConventionRecord r = Record;
        RefusePreset(route);
        Untaken(route, r, nameof(r.PriceType), r.PriceType);
        return new FixedRateBond(
            Required(r, nameof(r.SettlementDays), r.SettlementDays),
            faceAmount,
            Schedule(issueDate, maturityDate),
            coupons,
            Set.DayCounter(Required(r, nameof(r.AccrualDayCounter), r.AccrualDayCounter)),
            PaymentConvention(),
            Redemption(),
            issueDate,
            CalendarOf(Set, r.PaymentCalendar),
            r.ExCouponPeriod ?? default,
            CalendarOf(Set, r.ExCouponCalendar),
            ExCouponConvention(),
            ExCouponEndOfMonth());
    }

    /// <summary>A bond helper on a fixed-rate bond from the issue date to the maturity on the record's conventions.</summary>
    /// <param name="price">The bond's price quoted, clean or dirty by the record.</param>
    /// <param name="issueDate">The issue date, the schedule's first.</param>
    /// <param name="maturityDate">The maturity date, the schedule's last.</param>
    /// <param name="coupons">The coupon rates.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="faceAmount">The face amount.</param>
    /// <returns>The helper.</returns>
    /// <exception cref="ArgumentException">The record leaves out a field the bond needs, or a name it gives is
    /// unknown.</exception>
    public FixedRateBondHelper RateHelper(
        double price,
        Date issueDate,
        Date maturityDate,
        IEnumerable<double> coupons,
        EvaluationContext context,
        double faceAmount = 100.0)
    {
        BondConventionRecord r = Record;
        RefusePreset(nameof(FixedRateBondHelper));
        return new FixedRateBondHelper(
            price,
            Required(r, nameof(r.SettlementDays), r.SettlementDays),
            faceAmount,
            Schedule(issueDate, maturityDate),
            coupons,
            Set.DayCounter(Required(r, nameof(r.AccrualDayCounter), r.AccrualDayCounter)),
            context,
            PaymentConvention(),
            Redemption(),
            issueDate,
            CalendarOf(Set, r.PaymentCalendar),
            r.ExCouponPeriod ?? default,
            CalendarOf(Set, r.ExCouponCalendar),
            ExCouponConvention(),
            ExCouponEndOfMonth(),
            r.PriceType ?? Default(defaults?.PriceType, BondPriceType.Clean));
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        _ = CalendarOf(Set, Record.Calendar);
        _ = CalendarOf(Set, Record.PaymentCalendar);
        _ = CalendarOf(Set, Record.ExCouponCalendar);
        _ = DayCounterOf(Set, Record.AccrualDayCounter);
    }

    private Schedule Schedule(Date issueDate, Date maturityDate)
    {
        BondConventionRecord r = Record;
        MakeSchedule b = new MakeSchedule()
            .From(issueDate)
            .To(maturityDate)
            .WithFrequency(Required(r, nameof(r.Frequency), r.Frequency))
            .WithCalendar(Set.Calendar(Required(r, nameof(r.Calendar), r.Calendar)))
            .WithConvention(Required(r, nameof(r.Convention), r.Convention));
        b = r.TerminationDateConvention is BusinessDayConvention termination ? b.WithTerminationDateConvention(termination) : b;
        b = r.Rule is DateGenerationRule rule ? b.WithRule(rule) : b;
        return (r.EndOfMonth is bool endOfMonth ? b.EndOfMonth(endOfMonth) : b).ToSchedule();
    }

    private BusinessDayConvention PaymentConvention() =>
        Record.PaymentConvention ?? Default(defaults?.PaymentConvention, BusinessDayConvention.Following);

    private double Redemption() => Record.Redemption ?? Default(defaults?.Redemption, 100.0);

    private BusinessDayConvention ExCouponConvention() =>
        Record.ExCouponConvention ?? Default(defaults?.ExCouponConvention, BusinessDayConvention.Unadjusted);

    private bool ExCouponEndOfMonth() => Record.ExCouponEndOfMonth ?? Default(defaults?.ExCouponEndOfMonth, false);

    // A field from the set's FixedRateBond record, else QuantLib's constructors' default on a set built on QuantLib's
    // presets; a set without them that defines no FixedRateBond record refuses it.
    private T Default<T>(T? fromRecord, T quantLibs)
        where T : struct =>
        fromRecord ?? (defaults is not null || Set.IsOnQuantLib
            ? quantLibs
            : throw new ArgumentException("the set defines no FixedRateBond, and isn't built on QuantLib's presets", "name"));

    private void RefusePreset(string route)
    {
        if (Preset is string preset)
        {
            throw Refusal(route, Record, "base", $"its chain reaches the {preset} preset, whose class rounds its accrued amount: build {preset} in the set");
        }
    }
}
