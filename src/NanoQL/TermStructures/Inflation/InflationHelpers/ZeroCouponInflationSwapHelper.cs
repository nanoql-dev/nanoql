// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/inflationhelpers.hpp ql/termstructures/inflation/inflationhelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A bootstrap helper for zero-inflation term structures, on a zero-coupon inflation swap's quoted fixed rate.
/// </summary>
/// <remarks>
/// <para>
/// The helper's dates are those of the index's period at the maturity less the swap's observation lag: with an
/// interpolated observation, from that period's start to the next one's, the pillar chosen as QuantLib chooses it;
/// otherwise the period's start alone. Without a start date the swap starts on the evaluation date, which the helper's
/// dates follow; with one, they don't.
/// </para>
/// <para>
/// The helper bound to a curve (<see cref="ZeroInflationHelperExtensions.SetTermStructure{T}"/>) holds the index
/// cloned on the curve, and the swap rebuilt on it. Its implied quote is the swap's fair rate, which reads the index
/// through the curve and no discount: QuantLib's swap is priced on a flat zero nominal curve, whose equal discounts on
/// the two legs' one payment date cancel, and its fair rate reads none of them. QuantLib's deprecated nominal-curve
/// member isn't ported.
/// </para>
/// </remarks>
public sealed class ZeroCouponInflationSwapHelper : RelativeDateBootstrapHelper<ZeroInflationTermStructure>
{
    private readonly Period swapObsLag;
    private readonly Date? startDate;
    private readonly Date maturity;
    private readonly Calendar? calendar;
    private readonly BusinessDayConvention paymentConvention;
    private readonly DayCounter dayCounter;
    private readonly CPIInterpolationType observationInterpolation;

    // The index cloned on the bound curve, or on none; the swap built on it. Written at construction, by
    // InitializeDates and on a fresh copy (WithTermStructure), never after.
    private ZeroInflationIndex zii;
    private ZeroCouponInflationSwap zciis = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZeroCouponInflationSwapHelper"/> class whose swap starts on the
    /// evaluation date.
    /// </summary>
    /// <param name="quote">The swap's quoted fixed rate.</param>
    /// <param name="swapObsLag">The swap's observation lag.</param>
    /// <param name="maturity">The swap's maturity.</param>
    /// <param name="calendar">The payment calendar, or <see langword="null"/> for QuantLib's empty calendar, used
    /// unchecked as QuantLib uses it; the index has none, being valid on every day.</param>
    /// <param name="paymentConvention">The payment convention.</param>
    /// <param name="dayCounter">The swap's day counter.</param>
    /// <param name="zii">The zero-inflation index.</param>
    /// <param name="observationInterpolation">How the index is observed.</param>
    /// <param name="context">The evaluation context whose evaluation date the swap starts on.</param>
    /// <param name="pillar">How the pillar date is chosen.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="zii"/>, <paramref name="dayCounter"/> or
    /// <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The custom pillar date is outside the helper's dates, the observation lag
    /// can't observe fixings the index has made available, or a date is <c>default(Date)</c>.</exception>
    public ZeroCouponInflationSwapHelper(
        double quote,
        Period swapObsLag,
        Date maturity,
        Calendar? calendar,
        BusinessDayConvention paymentConvention,
        DayCounter dayCounter,
        ZeroInflationIndex zii,
        CPIInterpolationType observationInterpolation,
        EvaluationContext context,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null)
        : this(
            quote,
            swapObsLag,
            null,
            maturity,
            calendar,
            paymentConvention,
            dayCounter,
            zii,
            observationInterpolation,
            context ?? throw new ArgumentNullException(nameof(context)),
            pillar,
            customPillarDate)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZeroCouponInflationSwapHelper"/> class whose swap starts on a given
    /// date; its dates don't follow the evaluation date.
    /// </summary>
    /// <param name="quote">The swap's quoted fixed rate.</param>
    /// <param name="swapObsLag">The swap's observation lag.</param>
    /// <param name="startDate">The swap's start date.</param>
    /// <param name="endDate">The swap's maturity.</param>
    /// <param name="calendar">The payment calendar, or <see langword="null"/> for QuantLib's empty calendar, used
    /// unchecked as QuantLib uses it.</param>
    /// <param name="paymentConvention">The payment convention.</param>
    /// <param name="dayCounter">The swap's day counter.</param>
    /// <param name="zii">The zero-inflation index.</param>
    /// <param name="observationInterpolation">How the index is observed.</param>
    /// <param name="pillar">How the pillar date is chosen.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="zii"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The custom pillar date is outside the helper's dates, the observation lag
    /// can't observe fixings the index has made available, or a date is <c>default(Date)</c>.</exception>
    public ZeroCouponInflationSwapHelper(
        double quote,
        Period swapObsLag,
        Date startDate,
        Date endDate,
        Calendar? calendar,
        BusinessDayConvention paymentConvention,
        DayCounter dayCounter,
        ZeroInflationIndex zii,
        CPIInterpolationType observationInterpolation,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null)
        : this(
            quote,
            swapObsLag,
            CheckStart(startDate),
            endDate,
            calendar,
            paymentConvention,
            dayCounter,
            zii,
            observationInterpolation,
            null,
            pillar,
            customPillarDate)
    {
    }

