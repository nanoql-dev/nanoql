// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The CVA swap engine's investor curve: QuantLib's constructors build, for an empty investor handle, a flat hazard
/// rate of 1e-12 on no settlement days, the null calendar and the counterparty curve's day counter
/// (<c>cvaswapengine.cpp:46-48</c>, <c>:72-74</c>, <c>:97-99</c>); nanoql's <see cref="CounterpartyAdjSwapEngine"/>
/// takes the curve as nullable and builds that one per call, under the call's context, so with none given the results
/// are those on that curve given explicitly, bit for bit; a curve given is used.
/// </summary>
public class CvaDefaultInvestorCurveTests
{
    [Fact]
    public void NoInvestorCurveIsQuantLibsDefaultFreeOne()
    {
        VectorCase c = Case("5Y payer, Black, no investor curve");
        EvaluationContext context = c.Context();
        FixedVsFloatingSwapResults implicitCurve = CvaSwapEngineCxxVectorTests.Price(c, context);

        VanillaSwap swap = SwapInputs.VanillaSwapOf(c.Input("swap"), context);
        YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
        DefaultProbabilityTermStructure ctpty = CreditInputs.CurveOf(c.Input("ctptyCurve"), context);
        FlatHazardRate defaultFree = FlatHazardRate.Create(0, new NullCalendar(), 1.0e-12, ctpty.DayCounter, context);
        FixedVsFloatingSwapResults explicitCurve = CounterpartyAdjSwapEngine.Calculate(
            swap, context, discount, 0.15, ctpty, c.Double("ctptyRecoveryRate"), defaultFree, c.Double("investorRecoveryRate"));

        Assert.Equal(explicitCurve.NPV, implicitCurve.NPV);
        Assert.Equal(explicitCurve.FairRate, implicitCurve.FairRate);
    }

    [Fact]
    public void AnInvestorCurveGivenIsUsed()
    {
        VectorCase c = Case("5Y payer, Black, no investor curve");
        EvaluationContext context = c.Context();
        VanillaSwap swap = SwapInputs.VanillaSwapOf(c.Input("swap"), context);
        YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
        DefaultProbabilityTermStructure ctpty = CreditInputs.CurveOf(c.Input("ctptyCurve"), context);
        double recovery = c.Double("ctptyRecoveryRate");

        FixedVsFloatingSwapResults unilateral = CounterpartyAdjSwapEngine.Calculate(swap, context, discount, 0.15, ctpty, recovery);
        FixedVsFloatingSwapResults bilateral = CounterpartyAdjSwapEngine.Calculate(
            swap, context, discount, 0.15, ctpty, recovery, new FlatHazardRate(context.EvaluationDate, 0.01, new Actual365Fixed()), 0.4);

        Assert.True(bilateral.NPV > unilateral.NPV, $"{bilateral.NPV} {unilateral.NPV}");
    }

    private static VectorCase Case(string name) =>
        VectorFile.Load("cxx/pricingengines/swap/cvaswapengine").Cases.Single(x => x.Name == name);
}
