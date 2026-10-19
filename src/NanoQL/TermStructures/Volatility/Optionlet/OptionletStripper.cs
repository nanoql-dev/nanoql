// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/optionletstripper.hpp ql/termstructures/volatility/optionlet/optionletstripper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The base class of the strippers of optionlet (caplet and floorlet) volatilities from a cap and floor term-volatility
/// surface.
/// </summary>
/// <remarks>
/// <para>
/// The constructor checks its arguments and computes the optionlet tenors and the cap and floor lengths, as QuantLib's
/// does. A derived class strips in its own constructor, which sets the stripped values; QuantLib's lazy calculation is
/// that constructor, and its inspectors return what it set.
/// </para>
/// <para>
/// Unsealed: QuantLib leaves the strip to its derived classes (<see cref="OptionletStripper1"/>,
/// <see cref="OptionletStripper2"/>), and a stripper of nanoql's users is one more.
/// </para>
/// </remarks>
public abstract class OptionletStripper : StrippedOptionletBase
{
    private readonly ImmutableArray<Period> optionletTenors;

    /// <summary>Initializes a new instance of the <see cref="OptionletStripper"/> class.</summary>
    /// <param name="termVolSurface">The cap and floor term-volatility surface.</param>
    /// <param name="iborIndex">The index of the caps' and floors' coupons.</param>
    /// <param name="discount">The discount curve, or <see langword="null"/>, QuantLib's empty handle, for the index's
    /// forwarding curve.</param>
    /// <param name="type">The type of the stripped volatilities.</param>
    /// <param name="displacement">The displacement of shifted lognormal volatilities.</param>
    /// <param name="optionletFrequency">The optionlets' frequency, or <see langword="null"/> for the index's tenor;
    /// required for an overnight index.</param>
    /// <param name="paymentLag">The coupons' payment lag, in business days.</param>
    /// <exception cref="ArgumentNullException"><paramref name="termVolSurface"/> or <paramref name="iborIndex"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type, or
    /// <paramref name="paymentLag"/> is negative.</exception>
    /// <exception cref="ArgumentException">A normal volatility has a displacement, an overnight index has no optionlet
    /// frequency, or the surface's last option tenor is shorter than the first cap and floor length.</exception>
    protected OptionletStripper(
        CapFloorTermVolSurface termVolSurface,
        IborIndex iborIndex,
        YieldTermStructure? discount = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double displacement = 0.0,
        Period? optionletFrequency = null,
        int paymentLag = 0)
    {
        ArgumentNullException.ThrowIfNull(termVolSurface);
        ArgumentNullException.ThrowIfNull(iborIndex);
        EnumArguments.Check(type, nameof(type));
        ArgumentOutOfRangeException.ThrowIfNegative(paymentLag);

        TermVolSurface = termVolSurface;
        IborIndex = iborIndex;
        Discount = discount;
        VolatilityType = type;
        Displacement = displacement;
        OptionletFrequency = optionletFrequency;
        PaymentLag = paymentLag;
        IsOvernightIndex = iborIndex is OvernightIndex;
        NStrikes = termVolSurface.Strikes.Length;

        if (type == VolatilityType.Normal)
        {
            if (!(displacement == 0.0))
            {
                throw new ArgumentException("non-null displacement is not allowed with Normal model", nameof(displacement));
            }
        }

        if (IsOvernightIndex)
        {
            if (optionletFrequency is null)
            {
                throw new ArgumentException(
                    "an optionlet frequency is required when using an overnight index", nameof(optionletFrequency));
            }
        }

        Period indexTenor = optionletFrequency ?? iborIndex.Tenor;
        Period maxCapFloorTenor = termVolSurface.OptionTenors[^1];

        // optionlet tenors and capFloor lengths
        ImmutableArray<Period>.Builder tenors = ImmutableArray.CreateBuilder<Period>();
        ImmutableArray<Period>.Builder lengths = ImmutableArray.CreateBuilder<Period>();
        if (IsOvernightIndex)
        {
            Period nextCapFloorLength = indexTenor;
            if (!(maxCapFloorTenor >= nextCapFloorLength))
            {
                throw new ArgumentException(
                    $"too short ({maxCapFloorTenor}) capfloor term vol termVolSurface", nameof(termVolSurface));
            }

            while (nextCapFloorLength <= maxCapFloorTenor)
            {
                tenors.Add(nextCapFloorLength);
                lengths.Add(nextCapFloorLength);
                nextCapFloorLength += indexTenor;
            }
        }
        else
        {
            tenors.Add(indexTenor);
            lengths.Add(tenors[^1] + indexTenor);
            if (!(maxCapFloorTenor >= lengths[^1]))
            {
                throw new ArgumentException(
                    $"too short ({maxCapFloorTenor}) capfloor term vol termVolSurface", nameof(termVolSurface));
            }

            Period nextCapFloorLength = lengths[^1] + indexTenor;
            while (nextCapFloorLength <= maxCapFloorTenor)
            {
                tenors.Add(lengths[^1]);
                lengths.Add(nextCapFloorLength);
                nextCapFloorLength += indexTenor;
            }
        }

        optionletTenors = tenors.ToImmutable();
        CapFloorLengths = lengths.ToImmutable();
    }

