// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The markets the CMS pricers' convention and divergence tests price on, each the one a wheel probe of the S-cms run
/// (<c>scratchpad\c1\div_values.py</c>) read QuantLib's values on: the S-cms planner's flat 3% market, a 1-year coupon
/// fixing in 10 years on a 10-year index with constant volatilities; and QuantLib's cms.cpp market, a flat 5% curve and
/// its at-the-money matrix.
/// </summary>
internal static class CmsInputs
{
    /// <summary>The flat market's evaluation date, 23 October 2026.</summary>
    public static readonly Date Today = new(23, Month.October, 2026);

    /// <summary>The flat market's context.</summary>
    public static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    /// <summary>The flat market's curve, 3% on Actual/365.</summary>
    public static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    /// <summary>The flat market's Euribor 6M.</summary>
    public static readonly IborIndex Ibor = new Euribor6M(Curve);

    /// <summary>The cms.cpp market's evaluation date, 16 September 2015, adjusted on TARGET.</summary>
    public static readonly Date CmsToday = new TARGET().Adjust(new Date(16, Month.September, 2015));

    /// <summary>The cms.cpp market's context.</summary>
    public static readonly EvaluationContext CmsContext = new() { EvaluationDate = CmsToday };

    /// <summary>The cms.cpp market's curve, 5% on Actual/365.</summary>
    public static readonly YieldTermStructure CmsCurve = new FlatForward(CmsToday, 0.05, new Actual365Fixed());

    /// <summary>The flat market's swap index of a tenor: 30/360 against Euribor 6M, 2 fixing days, TARGET.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="fixedLegTenor">The fixed leg's tenor; a year by default.</param>
    /// <returns>The index.</returns>
    public static SwapIndex SwapIndex(Period tenor, Period? fixedLegTenor = null) => new(
        "EuriborSwapIsdaFixA",
        tenor,
        2,
        new EURCurrency(),
        new TARGET(),
        fixedLegTenor ?? new Period(1, TimeUnit.Years),
        BusinessDayConvention.ModifiedFollowing,
        new Thirty360(Thirty360Convention.BondBasis),
        Ibor);

    /// <summary>The flat market's coupon on an index, Actual/360, its accrual period its reference period.</summary>
    /// <param name="index">The index.</param>
    /// <param name="startYears">The years from the evaluation date to the start.</param>
    /// <param name="lengthYears">The coupon's length in years.</param>
    /// <param name="spread">The spread.</param>
    /// <returns>The coupon.</returns>
    public static CmsCoupon Coupon(SwapIndex index, int startYears = 10, int lengthYears = 1, double spread = 0.0)
    {
        (Date start, Date end) = Dates(startYears, lengthYears);
        return new CmsCoupon(end, 1.0, start, end, 2, index, 1.0, spread, null, null, new Actual360());
    }

    /// <summary>The flat market's capped coupon on an index.</summary>
    /// <param name="index">The index.</param>
    /// <param name="startYears">The years from the evaluation date to the start.</param>
    /// <param name="lengthYears">The coupon's length in years.</param>
    /// <param name="spread">The spread.</param>
    /// <param name="cap">The cap.</param>
    /// <returns>The coupon.</returns>
    public static CappedFlooredCmsCoupon Capped(SwapIndex index, int startYears, int lengthYears, double spread, double cap)
    {
        (Date start, Date end) = Dates(startYears, lengthYears);
        return new CappedFlooredCmsCoupon(end, 1.0, start, end, 2, index, 1.0, spread, cap, null, null, null, new Actual360());
    }

    /// <summary>The flat market's constant volatility, floating on the evaluation date.</summary>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="dayCounter">The day counter; Actual/365 by default.</param>
    /// <returns>The volatility: 20% lognormal, 0.8% normal.</returns>
    public static SwaptionVolatilityStructure Volatility(
        VolatilityType type = VolatilityType.ShiftedLognormal, double shift = 0.0, DayCounter? dayCounter = null) =>
        ConstantSwaptionVolatility.Create(
            0,
            new TARGET(),
            BusinessDayConvention.Following,
            type == VolatilityType.Normal ? 0.0080 : 0.20,
            dayCounter ?? new Actual365Fixed(),
            Context,
            type,
            shift);

    /// <summary>The cms.cpp market's at-the-money matrix (lognormal), floating on its evaluation date.</summary>
    /// <returns>The matrix.</returns>
    public static SwaptionVolatilityStructure CmsAtm()
    {
        Period[] options =
        [
            1 * TimeUnit.Months, 6 * TimeUnit.Months, 1 * TimeUnit.Years, 5 * TimeUnit.Years, 10 * TimeUnit.Years,
            30 * TimeUnit.Years,
        ];
        Period[] swaps = [1 * TimeUnit.Years, 5 * TimeUnit.Years, 10 * TimeUnit.Years, 30 * TimeUnit.Years];
        var m = new Matrix(new[,]
        {
            { 0.1300, 0.1560, 0.1390, 0.1220 },
            { 0.1440, 0.1580, 0.1460, 0.1260 },
            { 0.1600, 0.1590, 0.1470, 0.1290 },
            { 0.1640, 0.1470, 0.1370, 0.1220 },
            { 0.1400, 0.1300, 0.1250, 0.1100 },
            { 0.1130, 0.1090, 0.1070, 0.0930 },
        });
        return SwaptionVolatilityMatrix.Create(
            new TARGET(), BusinessDayConvention.Following, options, swaps, m, new Actual365Fixed(), CmsContext);
    }

    /// <summary>The cms.cpp market's coupon on its 10-year index, starting a number of years from its evaluation date
    /// and paid a number of months after its start.</summary>
    /// <param name="index">The index.</param>
    /// <param name="startYears">The years from the evaluation date to the start.</param>
    /// <param name="delayMonths">The months from the start to the payment.</param>
    /// <returns>The coupon.</returns>
    public static CmsCoupon CmsCoupon(SwapIndex index, int startYears, int delayMonths)
    {
        var calendar = new TARGET();
        Date start = calendar.Advance(CmsToday, startYears, TimeUnit.Years);
        Date pay = calendar.Advance(start, delayMonths, TimeUnit.Months);
        return new CmsCoupon(pay, 1.0, start, pay, index.FixingDays, index, 1.0, 0.0, null, null, new Actual360());
    }

    private static (Date Start, Date End) Dates(int startYears, int lengthYears)
    {
        var calendar = new TARGET();
        Date start = calendar.Advance(Today, startYears, TimeUnit.Years);
        return (start, calendar.Advance(start, lengthYears, TimeUnit.Years));
    }
}
