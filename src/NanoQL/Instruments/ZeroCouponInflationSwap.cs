// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/zerocouponinflationswap.hpp ql/instruments/zerocouponinflationswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A zero-coupon inflation-indexed swap, quoted as a fixed rate <c>K</c>: at maturity it exchanges the notional's
/// growth at <c>K</c>, <c>N[(1 + K)^T - 1]</c>, against its inflation growth, <c>N[I(T)/I(0) - 1]</c>, where <c>T</c>
/// is the time from the start to the maturity.
/// </summary>
/// <remarks>
/// <para>
/// The type, payer or receiver, refers to the inflation leg. The fixed leg is the first leg, one simple cash flow; the
/// inflation leg is the second, one <see cref="ZeroInflationCashFlow"/> paying the growth only. Inflation is available
/// on every day, holidays and weekends included, so the observation dates are the start and the maturity less the lag,
/// unadjusted. The standard <see cref="PricingEngines.DiscountingSwapEngine"/> prices it; QuantLib's own arguments and
/// engine classes aren't ported, since no engine uses them.
/// </para>
/// <para>
/// The constructor reads no evaluation date and forecasts nothing. It refuses a type that isn't a member of
/// <see cref="SwapType"/> on entry, where QuantLib refuses it after building the legs. The inflation leg pays on the
/// inflation calendar and convention, the fixed leg's when they aren't given. QuantLib's default convention is
/// <c>BusinessDayConvention()</c>, Following, which it replaces with the fixed leg's, so an inflation leg asked to pay
/// Following pays on the fixed convention; here a null convention takes the fixed leg's and an explicit one is kept, a
/// documented divergence. <see cref="AdjustObservationDates"/> is stored and exposed, as QuantLib's is, and read by
/// nothing.
/// </para>
/// <para>
/// The results-dependent members take the engine's <see cref="SwapResults"/>. QuantLib's <c>fairRate</c> is the rate
/// that pays the inflation growth, <c>growth^(1/T) - 1</c>, which zeroes the swap only when both legs pay on the same
/// day; where they pay on different days, <see cref="FairRate"/> scales the growth's excess by the ratio of the legs'
/// end discounts from the results, so the swap rebuilt at it is worth zero, a documented divergence.
/// </para>
/// </remarks>
public sealed class ZeroCouponInflationSwap : Swap
{
    private const double BasisPoint = 1.0e-4;

    private readonly Date startDate;
    private readonly Date maturityDate;

    /// <summary>Initializes a new instance of the <see cref="ZeroCouponInflationSwap"/> class.</summary>
    /// <param name="type">Whether the inflation leg is paid or received.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="startDate">The start date of the contract.</param>
    /// <param name="maturity">The maturity, before adjustment.</param>
    /// <param name="fixCalendar">The fixed leg's payment calendar, or <see langword="null"/> for QuantLib's empty
    /// calendar, used unchecked as QuantLib uses it.</param>
    /// <param name="fixConvention">The fixed leg's payment convention.</param>
    /// <param name="dayCounter">The day counter of the time from the start to the maturity.</param>
    /// <param name="fixedRate">The fixed rate <c>K</c>.</param>
    /// <param name="infIndex">The zero-inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="observationInterpolation">How the index is observed.</param>
    /// <param name="adjustInfObsDates">Whether the inflation observation dates are adjusted; stored and read by
    /// nothing, as in QuantLib.</param>
    /// <param name="infCalendar">The inflation leg's payment calendar, or <see langword="null"/> for the fixed
    /// leg's.</param>
    /// <param name="infConvention">The inflation leg's payment convention, or <see langword="null"/> for the fixed
    /// leg's.</param>
    /// <exception cref="ArgumentNullException"><paramref name="infIndex"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/>, a convention or
    /// <paramref name="observationInterpolation"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The observation lag can't observe fixings the index has made available, or a
    /// date is <c>default(Date)</c>.</exception>
    public ZeroCouponInflationSwap(
        SwapType type,
        double nominal,
        Date startDate,
        Date maturity,
        Calendar? fixCalendar,
        BusinessDayConvention fixConvention,
        DayCounter dayCounter,
        double fixedRate,
        ZeroInflationIndex infIndex,
        Period observationLag,
        CPIInterpolationType observationInterpolation,
        bool adjustInfObsDates = false,
        Calendar? infCalendar = null,
        BusinessDayConvention? infConvention = null)
        : base(
            BuildLegs(
                type,
                nominal,
                startDate,
                maturity,
                fixCalendar,
                fixConvention,
                dayCounter,
                fixedRate,
                infIndex,
                observationLag,
                observationInterpolation,
                ref infCalendar,
                ref infConvention),
            [type == SwapType.Receiver, type == SwapType.Payer])
    {
        Type = type;
        Nominal = nominal;
        this.startDate = startDate;
        maturityDate = maturity;
        FixedCalendar = fixCalendar;
        FixedConvention = fixConvention;
        DayCounter = dayCounter;
        FixedRate = fixedRate;
        InflationIndex = infIndex;
        ObservationLag = observationLag;
        ObservationInterpolation = observationInterpolation;
        AdjustObservationDates = adjustInfObsDates;
        InflationCalendar = infCalendar;
        InflationConvention = infConvention!.Value;
    }