    /// <summary>Gets the cap and floor term-volatility surface.</summary>
    public CapFloorTermVolSurface TermVolSurface { get; }

    /// <summary>Gets the index of the caps' and floors' coupons.</summary>
    public IborIndex IborIndex { get; }

    /// <inheritdoc/>
    public override VolatilityType VolatilityType { get; }

    /// <inheritdoc/>
    public override double Displacement { get; }

    /// <summary>Gets the optionlets' frequency; <see langword="null"/> where the index's tenor is used.</summary>
    public Period? OptionletFrequency { get; }

    /// <summary>Gets the coupons' payment lag, in business days.</summary>
    public int PaymentLag { get; }

    /// <summary>Gets the optionlets' fixing tenors.</summary>
    public ImmutableArray<Period> OptionletFixingTenors => optionletTenors;

    /// <inheritdoc/>
    public override int OptionletMaturities => optionletTenors.Length;

    /// <inheritdoc/>
    public override ImmutableArray<Date> OptionletFixingDates => StrippedFixingDates;

    /// <inheritdoc/>
    public override ImmutableArray<double> OptionletFixingTimes => StrippedFixingTimes;

    /// <summary>Gets the optionlets' payment dates.</summary>
    public ImmutableArray<Date> OptionletPaymentDates => StrippedPaymentDates;

    /// <summary>Gets the optionlets' accrual periods.</summary>
    public ImmutableArray<double> OptionletAccrualPeriods => StrippedAccrualPeriods;

    /// <summary>Gets the surface's day counter.</summary>
    public override DayCounter DayCounter => TermVolSurface.DayCounter;

    /// <summary>Gets the surface's calendar.</summary>
    public override Calendar? Calendar => TermVolSurface.Calendar;

    /// <summary>Gets the surface's settlement days, if it has them.</summary>
    public override int? SettlementDays => TermVolSurface.SettlementDays;

    /// <summary>Gets the surface's business-day convention.</summary>
    public override BusinessDayConvention BusinessDayConvention => TermVolSurface.BusinessDayConvention;

    /// <summary>Gets the discount curve; <see langword="null"/> for the index's forwarding curve.</summary>
    protected YieldTermStructure? Discount { get; }

    /// <summary>Gets the number of the surface's strikes.</summary>
    protected int NStrikes { get; }

    /// <summary>Gets the cap and floor lengths, one per optionlet tenor.</summary>
    protected ImmutableArray<Period> CapFloorLengths { get; }

    /// <summary>Gets a value indicating whether the index is an overnight index.</summary>
    protected bool IsOvernightIndex { get; }

