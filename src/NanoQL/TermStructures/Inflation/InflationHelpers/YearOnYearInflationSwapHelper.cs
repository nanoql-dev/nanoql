// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/inflationhelpers.hpp ql/termstructures/inflation/inflationhelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A bootstrap helper for year-on-year inflation term structures, on a year-on-year inflation swap's quoted fixed rate.
/// </summary>
/// <remarks>
/// <para>
/// The helper's dates are those of the index's period at the maturity less the swap's observation lag: with an
/// interpolated observation, from that period's start to the next one's, the pillar chosen as QuantLib chooses it;
/// otherwise the period's start alone. The swap's two legs share one annual schedule, generated backwards from the
/// maturity, unadjusted. Without a start date the swap starts on the evaluation date, which the helper's dates follow;
/// with one, they don't.
/// </para>
/// <para>
/// The helper bound to a curve (<see cref="YoYInflationHelperExtensions.SetTermStructure{T}"/>) holds the index cloned
/// on the curve, and the swap rebuilt on it. Its implied quote is the swap's fair rate, priced by the discounting swap
/// engine on the nominal curve; with no nominal curve, it refuses with QuantLib's engine's text unless the swap has
/// expired, and then with QuantLib's "result not available".
/// </para>
/// </remarks>
public sealed class YearOnYearInflationSwapHelper : RelativeDateBootstrapHelper<YoYInflationTermStructure>
{
    private readonly Period swapObsLag;
    private readonly Date? startDate;
    private readonly Date maturity;
    private readonly Calendar? calendar;
    private readonly BusinessDayConvention paymentConvention;
    private readonly DayCounter dayCounter;
    private readonly CPIInterpolationType interpolation;

    // The index cloned on the bound curve, or on none; the swap built on it. Written at construction, by
    // InitializeDates and on a fresh copy (WithTermStructure), never after.
    private YoYInflationIndex yii;
    private YearOnYearInflationSwap yyiis = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="YearOnYearInflationSwapHelper"/> class whose swap starts on the
    /// evaluation date.
    /// </summary>
    /// <param name="quote">The swap's quoted fixed rate.</param>
    /// <param name="swapObsLag">The swap's observation lag.</param>
    /// <param name="maturity">The swap's maturity.</param>
    /// <param name="calendar">The schedule's and the payments' calendar, or <see langword="null"/> for QuantLib's empty
    /// calendar, used unchecked as QuantLib uses it.</param>
    /// <param name="paymentConvention">The payment convention.</param>
    /// <param name="dayCounter">The swap's day counter, on both legs.</param>
    /// <param name="yii">The year-on-year inflation index.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="nominalTermStructure">The nominal curve the swap is discounted on, or <see langword="null"/>,
    /// QuantLib's empty handle.</param>
    /// <param name="context">The evaluation context whose evaluation date the swap starts on.</param>
    /// <param name="pillar">How the pillar date is chosen.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="yii"/>, <paramref name="dayCounter"/> or
    /// <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The custom pillar date is outside the helper's dates, the observation lag
    /// can't observe fixings the index has made available, or a date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The swap's schedule or legs can't be built.</exception>
    public YearOnYearInflationSwapHelper(
        double quote,
        Period swapObsLag,
        Date maturity,
        Calendar? calendar,
        BusinessDayConvention paymentConvention,
        DayCounter dayCounter,
        YoYInflationIndex yii,
        CPIInterpolationType interpolation,
        YieldTermStructure? nominalTermStructure,
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
            yii,
            interpolation,
            nominalTermStructure,
            context ?? throw new ArgumentNullException(nameof(context)),
            pillar,
            customPillarDate)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YearOnYearInflationSwapHelper"/> class whose swap starts on a given
    /// date; its dates don't follow the evaluation date.
    /// </summary>
    /// <param name="quote">The swap's quoted fixed rate.</param>
    /// <param name="swapObsLag">The swap's observation lag.</param>
    /// <param name="startDate">The swap's start date.</param>
    /// <param name="endDate">The swap's maturity.</param>
    /// <param name="calendar">The schedule's and the payments' calendar, or <see langword="null"/> for QuantLib's empty
    /// calendar, used unchecked as QuantLib uses it.</param>
    /// <param name="paymentConvention">The payment convention.</param>
    /// <param name="dayCounter">The swap's day counter, on both legs.</param>
    /// <param name="yii">The year-on-year inflation index.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="nominalTermStructure">The nominal curve the swap is discounted on, or <see langword="null"/>,
    /// QuantLib's empty handle.</param>
    /// <param name="pillar">How the pillar date is chosen.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="yii"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The custom pillar date is outside the helper's dates, the observation lag
    /// can't observe fixings the index has made available, or a date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The swap's schedule or legs can't be built.</exception>
    public YearOnYearInflationSwapHelper(
        double quote,
        Period swapObsLag,
        Date startDate,
        Date endDate,
        Calendar? calendar,
        BusinessDayConvention paymentConvention,
        DayCounter dayCounter,
        YoYInflationIndex yii,
        CPIInterpolationType interpolation,
        YieldTermStructure? nominalTermStructure,
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
            yii,
            interpolation,
            nominalTermStructure,
            null,
            pillar,
            customPillarDate)
    {
    }

