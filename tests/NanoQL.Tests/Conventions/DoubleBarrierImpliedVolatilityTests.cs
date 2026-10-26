// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="DoubleBarrierOption.ImpliedVolatility"/>, which QuantLib-SWIG 1.44 doesn't bind: QuantLib's solve on the
/// analytic double-barrier engine at its five terms, over a copy of the process with a constant volatility, so the
/// volatility a knock-out and a knock-in were priced at comes back within the accuracy asked; and QuantLib's refusals,
/// an expired option ("option expired") and an exercise other than European ("engine not available for non-European
/// barrier option"), each a <see cref="NanoQLException"/> (<c>doublebarrieroption.cpp:55-80</c>).
/// </summary>
public class DoubleBarrierImpliedVolatilityTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.04, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    [Theory]
    [InlineData(DoubleBarrierType.KnockOut, OptionType.Call)]
    [InlineData(DoubleBarrierType.KnockIn, OptionType.Put)]
    public void TheVolatilityComesBack(DoubleBarrierType type, OptionType optionType)
    {
        var option = new DoubleBarrierOption(
            type, 80.0, 125.0, 0.0, new PlainVanillaPayoff(optionType, 100.0), new EuropeanExercise(Today + 365));
        double value = AnalyticDoubleBarrierEngine.Calculate(option, Context, Process).NPV!.Value;
        Assert.Equal(0.25, option.ImpliedVolatility(value, Process, Context, 1e-10), 1e-8);
    }

    [Fact]
    public void QuantLibsRefusalsStand()
    {
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var expired = new DoubleBarrierOption(DoubleBarrierType.KnockOut, 80.0, 125.0, 0.0, payoff, new EuropeanExercise(Today - 1));
        Assert.Equal("option expired", Assert.Throws<NanoQLException>(() => expired.ImpliedVolatility(1.0, Process, Context)).Message);
        var american = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 125.0, 0.0, payoff, new AmericanExercise(Today, Today + 365));
        Assert.Equal(
            "engine not available for non-European barrier option",
            Assert.Throws<NanoQLException>(() => american.ImpliedVolatility(1.0, Process, Context)).Message);
    }
}
