// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Black Ibor pricer's option struck below −shift on a lognormal volatility (the S-cms plan's CMR5 (xxiii), its
/// Ibor part): QuantLib's <c>BlackIborCouponPricer::optionletRate</c> calls <c>blackFormula</c> at the effective strike
/// (<c>couponpricer.cpp</c>), which refuses a strike below −shift ("strike + displacement (-0.005 + 0) must be
/// non-negative"), so a capped or floored Ibor coupon struck there, and a digital Ibor coupon whose replication strikes
/// reach there, refuse; at −shift itself the formula prices the intrinsic. As ruled: at or below −shift a caplet is
/// the forward less the strike and a floorlet 0, the formula's limit there, on a forward above −shift; a forward at or
/// below −shift reaches the formula and its refusal, as QuantLib's. QuantLib's check: its Black formula at
/// −shift + 1e-12, and its digital coupon struck 1e-12 above its replication's reach (the wheel and the C++ harness).
/// </summary>
public class BlackIborBelowShiftTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void ACapAtOrBelowMinusShiftIsTheForwardsIntrinsic()
    {
        // QuantLib's, on a flat 3% curve: the Ibor rate 0.029810230417612306; capped at -shift 0.0 and
        // -0.010000000000000002, and at -shift + 1e-12 9.999986949615902e-13 and -0.009999999999000003; floored at
        // -shift the Ibor rate; capped or floored 0.005 below -shift, refused
        foreach ((double shift, double atMinusShift, double aboveMinusShift) in new[]
                 { (0.0, 0.0, 9.999986949615902e-13), (0.01, -0.010000000000000002, -0.009999999999000003) })
        {
            Assert.Equal(0.029810230417612306, Coupon(0.03, shift).Rate(Context));
            double capped = Coupon(0.03, shift, cap: -shift).Rate(Context);
            Assert.Equal(atMinusShift, capped);
            Assert.True(Math.Abs(aboveMinusShift - capped - 1e-12) <= 1e-17, $"{capped}");
            Assert.True(Math.Abs(Coupon(0.03, shift, cap: -shift - 0.005).Rate(Context) - (-shift - 0.005)) <= 1e-17);
            Assert.Equal(0.029810230417612306, Coupon(0.03, shift, floor: -shift).Rate(Context));
            Assert.Equal(0.029810230417612306, Coupon(0.03, shift, floor: -shift - 0.005).Rate(Context));
        }
    }

    [Fact]
    public void ADigitalAtMinusShiftReplicatesToOne()
    {
        // QuantLib's, on the digital Ibor harness vectors' index: struck at -shift a cash call and an asset call
        // refuse; struck at -shift + 5e-5 + 1e-12, the central replication's lower strike 1e-12 above -shift, the cash
        // call's option rate is 0.010000000000000286 and the asset call's 0.031054195614104378 (0.031054195614104094 on
        // a 1% shift), the underlying's rate
        VectorCase c = VectorFile.Load("cxx/cashflows/digitaliborcoupon").Cases.Single(x => x.Name == "cash calls at 5%");
        IborIndex index = IndexInputs.IborIndexOf(c.Input("leg").GetProperty("index"));
        var underlying = new IborCoupon(
            new Date(19, Month.January, 2027), 1e6, new Date(19, Month.January, 2026), new Date(19, Month.January, 2027), 2, index);
        foreach (double shift in new[] { 0.0, 0.01 })
        {
            var pricer = new BlackIborCouponPricer(Context, Volatility(shift));
            DigitalCoupon cash = new DigitalCoupon(underlying, -shift, callDigitalPayoff: 0.01).SetPricer(pricer);
            Assert.True(Math.Abs(cash.CallOptionRate(Context) - 0.01) <= 1e-15, $"{cash.CallOptionRate(Context)}");
            DigitalCoupon asset = new DigitalCoupon(underlying, -shift).SetPricer(pricer);
            Assert.True(Math.Abs(asset.CallOptionRate(Context) - 0.031054195614104378) <= 1e-15, $"{asset.CallOptionRate(Context)}");
        }
    }

    [Fact]
    public void AForwardAtOrBelowMinusShiftIsRefused()
    {
        // QuantLib's, on a flat -0.5% curve: the Ibor rate -0.004925398181679201; capped at 0.005, "forward +
        // displacement (-0.0049254 + 0) must be positive"; capped at -0.005, "strike + displacement (-0.005 + 0) must
        // be non-negative"
        Assert.Equal(-0.004925398181679201, Coupon(-0.005, 0.0).Rate(Context));
        ArgumentException e = Assert.Throws<ArgumentException>(() => Coupon(-0.005, 0.0, cap: 0.005).Rate(Context));
        Assert.Equal("forward", e.ParamName);
        Assert.StartsWith("forward + displacement (-0.004925398181679201 + 0) must be positive", e.Message);
        e = Assert.Throws<ArgumentException>(() => Coupon(-0.005, 0.0, cap: -0.005).Rate(Context));
        Assert.Equal("strike", e.ParamName);
        Assert.StartsWith("strike + displacement (-0.005 + 0) must be non-negative", e.Message);
    }

    // A 6-month Euribor coupon fixing in a year on a flat curve, capped or floored, on a 20% lognormal volatility.
    private static FloatingRateCoupon Coupon(double rate, double shift, double? cap = null, double? floor = null)
    {
        var index = new Euribor6M(new FlatForward(Today, rate, new Actual365Fixed()));
        var calendar = new TARGET();
        Date start = calendar.Advance(Today, 1, TimeUnit.Years);
        Date end = calendar.Advance(start, 6, TimeUnit.Months);
        var pricer = new BlackIborCouponPricer(Context, Volatility(shift));
        FloatingRateCoupon coupon = cap is null && floor is null
            ? new IborCoupon(end, 1.0, start, end, 2, index)
            : new CappedFlooredIborCoupon(end, 1.0, start, end, 2, index, cap: cap, floor: floor);
        return coupon.SetPricer(pricer);
    }

    private static ConstantOptionletVolatility Volatility(double shift) =>
        new(Today, new TARGET(), BusinessDayConvention.Following, 0.20, new Actual365Fixed(), VolatilityType.ShiftedLognormal, shift);
}
