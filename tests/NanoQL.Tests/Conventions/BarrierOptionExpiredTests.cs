// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// An expired barrier option (its exercise date before the evaluation date) prices, under every barrier engine, as
/// <see cref="OneAssetOption"/>'s expired results, D14's first step, before the engine checks its arguments; its
/// implied volatility is refused with QuantLib's "option expired" (<c>barrieroption.cpp:62</c>; the S-rx plan's
/// SRXD11 and SRXD12).
/// </summary>
public class BarrierOptionExpiredTests
{
    [Fact]
    public void AnExpiredOptionGivesTheExpiredResults()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        YieldTermStructure rTS = new FlatForward(today, 0.04, dc), qTS = new FlatForward(today, 0.01, dc);
        var process = new BlackScholesMertonProcess(
            100.0, qTS, rTS, new BlackConstantVol(today, new NullCalendar(), 0.2, dc));
        var option = new BarrierOption(
            BarrierType.UpOut, 120.0, 2.0, new PlainVanillaPayoff(
                OptionType.Call, 100.0), new EuropeanExercise(today - 1));
        OneAssetOptionResults expired = option.SetupExpired();

        Assert.Equal(0.0, expired.NPV);
        Assert.Equal(expired, AnalyticBarrierEngine.Calculate(option, context, process));
        Assert.Equal(expired, FdBlackScholesBarrierEngine.Calculate(option, context, process));
        Assert.Equal(expired, FdBlackScholesRebateEngine.Calculate(option, context, process));
        Assert.Equal(
            expired,
            VannaVolgaBarrierEngine.Calculate(
                option,
                context,
                new DeltaVolQuote(0.1, DeltaVolQuoteDeltaType.Fwd, 1.0, DeltaVolQuoteAtmType.AtmDeltaNeutral),
                new DeltaVolQuote(-0.25, 0.11, 1.0, DeltaVolQuoteDeltaType.Fwd),
                new DeltaVolQuote(0.25, 0.105, 1.0, DeltaVolQuoteDeltaType.Fwd),
                100.0,
                rTS,
                qTS));

        NanoQLException e = Assert.Throws<NanoQLException>(() => option.ImpliedVolatility(1.0, process, context));
        Assert.Equal("option expired", e.Message);
    }
}
