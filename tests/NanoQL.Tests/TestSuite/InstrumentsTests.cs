// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/instruments.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Not ported: testObservable (observer notifications and freezing, which an immutable instrument has no use for, R1)

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class InstrumentsTests
{
    // Adapted: Date::todaysDate() (line 75) is a fixed date, as the suite reads none of the clock. QuantLib's
    // flatRate and flatVol curves move with the evaluation date, and the option carries its engine; here the
    // component's engine builds them on the context's evaluation date.
    [Fact]
    public void TestCompositeWhenShiftingDates()
    {
        // Testing reaction of composite instrument to date changes...
        var today = new Date(16, Month.September, 2015);
        DayCounter dc = new Actual360();

        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        Exercise exercise = new EuropeanExercise(today + 30);

        var option = new EuropeanOption(payoff, exercise);

        InstrumentResults Engine(EvaluationContext context)
        {
            Date d = context.EvaluationDate;
            var process = new BlackScholesMertonProcess(
                100.0,
                new FlatForward(d, 0.0, dc),
                new FlatForward(d, 0.01, dc),
                new BlackConstantVol(d, new NullCalendar(), 0.1, dc));
            return AnalyticEuropeanEngine.Calculate(option, context, process);
        }

        CompositeInstrument composite = new CompositeInstrument().Add(option, Engine);

        var later = new EvaluationContext { EvaluationDate = today + 45 };

        Assert.True(composite.IsExpired(later), "Composite didn't detect expiration");
        Assert.True(composite.Calculate(later).NPV == 0.0, "Composite didn't return a null NPV");

        var context = new EvaluationContext { EvaluationDate = today };

        Assert.False(composite.IsExpired(context), "Composite didn't detect aliveness");
        Assert.False(composite.Calculate(context).NPV == 0.0, "Composite didn't recalculate");
    }
}
