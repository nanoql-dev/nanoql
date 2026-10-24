// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A range accrual in progress (the S-cms plan's CMR5 (xviii)): QuantLib's BGM pricer prices an observation from its
/// expiry, so a coupon whose start is past is priced on QuantLib's call-spread route and smile correction with the
/// before-fixing phase clamped (<c>rangeaccrual.cpp:403-423</c>, <c>:474-478</c>), but its no-smile route takes the
/// phase unclamped (<c>:365-372</c>), and an observation whose expiry is past is priced by no route (NaN): on the
/// wheel, on the C++ range accrual vectors' Euribor 6M, curve and fixings, a weekly coupon from 10 January 2025 (its
/// first observation on 17 January, fixing on the evaluation date, 15 January) pays 0.033335938211808636 on the
/// no-smile route at volatilities 10% and 30% and 0.03337731105469358 on the call-spread route, and the no-smile route
/// refuses at 30% and 10% (0.03465775920672742 by call spread); a coupon from 19 December 2024 refuses on every route.
/// nanoql's no-smile route clamps the phase as the other two do, and an observation whose fixing is known, its fixing
/// date before the evaluation date, or on it once published, is counted by its fixing f, the deflator times 1{f &gt; l}
/// − 1{f &gt; u}, the two digitals QuantLib's range reads, its date past or not. QuantLib's checks: its call-spread
/// route, within that route's rounding; its pricer on the coupon restated with only its observations not yet known (the
/// same start, end and fixings), scaled by their share, plus the known part, with the 19 December coupon's fourth known
/// observation dated after the evaluation date (16 January, fixing on 14 January), and a coupon from 9 January whose
/// one known observation is that one, which QuantLib's pricer takes as a digital on a day's expiry,
/// 0.014901944616257642 at 2.92% to 4% where the known fixing gives 0.015369074793388015; the 19 December coupon
/// below 3.2%, from 2.6% (known fixings 3.3022%, above, 2.589% and 2.5592%, below, and 2.9258%) and from 0, a lower
/// trigger at or below the pricer's ε/2, which QuantLib's digital takes as 1 (<c>:347</c>); on a coupon's start date,
/// QuantLib's values on every route; and the smile-correction route, which reads the no-smile digital
/// (<c>:474-478</c>), so QuantLib's equals its unclamped no-smile route on the 10 January coupon, 0.033335938211808636
/// at 10% and 30%, and refuses at 30% and 10%, where nanoql's equals its clamped one, the correction 0 once the start
/// is past.
/// </summary>
public class RangeAccrualStartedCouponTests
{
    [Fact]
    public void BeforeItsFirstObservationTheNoSmileRouteIsTheCallSpreadRoutes()
    {
        (IborIndex index, EvaluationContext context) = Market();
        RangeAccrualFloatersCoupon coupon = Coupon(index, new Date(10, January, 2025), new Date(10, July, 2025), context);
        foreach ((double ls, double lt, double callSpread) in new[] { (0.10, 0.30, 0.03337731105469358), (0.30, 0.10, 0.03465775920672742) })
        {
            double noSmile = coupon.SetPricer(Pricer(ls, lt, false, false)).Rate(context);
            double byCallSpread = coupon.SetPricer(Pricer(ls, lt, true, true)).Rate(context);
            Assert.Equal(callSpread, byCallSpread);
            Assert.True(Math.Abs(noSmile - callSpread) <= 1e-11, $"{noSmile} {callSpread}");
        }
    }

    [Fact]
    public void KnownObservationsAreCountedByTheirFixings()
    {
        (IborIndex index, EvaluationContext context) = Market();
        RangeAccrualFloatersCoupon coupon = Coupon(index, new Date(19, December, 2024), new Date(19, June, 2025), context);
        Assert.Equal(25, coupon.ObservationsNo);

        // the four known observations, the fourth dated after the evaluation date, each fixing in [2%, 4%], on the
        // coupon's own fixing, 0.029276 (17 December 2024)
        double knownPart = 0.029276 * 4 / 25;
        foreach ((double ls, double lt, double restated) in new[]
        {
            (0.10, 0.30, 0.028400975093099523), (0.30, 0.10, 0.0292759361113822), (0.2, 0.2, 0.029148673906431093),
        })
        {
            double rate = coupon.SetPricer(Pricer(ls, lt, true, true)).Rate(context);
            double expected = (restated * 21 / 25) + knownPart;
            Assert.True(Math.Abs(rate - expected) <= 1e-15, $"{rate} {expected}");
        }
    }

    [Fact]
    public void AnObservationFixedBeforeItsDateIsKnown()
    {
        // a weekly coupon from 9 January 2025 between 2.92% and 4%: its one known observation, on 16 January, after
        // the evaluation date, fixes on 14 January at 0.029258, in range, and counts by that fixing on the coupon's
        // own, 0.025592 (7 January); QuantLib's pricer takes it as a digital on a day's expiry, 0.014901944616257642
        // and 0.017712038120008083 on the coupon
        (IborIndex index, EvaluationContext context) = Market();
        Date observation = new(16, January, 2025);
        Assert.True(observation > context.EvaluationDate);
        Assert.Equal(new Date(14, January, 2025), index.FixingCalendar.Advance(observation, -2, TimeUnit.Days));
        RangeAccrualFloatersCoupon coupon = Coupon(index, new Date(9, January, 2025), new Date(9, July, 2025), context, 0.0292);
        Assert.Equal(25, coupon.ObservationsNo);
        Assert.Equal(observation, coupon.ObservationDates[0]);

        foreach ((double ls, double lt, double restated) in new[] { (0.10, 0.30, 0.01494311957644585), (0.2, 0.2, 0.017841667975024806) })
        {
            double rate = coupon.SetPricer(Pricer(ls, lt, true, true)).Rate(context);
            double expected = (restated * 24 / 25) + (0.025592 / 25);
            Assert.True(Math.Abs(rate - expected) <= 1e-15, $"{rate} {expected}");
        }
    }

