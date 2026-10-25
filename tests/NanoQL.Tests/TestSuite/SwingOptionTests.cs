// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swingoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testExtendedOrnsteinUhlenbeckProcess, testFdmExponentialJump1dMesher, testExtOUJumpVanillaEngine, testExtOUJumpSwingOption, testKlugeChFVanillaPricing, which wait for S-xeq's extended Ornstein-Uhlenbeck processes, jump mesher and engines

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SwingOptionTests
{
    [Fact]
    public void TestFdBSSwingOption()
    {
        // Testing Black-Scholes vanilla swing option pricing...
        Date settlementDate = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        Date maturityDate = settlementDate + new Period(12, TimeUnit.Months);

        const Real strike = 30;
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Put, strike);
        StrikedTypePayoff forward = new VanillaForwardPayoff(OptionType.Put, strike);

        List<Date> exerciseDates = [settlementDate + new Period(1, TimeUnit.Months)];
        while (exerciseDates[^1] < maturityDate)
        {
            exerciseDates.Add(exerciseDates[^1] + new Period(1, TimeUnit.Months));
        }

        var swingExercise = new SwingExercise(exerciseDates);

        YieldTermStructure riskFreeTS = FlatForward.Create(0, new NullCalendar(), 0.14, dayCounter, context);
        YieldTermStructure dividendTS = FlatForward.Create(0, new NullCalendar(), 0.02, dayCounter, context);
        BlackVolTermStructure volTS = new BlackConstantVol(settlementDate, new NullCalendar(), 0.4, dayCounter);

        const Real s0 = 30.0;

        var process = new BlackScholesMertonProcess(s0, dividendTS, riskFreeTS, volTS);

        var bermudanOption = new VanillaOption(payoff, swingExercise);
        Real bermudanOptionPrices = FdBlackScholesVanillaEngine.Calculate(
            bermudanOption, context, process, tGrid: 50, xGrid: 200).NPV!.Value;

        for (Size i = 0; i < exerciseDates.Count; ++i)
        {
            Size exerciseRights = i + 1;

            var swingOption = new VanillaSwingOption(forward, swingExercise, 0, exerciseRights);
            Real swingOptionPrice = FdSimpleBSSwingEngine.Calculate(swingOption, context, process, 50, 200).NPV!.Value;

            Real upperBound = exerciseRights * bermudanOptionPrices;

            Assert.False(
                swingOptionPrice - upperBound > 0.01,
                "Failed to reproduce upper bounds"
                + $"\n    upper Bound: {upperBound}"
                + $"\n    Price:       {swingOptionPrice}"
                + $"\n    diff:        {swingOptionPrice - upperBound}");

            Real lowerBound = 0.0;
            for (Size j = exerciseDates.Count - i - 1; j < exerciseDates.Count; ++j)
            {
                var europeanOption = new VanillaOption(payoff, new EuropeanExercise(exerciseDates[j]));
                lowerBound += AnalyticEuropeanEngine.Calculate(europeanOption, context, process).NPV!.Value;
            }

            Assert.False(
                lowerBound - swingOptionPrice > 4e-2,
                "Failed to reproduce lower bounds"
                + $"\n    lower Bound: {lowerBound}"
                + $"\n    Price:       {swingOptionPrice}"
                + $"\n    diff:        {lowerBound - swingOptionPrice}");
        }
    }
}
