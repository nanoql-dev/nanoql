// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The types of the cash-flow functions' refusals, which a vector's <c>{"throws": true}</c> can't tell apart (playbook
/// §9.4), with QuantLib's texts: a check of an argument's own content throws <see cref="ArgumentException"/> naming it
/// (two different coupons on one date in the leg, a leg whose flows can't give the market price, Macaulay's duration of
/// a rate that isn't compounded), and the others <see cref="NanoQLException"/> (no coupon on the date found, a BPS of
/// 0 for the at-the-money rate, a solver that can't bracket, among them the Z-spread of an empty leg for a non-zero
/// target and the yield from a zero guess or with one iteration, and a discount factor's derivative under a rule that
/// mixes compoundings).
/// </summary>
public class CashFlowsErrorTests
{
    private static readonly Date Today = new(16, Month.June, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void TheCashFlowFunctionsRefusals()
    {
        Date start = Today - 30, end = Today + 152;
        ImmutableArray<CashFlow> twoCoupons =
        [
            new FixedRateCoupon(end, 100.0, 0.03, Dc, start, end),
            new FixedRateCoupon(end, 200.0, 0.04, Dc, start, end),
        ];
        ArgumentException a = Assert.Throws<ArgumentException>(() => CashFlows.NextCouponRate(twoCoupons, Context));
        Assert.Equal("leg", a.ParamName);
        Assert.StartsWith(
            "cannot aggregate two different coupons on November 15th, 2025", a.Message, StringComparison.Ordinal);

        ImmutableArray<CashFlow> redemption = [new Redemption(100.0, end)];
        Assert.Equal(
            "no coupon paid at cashflow date November 15th, 2025",
            Assert.Throws<NanoQLException>(() => CashFlows.NextCouponRate(redemption, Context)).Message);

        var curve = new FlatForward(Today, 0.03, Dc);
        Assert.Equal(
            "null bps: impossible atm rate",
            Assert.Throws<NanoQLException>(() => CashFlows.AtmRate(redemption, curve, Context, npv: 99.0)).Message);

        a = Assert.Throws<ArgumentException>(
            () => CashFlows.Yield([], 99.0, Dc, Compounding.Compounded, Frequency.Annual, Context));
        Assert.Equal("npv", a.ParamName);
        Assert.StartsWith(
            "the given cash flows cannot result in the given market price due to their sign", a.Message, StringComparison.Ordinal);

        var continuous = new InterestRate(0.03, Dc, Compounding.Continuous, Frequency.Annual);
        a = Assert.Throws<ArgumentException>(
            () => CashFlows.Duration(redemption, continuous, DurationType.Macaulay, Context));
        Assert.Equal("yield", a.ParamName);
        Assert.StartsWith("compounded rate required", a.Message, StringComparison.Ordinal);

        NanoQLException n = Assert.Throws<NanoQLException>(() => CashFlows.ZSpread(
            redemption, 50.0, curve, Compounding.Continuous, Frequency.Annual, Context, maxIterations: 2));
        Assert.StartsWith("unable to bracket root in 2 function evaluations", n.Message, StringComparison.Ordinal);

        // an empty leg's Z-spread for a non-zero target: its NPV is 0 at every spread
        n = Assert.Throws<NanoQLException>(
            () => CashFlows.ZSpread([], 50.0, curve, Compounding.Continuous, Frequency.Annual, Context));
        Assert.StartsWith("unable to bracket root in 100 function evaluations", n.Message, StringComparison.Ordinal);

        // the yield from a zero guess, whose first step, a tenth of it, is 0, and with one iteration
        n = Assert.Throws<NanoQLException>(
            () => CashFlows.Yield(redemption, 95.0, Dc, Compounding.Compounded, Frequency.Annual, Context, guess: 0.0));
        Assert.StartsWith("unable to bracket root in 100 function evaluations", n.Message, StringComparison.Ordinal);
        n = Assert.Throws<NanoQLException>(() => CashFlows.Yield(
            redemption, 95.0, Dc, Compounding.Compounded, Frequency.Annual, Context, maxIterations: 1));
        Assert.StartsWith("unable to bracket root in 1 function evaluations", n.Message, StringComparison.Ordinal);

        var mixed = new InterestRate(0.03, Dc, Compounding.SimpleThenCompounded, Frequency.Annual);
        Assert.Equal(
            "SimpleThenCompounded is not supported for direct calculations: use the CashFlow methods",
            Assert.Throws<NanoQLException>(() => mixed.DiscountFactorFirstDerivative(1.0)).Message);
        mixed = new InterestRate(0.03, Dc, Compounding.CompoundedThenSimple, Frequency.Annual);
        Assert.Equal(
            "CompoundedThenSimple is not supported for direct calculations: use the CashFlow methods",
            Assert.Throws<NanoQLException>(() => mixed.DiscountFactorSecondDerivative(1.0)).Message);
    }
}
