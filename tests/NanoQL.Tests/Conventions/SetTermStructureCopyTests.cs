// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's <c>setTermStructure</c> links a curve into the helper it is called on; nanoql's
/// <see cref="BootstrapHelperExtensions.SetTermStructure{T}(T, YieldTermStructure)"/> returns a copy bound to the curve,
/// of the helper's own static type (playbook §2). The helper it is called on stays unbound, and an unbound helper
/// refuses to price with QuantLib's "term structure not set".
/// </summary>
public class SetTermStructureCopyTests
{
    private static readonly Date Today = new(15, Month.January, 2024);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void ABoundCopyLeavesTheHelperUnbound()
    {
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());
        DepositRateHelper deposit = Deposit();
        SwapRateHelper swap = Swap();

        DepositRateHelper boundDeposit = deposit.SetTermStructure(curve);
        SwapRateHelper boundSwap = swap.SetTermStructure(curve);

        Assert.NotSame(deposit, boundDeposit);
        Assert.NotSame(swap, boundSwap);
        Assert.NotSame(swap.Swap, boundSwap.Swap);
        _ = boundDeposit.ImpliedQuote(Context);
        _ = boundSwap.ImpliedQuote(Context);
        Assert.Equal("term structure not set", Assert.Throws<NanoQLException>(() => deposit.ImpliedQuote(Context)).Message);
        Assert.Equal("term structure not set", Assert.Throws<NanoQLException>(() => swap.ImpliedQuote(Context)).Message);

        // the copy keeps the helper's dates and quote
        Assert.Equal(deposit.Quote, boundDeposit.Quote);
        Assert.Equal(swap.PillarDate, boundSwap.PillarDate);
        Assert.Equal(swap.LatestRelevantDate, boundSwap.LatestRelevantDate);
    }

    [Fact]
    public void AnUnboundHelperRefusesToPrice()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(() => Swap().QuoteError(Context));
        Assert.Equal("term structure not set", e.Message);

        ArgumentNullException n = Assert.Throws<ArgumentNullException>(() => Deposit().SetTermStructure(null!));
        Assert.Equal("t", n.ParamName);
        Assert.StartsWith("null term structure given", n.Message, StringComparison.Ordinal);
    }

    private static DepositRateHelper Deposit() =>
        new(0.03, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
            new Actual360(), Context);

    private static SwapRateHelper Swap() =>
        new(
            0.031,
            new Period(5, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(),
            Context);
}
