// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/cdsoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's CDS-option tests, <c>test-suite/cdsoption.cpp</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class CdsOptionTests
{
    // Adapted: the swap's engine is passed to the option's engine as a delegate (the S-xcr plan's XD4).
    [Fact]
    public void TestCached()
    {
        // Testing CDS-option value against cached values...
        var cachedToday = new Date(10, December, 2007);
        var context = new EvaluationContext { EvaluationDate = cachedToday };

        Calendar calendar = new TARGET();

        var riskFree = new FlatForward(cachedToday, 0.02, new Actual360());

        Date expiry = calendar.Advance(cachedToday, 9, TimeUnit.Months);
        Date startDate = calendar.Advance(expiry, 1, TimeUnit.Months);
        Date maturity = calendar.Advance(startDate, 7, TimeUnit.Years);

        DayCounter dayCounter = new Actual360();
        BusinessDayConvention convention = BusinessDayConvention.ModifiedFollowing;
        Real notional = 1000000.0;

        Real hazardRate = 0.001;

        var schedule = new Schedule(
            startDate, maturity, new Period(Frequency.Quarterly), calendar, convention, convention,
            DateGenerationRule.Forward, false, context);

        Real recoveryRate = 0.4;
        FlatHazardRate defaultProbability = FlatHazardRate.Create(0, calendar, hazardRate, dayCounter, context);

        CreditDefaultSwapResults SwapEngine(CreditDefaultSwap cds, EvaluationContext ctx) =>
            MidPointCdsEngine.Calculate(cds, ctx, defaultProbability, recoveryRate, riskFree);

        var swap = new CreditDefaultSwap(ProtectionSide.Seller, notional, 0.001, schedule, convention, dayCounter);
        Rate strike = SwapEngine(swap, context).FairSpread!.Value;

        Real cdsVol = 0.20;

        var underlying = new CreditDefaultSwap(ProtectionSide.Seller, notional, strike, schedule, convention, dayCounter);

        Exercise exercise = new EuropeanExercise(expiry);
        var option1 = new CdsOption(underlying, exercise);

        Real cachedValue = 270.976348;
        Real npv = BlackCdsOptionEngine.Calculate(option1, context, defaultProbability, recoveryRate, riskFree, cdsVol, SwapEngine).NPV!.Value;
        Assert.False(
            Math.Abs(npv - cachedValue) > 1.0e-5,
            $"failed to reproduce cached value: calculated {npv:F6}, expected {cachedValue:F6}");

        underlying = new CreditDefaultSwap(ProtectionSide.Buyer, notional, strike, schedule, convention, dayCounter);

        var option2 = new CdsOption(underlying, exercise);

        cachedValue = 270.976348;
        npv = BlackCdsOptionEngine.Calculate(option2, context, defaultProbability, recoveryRate, riskFree, cdsVol, SwapEngine).NPV!.Value;
        Assert.False(
            Math.Abs(npv - cachedValue) > 1.0e-5,
            $"failed to reproduce cached value: calculated {npv:F6}, expected {cachedValue:F6}");
    }
}
