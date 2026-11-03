// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The equity quanto pricer's inputs: QuantLib's pricer holds handles, which may be empty, and its <c>initialize</c>
/// refuses an empty one when a flow is priced (<c>equitycashflow.cpp:91-113</c>); nanoql's takes each input as
/// nullable, builds with any of them null, and refuses a null one at pricing with QuantLib's text, in QuantLib's order:
/// the fixing date before the base date first, then the quanto curve, the equity volatility, the FX volatility and the
/// correlation. A flow's pricer is set by <see cref="EquityCashFlow.SetPricer"/>, which returns a copy holding it,
/// where QuantLib's <c>setPricer</c> changes the flow.
/// </summary>
public class EquityPricerRefusalTests
{
    private static readonly Date Today = new(27, January, 2023);

    [Fact]
    public void NullInputsAreRefusedAtPricingInQuantLibsOrder()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        EquityIndex index = Index();
        var quanto = new FlatForward(Today, 0.02, new Actual365Fixed());
        var vol = new BlackConstantVol(Today, new TARGET(), 0.25, new Actual365Fixed());
        var flow = new EquityCashFlow(1e6, index, Today, new Date(29, January, 2024), new Date(31, January, 2024));

        Refused(flow.SetPricer(new EquityQuantoCashFlowPricer(null, null, null, null)), context, "Quanto currency term structure handle cannot be empty.");
        Refused(flow.SetPricer(new EquityQuantoCashFlowPricer(quanto, null, null, null)), context, "Equity volatility term structure handle cannot be empty.");
        Refused(flow.SetPricer(new EquityQuantoCashFlowPricer(quanto, vol, null, null)), context, "FX volatility term structure handle cannot be empty.");
        Refused(flow.SetPricer(new EquityQuantoCashFlowPricer(quanto, vol, vol, null)), context, "Correlation handle cannot be empty.");
        Assert.True(double.IsFinite(flow.SetPricer(new EquityQuantoCashFlowPricer(quanto, vol, vol, 0.5)).Amount(context)));

        // the dates are checked before the inputs
        var reversed = new EquityCashFlow(1e6, index, new Date(29, January, 2024), Today, new Date(31, January, 2024));
        Refused(reversed.SetPricer(new EquityQuantoCashFlowPricer(null, null, null, null)), context, "Fixing date cannot fall before base date.");
    }

    [Fact]
    public void SetPricerReturnsACopy()
    {
        var flow = new EquityCashFlow(1e6, Index(), Today, new Date(29, January, 2024), new Date(31, January, 2024));
        var pricer = new EquityQuantoCashFlowPricer(null, null, null, null);
        EquityCashFlow priced = flow.SetPricer(pricer);

        Assert.NotSame(flow, priced);
        Assert.Null(flow.Pricer);
        Assert.Same(pricer, priced.Pricer);
        Assert.Equal(flow.FixingDate, priced.FixingDate);
        Assert.Equal(flow.BaseDate(), priced.BaseDate());
    }

    private static EquityIndex Index() => new(
        "eqIndex",
        new TARGET(),
        new EURCurrency(),
        new FlatForward(Today, 0.03, new Actual365Fixed()),
        new FlatForward(Today, 0.01, new Actual365Fixed()),
        8700.0);

    private static void Refused(EquityCashFlow flow, EvaluationContext context, string text)
    {
        NanoQLException e = Assert.Throws<NanoQLException>(() => flow.Amount(context));
        Assert.Equal(text, e.Message);
    }
}
