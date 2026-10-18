// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/stubiborcoupon.hpp ql/cashflows/stubiborcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>An Ibor coupon for a broken period, fixing on indices chosen by a <see cref="StubIndexSelection"/>.</summary>
/// <remarks>
/// <para>
/// The coupon fixes on a <see cref="StubIborCouponWeightedIndex"/> of the indices the selection picks for its accrual
/// dates, which is its <see cref="IborCoupon.IborIndex"/>. Its fixing reads no at-par choice, so it needs no pricer;
/// its rate does, as any Ibor coupon's.
/// </para>
/// <para>
/// Its fixing days are a plain count: QuantLib's stub index reads <c>Null&lt;Natural&gt;()</c> as a count of days, and
/// its only caller, <see cref="IborLeg"/>, passes the leg's coupon's resolved count.
/// </para>
/// </remarks>
public sealed class StubIborCoupon : IborCoupon
{
    private readonly StubIborCouponWeightedIndex weightedIndex;

    /// <summary>Initializes a new instance of the <see cref="StubIborCoupon"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start of the accrual period, which must be each selected index's value date.</param>
    /// <param name="endDate">The end of the accrual period.</param>
    /// <param name="fixingDays">The business days from the fixing date to the accrual start.</param>
    /// <param name="stubIndexSelection">The selection of indices, which must not be empty.</param>
    /// <param name="gearing">The gearing applied to the index fixing.</param>
    /// <param name="spread">The spread added to the geared fixing.</param>
    /// <param name="refPeriodStart">The reference period's start; the accrual start when <see langword="null"/>.</param>
    /// <param name="refPeriodEnd">The reference period's end; the accrual end when <see langword="null"/>.</param>
    /// <param name="dayCounter">The accrual day counter; the indices' when <see langword="null"/>.</param>
    /// <param name="isInArrears">Whether the index fixes at the end of the accrual period, which a stub coupon refuses.</param>
    /// <param name="exCouponDate">The ex-coupon date, if any.</param>
    /// <param name="fixingConvention">The convention adjusting the fixing date.</param>
    /// <param name="pricer">The pricer, or <see langword="null"/> to set one later through
    /// <see cref="FloatingRateCouponExtensions.SetPricer"/>.</param>
    /// <exception cref="ArgumentException">The selection is empty; the coupon is in arrears; a candidate's value date
    /// isn't the accrual start; two candidates share a maturity; or no two maturities bracket the end date.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative, or
    /// <paramref name="fixingConvention"/> isn't a member of its type.</exception>
    public StubIborCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int fixingDays,
        StubIndexSelection stubIndexSelection,
        double gearing = 1.0,
        double spread = 0.0,
        Date? refPeriodStart = null,
        Date? refPeriodEnd = null,
        DayCounter? dayCounter = null,
        bool isInArrears = false,
        Date? exCouponDate = null,
        BusinessDayConvention fixingConvention = BusinessDayConvention.Preceding,
        FloatingRateCouponPricer? pricer = null)
        : this(
            paymentDate,
            nominal,
            startDate,
            endDate,
            fixingDays,
            StubIborCouponSelection.MakeStubIndex(
                stubIndexSelection, startDate, endDate, fixingDays, isInArrears, fixingConvention),
            stubIndexSelection,
            gearing,
            spread,
            refPeriodStart,
            refPeriodEnd,
            dayCounter,
            isInArrears,
            exCouponDate,
            fixingConvention,
            pricer)
    {
    }

    private StubIborCoupon(
        Date paymentDate,
        double nominal,
        Date startDate,
        Date endDate,
        int fixingDays,
        StubIborCouponWeightedIndex weightedIndex,
        StubIndexSelection stubIndexSelection,
        double gearing,
        double spread,
        Date? refPeriodStart,
        Date? refPeriodEnd,
        DayCounter? dayCounter,
        bool isInArrears,
        Date? exCouponDate,
        BusinessDayConvention fixingConvention,
        FloatingRateCouponPricer? pricer)
        : base(
            paymentDate,
            nominal,
            startDate,
            endDate,
            fixingDays,
            weightedIndex,
            gearing,
            spread,
            refPeriodStart,
            refPeriodEnd,
            dayCounter,
            isInArrears,
            exCouponDate,
            fixingConvention,
            pricer)
    {
        StubIndexSelection = stubIndexSelection;
        this.weightedIndex = weightedIndex;
        foreach ((IborIndex index, _) in weightedIndex.Components)
        {
            if (index.ValueDate(FixingDate()) != AccrualStartDate)
            {
                throw new ArgumentException(
                    $"{index.Name} value date for the coupon fixing date {FixingDate()} does not match accrual start "
                    + $"date {AccrualStartDate}",
                    nameof(startDate));
            }
        }
    }

    /// <summary>Gets the selection of indices the coupon was built with.</summary>
    public StubIndexSelection StubIndexSelection { get; }

    /// <summary>
    /// Whether the coupon has fixed: its fixing date is before the evaluation date, or on it when the context enforces
    /// today's historic fixings or holds a fixing for every selected index.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the coupon has fixed.</returns>
    public override bool HasFixed(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date today = context.EvaluationDate;
        if (FixingDate() > today)
        {
            return false;
        }

        if (FixingDate() < today)
        {
            return true;
        }

        if (context.EnforcesTodaysHistoricFixings)
        {
            return true;
        }

        foreach ((IborIndex index, _) in weightedIndex.Components)
        {
            if (!index.HasHistoricalFixing(FixingDate(), context))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The fixing of the weighted index on the fixing date, whatever the at-par choice.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="useIndexedCoupon">Ignored: the weighted index fixes on its own dates.</param>
    /// <returns>The fixing.</returns>
    protected internal override double IndexFixing(EvaluationContext context, bool useIndexedCoupon)
    {
        ArgumentNullException.ThrowIfNull(context);
        return weightedIndex.Fixing(FixingDate(), context);
    }
}

/// <summary>The stub coupon's index selection: QuantLib's anonymous <c>selectComponents</c> and <c>makeStubIndex</c>.</summary>
file static class StubIborCouponSelection
{
    /// <summary>The weighted index a stub coupon fixes on.</summary>
    /// <param name="selection">The selection.</param>
    /// <param name="startDate">The accrual start.</param>
    /// <param name="endDate">The accrual end.</param>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="isInArrears">Whether the coupon is in arrears.</param>
    /// <param name="fixingConvention">The convention adjusting the fixing date.</param>
    /// <returns>The weighted index.</returns>
    internal static StubIborCouponWeightedIndex MakeStubIndex(
        StubIndexSelection selection,
        Date startDate,
        Date endDate,
        int fixingDays,
        bool isInArrears,
        BusinessDayConvention fixingConvention)
    {
        ArgumentNullException.ThrowIfNull(selection, "stubIndexSelection");
        ArgumentOutOfRangeException.ThrowIfNegative(fixingDays);
        EnumArguments.Check(fixingConvention, nameof(fixingConvention));
        if (selection.Empty)
        {
            throw new ArgumentException("StubIborCoupon requires a non-empty stub index selection", "stubIndexSelection");
        }

        if (isInArrears)
        {
            throw new ArgumentException("stub index selection is not supported for in-arrears coupons", nameof(isInArrears));
        }

        Date fixingDate = selection.Indices[0].FixingCalendar.Advance(
            startDate, -fixingDays, TimeUnit.Days, fixingConvention);

        return new StubIborCouponWeightedIndex(SelectComponents(selection, fixingDate, startDate, endDate));
    }

    /// <summary>
    /// The indices an accrual period fixes on, with their weights. All candidates should start the rate on the accrual
    /// start, and their maturities must be distinct.
    /// </summary>
    private static (IborIndex Index, double Weight)[] SelectComponents(
        StubIndexSelection selection,
        Date fixingDate,
        Date accrualStartDate,
        Date accrualEndDate)
    {
        // MakeStubIndex has refused an empty selection, QuantLib's first check here.

        // (maturity, index) pairs, sorted by maturity so selection reduces to
        // a nearest-neighbour or bracket search.
        var candidates = new List<(Date Maturity, IborIndex Index)>(selection.Indices.Length);
        foreach (IborIndex candidate in selection.Indices)
        {
            Date candidateValueDate = candidate.ValueDate(fixingDate);
            if (candidateValueDate != accrualStartDate)
            {
                throw new ArgumentException(
                    $"{candidate.Name} value date {candidateValueDate} does not match accrual start date {accrualStartDate}",
                    "startDate");
            }

            candidates.Add((candidate.MaturityDate(candidateValueDate), candidate));
        }

        candidates = [.. candidates.OrderBy(c => c.Maturity)];
        for (int i = 1; i < candidates.Count; ++i)
        {
            if (!(candidates[i - 1].Maturity < candidates[i].Maturity))
            {
                throw new ArgumentException(
                    $"duplicate candidate index maturity {candidates[i].Maturity}", "stubIndexSelection");
            }
        }

        switch (selection.Convention)
        {
            case StubIndexSelectionConvention.ClosestIndex:
            {
                // Since candidates are sorted, ties select the smaller maturity.
                (Date Maturity, IborIndex Index) closest = candidates[0];
                for (int i = 1; i < candidates.Count; ++i)
                {
                    if (Math.Abs(candidates[i].Maturity - accrualEndDate) < Math.Abs(closest.Maturity - accrualEndDate))
                    {
                        closest = candidates[i];
                    }
                }

                return [(closest.Index, 1.0)];
            }

            case StubIndexSelectionConvention.Interpolated:
            {
                int longer = candidates.FindIndex(c => !(c.Maturity < accrualEndDate));
                if (longer < 0)
                {
                    throw new ArgumentException(
                        $"index maturities do not bracket irregular coupon end date {accrualEndDate}", "endDate");
                }

                if (candidates[longer].Maturity == accrualEndDate)
                {
                    return [(candidates[longer].Index, 1.0)];
                }

                if (longer == 0)
                {
                    throw new ArgumentException(
                        $"index maturities do not bracket irregular coupon end date {accrualEndDate}", "endDate");
                }

                (Date Maturity, IborIndex Index) shorter = candidates[longer - 1];
                double weight = (double)(accrualEndDate - shorter.Maturity)
                    / (double)(candidates[longer].Maturity - shorter.Maturity);
                return [(shorter.Index, 1.0 - weight), (candidates[longer].Index, weight)];
            }

            default:
                throw new NanoQLException("unknown stub index convention");
        }
    }
}
