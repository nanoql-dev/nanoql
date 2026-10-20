// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The credit default swap's refusals, with QuantLib's texts (<c>creditdefaultswap.cpp</c>,
/// <c>midpointcdsengine.cpp</c>): the constructors' checks, each an <see cref="ArgumentException"/> naming its
/// argument; the arguments' validation, a <see cref="NanoQLException"/> ("side not set" for QuantLib's unset side, -1;
/// "null notional set"); the mid-point engine's missing curves and a side outside the enum, a
/// <see cref="NanoQLException"/>; and
/// <c>cdsMaturity</c>'s checks, each an <see cref="ArgumentException"/> naming its argument, and a maturity not after
/// the trade date a <see cref="NanoQLException"/>.
/// </summary>
public class CreditDefaultSwapRefusalTests
{
    [Fact]
    public void TheConstructorsChecks()
    {
        Schedule forward = ForwardSchedule(new Date(20, Month.March, 2024));

        AssertArgument(
            "schedule",
            "CreditDefaultSwap needs a non-empty schedule.",
            () => _ = new CreditDefaultSwap(
                ProtectionSide.Buyer, 1e6, 0.01, new Schedule([], new TARGET()), BusinessDayConvention.Following, new Actual360()));
        AssertArgument(
            "protectionStart",
            "protection can not start after accrual",
            () => _ = new CreditDefaultSwap(
                ProtectionSide.Buyer, 1e6, 0.01, forward, BusinessDayConvention.Following, new Actual360(), protectionStart: new Date(21, Month.March, 2024)));
        AssertArgument(
            "upfrontDate",
            "The cash settlement date must not be before the protection start date.",
            () => _ = new CreditDefaultSwap(
                ProtectionSide.Buyer, 1e6, 0.0, 0.01, forward, BusinessDayConvention.Following, new Actual360(), upfrontDate: new Date(19, Month.March, 2024)));
    }

    [Fact]
    public void TheArgumentsAreValidated()
    {
        EvaluationContext context = CreditFixture.Context();
        var unset = new CreditDefaultSwap(
            (ProtectionSide)(-1), 1e6, 0.01, CreditFixture.ScheduleOf(false), BusinessDayConvention.Following, new Actual360());
        Assert.Equal("side not set", Assert.Throws<NanoQLException>(() => Price(unset, context)).Message);

        var noNotional = new CreditDefaultSwap(
            ProtectionSide.Buyer, 0.0, 0.01, CreditFixture.ScheduleOf(false), BusinessDayConvention.Following, new Actual360());
        Assert.Equal("null notional set", Assert.Throws<NanoQLException>(() => Price(noNotional, context)).Message);
    }

    [Fact]
    public void TheMidPointEngineRefusesMissingCurvesAndAnUnknownSide()
    {
        EvaluationContext context = CreditFixture.Context();
        CreditDefaultSwap cds = CreditFixture.Swap(false, 0.01);
        Assert.Equal(
            "no discount term structure set",
            Assert.Throws<NanoQLException>(() => MidPointCdsEngine.Calculate(cds, context, CreditFixture.Probability(), 0.4, null)).Message);
        Assert.Equal(
            "no probability term structure set",
            Assert.Throws<NanoQLException>(() => MidPointCdsEngine.Calculate(cds, context, null, 0.4, CreditFixture.Discount())).Message);

        var unknown = new CreditDefaultSwap(
            (ProtectionSide)2, 1e6, 0.01, CreditFixture.ScheduleOf(false), BusinessDayConvention.Following, new Actual360());
        Assert.Equal("unknown protection side", Assert.Throws<NanoQLException>(() => Price(unknown, context)).Message);
    }

    [Fact]
    public void CdsMaturityChecksItsArguments()
    {
        var tradeDate = new Date(15, Month.June, 2024);
        var fiveYears = new Period(5, TimeUnit.Years);
        AssertArgument(
            "rule",
            "cdsMaturity should only be used with date generation rule CDS2015, CDS or OldCDS",
            () => CreditDefaultSwapFunctions.CdsMaturity(tradeDate, fiveYears, DateGenerationRule.Forward));
        AssertArgument(
            "tenor",
            "cdsMaturity expects a tenor that is a multiple of 3 months.",
            () => CreditDefaultSwapFunctions.CdsMaturity(tradeDate, new Period(2, TimeUnit.Months), DateGenerationRule.CDS));
        AssertArgument(
            "tenor",
            "A tenor of 0M is not supported for OldCDS.",
            () => CreditDefaultSwapFunctions.CdsMaturity(tradeDate, new Period(0, TimeUnit.Months), DateGenerationRule.OldCDS));
        AssertArgument("tradeDate", string.Empty, () => CreditDefaultSwapFunctions.CdsMaturity(default, fiveYears, DateGenerationRule.CDS));

        var back = new Period(-3, TimeUnit.Months);
        Assert.Equal(
            $"error calculating CDS maturity. Tenor is {back}, trade date is 2024-06-15 generating a maturity of 2024-03-20 <= trade date.",
            Assert.Throws<NanoQLException>(() => CreditDefaultSwapFunctions.CdsMaturity(tradeDate, back, DateGenerationRule.CDS)).Message);
    }

    private static CreditDefaultSwapResults Price(CreditDefaultSwap cds, EvaluationContext context) =>
        MidPointCdsEngine.Calculate(cds, context, CreditFixture.Probability(), 0.4, CreditFixture.Discount());

    private static Schedule ForwardSchedule(Date start) =>
        new(
            start,
            new Date(20, Month.March, 2029),
            new Period(Frequency.Quarterly),
            new TARGET(),
            BusinessDayConvention.Following,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Forward,
            false,
            CreditFixture.Context());

    private static void AssertArgument(string paramName, string text, Action build)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(build);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
    }
}