    private ZeroCouponInflationSwapHelper(
        double quote,
        Period swapObsLag,
        Date? startDate,
        Date endDate,
        Calendar? calendar,
        BusinessDayConvention paymentConvention,
        DayCounter dayCounter,
        ZeroInflationIndex zii,
        CPIInterpolationType observationInterpolation,
        EvaluationContext? context,
        PillarChoice pillar,
        Date? customPillarDate)
        : base(quote, context, startDate is null)
    {
        Date.ThrowIfDefault(endDate, nameof(endDate));
        Date.ThrowIfDefault(customPillarDate, nameof(customPillarDate));
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(observationInterpolation, nameof(observationInterpolation));
        EnumArguments.Check(pillar, nameof(pillar));
        ArgumentNullException.ThrowIfNull(dayCounter);
        if (zii is null)
        {
            throw new ArgumentNullException(nameof(zii), "no index provided");
        }

        this.swapObsLag = swapObsLag;
        this.startDate = startDate;
        maturity = endDate;
        this.calendar = calendar;
        this.paymentConvention = paymentConvention;
        this.dayCounter = dayCounter;
        this.observationInterpolation = observationInterpolation;
        PillarChoice = pillar;

        // unbound: QuantLib's index cloned on an empty relinkable handle
        this.zii = zii.Clone(null);

        (Date fixingStart, Date fixingEnd) = InflationTermStructureFunctions.InflationPeriod(
            maturity - swapObsLag, this.zii.Frequency);

        if (CPI.IsInterpolated(observationInterpolation))
        {
            earliestDate = fixingStart;
            latestDate = fixingEnd + 1;

            switch (pillar)
            {
                case PillarChoice.MaturityDate:
                    pillarDate = latestDate;
                    break;
                case PillarChoice.LastRelevantDate:
                    // Assign the pillar to the node with the dominant
                    // interpolation weight.  We use startDate_ (the swap
                    // effective date) rather than maturity_ so that all
                    // helpers sharing the same effective date make the same
                    // LEFT/RIGHT decision.  Using maturity_ would cause
                    // collisions when consecutive helpers mature in months
                    // of different length (e.g. February vs March) and the
                    // day-of-month is near the mid-month threshold.
                    {
                        Date weightDate = startDate ?? maturity;
                        (Date weightStart, Date weightEnd) = InflationTermStructureFunctions.InflationPeriod(
                            weightDate, this.zii.Frequency);
                        double dp = (weightEnd + 1) - weightStart;
                        double dt = weightDate - weightStart;
                        if (dt / dp <= 0.5)
                        {
                            pillarDate = fixingStart;
                        }
                    }

                    break;
                default:
                    pillarDate = customPillarDate;
                    if (!(customPillarDate is Date custom && custom >= fixingStart))
                    {
                        throw new ArgumentException(
                            $"pillar date ({Show(customPillarDate)}) must be later than or equal to the instrument's "
                            + $"earliest date ({fixingStart})",
                            nameof(customPillarDate));
                    }

                    if (!(custom <= latestDate.Value))
                    {
                        throw new ArgumentException(
                            $"pillar date ({custom}) must be before or equal to the instrument's latest relevant date "
                            + $"({latestDate.Value})",
                            nameof(customPillarDate));
                    }

                    break;
            }
        }
        else
        {
            // Not interpolated: only the left node matters, so
            // earliestDate_ == latestDate_ and pillarChoice_ is
            // irrelevant (there is only one possible pillar).
            earliestDate = fixingStart;
            latestDate = fixingStart;
        }

        // check that the observation lag of the swap
        // is compatible with the availability lag of the index AND
        // it's interpolation (assuming the start day is spot)
        if (CPI.IsInterpolated(observationInterpolation))
        {
            var pShift = new Period(this.zii.Frequency);
            if (!(swapObsLag - pShift >= this.zii.AvailabilityLag))
            {
                throw new ArgumentException(
                    $"inconsistency between swap observation lag {swapObsLag}, index period {pShift} and index "
                    + $"availability {this.zii.AvailabilityLag}: need (obsLag-index period) >= availLag",
                    nameof(swapObsLag));
            }
        }

        zciis = BuildSwap();
    }

    /// <summary>Gets how the pillar date was chosen.</summary>
    public PillarChoice PillarChoice { get; }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>.</summary>
    public ZeroCouponInflationSwap Swap => zciis;

    /// <inheritdoc/>
    /// <remarks>The swap's fair rate, through the index cloned on the bound curve.</remarks>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        return zciis.FairRate(context);
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<ZeroInflationTermStructure> WithTermStructure(
        ZeroInflationTermStructure t)
    {
        var copy = (ZeroCouponInflationSwapHelper)base.WithTermStructure(t);
        copy.zii = zii.Clone(t);
        copy.zciis = copy.BuildSwap();
        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        zciis = BuildSwap();
    }

    private static Date CheckStart(Date startDate)
    {
        Date.ThrowIfDefault(startDate, nameof(startDate));
        return startDate;
    }

    private static string Show(Date? d) => d is Date date ? date.ToString() : "null date";

    // QuantLib's initializeDates: the swap from the evaluation date the helper's dates follow, or from its start date.
    private ZeroCouponInflationSwap BuildSwap() =>
        new(
            SwapType.Payer,
            1.0,
            UpdateDates ? EvaluationDate!.Value : startDate!.Value,
            maturity,
            calendar,
            paymentConvention,
            dayCounter,
            0.0,
            zii,
            swapObsLag,
            observationInterpolation);
}
