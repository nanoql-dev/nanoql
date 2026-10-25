// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic GJR-GARCH engine refuses a payoff that isn't a plain vanilla (the S-hes plan's HR12). QuantLib's engine
/// reads the payoff as a <c>StrikedTypePayoff</c> and prices the plain vanilla on its strike and type
/// (<c>analyticgjrgarchengine.cpp</c>), so a cash-or-nothing or an asset-or-nothing option is priced as the call it
/// isn't. nanoql refuses them, as QuantLib's Monte Carlo GJR-GARCH engine does ("non-plain payoff given").
/// </summary>
public class AnalyticGjrGarchPayoffRefusalTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ANonPlainPayoffIsRefused()
    {
        var model = new GJRGARCHModel(new GJRGARCHProcess(
            new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.0, A365), 50.0, 2.0e-4, 2.0e-6, 0.024, 0.93, 0.059, 0.1, 365.0));
        StrikedTypePayoff[] payoffs = [new CashOrNothingPayoff(OptionType.Call, 50.0, 10.0), new AssetOrNothingPayoff(OptionType.Call, 50.0)];
        foreach (StrikedTypePayoff payoff in payoffs)
        {
            var option = new VanillaOption(payoff, new EuropeanExercise(Today + 180));
            ArgumentException refused = Assert.Throws<ArgumentException>(() => AnalyticGJRGARCHEngine.Calculate(option, Context, model));
            Assert.Equal("option", refused.ParamName);
            Assert.StartsWith("non-plain payoff given", refused.Message, StringComparison.Ordinal);
        }
    }
}