    /// <summary>Gets whether the inflation leg is paid or received.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the nominal.</summary>
    public double Nominal { get; }

    /// <summary>Gets the fixed leg's payment calendar, or <see langword="null"/> for QuantLib's empty
    /// calendar.</summary>
    public Calendar? FixedCalendar { get; }

    /// <summary>Gets the fixed leg's payment convention.</summary>
    public BusinessDayConvention FixedConvention { get; }

    /// <summary>Gets the day counter of the time from the start to the maturity.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Gets the fixed rate <c>K</c>.</summary>
    public double FixedRate { get; }

    /// <summary>Gets the zero-inflation index.</summary>
    public ZeroInflationIndex InflationIndex { get; }

    /// <summary>Gets the observation lag.</summary>
    public Period ObservationLag { get; }

    /// <summary>Gets how the index is observed.</summary>
    public CPIInterpolationType ObservationInterpolation { get; }

    /// <summary>Gets a value indicating whether the inflation observation dates are adjusted; QuantLib reads it
    /// nowhere.
    /// </summary>
    public bool AdjustObservationDates { get; }

    /// <summary>Gets the inflation leg's payment calendar: the given one, or the fixed leg's.</summary>
    public Calendar? InflationCalendar { get; }

    /// <summary>Gets the inflation leg's payment convention: the given one, or the fixed leg's.</summary>
    public BusinessDayConvention InflationConvention { get; }

    /// <summary>Gets the fixed leg: one simple cash flow.</summary>
    public ImmutableArray<CashFlow> FixedLeg => Legs[0];

    /// <summary>Gets the inflation leg: one zero-inflation cash flow paying the growth only.</summary>
    public ImmutableArray<CashFlow> InflationLeg => Legs[1];

    /// <summary>The start date of the contract.</summary>
    /// <returns>The start date.</returns>
    public override Date StartDate() => startDate;

    /// <summary>The maturity, before adjustment.</summary>
    /// <returns>The maturity.</returns>
    public override Date MaturityDate() => maturityDate;

