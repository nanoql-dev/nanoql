// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the inflation coupon pricers' refusals, which the vectors record only as throws: an empty
/// volatility given to <c>SetCapletVolatility</c> is an <see cref="ArgumentNullException"/> naming it, with QuantLib's
/// "empty capletVol handle", and the receiver keeps its own; a coupon of the other kind is an
/// <see cref="ArgumentException"/> naming the coupon, QuantLib's "year-on-year inflation coupon needed" and nanoql's
/// "CPI coupon needed"; a price without a nominal curve, a live optionlet without a volatility and the base pricers'
/// optionlet are each a <see cref="NanoQLException"/> with QuantLib's text.
/// </summary>
public class InflationCouponPricerTests
{
    private static readonly Date Today = new(15, Month.June, 2024);

    private static readonly EvaluationContext Context = new()
    {
        EvaluationDate = Today,
        Fixings = IndexFixings.Empty.AddFixing(new UKRPI(), new Date(1, Month.April, 2024), 310.0),
    };

    [Fact]
    public void AnEmptyVolatilityIsRefusedAndTheReceiverKept()
    {
        var yoy = new YoYInflationCouponPricer(Nominal());
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => yoy.SetCapletVolatility(null!));
        Assert.Equal("capletVol", e.ParamName);
        Assert.StartsWith("empty capletVol handle", e.Message, StringComparison.Ordinal);
        YoYInflationCouponPricer withVol = yoy.SetCapletVolatility(YoYVolatility());
        Assert.NotNull(withVol.CapletVolatility);
        Assert.Null(yoy.CapletVolatility);

        var cpi = new CPICouponPricer(Nominal());
        e = Assert.Throws<ArgumentNullException>(() => cpi.SetCapletVolatility(null!));
        Assert.Equal("capletVol", e.ParamName);
        Assert.StartsWith("empty capletVol handle", e.Message, StringComparison.Ordinal);
        Assert.NotNull(cpi.SetCapletVolatility(CPIVolatility()).CapletVolatility);
        Assert.Null(cpi.CapletVolatility);
    }

    [Fact]
    public void AYearOnYearPricerRefusesACpiCoupon()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new BlackYoYInflationCouponPricer(YoYVolatility(), Nominal()).SwapletRate(CpiCoupon(), Context));
        Assert.Equal("coupon", e.ParamName);
        Assert.StartsWith("year-on-year inflation coupon needed", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACpiPricerRefusesAYearOnYearCoupon()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new CPICouponPricer(CPIVolatility(), Nominal()).CapletRate(YoYCoupon(), 0.03, Context));
        Assert.Equal("coupon", e.ParamName);
        Assert.StartsWith("CPI coupon needed", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APriceWithoutANominalCurveIsRefused()
    {
        var yoy = new BlackYoYInflationCouponPricer(YoYVolatility(), null);
        Assert.Equal(
            "no nominal term structure provided",
            Assert.Throws<NanoQLException>(() => yoy.SwapletPrice(YoYCoupon(), Context)).Message);
        Assert.Equal(
            "no nominal term structure provided",
            Assert.Throws<NanoQLException>(() => yoy.CapletPrice(YoYCoupon(), 0.03, Context)).Message);
        Assert.True(yoy.SwapletRate(YoYCoupon(), Context) > 0.0);

        var cpi = new CPICouponPricer();
        Assert.Equal(
            "no nominal term structure provided",
            Assert.Throws<NanoQLException>(() => cpi.SwapletPrice(CpiCoupon(), Context)).Message);
        Assert.True(cpi.SwapletRate(CpiCoupon(), Context) > 0.0);
    }

    [Fact]
    public void TheBasePricersOptionletIsRefused()
    {
        const string Text = "you must implement this to get a vol-dependent price";
        Assert.Equal(
            Text,
            Assert.Throws<NanoQLException>(
                () => new YoYInflationCouponPricer(YoYVolatility(), Nominal()).CapletRate(YoYCoupon(), 0.03, Context)).Message);
        Assert.Equal(
            Text,
            Assert.Throws<NanoQLException>(
                () => new CPICouponPricer(CPIVolatility(), Nominal()).FloorletRate(CpiCoupon(), 1.05, Context)).Message);
    }

    [Fact]
    public void ALiveCpiOptionletWithoutAVolatilityIsRefused()
    {
        Assert.Equal(
            "missing optionlet volatility",
            Assert.Throws<NanoQLException>(() => new CPICouponPricer(Nominal()).CapletRate(CpiCoupon(), 1.05, Context)).Message);
    }

    private static FlatForward Nominal() => new(Today, 0.04, new Actual365Fixed());

    private static YoYInflationCoupon YoYCoupon() =>
        new(
            new Date(15, Month.July, 2025),
            1e6,
            new Date(15, Month.July, 2024),
            new Date(15, Month.July, 2025),
            0,
            new YoYInflationIndex(
                new UKRPI(),
                new YoYInflationCurve(
                    Today,
                    [new Date(1, Month.April, 2024), new Date(1, Month.April, 2034)],
                    [0.03, 0.03],
                    Frequency.Monthly,
                    new Actual365Fixed())),
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            new Actual365Fixed());

    private static CPICoupon CpiCoupon() =>
        new(
            305.0,
            new Date(15, Month.July, 2025),
            1e6,
            new Date(15, Month.July, 2024),
            new Date(15, Month.July, 2025),
            new UKRPI(
                new ZeroInflationCurve(
                    Today,
                    [new Date(1, Month.April, 2024), new Date(1, Month.April, 2034)],
                    [0.03, 0.03],
                    Frequency.Monthly,
                    new Actual365Fixed())),
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            new Actual365Fixed(),
            0.02);

    private static YoYOptionletVolatilitySurface YoYVolatility() =>
        ConstantYoYOptionletVolatility.Create(
            0.15,
            0,
            new UnitedKingdom(),
            BusinessDayConvention.ModifiedFollowing,
            new Actual365Fixed(),
            new Period(2, TimeUnit.Months),
            Frequency.Monthly,
            false,
            Context);

    private static CPIVolatilitySurface CPIVolatility() =>
        ConstantCPIVolatility.Create(
            0.01,
            0,
            new UnitedKingdom(),
            BusinessDayConvention.ModifiedFollowing,
            new Actual365Fixed(),
            new Period(3, TimeUnit.Months),
            Frequency.Monthly,
            false,
            Context);
}
