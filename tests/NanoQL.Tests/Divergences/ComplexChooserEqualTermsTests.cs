// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's fix after v1.44 to the complex chooser engine (#2891, <c>bc3a54105</c>): its maturities measured from the
/// evaluation date, where 1.44 measures them from the choosing date and its critical value from twice it
/// (<c>analyticcomplexchooserengine.cpp:45-46</c>, <c>:88</c>, <c>:96</c>), −12% on a flat market; 1.44's own case
/// passes by placing its maturities after the choosing date (<c>ChooserOptionTests</c>' moved line). nanoql takes the fix
/// (the S-exo plan's ER6 (ii)). The check is master's case, <c>testComplexChooserWithEqualTermsMatchesSimpleChooser</c>:
/// a complex chooser with equal strikes and maturities is the simple chooser, within 1e-5, here until the pin moves.
/// </summary>
public class ComplexChooserEqualTermsTests
{
    [Fact]
    public void TestComplexChooserWithEqualTermsMatchesSimpleChooser()
    {
        // Testing complex chooser with equal strikes and maturities against simple chooser...
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        Real strike = 100.0;
        Real tolerance = 1e-5;

        foreach (Real s in new[] { 80.0, 100.0, 120.0 })
        {
            foreach (Real q in new[] { 0.0, 0.03 })
            {
                foreach (Real r in new[] { 0.01, 0.06 })
                {
                    foreach (Real v in new[] { 0.15, 0.40 })
                    {
                        foreach (Integer choosingDays in new[] { 30, 90 })
                        {
                            foreach (Integer exerciseDays in new[] { 91, 180, 365 })
                            {
                                var stochProcess = new BlackScholesMertonProcess(
                                    s, new FlatForward(today, q, dc), new FlatForward(today, r, dc),
                                    new BlackConstantVol(today, new NullCalendar(), v, dc));

                                Date choosingDate = today + choosingDays;
                                var exercise = new EuropeanExercise(today + exerciseDays);

                                var complexOption = new ComplexChooserOption(choosingDate, strike, strike, exercise, exercise);
                                var simpleOption = new SimpleChooserOption(choosingDate, strike, exercise);

                                Real calculated = AnalyticComplexChooserEngine.Calculate(complexOption, context, stochProcess).NPV!.Value;
                                Real expected = AnalyticSimpleChooserEngine.Calculate(simpleOption, context, stochProcess).NPV!.Value;
                                Assert.True(
                                    Math.Abs(calculated - expected) <= tolerance,
                                    $"spot {s}, q {q}, r {r}, vol {v}, choosing {choosingDays}, exercise {exerciseDays}:"
                                    + $" expected {expected}, calculated {calculated}");
                            }
                        }
                    }
                }
            }
        }
    }
}