    /// <summary>Gets the stripped fixing dates, which the derived constructor sets.</summary>
    protected ImmutableArray<Date> StrippedFixingDates { get; init; }

    /// <summary>Gets the stripped fixing times, which the derived constructor sets.</summary>
    protected ImmutableArray<double> StrippedFixingTimes { get; init; }

    /// <summary>Gets the stripped payment dates, which the derived constructor sets.</summary>
    protected ImmutableArray<Date> StrippedPaymentDates { get; init; }

    /// <summary>Gets the stripped accrual periods, which the derived constructor sets.</summary>
    protected ImmutableArray<double> StrippedAccrualPeriods { get; init; }

    /// <summary>Gets the stripped at-the-money rates, which the derived constructor sets.</summary>
    protected ImmutableArray<double> StrippedAtmRates { get; init; }

    /// <summary>Gets the strikes per fixing date, which the derived constructor sets.</summary>
    protected ImmutableArray<ImmutableArray<double>> StrippedStrikes { get; init; }

    /// <summary>Gets the volatilities per fixing date, which the derived constructor sets.</summary>
    protected ImmutableArray<ImmutableArray<double>> StrippedVolatilities { get; init; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative or not less than the number of
    /// fixing dates.</exception>
    public override ImmutableArray<double> OptionletStrikes(int i)
    {
        if (!(i >= 0 && i < StrippedStrikes.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i), $"index ({i}) must be less than optionletStrikes size ({StrippedStrikes.Length})");
        }

        return StrippedStrikes[i];
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative or not less than the number of
    /// fixing dates.</exception>
    public override ImmutableArray<double> OptionletVolatilities(int i)
    {
        if (!(i >= 0 && i < StrippedVolatilities.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i), $"index ({i}) must be less than optionletVolatilities size ({StrippedVolatilities.Length})");
        }

        return StrippedVolatilities[i];
    }

