// SPDX-FileCopyrightText: Copyright (C) 2018 Roy Zywina
// SPDX-FileCopyrightText: Copyright (C) 2019 Eisuke Tani
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/overnightindexfuture.hpp ql/instruments/overnightindexfuture.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A future on an overnight index's rate over a reference period, compounded or averaged: SOFR and SONIA futures on
/// CME and ICE. Its NPV is 100 × (1 − R), R the period's rate plus the convexity adjustment.
/// </summary>
/// <remarks>
/// <para>
/// The rate's known part reads the stored fixings, each compounded through the index's
/// <see cref="OvernightIndex.GrowthFactor"/> or accrued through its <see cref="OvernightIndex.SimpleAccrual"/>,
/// QuantLib's 1 + r·τ and r·τ on a simple index; the rest is forecast on the index's forwarding curve, which must reach
/// the dates it forecasts from, with QuantLib master's texts (#2897).
/// </para>
/// <para>
/// A compounded future on its value date uses today's fixing once it is published, as the averaged one does and as the
/// compounded one does on later dates; QuantLib's skips it there (<c>overnightindexfuture.cpp:89-114</c>, its <c>today
/// &gt; valueDate_</c>), so the same future's price moves when its first fixing becomes a past one.
/// </para>
/// </remarks>
public sealed class OvernightIndexFuture : Instrument
{
    private readonly double? convexityAdjustment;

    /// <summary>Initializes a new instance of the <see cref="OvernightIndexFuture"/> class.</summary>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <param name="valueDate">The first day of the reference period.</param>
    /// <param name="maturityDate">The end of the reference period, the delivery date.</param>
    /// <param name="convexityAdjustment">The convexity adjustment, or <see langword="null"/>, QuantLib's empty handle,
    /// for none.</param>
    /// <param name="averagingMethod">Whether the rate compounds or averages over the period.</param>
    /// <exception cref="ArgumentNullException"><paramref name="overnightIndex"/> is null, QuantLib's "null overnight
    /// index".</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="averagingMethod"/> isn't a member of its
    /// enum.</exception>
    public OvernightIndexFuture(
        OvernightIndex overnightIndex,
        Date valueDate,
        Date maturityDate,
        double? convexityAdjustment = null,
        RateAveragingType averagingMethod = RateAveragingType.Compound)
    {
        if (overnightIndex is null)
        {
            throw new ArgumentNullException(nameof(overnightIndex), "null overnight index");
        }

        EnumArguments.Check(averagingMethod, nameof(averagingMethod));
        OvernightIndex = overnightIndex;
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        this.convexityAdjustment = convexityAdjustment;
        AveragingMethod = averagingMethod;
    }

    /// <summary>Gets the overnight index.</summary>
    public OvernightIndex OvernightIndex { get; }

    /// <summary>Gets the first day of the reference period.</summary>
    public Date ValueDate { get; }

    /// <summary>Gets the end of the reference period.</summary>
    public Date MaturityDate { get; }

    /// <summary>Gets whether the rate compounds or averages.</summary>
    public RateAveragingType AveragingMethod { get; }

    /// <summary>Gets the convexity adjustment, 0 for none.</summary>
    public double ConvexityAdjustment => convexityAdjustment ?? 0.0;

    /// <summary>A copy on another overnight index, the same future otherwise; a rate helper's bound copy.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The copy.</returns>
    internal OvernightIndexFuture WithIndex(OvernightIndex index) =>
        new(index, ValueDate, MaturityDate, convexityAdjustment, AveragingMethod);