    [Theory]
    [InlineData(0.026, 1, 0.015999185127224765, 0.024186854903906537, 0.019560769011535816)]
    [InlineData(0.0, 3, 0.019800164634580994, 0.024215878678555892, 0.020930051693718612)]
    public void KnownObservationsOutOfTheirRangeCountNothing(
        double lower, int inRange, double restated1, double restated2, double restated3)
    {
        // the 19 December coupon below 3.2%: its known fixings 0.033022 (23 December), above it, 0.02589 (30 December)
        // and 0.025592 (7 January), below 2.6%, and 0.029258 (14 January); from 0, at or below the pricer's ε/2, the
        // lower trigger counts every fixing
        (IborIndex index, EvaluationContext context) = Market();
        RangeAccrualFloatersCoupon coupon =
            Coupon(index, new Date(19, December, 2024), new Date(19, June, 2025), context, lower, 0.032);
        double knownPart = 0.029276 * inRange / 25;
        foreach ((double ls, double lt, double restated) in new[]
        {
            (0.10, 0.30, restated1), (0.30, 0.10, restated2), (0.2, 0.2, restated3),
        })
        {
            double rate = coupon.SetPricer(Pricer(ls, lt, true, true)).Rate(context);
            double expected = (restated * 21 / 25) + knownPart;
            Assert.True(Math.Abs(rate - expected) <= 1e-15, $"{rate} {expected}");
        }
    }

    [Fact]
    public void BeforeItsFirstObservationTheSmileCorrectionRouteIsTheNoSmileRoutes()
    {
        (IborIndex index, EvaluationContext context) = Market();
        RangeAccrualFloatersCoupon coupon = Coupon(index, new Date(10, January, 2025), new Date(10, July, 2025), context);
        foreach ((double ls, double lt, double callSpread) in new[] { (0.10, 0.30, 0.03337731105469358), (0.30, 0.10, 0.03465775920672742) })
        {
            double noSmile = coupon.SetPricer(Pricer(ls, lt, false, false)).Rate(context);
            double smileCorrection = coupon.SetPricer(Pricer(ls, lt, true, false)).Rate(context);
            Assert.Equal(noSmile, smileCorrection);
            Assert.True(Math.Abs(smileCorrection - callSpread) <= 1e-11, $"{smileCorrection} {callSpread}");
        }
    }

    [Fact]
    public void OnItsStartDateEveryRouteIsQuantLibs()
    {
        (IborIndex index, EvaluationContext context) = Market();
        RangeAccrualFloatersCoupon coupon = Coupon(index, context.EvaluationDate, new Date(15, July, 2025), context);
        Assert.Equal(0.03224747570037857, coupon.SetPricer(Pricer(0.10, 0.30, false, false)).Rate(context));
        Assert.Equal(0.03224747569642593, coupon.SetPricer(Pricer(0.10, 0.30, true, true)).Rate(context));
        Assert.Equal(0.03224747570037857, coupon.SetPricer(Pricer(0.10, 0.30, true, false)).Rate(context));
        Assert.Equal(0.033584697737447385, coupon.SetPricer(Pricer(0.30, 0.10, false, false)).Rate(context));
        Assert.Equal(0.033584697738265355, coupon.SetPricer(Pricer(0.30, 0.10, true, true)).Rate(context));
        Assert.Equal(0.033584697737447385, coupon.SetPricer(Pricer(0.30, 0.10, true, false)).Rate(context));
    }

    // The C++ range accrual vectors' Euribor 6M on its curve, and their evaluation date and stored fixings.
    private static (IborIndex Index, EvaluationContext Context) Market()
    {
        VectorCase c = VectorFile.Load("cxx/cashflows/rangeaccrual").Cases[0];
        JsonElement index = c.Input("coupon").GetProperty("index");
        return (IndexInputs.IborIndexOf(index), c.Context());
    }

    // A weekly coupon on 1e6 between 2% and 4% (or the triggers given), 2 fixing days, Actual/360.
    private static RangeAccrualFloatersCoupon Coupon(
        IborIndex index, Date start, Date end, EvaluationContext context, double lower = 0.02, double upper = 0.04) =>
        new(
            end,
            1e6,
            index,
            start,
            end,
            2,
            new Actual360(),
            1.0,
            0.0,
            start,
            end,
            new Schedule(
                start,
                end,
                new Period(1, TimeUnit.Weeks),
                new TARGET(),
                BusinessDayConvention.ModifiedFollowing,
                BusinessDayConvention.ModifiedFollowing,
                DateGenerationRule.Forward,
                false,
                context),
            lower,
            upper);

    // Flat smiles in their time form, the expiry's at 0.5 years and the payment's at 0.75: the pricer reads their
    // volatilities only.
    private static RangeAccrualPricerByBgm Pricer(double lambdaS, double lambdaT, bool withSmile, bool byCallSpread) => new(
        1.0, new FlatSmileSection(0.5, lambdaS, new Actual365Fixed()), new FlatSmileSection(0.75, lambdaT, new Actual365Fixed()), withSmile, byCallSpread);
}
