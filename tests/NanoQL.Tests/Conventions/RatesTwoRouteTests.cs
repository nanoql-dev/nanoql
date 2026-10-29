// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// C3's rate and process routes against the landed classes (the S-xrt plan's §2.5): generalized Hull-White at a constant
/// reversion and volatility against Hull-White, its bond options within its numerical integrals' 4.6e-4 (QuantLib's
/// design, kept) and its fitting parameter φ to rounding; its Ornstein-Uhlenbeck process against the landed one; the
/// quanto pricer at zero correlation against the Black Ibor pricer; the stripped coupon against its capped coupon less
/// the underlying; and the extended process on flat parameters against the Black-Scholes-Merton process.
/// </summary>
public class RatesTwoRouteTests
{
    private static readonly Date Today = new(15, Month.June, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void GeneralizedHullWhiteAtConstantParametersIsHullWhite()
    {
        var ghw = new GeneralizedHullWhite(Curve, 0.1, 0.01);
        var hw = new HullWhite(Curve, 0.1, 0.01);
        double a = ghw.DiscountBondOption(OptionType.Call, 0.9, 1.0, 5.0);
        double b = hw.DiscountBondOption(OptionType.Call, 0.9, 1.0, 5.0);
        Assert.True(Math.Abs((a / b) - 1) <= 5e-4, $"{a} for {b}");
        foreach (double t in new[] { 0.5, 2.0, 5.0 })
        {
            Assert.Equal(hw.Dynamics().ShortRate(t, 0.0), ghw.HwDynamics().ShortRate(t, 0.0), 15);
        }
    }

    [Fact]
    public void TheGeneralizedProcessAtConstantParametersIsTheOrnsteinUhlenbeckProcess()
    {
        var generalized = new GeneralizedOrnsteinUhlenbeckProcess(_ => 0.1, _ => 0.01, 0.02, 0.03);
        var ou = new OrnsteinUhlenbeckProcess(0.1, 0.01, 0.02, 0.03);
        Assert.Equal(ou.Expectation(0.5, 0.02, 0.25), generalized.Expectation(0.5, 0.02, 0.25));
        Assert.True(Math.Abs((generalized.Variance(0.5, 0.02, 0.25) / ou.Variance(0.5, 0.02, 0.25)) - 1) <= 4e-16);
        Assert.Equal(ou.Drift(0.5, 0.02), generalized.Drift(0.5, 0.02));
    }

    [Fact]
    public void TheQuantoPricerAtZeroCorrelationIsTheBlackIborPricer()
    {
        OptionletVolatilityStructure capletVol = new ConstantOptionletVolatility(
            Today, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
        BlackVolTermStructure fxVol = new BlackConstantVol(Today, new TARGET(), 0.1, new Actual365Fixed());
        IborCoupon coupon = Coupon();
        double black = coupon.SetPricer(new BlackIborCouponPricer(Context, capletVol)).Rate(Context);
        Assert.Equal(black, coupon.SetPricer(new BlackIborQuantoCouponPricer(fxVol, 0.0, capletVol, Context)).Rate(Context));
        Assert.NotEqual(black, coupon.SetPricer(new BlackIborQuantoCouponPricer(fxVol, 0.3, capletVol, Context)).Rate(Context));
    }

    [Fact]
    public void TheStrippedCouponIsTheCappedCouponLessTheUnderlying()
    {
        OptionletVolatilityStructure capletVol = new ConstantOptionletVolatility(
            Today, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
        var pricer = new BlackIborCouponPricer(Context, capletVol);
        Date start = new TARGET().Advance(Today, 1, TimeUnit.Years);
        Date end = new TARGET().Advance(start, 6, TimeUnit.Months);
        var index = new Euribor6M(Curve);
        foreach ((double? cap, double? floor) in new (double?, double?)[] { (0.035, null), (null, 0.025), (0.035, 0.025) })
        {
            CappedFlooredIborCoupon capped = new CappedFlooredIborCoupon(end, 100.0, start, end, 2, index, cap: cap, floor: floor)
                .SetPricer(pricer);
            var stripped = new StrippedCappedFlooredCoupon(capped);
            double underlying = capped.Underlying.Rate(Context);
            double expected = capped.IsCapped && !capped.IsFloored
                ? underlying - capped.Rate(Context)
                : capped.Rate(Context) - underlying;
            Assert.Equal(expected, stripped.Rate(Context), 15);
        }
    }

    [Fact]
    public void TheExtendedProcessOnFlatParametersIsTheBlackScholesMertonProcess()
    {
        DayCounter dc = new Actual365Fixed();
        BlackVolTermStructure vol = new BlackConstantVol(Today, new NullCalendar(), 0.2, dc);
        var extended = new ExtendedBlackScholesMertonProcess(100.0, new FlatForward(Today, 0.01, dc), Curve, vol);
        var bsm = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.01, dc), Curve, vol);
        Assert.Equal(bsm.Drift(0.5, 100.0), extended.Drift(0.5, 100.0), 15);
        Assert.Equal(bsm.Diffusion(0.5, 100.0), extended.Diffusion(0.5, 100.0), 15);
        Assert.Equal(bsm.Evolve(0.5, 100.0, 0.25, 0.7), extended.Evolve(0.5, 100.0, 0.25, 0.7), 10);
    }

    private static IborCoupon Coupon()
    {
        Date start = new TARGET().Advance(Today, 1, TimeUnit.Years);
        Date end = new TARGET().Advance(start, 6, TimeUnit.Months);
        return new IborCoupon(end, 100.0, start, end, 2, new Euribor6M(Curve));
    }
}
