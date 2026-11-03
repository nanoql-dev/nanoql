// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Quotes;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The derived quotes' construction-time values and refusals: each quote computes its value once, from its inputs and
/// the context it is built under, and refuses at construction what QuantLib's would refuse when read, with QuantLib's
/// texts.
/// </summary>
public class QuoteConventionTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.January, 2025) };

    [Fact]
    public void AQuoteIsItsValueAtConstruction()
    {
        // built under one evaluation date, the adjustment is that date's, whatever date a reader holds later
        var index = new Euribor3M();
        var futuresDate = new Date(19, Month.March, 2025);
        var early = new FuturesConvAdjustmentQuote(index, futuresDate, 97.5, 0.01, 0.03, Context);
        var later = new FuturesConvAdjustmentQuote(
            index, futuresDate, 97.5, 0.01, 0.03, Context with { EvaluationDate = new Date(15, Month.February, 2025) });
        Assert.NotEqual(early.Value, later.Value);
        Assert.Equal(early.Value, new FuturesConvAdjustmentQuote(index, futuresDate, 97.5, 0.01, 0.03, Context).Value);

        // the IMM code's date is the first on or after the evaluation date
        var byCode = new FuturesConvAdjustmentQuote(index, "H5", 97.5, 0.01, 0.03, Context);
        Assert.Equal(futuresDate, byCode.ImmDate);
        Assert.Equal(early.Value, byCode.Value);
    }

    [Fact]
    public void ALastFixingQuoteOnNoFixingIsRefused()
    {
        var index = new Euribor6M();
        NanoQLException e = Assert.Throws<NanoQLException>(() => new LastFixingQuote(index, Context));
        Assert.Equal($"{index.Name} has no fixing", e.Message);
    }

    [Fact]
    public void TheFunctionalQuotesRefuseNoFunction()
    {
        Assert.Equal("f", Assert.Throws<ArgumentNullException>(() => new DerivedQuote(1.0, null!)).ParamName);
        Assert.Equal("f", Assert.Throws<ArgumentNullException>(() => new CompositeQuote(1.0, 2.0, null!)).ParamName);
        Assert.Equal("f", Assert.Throws<ArgumentNullException>(() => new MultiCompositeQuote([1.0], null!)).ParamName);
        Assert.Equal(
            "elements", Assert.Throws<ArgumentNullException>(() => new MultiCompositeQuote(null!, _ => 0.0)).ParamName);
    }

    [Fact]
    public void AnInputValueOutOfRangeIsRefused()
    {
        // QuantLib's elements_.at(i)
        var q = new MultiCompositeQuote([1.0, 2.0], x => x[0] + x[1]);
        Assert.Equal(2.0, q.InputValue(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => q.InputValue(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => q.InputValue(-1));
    }

    [Fact]
    public void AFailedImpliedSolveIsZeroAndABadArgumentRefused()
    {
        // QuantLib's catch (Error&): a price the formula can't reach, and a negative price, give 0
        Assert.Equal(0.0, new ImpliedStdDevQuote(OptionType.Call, 0.05, 0.11, 0.04, 0.15).Value);
        Assert.Equal(0.0, new ImpliedStdDevQuote(OptionType.Put, 0.05, -0.01, 0.04, 0.15).Value);

        // a Natural that can't be negative, and an option type out of its type, are refused before the solve
        Assert.Equal(
            "maxIter",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ImpliedStdDevQuote(OptionType.Call, 0.05, 0.02, 0.04, 0.15, 1e-6, -1)).ParamName);
        Assert.Equal(
            "optionType",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ImpliedStdDevQuote((OptionType)7, 0.05, 0.02, 0.04, 0.15)).ParamName);
    }

    [Fact]
    public void AFailedEurodollarSolveIsRefused()
    {
        // QuantLib's quote lets the solver's refusal through (eurodollarfuturesquote.cpp:58-75): a rate strike of 5%
        // above the 4% rate forward, solved as a call on the rate at the put's price, priced above the forward
        Assert.ThrowsAny<NanoQLException>(() => new EurodollarFuturesImpliedStdDevQuote(96.0, 0.3, 5.0, 95.0).Value);
        Assert.Equal(
            "maxIter",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EurodollarFuturesImpliedStdDevQuote(96.0, 0.2, 0.3, 95.0, 0.15, 1e-6, -1)).ParamName);
    }

    [Fact]
    public void ADefaultDateIsRefused()
    {
        var index = new Euribor3M();
        Assert.Equal(
            "fixingDate",
            Assert.Throws<ArgumentException>(() => new ForwardValueQuote(index, default, Context)).ParamName);
        Assert.Equal(
            "futuresDate",
            Assert.Throws<ArgumentException>(
                () => new FuturesConvAdjustmentQuote(index, default(Date), 97.5, 0.01, 0.03, Context)).ParamName);
        Assert.ThrowsAny<ArgumentException>(() => new FuturesConvAdjustmentQuote(index, "A9", 97.5, 0.01, 0.03, Context));
    }
}