    /// <summary>The at-the-money rates the strip computed, one per fixing date.</summary>
    /// <param name="context">The evaluation context, which the stripped rates don't read.</param>
    /// <returns>The rates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public override ImmutableArray<double> AtmOptionletRates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return StrippedAtmRates;
    }

    /// <summary>
    /// The leg of a cap or floor of a given length: on an overnight index, compounded coupons from the surface's
    /// reference date; on an Ibor index, the coupons from the index's value date of the adjusted evaluation date,
    /// without the first, already fixed.
    /// </summary>
    /// <param name="capFloorLength">The cap's or floor's length.</param>
    /// <param name="context">The evaluation context: its evaluation date starts an Ibor leg.</param>
    /// <returns>The leg, with notional 1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">An Ibor leg of the length has no optionlet after the first.</exception>
    protected ImmutableArray<CashFlow> MakeCapFloorLeg(Period capFloorLength, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsOvernightIndex)
        {
            var overnightIndex = (OvernightIndex)IborIndex;
            Calendar calendar = TermVolSurface.Calendar ?? EmptyCalendar.Instance;
            BusinessDayConvention bdc = TermVolSurface.BusinessDayConvention;
            Date startDate = TermVolSurface.ReferenceDate;
            Date endDate = calendar.Advance(startDate, capFloorLength, bdc);
            var overnightSchedule = new Schedule(
                startDate, endDate, OptionletFrequency!.Value, TermVolSurface.Calendar, bdc, bdc,
                DateGenerationRule.Forward, false, context);
            return new OvernightLeg(overnightSchedule, overnightIndex)
                .WithNotionals(1.0)
                .WithPaymentCalendar(TermVolSurface.Calendar)
                .WithPaymentAdjustment(bdc)
                .WithPaymentLag(PaymentLag)
                .WithCouponPricer(new CompoundingOvernightIndexedCouponPricer())
                .ToLeg();
        }

        Date start = IborIndex.ValueDate(IborIndex.FixingCalendar.Adjust(context.EvaluationDate));
        Date end = start + capFloorLength;
        var schedule = new Schedule(
            start, end, IborIndex.Tenor, IborIndex.FixingCalendar, IborIndex.BusinessDayConvention,
            IborIndex.BusinessDayConvention, DateGenerationRule.Backward, false, context);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, IborIndex)
            .WithNotionals(1.0)
            .WithPaymentDayCounter(IborIndex.DayCounter)
            .WithPaymentAdjustment(IborIndex.BusinessDayConvention)
            .WithPaymentLag(PaymentLag)
            .ToLeg(context);

        // Spot-starting Ibor caps exclude the first, already-fixed caplet.
        leg = leg.RemoveAt(0);
        if (leg.IsEmpty)
        {
            throw new NanoQLException($"cap/floor length {capFloorLength} does not contain any optionlets");
        }

        return leg;
    }

    /// <summary>
    /// A constant optionlet volatility on the surface's reference date, calendar, convention and day counter, with the
    /// stripper's type and displacement.
    /// </summary>
    /// <param name="volatility">The volatility.</param>
    /// <returns>The structure.</returns>
    protected OptionletVolatilityStructure ConstantOptionletVolatility(double volatility) =>
        new global::NanoQL.TermStructures.ConstantOptionletVolatility(
            TermVolSurface.ReferenceDate, TermVolSurface.Calendar, TermVolSurface.BusinessDayConvention, volatility,
            TermVolSurface.DayCounter, VolatilityType, Displacement);

    /// <summary>
    /// The engine that prices the stripper's caps and floors on a constant volatility: on an overnight index, the
    /// engine on <see cref="ConstantOptionletVolatility(double)"/>; on an Ibor index, the engine on the volatility itself,
    /// whose reference date is the evaluation date.
    /// </summary>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="volatility">The volatility.</param>
    /// <returns>The engine, the Black engine for shifted lognormal volatilities and the Bachelier engine for normal
    /// ones.</returns>
    protected Func<CapFloor, EvaluationContext, InstrumentResults> MakeCapFloorPricingEngine(
        YieldTermStructure discountCurve, double volatility)
    {
        if (IsOvernightIndex)
        {
            return MakeCapFloorPricingEngine(discountCurve, ConstantOptionletVolatility(volatility));
        }

        // Preserve the quote-based engines' zero-settlement reference date for Ibor.
        DayCounter dc = TermVolSurface.DayCounter;
        double displacement = Displacement;
        return VolatilityType switch
        {
            VolatilityType.ShiftedLognormal => (capFloor, context) =>
                BlackCapFloorEngine.Calculate(capFloor, context, discountCurve, volatility, dc, displacement),
            VolatilityType.Normal => (capFloor, context) =>
                BachelierCapFloorEngine.Calculate(capFloor, context, discountCurve, volatility, dc),
            _ => throw new NanoQLException($"unknown volatility type: {VolatilityType}"),
        };
    }

    /// <summary>The engine that prices the stripper's caps and floors on a volatility structure.</summary>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="volatility">The volatility structure.</param>
    /// <returns>The engine, the Black engine, with the stripper's displacement, for shifted lognormal volatilities and
    /// the Bachelier engine for normal ones.</returns>
    protected Func<CapFloor, EvaluationContext, InstrumentResults> MakeCapFloorPricingEngine(
        YieldTermStructure discountCurve, OptionletVolatilityStructure volatility)
    {
        double displacement = Displacement;
        return VolatilityType switch
        {
            VolatilityType.ShiftedLognormal => (capFloor, context) =>
                BlackCapFloorEngine.Calculate(capFloor, context, discountCurve, volatility, displacement),
            VolatilityType.Normal => (capFloor, context) =>
                BachelierCapFloorEngine.Calculate(capFloor, context, discountCurve, volatility),
            _ => throw new NanoQLException($"unknown volatility type: {VolatilityType}"),
        };
    }
}
