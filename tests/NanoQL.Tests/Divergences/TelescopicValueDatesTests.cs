// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A coupon built with <c>telescopicValueDates</c>. QuantLib builds its daily value dates only to 7 business days
/// after the evaluation date it is built with, then jumps to the lockout or end dates
/// (<c>overnightindexedcoupon.cpp:107-132</c>), and wherever the skipped dates are read its results are wrong, with no
/// error. nanoql builds every daily value date whatever the flag says (the flag keeps its check and, on an averaged
/// coupon, its choice of the averaging pricer's approximation), so a telescopic coupon gives QuantLib's full-schedule
/// result: here, each expected value is QuantLib's for the same coupon built without the flag, the approximating pricer
/// set on an averaged one, as the flag sets it.
/// </summary>
public class TelescopicValueDatesTests
{
    private static readonly Date T0 = new(15, January, 2024);
    private static readonly Date Start = new(17, January, 2024);
    private static readonly Date End = new(17, July, 2024);

    [Fact]
    public void ATelescopicCouponGivesTheFullSchedulesResults()
    {
        var t0 = new EvaluationContext { EvaluationDate = T0 };

        // (1) An aged coupon: built on 15 January, priced on 29 January with every fixing since 9 January at 4%.
        // QuantLib's telescopic coupon gives 0.04009911855222946.
        var sofr = new Sofr(new FlatForward(T0, 0.05, new Actual360()));
        Calendar cal = sofr.FixingCalendar;
        var aged = new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, telescopicValueDates: true);
        var priced = new Date(29, January, 2024);
        ImmutableArray<Date> published = cal.BusinessDayList(cal.Advance(Start, -5, TimeUnit.Days), priced - 1);
        var later = new EvaluationContext
        {
            EvaluationDate = priced,
            Fixings = IndexFixings.Empty.AddFixings(sofr, published, published.Select(_ => 0.04)),
        };
        AssertQuantLibFull(0.04996089552574429, aged.Rate(later), 0.04009911855222946);

        // On a zero curve rising from 3% to 6% over the year, the coupon built and priced on 15 January:
        var rising = new Sofr(new LinearZeroCurve(T0, [T0, T0 + new Period(1, TimeUnit.Years), T0 + new Period(2, TimeUnit.Years)], [0.03, 0.06, 0.06]));

        // (2) a lockout of 2 days, which QuantLib's telescopic coupon freezes at the front stub's last fixing date;
        var lockout = new OvernightIndexedCoupon(End, 1e6, Start, End, rising, telescopicValueDates: true, lockoutDays: 2);
        AssertQuantLibFull(0.04512887048123588, lockout.Rate(t0), 0.044823504188753645);

        // (3) accrued amounts inside the skipped dates, and QuantLib's own testExCouponAccruedAmountAroundAccrualEndDate
        // (test-suite/overnightindexedcoupon.cpp:1936), which asserts the skipped dates' -0.0445 on 27 March 2027;
        var compounded = new OvernightIndexedCoupon(End, 1e6, Start, End, rising, telescopicValueDates: true);
        AssertQuantLibFull(9216.023821898123, compounded.AccruedAmount(new Date(15, April, 2024), t0), 7797.08057399886);
        AssertQuantLibFull(20182.095449102988, compounded.AccruedAmount(new Date(1, July, 2024), t0), 14564.212745409532);
        var exCouponToday = new EvaluationContext { EvaluationDate = new Date(26, March, 2026) };
        var exCoupon = new OvernightIndexedCoupon(
            new Date(2, April, 2027),
            100.0,
            new Date(31, March, 2026),
            new Date(31, March, 2027),
            new Sofr(FlatForward.Create(0, new NullCalendar(), 0.04, new Actual360(), exCouponToday)),
            telescopicValueDates: true,
            lookbackDays: 0,
            exCouponDate: new Date(27, March, 2027));
        AssertQuantLibFull(
            -0.04534786920869596, exCoupon.AccruedAmount(new Date(27, March, 2027), exCouponToday), -0.04450038115169223);