    private YearOnYearInflationSwapHelper(
        double quote,
        Period swapObsLag,
        Date? startDate,
        Date endDate,
        Calendar? calendar,
        BusinessDayConvention paymentConvention,
        DayCounter dayCounter,
        YoYInflationIndex yii,
        CPIInterpolationType interpolation,
        YieldTermStructure? nominalTermStructure,
        EvaluationContext? context,
        PillarChoice pillar,
        Date? customPillarDate)
        : base(quote, context, startDate is null)
    {
        Date.ThrowIfDefault(endDate, nameof(endDate));
        Date.ThrowIfDefault(customPillarDate, nameof(customPillarDate));
        EnumArguments.Check(paymentConvention, nameof(paymentConvention));
        EnumArguments.Check(interpolation, nameof(interpolation));
        EnumArguments.Check(pillar, nameof(pillar));
        ArgumentNullException.ThrowIfNull(dayCounter);
        if (yii is null)
        {
            throw new ArgumentNullException(nameof(yii), "no index provided");
        }

        this.swapObsLag = swapObsLag;
        this.startDate = startDate;
        maturity = endDate;
        this.calendar = calendar;
        this.paymentConvention = paymentConvention;
        this.dayCounter = dayCounter;
        this.interpolation = interpolation;
        PillarChoice = pillar;
        NominalTermStructure = nominalTermStructure;

        // unbound: QuantLib's index cloned on an empty relinkable handle
        this.yii = yii.Clone(null);

        (Date fixingStart, Date fixingEnd) = InflationTermStructureFunctions.InflationPeriod(
            maturity - swapObsLag, this.yii.Frequency);

        if (CPI.IsInterpolated(interpolation))
        {
            earliestDate = fixingStart;
            latestDate = fixingEnd + 1;

            switch (pillar)
            {
                case PillarChoice.MaturityDate:
                    pillarDate = latestDate;
                    break;
                case PillarChoice.LastRelevantDate:
                    {
                        Date weightDate = startDate ?? maturity;
                        (Date weightStart, Date weightEnd) = InflationTermStructureFunctions.InflationPeriod(
                            weightDate, this.yii.Frequency);
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
        // its interpolation (assuming the start day is spot)
        if (CPI.IsInterpolated(interpolation))
        {
            var pShift = new Period(this.yii.Frequency);
            if (!(swapObsLag - pShift >= this.yii.AvailabilityLag))
            {
                throw new ArgumentException(
                    $"inconsistency between swap observation lag {swapObsLag}, index period {pShift} and index "
                    + $"availability {this.yii.AvailabilityLag}: need (obsLag-index period) >= availLag",
                    nameof(swapObsLag));
            }
        }

        yyiis = BuildSwap();
    }

    /// <summary>Gets how the pillar date was chosen.</summary>
    public PillarChoice PillarChoice { get; }

    /// <summary>Gets the nominal curve the swap is discounted on, or <see langword="null"/> for none.</summary>
    public YieldTermStructure? NominalTermStructure { get; }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>.</summary>
    public YearOnYearInflationSwap Swap => yyiis;

    /// <inheritdoc/>
    /// <remarks>The swap's fair rate, priced on the nominal curve through the index cloned on the bound
    /// curve.</remarks>
    /// <exception cref="NanoQLException">There is no nominal curve and the swap hasn't expired; or the swap has
    /// expired, or its fixed leg has no BPS, so that there is no fair rate.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        YearOnYearInflationSwapResults results;
        if (NominalTermStructure is { } nominal)
        {
            results = DiscountingSwapEngine.Calculate(yyiis, context, nominal);
        }
        else if (yyiis.IsExpired(context))
        {
            results = yyiis.SetupExpired();
        }
        else
        {
            // QuantLib's engine on its empty handle, after the swap's arguments are validated
            yyiis.Validate();
            throw new NanoQLException("discounting term structure handle is empty");
        }

        return results.FairRate ?? throw new NanoQLException("result not available");
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YoYInflationTermStructure> WithTermStructure(
        YoYInflationTermStructure t)
    {
        var copy = (YearOnYearInflationSwapHelper)base.WithTermStructure(t);
        copy.yii = yii.Clone(t);
        copy.yyiis = copy.BuildSwap();
        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        yyiis = BuildSwap();
    }

    private static Date CheckStart(Date startDate)
    {
        Date.ThrowIfDefault(startDate, nameof(startDate));
        return startDate;
    }

    private static string Show(Date? d) => d is Date date ? date.ToString() : "null date";

    // QuantLib's initializeDates: the swap from the evaluation date the helper's dates follow, or from its start date.
    private YearOnYearInflationSwap BuildSwap()
    {
        // always works because tenor is always 1 year so
        // no problem with different days-in-month
        Schedule fixedSchedule = new MakeSchedule()
            .From(UpdateDates ? EvaluationDate!.Value : startDate!.Value)
            .To(maturity)
            .WithTenor(new Period(1, TimeUnit.Years))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .WithCalendar(calendar) // fixed leg gets cal from sched
            .Backwards()
            .ToSchedule();
        Schedule yoySchedule = fixedSchedule;

        return new YearOnYearInflationSwap(
            SwapType.Payer,
            1.0,
            fixedSchedule,
            0.0,
            dayCounter,
            yoySchedule,
            yii,
            swapObsLag,
            interpolation,
            0.0,
            dayCounter,
            calendar,
            paymentConvention);
    }
}
