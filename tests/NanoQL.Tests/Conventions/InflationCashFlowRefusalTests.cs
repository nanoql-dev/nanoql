// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The inflation cash flows' refusals no vector reaches: an inflation coupon built without a pricer refuses its rate
/// with QuantLib's "pricer not set", and a CPI coupon its accrued amount with QuantLib's "pricer not set or of wrong
/// type"; an indexed cash flow without a base date refuses its base fixing and its amount with "no base date
/// specified"; each a <see cref="NanoQLException"/>. The inflation pricer setter refuses a null cash flow, and a
/// <c>default</c> array, which isn't a leg, with an <see cref="ArgumentException"/> naming the leg.
/// </summary>
public class InflationCashFlowRefusalTests
{
    private static readonly Date Today = new(15, Month.June, 2024);

    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void ACouponWithoutAPricerIsRefused()
    {
        Assert.Equal("pricer not set", Assert.Throws<NanoQLException>(() => YoYCoupon().Rate(Context)).Message);
        Assert.Equal(
            "pricer not set or of wrong type",
            Assert.Throws<NanoQLException>(() => CpiCoupon().AccruedAmount(new Date(15, Month.January, 2025), Context)).Message);
    }

    [Fact]
    public void AnIndexedCashFlowWithoutABaseDateIsRefused()
    {
        var flow = new IndexedCashFlow(
            1e6, new UKRPI(ZeroCurve()), null, new Date(1, Month.March, 2024), new Date(15, Month.June, 2025));

        Assert.Equal("no base date specified", Assert.Throws<NanoQLException>(() => flow.BaseFixing(Context)).Message);
        Assert.Equal("no base date specified", Assert.Throws<NanoQLException>(() => flow.Amount(Context)).Message);
    }

    [Fact]
    public void TheSetterRefusesANullCashFlow()
    {
        var pricer = new YoYInflationCouponPricer();

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => InflationCouponPricerFunctions.SetCouponPricer([YoYCoupon(), null!], pricer));
        Assert.Equal("leg", e.ParamName);
        Assert.StartsWith("null cash flow in leg", e.Message, StringComparison.Ordinal);
        Assert.Equal(
            "leg",
            Assert.Throws<ArgumentException>(() => InflationCouponPricerFunctions.SetCouponPricer(default, pricer)).ParamName);
    }

    private static ZeroInflationCurve ZeroCurve() =>
        new(
            Today,
            [new Date(1, Month.April, 2024), new Date(1, Month.April, 2034)],
            [0.03, 0.03],
            Frequency.Monthly,
            new Actual365Fixed());

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
            new UKRPI(ZeroCurve()),
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            new Actual365Fixed(),
            0.02);
}
