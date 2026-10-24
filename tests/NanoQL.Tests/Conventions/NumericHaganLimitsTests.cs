// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The numeric Hagan pricer's integration limits (the S-cms plan's CMR1 (iv)): QuantLib's optionlet price sets them in
/// mutable members, which <c>upperLimit()</c>, <c>lowerLimit()</c> and <c>stdDeviations()</c> read afterwards;
/// <see cref="NumericHaganPricer.IntegrationLimits"/> computes them from the coupon and the optionlet's type (the
/// harness vectors check them against QuantLib's). A coupon whose fixing is known integrates nothing: the constructor's
/// limits, and no number of standard deviations. A coupon that isn't a CMS coupon and a type outside
/// <see cref="OptionType"/> are refused.
/// </summary>
public class NumericHaganLimitsTests
{
    [Fact]
    public void AKnownFixingIntegratesNothing()
    {
        var pricer = new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0, 0.001, 0.5);
        Date start = new TARGET().Advance(CmsInputs.Today, 2, TimeUnit.Days);
        SwapIndex index = CmsInputs.SwapIndex(new Period(10, TimeUnit.Years));
        var fixedToday = new CmsCoupon(start + new Period(1, TimeUnit.Years), 1.0, start, start + new Period(1, TimeUnit.Years), 2, index);
        Assert.Equal(new NumericHaganPricerIntegrationLimits(0.001, 0.5, null), pricer.IntegrationLimits(fixedToday, OptionType.Call, CmsInputs.Context));

        // a future fixing: a call sets the upper limit, a put leaves both on a lognormal volatility
        NumericHaganPricerIntegrationLimits call = pricer.IntegrationLimits(CmsInputs.Coupon(index), OptionType.Call, CmsInputs.Context);
        Assert.Equal(0.001, call.LowerLimit);
        Assert.True(call.UpperLimit > 0.1);
        Assert.Equal(8.0, call.StdDeviations);
        Assert.Equal(new NumericHaganPricerIntegrationLimits(0.001, 0.5, 8.0), pricer.IntegrationLimits(CmsInputs.Coupon(index), OptionType.Put, CmsInputs.Context));
    }

    [Fact]
    public void TheRefusals()
    {
        var pricer = new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0);
        CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)));
        Assert.Equal("optionType", Assert.Throws<ArgumentOutOfRangeException>(() => pricer.IntegrationLimits(c, (OptionType)0, CmsInputs.Context)).ParamName);
        var ibor = new IborCoupon(c.Date, 1.0, c.AccrualStartDate, c.AccrualEndDate, 2, CmsInputs.Ibor);
        ArgumentException e = Assert.Throws<ArgumentException>(() => pricer.IntegrationLimits(ibor, OptionType.Call, CmsInputs.Context));
        Assert.Equal("coupon", e.ParamName);
        Assert.StartsWith("CMS coupon needed", e.Message, StringComparison.Ordinal);
    }
}