        // (4) the exact averaging pricer, which QuantLib's telescopic coupon averages over the skipped dates;
        OvernightIndexedCoupon averaged = new OvernightIndexedCoupon(
                End, 1e6, Start, End, rising, telescopicValueDates: true, averagingMethod: RateAveragingType.Simple)
            .SetPricer(new ArithmeticAveragedOvernightIndexedCouponPricer(0.03, 0.0, false));
        AssertQuantLibFull(0.044631769184303144, averaged.Rate(t0), 0.03171816198815553);

        // (5) the Black averaging pricer, whose caps and floors on the period rate read the skipped dates. Its values are
        // the full-dates coupon's with the approximating pricer on the underlying, as the flag sets it.
        var vol = new ConstantOptionletVolatility(
            T0, new UnitedStates(UnitedStatesMarket.SOFR), BusinessDayConvention.Following, 0.30, new Actual365Fixed());
        var underlying = new OvernightIndexedCoupon(
            End, 1e6, Start, End, rising, telescopicValueDates: true, averagingMethod: RateAveragingType.Simple);
        CappedFlooredOvernightIndexedCoupon capped = new CappedFlooredOvernightIndexedCoupon(underlying, 0.045, null)
            .SetPricer(new BlackAveragingOvernightIndexedCouponPricer(vol));
        CappedFlooredOvernightIndexedCoupon floored = new CappedFlooredOvernightIndexedCoupon(underlying, null, 0.044)
            .SetPricer(new BlackAveragingOvernightIndexedCouponPricer(vol));
        AssertQuantLibFull(0.04259022445515445, capped.Rate(t0), 0.04462278227516141);
        AssertQuantLibFull(0.04651507290015296, floored.Rate(t0), 0.05691388724360589);
    }

    [Fact]
    public void ATelescopicCouponListsEveryDailyDate()
    {
        var t0 = new EvaluationContext { EvaluationDate = T0 };
        var rising = new Sofr(new LinearZeroCurve(T0, [T0, T0 + new Period(1, TimeUnit.Years), T0 + new Period(2, TimeUnit.Years)], [0.03, 0.06, 0.06]));
        var start = new Date(17, April, 2024);
        var end = new Date(17, October, 2024);

        // QuantLib's telescopic coupon lists 10 value dates and 9 fixing dates; the full schedule 127 and 126, which
        // the telescopic coupon here lists too, with the same rate, 0.06032713365607166 in QuantLib either way.
        var telescopic = new OvernightIndexedCoupon(end, 1e6, start, end, rising, telescopicValueDates: true);
        var full = new OvernightIndexedCoupon(end, 1e6, start, end, rising);
        Assert.Equal(127, telescopic.ValueDates().Length);
        Assert.Equal(full.ValueDates(), telescopic.ValueDates());
        Assert.Equal(full.FixingDates(), telescopic.FixingDates());
        Assert.Equal(full.InterestDates(), telescopic.InterestDates());
        Assert.Equal(full.Dt(), telescopic.Dt());
        Assert.Equal(full.IndexFixings(t0), telescopic.IndexFixings(t0));
        Assert.Equal(126, telescopic.IndexFixings(t0).Length);
        Assert.Equal(cal(rising).BusinessDayList(start, end), telescopic.ValueDates());
        AssertQuantLibFull(0.06032713365607166, telescopic.Rate(t0), 0.06032713365607166);

        // D11: a lockout of 100,000 days. QuantLib's telescopic coupon first walks the calendar back by it and refuses
        // the date, "Date's serial number (366) outside allowed range [367-109574], ..." (overnightindexedcoupon.cpp:
        // 123-124); with every daily date the coupon checks the lockout first, QuantLib's full-schedule refusal.
        ArgumentException e = Assert.Throws<ArgumentException>(() =>
            new OvernightIndexedCoupon(End, 1e6, Start, End, rising, telescopicValueDates: true, lockoutDays: 100000));
        Assert.Equal("lockoutDays", e.ParamName);
        Assert.StartsWith("Lockout period cannot be negative or exceed the number of fixing days.", e.Message, StringComparison.Ordinal);

        static Calendar cal(OvernightIndex index) => index.FixingCalendar;
    }

    [Fact]
    public void ALockoutLegsNPVIsTheFullSchedules()
    {
        // QuantLib's own testOvernightLegNPV (test-suite/overnightindexedcoupon.cpp:1033): a quarterly SOFR leg on
        // 1 million with a lockout of 3 days and telescopic value dates, forecast on a cubic zero curve and discounted
        // flat at 0.15%. QuantLib's telescopic coupons freeze the lockout at the front stub's last fixing date, and
        // the test asserts their NPV, 34883.949669756257; with every daily value date it is QuantLib's full-schedule
        // NPV, 34638.786842593545. The leg starts after the evaluation date, so it reads no past fixing.
        var today = new Date(1, June, 2025);
        var dc = new Actual360();
        var context = new EvaluationContext { EvaluationDate = today };
        var zeroCurve = new InterpolatedZeroCurve<Cubic>(
            [
                today, new Date(30, July, 2025), new Date(29, August, 2025), new Date(30, September, 2025),
                new Date(30, December, 2025), new Date(30, March, 2026), new Date(30, June, 2026),
            ],
            [0.0434, 0.0436, 0.0431, 0.0413, 0.0390, 0.0370, 0.0348],
            dc,
            new UnitedStates(UnitedStatesMarket.SOFR));
        var sofr = new Sofr(zeroCurve.EnableExtrapolation());
        var schedule = new Schedule(
            new Date(1, July, 2025),
            new Date(1, July, 2026),
            new Period(3, TimeUnit.Months),
            new UnitedStates(UnitedStatesMarket.GovernmentBond),
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            false,
            context);
        ImmutableArray<CashFlow> leg = new OvernightLeg(schedule, sofr)
            .WithNotionals(1000000.0)
            .WithPaymentDayCounter(dc)
            .WithLockoutDays(3)
            .WithTelescopicValueDates(true)
            .ToLeg();
        YieldTermStructure discountCurve = FlatForward.Create(0, new NullCalendar(), 0.0015, dc, context);

        double npv = leg.Sum(cf => cf.Amount(context) * discountCurve.Discount(cf.Date));
        AssertQuantLibFull(34638.786842593545, npv, 34883.949669756257);
    }

    // nanoql's value is QuantLib's full-schedule value, and differs from its telescopic one.
    private static void AssertQuantLibFull(double quantLibFull, double actual, double quantLibTelescopic)
    {
        Assert.True(
            Math.Abs(actual - quantLibFull) <= 1e-12 * Math.Max(1.0, Math.Abs(quantLibFull)),
            $"expected QuantLib's full-schedule {quantLibFull:R}, got {actual:R} (QuantLib's telescopic coupon: {quantLibTelescopic:R})");
    }

    /// <summary>
    /// QuantLib's <c>ZeroCurve</c> (<c>InterpolatedZeroCurve&lt;Linear&gt;</c>) on Actual/365 (Fixed) with continuous zero
    /// rates, as the plan's premises build it: the rates interpolated linearly in time from the first date, and the
    /// discount factor exp(-r t), 1 at the reference date.
    /// </summary>
    private sealed class LinearZeroCurve : YieldTermStructure
    {
        private readonly LinearInterpolation interpolation;
        private readonly Date maxDate;

        public LinearZeroCurve(Date referenceDate, Date[] dates, double[] rates)
            : base(referenceDate, null, new Actual365Fixed())
        {
            double[] times = dates.Select(d => new Actual365Fixed().YearFraction(dates[0], d)).ToArray();
            interpolation = new LinearInterpolation(times, rates);
            maxDate = dates[^1];
        }

        public override Date MaxDate => maxDate;

        protected override double DiscountImpl(double t) =>
            t == 0.0 ? 1.0 : Math.Exp(-interpolation.Evaluate(t, true) * t);
    }
}
