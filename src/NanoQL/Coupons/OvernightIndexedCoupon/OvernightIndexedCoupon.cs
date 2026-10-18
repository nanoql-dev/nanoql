// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/overnightindexedcoupon.hpp ql/cashflows/overnightindexedcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A coupon paying the interest, depending on the averaging convention, due to daily overnight fixings: compounded or
/// averaged, with QuantLib's lookback, lockout and observation-shift conventions.
/// </summary>
/// <remarks>
/// <para>
/// The constructor attaches QuantLib's default pricer: a <see cref="CompoundingOvernightIndexedCouponPricer"/> for a
/// compounded coupon, an <see cref="ArithmeticAveragedOvernightIndexedCouponPricer"/> for an averaged one;
/// <see cref="FloatingRateCouponExtensions.SetPricer"/> returns a copy holding another.
/// </para>
/// <para>
/// <b>Telescopic value dates.</b> The flag is taken, with QuantLib's check (a lookback without the observation shift
/// can't use it), and on an averaged coupon it selects the averaging pricer's approximation, as QuantLib's does. Its
/// date optimisation isn't ported: the value dates are always the full daily list, so the coupon reads no evaluation
/// date and gives QuantLib's full-schedule result on any date. The compounding pricer still telescopes the forward
/// part wherever <see cref="CanApplyTelescopicFormula"/> holds. QuantLib's shortened dates, which can give wrong results
/// once the coupon ages, are a documented divergence.
/// </para>
/// </remarks>
public sealed class OvernightIndexedCoupon : FloatingRateCoupon
{
    private static readonly CompoundingOvernightIndexedCouponPricer CompoundingPricer = new();
    private static readonly ArithmeticAveragedOvernightIndexedCouponPricer AveragingPricer = new(byApprox: false);
    private static readonly ArithmeticAveragedOvernightIndexedCouponPricer ApproximatingPricer = new(byApprox: true);

    private readonly ImmutableArray<Date> valueDates;
    private readonly ImmutableArray<Date> interestDates;
    private readonly ImmutableArray<Date> fixingDates;
    private readonly ImmutableArray<double> dt;
    private readonly int? roundingPrecision;

