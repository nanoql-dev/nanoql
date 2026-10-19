// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The BMA family's refusals, whose type, message and <see cref="ArgumentException.ParamName"/> a vector's
/// <c>{"throws": true}</c> can't assert (playbook §9.4): the BMA index's, the average-BMA coupon's, its pricer's and
/// its leg's, the BMA swap's and the BMA swap rate helper's. A BMA swap refuses a type outside <see cref="SwapType"/>
/// on entry, before it builds its legs, where QuantLib refuses it after them (the enum row). An expired BMA swap rate
/// helper refuses its implied quote with QuantLib's "null libor NPV" text.
/// </summary>
public class BMAErrorTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly Schedule Quarterly = new MakeSchedule().From(new Date(17, Month.January, 2025))
        .To(new Date(17, Month.January, 2026)).WithTenor(new Period(3, TimeUnit.Months))
        .WithCalendar(new TARGET()).ToSchedule();

    private static readonly IborIndex Libor = new Euribor3M(Curve);
    private static readonly Calendar UsGovernmentBond = new UnitedStates(UnitedStatesMarket.GovernmentBond);

    [Fact]
    public void ABMASwapChecksItsTypeOnEntry()
    {
        // A null Libor index, which the Libor leg would refuse, is never reached: the type is checked first.
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => Swap((SwapType)5, Quarterly, null!, Quarterly, Context));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("Unknown BMA-swap type", e.Message, StringComparison.Ordinal);
        Assert.Equal(
            "type",
            Assert.Throws<ArgumentOutOfRangeException>(() => Swap((SwapType)5, Quarterly, Libor, Quarterly, Context))
                .ParamName);

        // The context is checked before it.
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(() => Swap((SwapType)5, Quarterly, Libor, Quarterly, null!)).ParamName);
        Assert.Equal(SwapType.Receiver, Swap(SwapType.Receiver, Quarterly, Libor, Quarterly, Context).Type);
    }

    [Fact]
    public void AnExpiredBMAHelperRefusesItsImpliedQuote()
    {
        // QuantLib's fairLiborFraction() on an expired swap, whose results are zeros (setupExpired), so its pure Libor
        // NPV is 0 (bmaswap.cpp:120-122): a 1Y helper built in January 2020, bound and priced in January 2025
        var built = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2020) };
        var helper = new BMASwapRateHelper(
            0.7, new Period(1, TimeUnit.Years), 2, UsGovernmentBond, new Period(1, TimeUnit.Weeks),
            BusinessDayConvention.Following, null, new BMAIndex(), Libor, built);
        Assert.Equal(
            "result not available (null libor NPV)",
            Assert.Throws<NanoQLException>(() => helper.SetTermStructure(Curve).ImpliedQuote(Context)).Message);
    }

    [Fact]
    public void TheBMAFamilysRefusals()
    {
        // the index
        var bma = new BMAIndex(Curve);
        Assert.Equal(
            "null term structure set to this instance of BMA1W Actual/Actual (ISDA)",
            Assert.Throws<NanoQLException>(() => new BMAIndex().ForecastFixing(Today, Context)).Message);
        AssertRefusal<ArgumentNullException>("context", () => bma.ForecastFixing(Today, null!));
        AssertRefusal<ArgumentException>("fixingDate", () => bma.IsValidFixingDate(default));
        AssertRefusal<ArgumentException>("fixingDate", () => bma.ValueDate(new Date(16, Month.January, 2025)));
        AssertRefusal<ArgumentException>("valueDate", () => bma.MaturityDate(default));
        AssertRefusal<ArgumentException>("start", () => bma.FixingSchedule(default, Today));
        AssertRefusal<ArgumentException>(
            "effectiveDate", () => bma.FixingSchedule(Today, new Date(1, Month.January, 2025)));

        // the coupon: no single fixing, and averaging days that differ from the interest days
        AverageBMACoupon coupon = Coupon(new Date(17, Month.January, 2025), new Date(17, Month.April, 2025));
        Assert.Equal(
            "no single fixing date for average-BMA coupon",
            Assert.Throws<NanoQLException>(() => coupon.FixingDate()).Message);
        Assert.Equal(
            "no single fixing for average-BMA coupon",
            Assert.Throws<NanoQLException>(() => coupon.IndexFixing(Context)).Message);
        Assert.Equal(
            "not defined for average-BMA coupon",
            Assert.Throws<NanoQLException>(() => coupon.ConvexityAdjustment(Context)).Message);
        AssertRefusal<ArgumentNullException>("context", () => coupon.IndexFixing(null!));
        AssertRefusal<ArgumentNullException>("context", () => coupon.ConvexityAdjustment(null!));
        AssertRefusal<ArgumentNullException>("context", () => coupon.IndexFixings(null!));
        Assert.Equal(
            "averaging days 0 differ from interest days -1",
            Assert.Throws<NanoQLException>(
                () => Coupon(new Date(24, Month.January, 2025), new Date(23, Month.January, 2025)).Rate(Context))
            .Message);
        Assert.Equal(
            "Null gearing not allowed (Parameter 'gearing')",
            AssertRefusal<ArgumentException>("gearing", () => new AverageBMACoupon(
                new Date(17, Month.April, 2025),
                1e6,
                new Date(17, Month.January, 2025),
                new Date(17, Month.April, 2025),
                bma,
                0.0)).Message);

        // its pricer gives the swaplet rate only, and refuses another coupon
        FloatingRateCouponPricer pricer = coupon.Pricer!;
        Func<object>[] unavailable =
        [
            () => pricer.SwapletPrice(coupon, Context),
            () => pricer.CapletPrice(coupon, 0.03, Context),
            () => pricer.CapletRate(coupon, 0.03, Context),
            () => pricer.FloorletPrice(coupon, 0.03, Context),
            () => pricer.FloorletRate(coupon, 0.03, Context),
        ];
        foreach (Func<object> f in unavailable)
        {
            Assert.Equal("not available", Assert.Throws<NanoQLException>(f).Message);
        }

        FloatingRateCoupon ibor = new IborCoupon(
                new Date(17, Month.April, 2025),
                1e6,
                new Date(17, Month.January, 2025),
                new Date(17, Month.April, 2025),
                2,
                new Euribor3M(Curve))
            .SetPricer(pricer);
        Assert.Equal("wrong coupon type", Assert.Throws<NanoQLException>(() => ibor.Rate(Context)).Message);

        // the leg
        var leg = new AverageBMALeg(Quarterly, bma);
        Assert.Equal("no notional given", Assert.Throws<NanoQLException>(() => leg.ToLeg()).Message);
        AssertRefusal<ArgumentOutOfRangeException>(
            "convention", () => leg.WithPaymentAdjustment((BusinessDayConvention)42));
        AssertRefusal<ArgumentNullException>("schedule", () => new AverageBMALeg(null!, bma));

        // the swap's own arguments
        Assert.StartsWith(
            "no index provided",
            AssertRefusal<ArgumentNullException>(
                "liborIndex", () => Swap(SwapType.Payer, Quarterly, null!, Quarterly, Context)).Message,
            StringComparison.Ordinal);
        AssertRefusal<ArgumentNullException>("liborSchedule", () => Swap(SwapType.Payer, null!, Libor, Quarterly, Context));
        AssertRefusal<ArgumentNullException>("bmaSchedule", () => Swap(SwapType.Payer, Quarterly, Libor, null!, Context));

        // the helper's own arguments, checked before the swap it builds would refuse them
        const BusinessDayConvention following = BusinessDayConvention.Following;
        AssertRefusal<ArgumentOutOfRangeException>(
            "settlementDays", () => Helper(-1, UsGovernmentBond, following, Libor, Context));
        AssertRefusal<ArgumentNullException>("calendar", () => Helper(2, null!, following, Libor, Context));
        AssertRefusal<ArgumentOutOfRangeException>(
            "bmaConvention", () => Helper(2, UsGovernmentBond, (BusinessDayConvention)42, Libor, Context));
        AssertRefusal<ArgumentNullException>("context", () => Helper(2, UsGovernmentBond, following, Libor, null!));
        BMASwapRateHelper helper = Helper(2, UsGovernmentBond, following, Libor, Context);

        // the helper's implied quote: unbound, and on an index with no curve to discount on
        Assert.Equal(
            "term structure not set",
            Assert.Throws<NanoQLException>(() => helper.ImpliedQuote(Context)).Message);
        Assert.Equal(
            "discounting term structure handle is empty",
            Assert.Throws<NanoQLException>(() => Helper(2, UsGovernmentBond, following, new Euribor3M(), Context)
                .SetTermStructure(Curve).ImpliedQuote(Context)).Message);
        AssertRefusal<ArgumentNullException>("context", () => helper.SetTermStructure(Curve).ImpliedQuote(null!));
    }

    private static AverageBMACoupon Coupon(Date start, Date end) =>
        new(end, 1e6, start, end, new BMAIndex(Curve));

    private static BMASwap Swap(
        SwapType type, Schedule liborSchedule, IborIndex liborIndex, Schedule bmaSchedule, EvaluationContext context) =>
        new(type, 1e6, liborSchedule, 0.7, 0.0, liborIndex, null, bmaSchedule, new BMAIndex(Curve), null, context);

    private static BMASwapRateHelper Helper(
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention bmaConvention,
        IborIndex index,
        EvaluationContext context) =>
        new(
            0.7,
            new Period(2, TimeUnit.Years),
            settlementDays,
            calendar,
            new Period(1, TimeUnit.Weeks),
            bmaConvention,
            null,
            new BMAIndex(),
            index,
            context);

    private static T AssertRefusal<T>(string paramName, Func<object> build)
        where T : ArgumentException
    {
        T e = Assert.Throws<T>(build);
        Assert.Equal(paramName, e.ParamName);
        return e;
    }
}
