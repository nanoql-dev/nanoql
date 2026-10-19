// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The FRA, futures and FX swap helpers' refusals, whose type, message and <see cref="ArgumentException.ParamName"/> a
/// vector's <c>{"throws": true}</c> can't assert (playbook §9.4): the two undefined paths these helpers refuse (a
/// custom futures helper's null maturity, read by its implied quote, and a dated FX swap helper's members), the FRA
/// months constructor's order, and the futures type's text.
/// </summary>
public class FraFuturesFxSwapErrorTests
{
    private static readonly Date Today = new(15, Month.January, 2024);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void AFuturesHelpersNullMaturityIsRefusedWhereItIsRead()
    {
        var helper = new FuturesRateHelper(
            96.5, new Date(20, Month.February, 2024), null, new Actual360(), 0.0, FuturesType.Custom);
        var curve = new FlatForward(Today, 0.03, new Actual360());

        // the helper itself is unbound: QuantLib's own refusal comes first
        Assert.Equal("term structure not set", Assert.Throws<NanoQLException>(() => helper.ImpliedQuote(Context)).Message);

        BootstrapHelper<YieldTermStructure> bound = helper.SetTermStructure(curve);
        NanoQLException e = Assert.Throws<NanoQLException>(() => bound.ImpliedQuote(Context));
        Assert.Equal("date (null date) before reference date (January 15th, 2024)", e.Message);
        Assert.Throws<NanoQLException>(() => bound.QuoteError(Context));
    }

    [Fact]
    public void ADatedFxSwapHelpersMissingMembersAreRefused()
    {
        var helper = new FxSwapRateHelper(0.0131, 1.0950, new Date(17, Month.January, 2024), new Date(17, Month.April, 2024), true, null);

        Assert.Equal(
            "no fixing days for an FX swap helper built from dates",
            Assert.Throws<NanoQLException>(() => helper.FixingDays()).Message);
        Assert.Equal(
            "no business-day convention for an FX swap helper built from dates",
            Assert.Throws<NanoQLException>(() => helper.BusinessDayConvention()).Message);
        Assert.Equal(
            "no end-of-month flag for an FX swap helper built from dates",
            Assert.Throws<NanoQLException>(() => helper.EndOfMonth()).Message);

        // QuantLib's refusal of an empty collateral curve where it binds the helper, and its own check after it
        Assert.Equal(
            "empty Handle cannot be dereferenced",
            Assert.Throws<NanoQLException>(() => helper.SetTermStructure(new FlatForward(Today, 0.03, new Actual360())))
                .Message);
    }

    [Fact]
    public void TheFraMonthsConstructorChecksTheMonthsAfterItsDelegate()
    {
        FraRateHelper Fra(int monthsToStart, int monthsToEnd, Calendar calendar) => new(
            0.03, monthsToStart, monthsToEnd, 2, calendar, BusinessDayConvention.ModifiedFollowing, false, new Actual360(), Context);

        // QuantLib builds the delegated helper on the months' difference first, so its empty calendar refuses first
        Assert.Equal("calendar", Assert.Throws<ArgumentNullException>(() => Fra(6, 3, null!)).ParamName);

        // then the months' order, QuantLib's text, its typo kept
        ArgumentException e = Assert.Throws<ArgumentException>(() => Fra(6, 3, new TARGET()));
        Assert.Equal("monthsToEnd", e.ParamName);
        Assert.StartsWith("monthsToEnd (3) must be grater than monthsToStart (6)", e.Message, StringComparison.Ordinal);
        Assert.Equal("monthsToEnd", Assert.Throws<ArgumentException>(() => Fra(3, 3, new TARGET())).ParamName);

        // a month count is QuantLib's unsigned Natural
        Assert.Equal("monthsToStart", Assert.Throws<ArgumentOutOfRangeException>(() => Fra(-1, 3, new TARGET())).ParamName);
        Assert.Equal("monthsToEnd", Assert.Throws<ArgumentOutOfRangeException>(() => Fra(1, -3, new TARGET())).ParamName);
    }

    [Fact]
    public void AFuturesTypeOutsideItsMembersIsRefusedWithQuantLibsText()
    {
        var start = new Date(20, Month.March, 2024);
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new FuturesRateHelper(96.0, start, new Euribor3M(), 0.0, (FuturesType)3));
        Assert.Equal("type", e.ParamName);
        Assert.Equal(
            "unknown futures type (Unknown(3)) (Parameter 'type')\nActual value was 3.",
            e.Message.ReplaceLineEndings("\n"));

        // and a start date that isn't one of the type's dates, QuantLib's text
        ArgumentException notImm = Assert.Throws<ArgumentException>(
            () => new FuturesRateHelper(96.0, new Date(21, Month.March, 2024), new Euribor3M()));
        Assert.Equal("iborStartDate", notImm.ParamName);
        Assert.StartsWith("March 21st, 2024 is not a valid IMM date", notImm.Message, StringComparison.Ordinal);
        Assert.StartsWith(
            "end date (March 20th, 2024) must be greater than start date (March 20th, 2024)",
            Assert.Throws<ArgumentException>(() => new FuturesRateHelper(96.0, start, start, new Actual360())).Message,
            StringComparison.Ordinal);
    }
}