    /// <inheritdoc/>
    /// <remarks>A future expires once its maturity date has occurred.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(MaturityDate).HasOccurred(context);
    }

    /// <summary>Prices the future: QuantLib's <c>Instrument::calculate</c>, 100 × (1 − R).</summary>
    /// <param name="context">The evaluation context, whose evaluation date splits known fixings from forecast
    /// ones.</param>
    /// <returns>The results; an expired future's NPV is zero.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">A past fixing is missing; or a rate is forecast with no forwarding curve, or
    /// from a date before the curve's reference date; each with QuantLib's text.</exception>
    public InstrumentResults Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsExpired(context))
        {
            return SetupExpired();
        }

        Validate();
        double r = ConvexityAdjustment + Rate(context);
        return FetchResults(new InstrumentResults { NPV = 100.0 * (1.0 - r) });
    }

    private double Rate(EvaluationContext context) => AveragingMethod switch
    {
        RateAveragingType.Simple => AveragedRate(context),
        _ => CompoundedRate(context),
    };

    // #2897's check: the forwarding curve exists and starts by the date it forecasts from.
    private YieldTermStructure CheckForecastStart(Date d)
    {
        YieldTermStructure forwardCurve = OvernightIndex.ForwardingTermStructure
            ?? throw new NanoQLException($"null term structure set to this instance of {OvernightIndex.Name}");

        // rates between the evaluation date and a later reference date can't be forecast
        if (!(d >= forwardCurve.ReferenceDate))
        {
            throw new NanoQLException(
                $"cannot forecast {OvernightIndex.Name} rates from {d}: the forwarding term structure starts on "
                + $"{forwardCurve.ReferenceDate}");
        }

        return forwardCurve;
    }

    private double ForecastFixing(Date fixingDate, Date end) =>
        CheckForecastStart(fixingDate).ForwardRate(fixingDate, end, OvernightIndex.DayCounter, Compounding.Simple).Rate;

    private double PastRate(Date fixingDate, EvaluationContext context) =>
        OvernightIndex.PastFixing(fixingDate, context)
        ?? throw new NanoQLException($"missing rate on {fixingDate} for index {OvernightIndex.Name}");

    private double AveragedRate(EvaluationContext context)
    {
        Date today = context.EvaluationDate;
        Calendar calendar = OvernightIndex.FixingCalendar;
        DayCounter dayCounter = OvernightIndex.DayCounter;
        double avg = 0;
        Date d1 = ValueDate;

        // d1 could be a holiday
        Date fixingDate = calendar.Adjust(d1, BusinessDayConvention.Preceding);
        while (d1 < MaturityDate)
        {
            Date d2 = calendar.Advance(d1, 1, TimeUnit.Days);

            // The rate is accrued starting from d1 even when the fixing date is earlier.
            // d2 might be beyond the maturity date if the latter is a holiday.
            double span = dayCounter.YearFraction(d1, d2 < MaturityDate ? d2 : MaturityDate);

            // a fixing accrues in the index's convention; the curve's forecast is a simple rate
            if (fixingDate < today)
            {
                avg += OvernightIndex.SimpleAccrual(PastRate(fixingDate, context), span);
            }
            else if (fixingDate == today && OvernightIndex.PastFixing(fixingDate, context) is double fixing)
            {
                avg += OvernightIndex.SimpleAccrual(fixing, span);
            }
            else
            {
                avg += ForecastFixing(fixingDate, d2) * span;
            }

            fixingDate = d1 = d2;
        }

        return avg / dayCounter.YearFraction(ValueDate, MaturityDate);
    }

    private double CompoundedRate(EvaluationContext context)
    {
        Date today = context.EvaluationDate;
        Calendar calendar = OvernightIndex.FixingCalendar;
        DayCounter dayCounter = OvernightIndex.DayCounter;
        double prod = 1;
        Date forwardDiscountStart = ValueDate;
        if (today > ValueDate)
        {
            // can't value on a weekend inside reference period because we
            // won't know the reset rate until start of next business day.
            // user can supply an estimate if they really want to do this
            today = calendar.Adjust(today);
            forwardDiscountStart = today;

            // for valuations inside the reference period, index quotes
            // must have been populated in the history
            Date d1 = ValueDate;

            // d1 could be a holiday
            Date fixingDate = calendar.Adjust(d1, BusinessDayConvention.Preceding);
            while (d1 < today)
            {
                double r = PastRate(fixingDate, context);
                Date d2 = calendar.Advance(d1, 1, TimeUnit.Days);

                // The rate is accrued starting from d1 even when the fixing date is earlier.
                // We can't get to the maturity date inside this loop,
                // so we don't need to cap d2 like we do in averagedRate above.
                prod *= OvernightIndex.GrowthFactor(r, dayCounter.YearFraction(d1, d2));
                fixingDate = d1 = d2;
            }
        }

        // here today is on or after the value date, and we might have today's fixing already: on the value date too,
        // where QuantLib's skips it
        if (today >= ValueDate && today < MaturityDate && OvernightIndex.PastFixing(today, context) is double todays)
        {
            Date tomorrow = calendar.Advance(today, 1, TimeUnit.Days);
            Date accrualEnd = tomorrow < MaturityDate ? tomorrow : MaturityDate;
            prod *= OvernightIndex.GrowthFactor(todays, dayCounter.YearFraction(today, accrualEnd));
            forwardDiscountStart = accrualEnd;
        }

        // the telescopic part goes from the end of the last known fixing to the maturity
        YieldTermStructure forwardCurve = CheckForecastStart(forwardDiscountStart);
        double forwardDiscount = forwardCurve.Discount(MaturityDate) / forwardCurve.Discount(forwardDiscountStart);
        prod /= forwardDiscount;
        return (prod - 1) / dayCounter.YearFraction(ValueDate, MaturityDate);
    }
}