    /// <summary>The fixed leg's NPV in a swap engine's results.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The NPV, signed by whether the leg is paid, or <see langword="null"/> where QuantLib refuses "result
    /// not available".</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    public static double? FixedLegNPV(SwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return Element(results.LegNPV, 0);
    }

    /// <summary>The inflation leg's NPV in a swap engine's results.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The NPV, signed by whether the leg is paid, or <see langword="null"/> where QuantLib refuses "result
    /// not available".</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    public static double? InflationLegNPV(SwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return Element(results.LegNPV, 1);
    }

    /// <summary>
    /// The fixed leg's sensitivity to a basis point on the fixed rate, from the fixed leg's end discount in a swap
    /// engine's results: the engine's own BPS is zero, a simple cash flow being insensitive to a rate, and its annual
    /// compounding isn't linear in the rate.
    /// </summary>
    /// <remarks>Given at the results' NPV date, as their leg NPVs are: the end discount over the NPV date's discount,
    /// where QuantLib's reads the end discount alone, from the curve's reference date, so with a later NPV date its BPS
    /// isn't the fixed leg's NPV move (<c>zerocouponinflationswap.cpp:147-154</c>). At the default NPV date the two
    /// agree.</remarks>
    /// <param name="results">The results.</param>
    /// <returns>The sensitivity, signed by whether the leg is paid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    /// <exception cref="NanoQLException">The results hold no end discount for the fixed leg.</exception>
    public double FixedLegBPS(SwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        double endDiscount = Element(results.EndDiscounts, 0)
            ?? throw new NanoQLException("cannot calculate fixedLegBPS because end discount is not populated");
        double df = PayerMultipliers[0] * endDiscount / (results.NpvDateDiscount ?? 1.0);
        double t = DayCounter.YearFraction(startDate, maturityDate);
        return df * Nominal * (Math.Pow(1.0 + FixedRate + BasisPoint, t) - Math.Pow(1.0 + FixedRate, t));
    }

    /// <summary>
    /// The fixed rate that zeroes the swap: QuantLib's rate paying the inflation growth, <c>growth^(1/T) - 1</c>, when
    /// the legs pay on the same day or the swap has expired; otherwise the growth's excess scaled by the ratio of the
    /// legs' end discounts in the results, <c>(1 + (D_inf/D_fixed)(growth - 1))^(1/T) - 1</c>.
    /// </summary>
    /// <param name="context">The evaluation context, under which the inflation growth is fixed or forecast.</param>
    /// <param name="results">A swap engine's results, read only where the legs pay on different days.</param>
    /// <returns>The fair rate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The legs pay on different days and no results, or no end discount for a leg,
    /// are given; or the growth can't be fixed or forecast.</exception>
    public double FairRate(EvaluationContext context, SwapResults? results = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        // What does this mean before or after trade date?
        // Always means that NPV is zero for _this_ instrument
        // if it was created with _this_ rate
        // _knowing_ the time from base to obs (etc).
        var icf = Legs[1][0] as IndexedCashFlow
            ?? throw new NanoQLException("failed to downcast to IndexedCashFlow in ::fairRate()");

        // +1 because the IndexedCashFlow has growthOnly=true
        double growth = (icf.Amount(context) / icf.Notional) + 1.0;
        double t = DayCounter.YearFraction(startDate, maturityDate);

        if (Legs[0][0].Date == icf.Date || IsExpired(context))
        {
            return Math.Pow(growth, 1.0 / t) - 1.0;
        }

        // the legs pay on different days: the growth's excess is discounted to the inflation leg's payment
        double? fixedDiscount = results is null ? null : Element(results.EndDiscounts, 0);
        double? inflationDiscount = results is null ? null : Element(results.EndDiscounts, 1);
        if (fixedDiscount is not double d0 || inflationDiscount is not double d1)
        {
            throw new NanoQLException("cannot calculate fairRate because end discount is not populated");
        }

        return Math.Pow(1.0 + (d1 / d0 * (growth - 1.0)), 1.0 / t) - 1.0;
    }

    // QuantLib builds the legs in its constructor's body; here they are built before the base constructor takes them,
    // in QuantLib's order, after the type is checked. The inflation calendar and convention come back defaulted.
    private static ImmutableArray<CashFlow>[] BuildLegs(
        SwapType type,
        double nominal,
        Date startDate,
        Date maturity,
        Calendar? fixCalendar,
        BusinessDayConvention fixConvention,
        DayCounter dayCounter,
        double fixedRate,
        ZeroInflationIndex infIndex,
        Period observationLag,
        CPIInterpolationType observationInterpolation,
        ref Calendar? infCalendar,
        ref BusinessDayConvention? infConvention)
    {
        if (type is not (SwapType.Payer or SwapType.Receiver))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown zero-inflation-swap type");
        }

        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(maturity, nameof(maturity));
        EnumArguments.Check(fixConvention, nameof(fixConvention));
        if (infConvention is BusinessDayConvention given)
        {
            EnumArguments.Check(given, nameof(infConvention));
        }

        EnumArguments.Check(observationInterpolation, nameof(observationInterpolation));
        ArgumentNullException.ThrowIfNull(dayCounter);
        if (infIndex is null)
        {
            throw new ArgumentNullException(nameof(infIndex), "no index provided");
        }

        // first check compatibility of index and swap definitions
        if (CPI.IsInterpolated(observationInterpolation))
        {
            var pShift = new Period(infIndex.Frequency);
            if (!(observationLag - pShift >= infIndex.AvailabilityLag))
            {
                throw new ArgumentException(
                    $"inconsistency between swap observation lag {observationLag}, interpolated index period {pShift} "
                    + $"and index availability {infIndex.AvailabilityLag}: need (obsLag-index period) >= availLag",
                    nameof(observationLag));
            }
        }
        else if (!(infIndex.AvailabilityLag <= observationLag))
        {
            throw new ArgumentException(
                "index tries to observe inflation fixings that do not yet exist:  availability lag "
                + $"{infIndex.AvailabilityLag} versus obs lag = {observationLag}",
                nameof(observationLag));
        }

        infCalendar ??= fixCalendar;
        infConvention ??= fixConvention;

        // QuantLib's empty calendars, null here, are used unchecked, as QuantLib uses them
        Date infPayDate = (infCalendar ?? EmptyCalendar.Instance).Adjust(maturity, infConvention.Value);
        Date fixedPayDate = (fixCalendar ?? EmptyCalendar.Instance).Adjust(maturity, fixConvention);

        const bool growthOnly = true;

        var inflationCashFlow = new ZeroInflationCashFlow(
            nominal, infIndex, observationInterpolation, startDate, maturity, observationLag, infPayDate, growthOnly);

        // At this point the index may not be able to forecast
        // i.e. do not want to force the existence of an inflation
        // term structure before allowing users to create instruments.
        double t = dayCounter.YearFraction(startDate, maturity);

        // N.B. the -1.0 is because swaps only exchange growth, not notionals as well
        double fixedAmount = nominal * (Math.Pow(1.0 + fixedRate, t) - 1.0);

        var fixedCashFlow = new SimpleCashFlow(fixedAmount, fixedPayDate);

        return [[fixedCashFlow], [inflationCashFlow]];
    }

    private static double? Element(ImmutableArray<double?> values, int j) =>
        !values.IsDefault && j < values.Length ? values[j] : null;
}
