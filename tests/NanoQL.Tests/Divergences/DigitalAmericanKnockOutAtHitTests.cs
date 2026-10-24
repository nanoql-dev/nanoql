// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The knock-out digital engine on a payoff at the hit (found by the S-van run, under R8). QuantLib's
/// <c>AnalyticDigitalAmericanKOEngine</c> passes its <c>knock_in()</c> only to the at-expiry formula; on a payoff at
/// the hit it prices the knock-in (<c>analyticdigitalamericanengine.cpp:60-77</c>), whose formula has no knock-out: on
/// Haug's case 1 (a put at 100, S 105, r 10%, vol 20%, half a year, 15 at the hit) the knock-out engine gives the
/// knock-in's 9.726393830820774 [executed on the wheel: <c>oracle_c1d.py</c>]. A knock-out pays at expiry if the strike
/// was never reached, so nanoql refuses the payoff at the hit, a forced departure that refuses rather than return the
/// other option's value.
/// </summary>
public class DigitalAmericanKnockOutAtHitTests
{
    [Fact]
    public void AKnockOutAtTheHitIsRefused()
    {
        var today = new Date(15, October, 2026);
        DayCounter dc = new Actual360();
        var process = new BlackScholesMertonProcess(
            105.0, new FlatForward(today, 0.0, dc), new FlatForward(today, 0.10, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.20, dc));
        var option = new VanillaOption(
            new CashOrNothingPayoff(OptionType.Put, 100.0, 15.0), new AmericanExercise(today, today + 180, false));
        var context = new EvaluationContext { EvaluationDate = today };

        NanoQLException e = Assert.Throws<NanoQLException>(
            () => AnalyticDigitalAmericanKOEngine.Calculate(option, context, process));
        Assert.Equal("a knock-out digital pays at expiry: no knock-out formula at the hit", e.Message);
        Assert.Equal(9.726393830820774, AnalyticDigitalAmericanEngine.Calculate(option, context, process).NPV);
    }
}
