// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/makeyoyinflationcapfloor.hpp ql/instruments/makeyoyinflationcapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A builder of standard year-on-year inflation caps and floors.</summary>
/// <remarks>
/// <para>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToYoYInflationCapFloor"/> builds the instrument, in place of QuantLib's conversion operators. The
/// leg is <see cref="YoYInflationLeg"/>'s on an annual schedule over the given number of years, from the effective date
/// or, without one, from the spot date (the evaluation date advanced by the fixing days) plus the forward start; the
/// fixing days move the start only. An optionlet keeps the last coupon alone. Its defaults are QuantLib's: a nominal of
/// 1,000,000, 30/360 (Bond Basis), ModifiedFollowing, no fixing days.
/// </para>
/// <para>
/// With no strike, the instrument is struck at the money: at the leg's at-the-money rate on the nominal curve given to
/// <see cref="WithAtmStrike"/>, at its reference date and without the flows paid on it; with neither, the conversion
/// refuses with QuantLib's empty-handle text. QuantLib's <c>withFirstCapletExcluded</c> is declared and never defined,
/// so it isn't ported. <see cref="WithPricingEngine"/> is kept and changes nothing: the instrument holds no engine. A
/// <see cref="YoYInflationCapFloorType.Collar"/> is refused by the instrument's constructor after the strike, as
/// QuantLib refuses it. A builder has no value equality.
/// </para>
/// </remarks>
public sealed record MakeYoYInflationCapFloor
{
    /// <summary>Initializes a new instance of the <see cref="MakeYoYInflationCapFloor"/> record.</summary>
    /// <param name="capFloorType">The type: a cap or a floor, which the conversion builds.</param>
    /// <param name="index">The year-on-year inflation index.</param>
    /// <param name="length">The instrument's length, in years.</param>
    /// <param name="cal">The calendar of the start, the schedule and the payments, or <see langword="null"/> for
    /// QuantLib's empty calendar, used unchecked as QuantLib uses it.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative, or
    /// <paramref name="interpolation"/> isn't a member of its type.</exception>
    public MakeYoYInflationCapFloor(
        YoYInflationCapFloorType capFloorType,
        YoYInflationIndex index,
        int length,
        Calendar? cal,
        Period observationLag,
        CPIInterpolationType interpolation)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        EnumArguments.Check(interpolation, nameof(interpolation));
        Provider = index.Provider;
        CapFloorType = capFloorType;
        Index = index;
        Length = length;
        Calendar = cal;
        ObservationLag = observationLag;
        Interpolation = interpolation;
    }

    // The provider the builder takes its conventions from and builds inside: its index's.
    private ConventionProvider Provider { get; init; }

    private YoYInflationCapFloorType CapFloorType { get; init; }

    private int Length { get; init; }

    private Calendar? Calendar { get; init; }

    private YoYInflationIndex Index { get; init; }

    private Period ObservationLag { get; init; }

    private CPIInterpolationType Interpolation { get; init; }

    private double? Strike { get; init; }

    private bool IsOptionlet { get; init; }

    private Date? EffectiveDate { get; init; }

    private Period ForwardStart { get; init; }

    private DayCounter? DayCounter { get; init; } = new Thirty360(Thirty360Convention.BondBasis);

    private BusinessDayConvention Roll { get; init; } = BusinessDayConvention.ModifiedFollowing;

    private int FixingDays { get; init; }

    private double Nominal { get; init; } = 1000000.0;

    private YieldTermStructure? NominalTermStructure { get; init; }

    /// <summary>Sets the nominal.</summary>
    /// <param name="n">The nominal.</param>
    /// <returns>The modified builder.</returns>
    public MakeYoYInflationCapFloor WithNominal(double n) => this with { Nominal = n };

    /// <summary>Sets the effective date, the leg's start.</summary>
    /// <param name="effectiveDate">The date, or <see langword="null"/>, QuantLib's <c>Date()</c>, for the spot date
    /// plus the forward start.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="effectiveDate"/> is <c>default(Date)</c>.</exception>
    public MakeYoYInflationCapFloor WithEffectiveDate(Date? effectiveDate)
    {
        Date.ThrowIfDefault(effectiveDate, nameof(effectiveDate));
        return this with { EffectiveDate = effectiveDate };
    }

    /// <summary>Sets the coupons' day counter; 30/360 (Bond Basis) by default.</summary>
    /// <param name="dc">The day counter, or <see langword="null"/>, QuantLib's empty one, which the leg
    /// refuses.</param>
    /// <returns>The modified builder.</returns>
    public MakeYoYInflationCapFloor WithPaymentDayCounter(DayCounter? dc) => this with { DayCounter = dc };

    /// <summary>Sets the convention adjusting the payment dates; ModifiedFollowing by default.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type.</exception>
    public MakeYoYInflationCapFloor WithPaymentAdjustment(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { Roll = bdc };
    }

    /// <summary>Sets the fixing days from the evaluation date to the spot date, which the leg doesn't take.</summary>
    /// <param name="fixingDays">The fixing days.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    public MakeYoYInflationCapFloor WithFixingDays(int fixingDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        return this with { FixingDays = fixingDays };
    }

    /// <summary>
    /// Kept from QuantLib, where it sets the engine the instrument is priced with: it changes nothing, since the
    /// instrument holds no engine; price the instrument by calling the engine.
    /// </summary>
    /// <param name="engine">The engine, a function of the instrument and the context, such as
    /// <c>(c, ctx) =&gt; YoYInflationBlackCapFloorEngine.Calculate(c, ctx, index, vol, curve)</c>.</param>
    /// <returns>The builder, unchanged.</returns>
    public MakeYoYInflationCapFloor WithPricingEngine(
        Func<YoYInflationCapFloor, EvaluationContext, InstrumentResults>? engine) => this;

    /// <summary>Sets whether only the last coupon is kept: an optionlet.</summary>
    /// <param name="b">The flag.</param>
    /// <returns>The modified builder.</returns>
    public MakeYoYInflationCapFloor AsOptionlet(bool b = true) => this with { IsOptionlet = b };

    /// <summary>Sets the strike.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="NanoQLException">An at-the-money strike was asked for, with QuantLib's "ATM strike already
    /// given".</exception>
    public MakeYoYInflationCapFloor WithStrike(double strike)
    {
        if (NominalTermStructure is not null)
        {
            throw new NanoQLException("ATM strike already given");
        }

        return this with { Strike = strike };
    }

    /// <summary>
    /// Asks for the at-the-money strike: the leg's at-the-money rate on the nominal curve, which the conversion
    /// computes.
    /// </summary>
    /// <param name="nominalTermStructure">The nominal curve, or <see langword="null"/>, QuantLib's empty
    /// handle.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="NanoQLException">A strike was given, with QuantLib's "explicit strike already
    /// given".</exception>
    public MakeYoYInflationCapFloor WithAtmStrike(YieldTermStructure? nominalTermStructure)
    {
        if (Strike is not null)
        {
            throw new NanoQLException("explicit strike already given");
        }

        return this with { NominalTermStructure = nominalTermStructure };
    }

    /// <summary>Sets the forward start, after the spot date.</summary>
    /// <param name="forwardStart">The forward start.</param>
    /// <returns>The modified builder.</returns>
    public MakeYoYInflationCapFloor WithForwardStart(Period forwardStart) => this with { ForwardStart = forwardStart };

    /// <summary>The instrument: QuantLib's conversion to <c>ext::shared_ptr&lt;YoYInflationCapFloor&gt;</c>.</summary>
    /// <param name="context">The evaluation context: without an effective date the leg starts from its evaluation date,
    /// and an at-the-money strike reads the coupons' fixings and forecasts.</param>
    /// <returns>The instrument, a <see cref="YoYInflationCapFloor"/> of the builder's type.</returns>
    /// <exception cref="ArgumentNullException">The context is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type isn't a cap or a floor.</exception>
    /// <exception cref="NanoQLException">With no strike, there is no nominal curve, QuantLib's "empty Handle cannot be
    /// dereferenced", or the leg's at-the-money rate can't be computed; or the leg can't be built.</exception>
    public YoYInflationCapFloor ToYoYInflationCapFloor(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Built inside the builder's provider, so what the build constructs takes its conventions.
        using IDisposable scope = ConventionProvider.Use(Provider);

        // QuantLib's empty calendar, null here, is used unchecked, as QuantLib uses it
        Calendar calendar = Calendar ?? EmptyCalendar.Instance;
        Date startDate;
        if (EffectiveDate is Date effective)
        {
            startDate = effective;
        }
        else
        {
            Date referenceDate = context.EvaluationDate;
            Date spotDate = calendar.Advance(referenceDate, FixingDays, TimeUnit.Days);
            startDate = spotDate + ForwardStart;
        }

        Date endDate = calendar.Advance(startDate, Length, TimeUnit.Years, BusinessDayConvention.Unadjusted);

        // ref periods & acc periods
        var schedule = new Schedule(
            startDate,
            endDate,
            new Period(Frequency.Annual),
            Calendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Forward,
            false,
            context);
        ImmutableArray<CashFlow> leg = new YoYInflationLeg(schedule, Calendar, Index, ObservationLag, Interpolation)
            .WithPaymentAdjustment(Roll)
            .WithPaymentDayCounter(DayCounter)
            .WithNotionals(Nominal)
            .ToLeg();

        // only leaves the last coupon
        if (IsOptionlet && leg.Length > 1)
        {
            leg = [leg[^1]];
        }

        double strike;
        if (Strike is double k)
        {
            strike = k;
        }
        else
        {
            // ATM on the forecasting curve
            YieldTermStructure nominal = NominalTermStructure
                ?? throw new NanoQLException("empty Handle cannot be dereferenced");
            strike = CashFlows.AtmRate(leg, nominal, context, false, nominal.ReferenceDate);
        }

        return new YoYInflationCapFloor(CapFloorType, leg, [strike]);
    }
}