    /// <summary>Initializes a new instance of the <see cref="OvernightIndexedCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date, on or after the accrual end.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start of the accrual period.</param>
    /// <param name="endDate">The end of the accrual period, after its start.</param>
    /// <param name="overnightIndex">The overnight index.</param>
    /// <param name="gearing">The gearing applied to the compounded or averaged rate.</param>
    /// <param name="spread">The spread.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="dayCounter">The accrual day counter; the index's when <see langword="null"/>.</param>
    /// <param name="telescopicValueDates">QuantLib's telescopic value dates: refused with a lookback that can't use
    /// them, and on an averaged coupon the choice of the averaging pricer's approximation. The value dates are the
    /// full daily list either way.</param>
    /// <param name="averagingMethod">Compounded or averaged.</param>
    /// <param name="lookbackDays">The lookback in business days; the index's fixing days when <see langword="null"/>,
    /// QuantLib's <c>Null&lt;Natural&gt;()</c>.</param>
    /// <param name="lockoutDays">The lockout in business days.</param>
    /// <param name="applyObservationShift">Whether the observation period shifts with the lookback.</param>
    /// <param name="compoundSpreadDaily">Whether the spread compounds daily, or is added after compounding.</param>
    /// <param name="rateComputationStartDate">The start of the rate computation; the accrual start when
    /// <see langword="null"/>.</param>
    /// <param name="rateComputationEndDate">The end of the rate computation; the accrual end when
    /// <see langword="null"/>.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    /// <param name="roundingPrecision">The decimal places the rate is rounded to, by closest rounding, before the amount
    /// is computed; none when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="overnightIndex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The lookback or the lockout is negative, the rounding precision
    /// is outside [0, 16], or the averaging method isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The dates are out of order; the gearing is zero; the lockout isn't shorter
    /// than the period; the telescopic flag is given with a lookback that can't use it; or an averaged coupon has a
    /// lookback, a lockout or the observation shift.</exception>
    /// <exception cref="NanoQLException">The rate computation spans fewer than two value dates.</exception>
    public OvernightIndexedCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        OvernightIndex overnightIndex,
        double gearing = 1.0,
        double spread = 0.0,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        DayCounter? dayCounter = null,
        bool telescopicValueDates = false,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        int? lookbackDays = null,
        int lockoutDays = 0,
        bool applyObservationShift = false,
        bool compoundSpreadDaily = false,
        Date? rateComputationStartDate = null,
        Date? rateComputationEndDate = null,
        Date? exCouponDate = null,
        int? roundingPrecision = null)
        : base(
            paymentDate,
            nominal,
            startDate,
            endDate,
            CheckedLookback(lookbackDays),
            overnightIndex ?? throw new ArgumentNullException(nameof(overnightIndex)),
            gearing,
            spread,
            refPeriodStart,
            refPeriodEnd,
            dayCounter,
            false,
            exCouponDate,
            BusinessDayConvention.Preceding,
            DefaultPricer(averagingMethod, telescopicValueDates))
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lockoutDays);
        NanoQL.Time.Date.ThrowIfDefault(rateComputationStartDate, nameof(rateComputationStartDate));
        NanoQL.Time.Date.ThrowIfDefault(rateComputationEndDate, nameof(rateComputationEndDate));
        if (roundingPrecision is int precision)
        {
            Rounding.CheckPrecision(precision, nameof(roundingPrecision));
        }

        AveragingMethod = averagingMethod;
        LockoutDays = lockoutDays;
        ApplyObservationShift = applyObservationShift;
        CompoundSpreadDaily = compoundSpreadDaily;
        RateComputationStartDate = rateComputationStartDate;
        RateComputationEndDate = rateComputationEndDate;
        this.roundingPrecision = roundingPrecision;

        // ctor guard prevents construction of an object with illogically ordered dates.
        if (!(startDate < endDate))
        {
            throw new ArgumentException("startDate must be less than endDate", nameof(startDate));
        }

        if (!(paymentDate >= endDate))
        {
            throw new ArgumentException("Payment date cannot be earlier than accrual end date", nameof(paymentDate));
        }

        Date rateCalcStartDate = rateComputationStartDate ?? startDate;
        Date rateCalcEndDate = rateComputationEndDate ?? endDate;

        if (!(CanApplyTelescopicFormula || !telescopicValueDates))
        {
            throw new ArgumentException(
                "Telescopic formula cannot be applied for a coupon with lookback.", nameof(telescopicValueDates));
        }

        // value dates: every business day of the rate computation, whatever telescopicValueDates says. QuantLib's
        // telescopic coupon builds a front stub to 7 business days after the evaluation date and jumps to the lockout
        // or end dates (overnightindexedcoupon.cpp:107-132); that optimisation isn't ported.
        Calendar fixingCal = overnightIndex.FixingCalendar;
        ImmutableArray<Date> businessDays = fixingCal.BusinessDayList(
            fixingCal.Adjust(rateCalcStartDate, BusinessDayConvention.Preceding),
            fixingCal.Adjust(rateCalcEndDate, BusinessDayConvention.Following));

        if (businessDays.Length < 2)
        {
            throw new NanoQLException("degenerate schedule");
        }

        int n = businessDays.Length - 1;
        Date[] value = [.. businessDays];
        Date[] interest = [.. businessDays];
        interest[0] = rateCalcStartDate;
        interest[n] = rateCalcEndDate;

        var fixing = new Date[n];
        if (FixingDays == overnightIndex.FixingDays && FixingDays == 0)
        {
            Array.Copy(value, fixing, n);
        }
        else
        {
            // Lookback (fixing days) without observation shift:
            // The date that the fixing rate is pulled  from (the observation date) is k
            // business days before the date that interest is applied (the interest date)
            // and is applied for the number of calendar days until the next business
            // day following the interest date.
            for (int i = 0; i <= n; ++i)
            {
                Date tmp = fixingCal.Advance(value[i], -FixingDays, TimeUnit.Days);
                if (i < n)
                {
                    fixing[i] = tmp;
                }

                if (FixingDays != overnightIndex.FixingDays)
                {
                    // If fixing dates of the coupon deviate from fixing days in the index
                    // we need to correct the value dates such that they reflect dates
                    // corresponding to a deposit instrument linked to the index.
                    // This is to ensure that future projections (which are computed
                    // based on the value dates) of the index do not
                    // yield any convexity corrections.
                    value[i] = overnightIndex.ValueDate(tmp);
                }
            }
        }

        // When lockout is used the fixing rate applied for the last k days of the
        // interest period is frozen at the rate observed k days before the period ends.
        if (lockoutDays != 0)
        {
            // A negative lockout was refused on entry.
            if (lockoutDays >= n)
            {
                throw new ArgumentException(
                    "Lockout period cannot be negative or exceed the number of fixing days.", nameof(lockoutDays));
            }

            Date lockoutDate = fixing[n - 1 - lockoutDays];
            Array.Fill(fixing, lockoutDate, n - lockoutDays, lockoutDays);
        }

        // accrual (compounding) periods. QuantLib tests the lookback itself, where Null<Natural>() is a large value, so
        // a null lookback with the shift accrues on the value dates.
        DayCounter dc = overnightIndex.DayCounter;
        Date[] accrualDates = applyObservationShift && lookbackDays is null or > 0 ? value : interest;
        var periods = new double[n];
        for (int i = 0; i < n; ++i)
        {
            periods[i] = dc.YearFraction(accrualDates[i], accrualDates[i + 1]);
        }

        if (averagingMethod == RateAveragingType.Simple
            && !(FixingDays == overnightIndex.FixingDays && !applyObservationShift && lockoutDays == 0))
        {
            throw new ArgumentException(
                "Cannot price an overnight coupon with simple averaging with lookback or lockout.",
                nameof(averagingMethod));
        }

        valueDates = [.. value];
        interestDates = [.. interest];
        fixingDates = [.. fixing];
        dt = [.. periods];
    }

    /// <summary>Gets the averaging method.</summary>
    public RateAveragingType AveragingMethod { get; }

    /// <summary>Gets the lockout days.</summary>
    public int LockoutDays { get; }

    /// <summary>Gets a value indicating whether the observation shift applies.</summary>
    public bool ApplyObservationShift { get; }

    /// <summary>Gets a value indicating whether the spread is compounded daily, or added after compounding.</summary>
    public bool CompoundSpreadDaily { get; }

    /// <summary>Gets the rate computation's start date, as given; <see langword="null"/> when none was.</summary>
    public Date? RateComputationStartDate { get; }

    /// <summary>Gets the rate computation's end date, as given; <see langword="null"/> when none was.</summary>
    public Date? RateComputationEndDate { get; }

    /// <summary>
    /// Gets a value indicating whether the telescopic formula applies: the lookback is the index's fixing delay, or the
    /// index fixes with no delay and the observation shift applies.
    /// </summary>
    public bool CanApplyTelescopicFormula =>
        FixingDays == Index.FixingDays || (ApplyObservationShift && Index.FixingDays == 0);

    /// <summary>The fixing dates of the rates compounded or averaged, one per accrual period.</summary>
    /// <returns>The fixing dates.</returns>
    public ImmutableArray<Date> FixingDates() => fixingDates;

    /// <summary>The accrual (compounding) periods, as year fractions by the index's day counter.</summary>
    /// <returns>The periods.</returns>
    public ImmutableArray<double> Dt() => dt;

    /// <summary>The value dates of the rates compounded or averaged: every daily value date.</summary>
    /// <returns>The value dates, one more than the fixing dates.</returns>
    public ImmutableArray<Date> ValueDates() => valueDates;

    /// <summary>The interest dates of the rates compounded or averaged.</summary>
    /// <returns>The interest dates: the value dates, starting and ending at the rate computation's dates.</returns>
    public ImmutableArray<Date> InterestDates() => interestDates;

    /// <summary>The fixings compounded or averaged: the index's fixing on each fixing date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixings.</returns>
    public ImmutableArray<double> IndexFixings(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ImmutableArray<double>.Builder fixings = ImmutableArray.CreateBuilder<double>(fixingDates.Length);
        foreach (Date d in fixingDates)
        {
            fixings.Add(Index.Fixing(d, context));
        }

        return fixings.MoveToImmutable();
    }

    /// <summary>
    /// The effective spread, which with <see cref="EffectiveIndexFixing"/> makes the amount nominal × accrual period ×
    /// (gearing × effective index fixing + effective spread): the spread itself unless it compounds daily on a
    /// compounded coupon.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The effective spread.</returns>
    /// <exception cref="NanoQLException">The spread compounds daily and the pricer isn't a compounding pricer.</exception>
    public double EffectiveSpread(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!CompoundSpreadDaily)
        {
            return Spread;
        }

        if (AveragingMethod == RateAveragingType.Simple)
        {
            return Spread;
        }

        var p = Pricer as CompoundingOvernightIndexedCouponPricer
            ?? throw new NanoQLException(
                "OvernightIndexedCoupon::effectiveSpread(): expected OvernightIndexedCouponPricer");
        return p.EffectiveSpread(this, context);
    }

    /// <summary>The effective index fixing (see <see cref="EffectiveSpread"/>).</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The effective index fixing.</returns>
    /// <exception cref="NanoQLException">The coupon is averaged, or its pricer isn't a compounding pricer.</exception>
    public double EffectiveIndexFixing(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var p = Pricer as CompoundingOvernightIndexedCouponPricer;

        if (AveragingMethod == RateAveragingType.Simple)
        {
            throw new NanoQLException("Average OIS Coupon does not have an effectiveIndexFixing");
        }

        if (p is null)
        {
            throw new NanoQLException(
                "OvernightIndexedCoupon::effectiveSpread(): expected OvernightIndexedCouponPricer");
        }

        return p.EffectiveIndexFixing(this, context);
    }

    /// <summary>The date when the coupon is fully determined: its last fixing date.</summary>
    /// <returns>The last fixing date.</returns>
    public override Date FixingDate() => fixingDates[^1];

    /// <inheritdoc/>
    public override double AccruedAmount(Date d, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        NanoQL.Time.Date.ThrowIfDefault(d, nameof(d));
        if (d <= AccrualStartDate || d > Date)
        {
            // out of coupon range
            return 0.0;
        }
        else if (TradingExCouponOn(d))
        {
            return Nominal(context) * AverageRate(d, context) * AccruedPeriod(d);
        }
        else
        {
            // usual case
            return Nominal(context) * AverageRate(d < AccrualEndDate ? d : AccrualEndDate, context) * AccruedPeriod(d);
        }
    }

    /// <summary>The amount: the rate, rounded when a rounding precision was given, × accrual period × nominal.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The amount.</returns>
    public override double Amount(EvaluationContext context)
    {
        double r = Rate(context);

        if (roundingPrecision is int precision)
        {
            r = new ClosestRounding(precision).Evaluate(r);
        }

        return r * AccrualPeriod() * Nominal(context);
    }

    private static int? CheckedLookback(int? lookbackDays)
    {
        if (lookbackDays is int days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(days, nameof(lookbackDays));
        }

        return lookbackDays;
    }

    // QuantLib's default pricers (overnightindexedcoupon.cpp:181-193); a pricer holds no per-coupon state, so one
    // instance of each serves every coupon. The averaging method is checked on entry, before anything reads it.
    private static OvernightIndexedCouponPricer DefaultPricer(RateAveragingType averagingMethod, bool telescopicValueDates)
    {
        EnumArguments.Check(averagingMethod, nameof(averagingMethod));
        return averagingMethod switch
        {
            RateAveragingType.Simple => telescopicValueDates ? ApproximatingPricer : AveragingPricer,
            _ => CompoundingPricer,
        };
    }

    private double AverageRate(Date d, EvaluationContext context)
    {
        FloatingRateCouponPricer p = Pricer ?? throw new NanoQLException("pricer not set");
        return p is OvernightIndexedCouponPricer overnightIndexedPricer
            ? overnightIndexedPricer.AverageRate(this, d, context)
            : p.SwapletRate(this, context);
    }
}
