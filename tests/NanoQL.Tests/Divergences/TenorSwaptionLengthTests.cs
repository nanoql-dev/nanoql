// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The tenor swaption structure's swaps at the swap length in whole months (the S-xrt plan's XRR5 (xv), fixed under
/// R8). QuantLib casts the swap length to whole years before it multiplies by twelve
/// (<c>((BigInteger)swapLength * 12.0)</c>, <c>tenorswaptionvts.cpp</c>), so an 18-month tenor builds 12-month swaps:
/// on QuantLib 1.44 (the run's <c>c2\probes\xd_c2.py</c> through the harness: test-suite/basismodels.cpp's markets on
/// 15 June 2026, Euribor 6M volatilities to a true Euribor 3M) the at-the-money level at a 1-year option on an
/// 18-month swap is −0.00018491732414727124, the 12-month swap's, and at a 5-year option 0.010348353529043619. nanoql's
/// 18-month level is the fair rate of the 18-month target swap, the vanilla swap on the 18-month schedule; whole-year
/// tenors are QuantLib's, the 12-month level QuantLib's to the last bits.
/// </summary>
public class TenorSwaptionLengthTests
{
    [Theory]
    [InlineData(1, -0.00018491732414727124)]
    [InlineData(5, 0.010348353529043619)]
    public void AnEighteenMonthSwapIsEighteenMonthsLong(int optionYears, double quantLib)
    {
        var today = new Date(15, Month.June, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure disc = BasisModelsTests.GetYTS(Terms(), Rates("disc"), context);
        YieldTermStructure proj3m = BasisModelsTests.GetYTS(Terms(), Rates("proj3m"), context);
        YieldTermStructure proj6m = BasisModelsTests.GetYTS(Terms(), Rates("proj3m"), context, 0.01);
        var euribor6m = new Euribor6M(proj6m);
        var euribor3m = new Euribor3M(proj3m);
        var bondBasis = new Thirty360(Thirty360Convention.BondBasis);
        var vts = new TenorSwaptionVTS(
            BasisModelsTests.GetSwaptionVTS(context), disc, euribor6m, euribor3m, new Period(1, TimeUnit.Years),
            new Period(1, TimeUnit.Years), bondBasis, bondBasis, context);
        var option = new Period(optionYears, TimeUnit.Years);
        Assert.Equal(quantLib, vts.SmileSection(option, new Period(12, TimeUnit.Months), true).AtmLevel()!.Value, 1e-16);

        // the 18-month target swap, built as the structure builds it, on the 18-month schedule
        double optionTime = vts.TimeFromReference(vts.OptionDateFromTenor(option));
        double oneDay = vts.DayCounter.YearFraction(vts.ReferenceDate, vts.ReferenceDate + 1);
        Date exercise = vts.ReferenceDate + (int)new Numerics.ClosestRounding(0).Evaluate(optionTime / oneDay);
        Date effective = euribor6m.FixingCalendar.Advance(exercise, euribor6m.FixingDays, TimeUnit.Days);
        Date maturity = euribor6m.FixingCalendar.Advance(
            effective, new Period(18, TimeUnit.Months), BusinessDayConvention.Unadjusted, false);
        Schedule fixedSchedule = ScheduleOf(effective, maturity, new Period(1, TimeUnit.Years), euribor3m.FixingCalendar, context);
        Schedule floatSchedule = ScheduleOf(effective, maturity, euribor3m.Tenor, euribor6m.FixingCalendar, context);
        var swap = new VanillaSwap(
            SwapType.Payer, 1.0, fixedSchedule, 1.0, bondBasis, floatSchedule, euribor3m, 0.0, euribor3m.DayCounter, context);
        double fair = DiscountingSwapEngine.Calculate(swap, context, disc).FairRate!.Value;
        double atm18 = vts.SmileSection(option, new Period(18, TimeUnit.Months), true).AtmLevel()!.Value;
        Assert.Equal(fair, atm18, 1e-15);
        Assert.True(Math.Abs(atm18 - quantLib) > 1e-4);
    }

    private static Period[] Terms() =>
    [
        new(0, TimeUnit.Days), new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years),
        new(5, TimeUnit.Years), new(7, TimeUnit.Years), new(10, TimeUnit.Years), new(15, TimeUnit.Years),
        new(20, TimeUnit.Years), new(61, TimeUnit.Years),
    ];

    private static double[] Rates(string which) => which == "disc"
        ? [-0.00147407, -0.001761684, -0.001736745, -0.00119244, 0.000896055, 0.003537077, 0.007213824, 0.011391278,
            0.013334611, 0.013982809]
        : [-0.000483439, -0.000578569, -0.000383832, 0.000272656, 0.002478699, 0.005100113, 0.008750643, 0.012788095,
            0.014534052, 0.014942896];

    private static Schedule ScheduleOf(Date start, Date end, Period tenor, Calendar calendar, EvaluationContext context) =>
        new(
            start, end, tenor, calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward, false, context);
}
