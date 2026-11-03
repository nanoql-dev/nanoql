// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The forward and forward-performance engines on a payoff that isn't plain vanilla. QuantLib builds the inner option's
/// plain vanilla payoff from the payoff's type alone (<c>forwardengine.hpp:84-86</c>), so a cash-or-nothing forward
/// start prices 10.456, the plain vanilla's, where its value is 5.003. nanoql refuses it, with the text of QuantLib's
/// Monte Carlo forward engines: the refusal's type and text are the facts.
/// </summary>
public class ForwardNonPlainPayoffTests
{
    private static readonly ForwardVanillaOption CashOrNothing = new(
        1.0, Today + 182, new CashOrNothingPayoff(OptionType.Call, 0.0, 10.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void TheForwardEngineRefusesACashOrNothingPayoff()
    {
        GeneralizedBlackScholesProcess p = Flat(100.0);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => ForwardVanillaEngine.Calculate(CashOrNothing, Context, p, AnalyticEuropeanEngine.Calculate));
        Assert.Equal("non-plain payoff given", e.Message);
    }

    [Fact]
    public void TheForwardPerformanceEngineRefusesACashOrNothingPayoff()
    {
        GeneralizedBlackScholesProcess p = Flat(100.0);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => ForwardPerformanceVanillaEngine.Calculate(CashOrNothing, Context, p, AnalyticEuropeanEngine.Calculate));
        Assert.Equal("non-plain payoff given", e.Message);
    }
}
