// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fix QuantLib's master made to the rate helpers after the pin, which nanoql takes: #2899 (<c>46ef4b7b6</c>), the
/// deposit's and the indexed FRA's implied quote forecast from the curve being bootstrapped, <c>forecastFixing</c>.
/// QuantLib 1.44 reads <c>fixing(fixingDate, true)</c>, which takes a stored fixing for a fixing date before the
/// evaluation date (<c>ratehelpers.cpp:210-215</c>, <c>:360-364</c>): on a weekend or holiday evaluation date the
/// helpers' fixing date is the business day before it, so a bootstrap without that day's fixing throws, and one with it
/// quotes the stored fixing in place of the curve's forecast. The C++ vectors' three deposits whose fixing precedes 15
/// January 2024, Martin Luther King Day on the US SOFR calendar, or a given Friday, leave their implied quotes out, two
/// of them below.
/// </summary>
public class RateHelperUpstreamFixesTests
{
    [Fact]
    public void TestWeekendDepositAndIndexedFraForecast()
    {
        // Upstream: QuantLib's master's case for #2899 (termstructures.cpp's testWeekendDepositAndIndexedFraForecast)
        var today = new Date(10, Month.October, 2026); // Saturday
        var context = new EvaluationContext { EvaluationDate = today };

        var index = new IborIndex(
            "weekend-rate-helper",
            new Period(6, TimeUnit.Months),
            2,
            null,
            new TARGET(),
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual360());

        var deposit = new DepositRateHelper(0.022, index, context);
        var fra = new FraRateHelper(0.022, 0, index, context);
        var swap = new SwapRateHelper(
            0.024,
            new Period(2, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            index,
            context);

        // Since #2653, the deposit and zero-start FRA begin on Tuesday, but their indexed fixing is the previous Friday
        // (before evaluation).
        var expectedStart = new Date(13, Month.October, 2026);
        Date pastFixing = index.FixingDate(expectedStart);
        Assert.Equal(expectedStart, deposit.EarliestDate);
        Assert.Equal(expectedStart, fra.EarliestDate);
        Assert.True(pastFixing < today);

        double BootstrappedDiscount(RateHelper shortRateHelper, EvaluationContext c)
        {
            var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                today, [shortRateHelper, swap], new Actual365Fixed(), c);
            return curve.Discount(new Date(10, Month.October, 2027));
        }

        // A weekend bootstrap must not demand a Friday market fixing.
        double depositDiscount = BootstrappedDiscount(deposit, context);
        double fraDiscount = BootstrappedDiscount(fra, context);
        Assert.True(double.IsFinite(depositDiscount));
        Assert.True(double.IsFinite(fraDiscount));
        Assert.True(depositDiscount > 0.0 && depositDiscount < 1.0);
        Assert.True(fraDiscount > 0.0 && fraDiscount < 1.0);

        // Historical fixings must not overwrite the calibration quote: both deposits and indexed FRA helpers must
        // always project from the curve.
        EvaluationContext withFixing = context with { Fixings = context.Fixings.AddFixing(index, pastFixing, 0.08) };
        double depositWithFixing = BootstrappedDiscount(deposit, withFixing);
        double fraWithFixing = BootstrappedDiscount(fra, withFixing);
        Assert.True(Math.Abs(depositWithFixing - depositDiscount) < 1e-12);
        Assert.True(Math.Abs(fraWithFixing - fraDiscount) < 1e-12);
    }

    [Fact]
    public void ADepositFixingBeforeAHolidayEvaluationDateForecastsItsQuote()
    {
        // the C++ vectors' deposits on Monday 15 January 2024, bound to a flat 3.5% curve (continuous, Actual/365
        // (Fixed)): QuantLib 1.44 refuses each, "Missing ... fixing for January 12th, 2024"
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.035, new Actual365Fixed(), Compounding.Continuous, Frequency.Annual);
        var onHoliday = new DepositRateHelper(
            0.0399, new Period(1, TimeUnit.Years), 2, new UnitedStates(UnitedStatesMarket.SOFR),
            BusinessDayConvention.ModifiedFollowing, false, new Actual360(), context);
        var onFriday = new DepositRateHelper(0.0392, new Date(12, Month.January, 2024), new Euribor3M());

        DayCounter dc = new Actual360();
        foreach (DepositRateHelper helper in new[] { onHoliday, onFriday })
        {
            // the forecast from the curve over the deposit's own period
            Date start = helper.EarliestDate!.Value, end = helper.MaturityDate!.Value;
            double forecast = ((curve.Discount(start) / curve.Discount(end)) - 1.0) / dc.YearFraction(start, end);
            Assert.Equal(forecast, helper.SetTermStructure(curve).ImpliedQuote(context), 1e-15);
        }

        Assert.Equal(new Date(17, Month.January, 2024), onHoliday.EarliestDate);
    }
}
