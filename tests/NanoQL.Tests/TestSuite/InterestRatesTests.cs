// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/interestrates.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.Compounding;
using static NanoQL.Time.Frequency;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class InterestRatesTests
{
    // Adapted: without the two checks of the converted rate against Haug's value rounded to the case's precision, lines
    // 138-146 and 160-169 (the second with its comment), and the rounding they use: line 72, line 83, and r3 on line
    // 73. ql/math/rounding isn't ported yet. The checks of the converted rate's day counter, compounding and frequency
    // (lines 147-158) are kept. Without lines 179-180 too, whose discountFactorFirstDerivative and
    // discountFactorSecondDerivative wait for the slot that ports the cash-flow duration and convexity.
    [Fact]
    public void TestConversions()
    {
        // Testing interest-rate conversions...

        InterestRateData[] cases =
        [
            // data from "Option Pricing Formulas", Haug, pag.181-182
            // Rate,Compounding,        Frequency,   Time, Compounding2,      Frequency2,  Rate2, precision
            new(0.0800, Compounded,        Quarterly,   1.00, Continuous,            Annual, 0.0792, 4),
            new(0.1200, Continuous,           Annual,   1.00, Compounded,            Annual, 0.1275, 4),
            new(0.0800, Compounded,        Quarterly,   1.00, Compounded,            Annual, 0.0824, 4),
            new(0.0700, Compounded,        Quarterly,   1.00, Compounded,        Semiannual, 0.0706, 4),

            // undocumented, but reasonable :)
            new(0.0100, Compounded,           Annual,   1.00,     Simple,            Annual, 0.0100, 4),
            new(0.0200,     Simple,           Annual,   1.00, Compounded,            Annual, 0.0200, 4),
            new(0.0300, Compounded,       Semiannual,   0.50,     Simple,            Annual, 0.0300, 4),
            new(0.0400,     Simple,           Annual,   0.50, Compounded,        Semiannual, 0.0400, 4),
            new(0.0500, Compounded, EveryFourthMonth,  1.0 / 3,     Simple,            Annual, 0.0500, 4),
            new(0.0600,     Simple,           Annual,  1.0 / 3, Compounded,  EveryFourthMonth, 0.0600, 4),
            new(0.0500, Compounded,        Quarterly,   0.25,     Simple,            Annual, 0.0500, 4),
            new(0.0600,     Simple,           Annual,   0.25, Compounded,         Quarterly, 0.0600, 4),
            new(0.0700, Compounded,        Bimonthly,  1.0 / 6,     Simple,            Annual, 0.0700, 4),
            new(0.0800,     Simple,           Annual,  1.0 / 6, Compounded,         Bimonthly, 0.0800, 4),
            new(0.0900, Compounded,          Monthly, 1.0 / 12,     Simple,            Annual, 0.0900, 4),
            new(0.1000,     Simple,           Annual, 1.0 / 12, Compounded,           Monthly, 0.1000, 4),
        ];

        Rate r2;

        // d2 is set by every row; the checks after the loop use the last row's.
        Date d1 = Date.TodaysDate(), d2 = d1;
        InterestRate ir, ir2, ir3, expectedIR;
        Real compoundf, error;
        DiscountFactor disc;

        foreach (InterestRateData i in cases)
        {
            ir = new InterestRate(i.R, new Actual360(), i.Comp, i.Freq);
            d2 = d1 + TimeToDays(i.T);

            // check that the compound factor is the inverse of the discount factor
            compoundf = ir.CompoundFactor(d1, d2);
            disc = ir.DiscountFactor(d1, d2);
            error = Math.Abs(disc - (1.0 / compoundf));
            if (error > 1e-15)
            {
                Assert.Fail(
                    $"\n  {ir}"
                    + $"\n  1.0/compound_factor: {1.0 / compoundf:R}"
                    + $"\n  discount_factor:     {disc:R}"
                    + $"\n  error:               {error:R}");
            }

            // check that the equivalent InterestRate with *same* daycounter,
            // compounding, and frequency is the *same* InterestRate
            ir2 = ir.EquivalentRate(ir.DayCounter, ir.Compounding, ir.Frequency, d1, d2);
            error = Math.Abs(ir.Rate - ir2.Rate);
            if (error > 1e-15)
            {
                Assert.Fail(
                    $"\n    original interest rate: {ir}"
                    + $"\n  equivalent interest rate: {ir2}"
                    + $"\n                rate error: {error:R}");
            }

            if (ir.DayCounter != ir2.DayCounter)
            {
                Assert.Fail(
                    "\n day counter error"
                    + $"\n original interest rate:   {ir}"
                    + $"\n equivalent interest rate: {ir2}");
            }

            if (ir.Compounding != ir2.Compounding)
            {
                Assert.Fail(
                    "\n compounding error"
                    + $"\n original interest rate:   {ir}"
                    + $"\n equivalent interest rate: {ir2}");
            }

            if (ir.Frequency != ir2.Frequency)
            {
                Assert.Fail(
                    "\n frequency error"
                    + $"\n    original interest rate: {ir}"
                    + $"\n  equivalent interest rate: {ir2}");
            }

            // check that the equivalent rate with *same* daycounter,
            // compounding, and frequency is the *same* rate
            r2 = ir.EquivalentRate(ir.DayCounter, ir.Compounding, ir.Frequency, d1, d2).Rate;
            error = Math.Abs(ir.Rate - r2);
            if (error > 1e-15)
            {
                Assert.Fail(
                    $"\n    original rate: {ir}"
                    + $"\n  equivalent rate: {DataFormattersFunctions.Rate(r2)}"
                    + $"\n            error: {error:R}");
            }

            // check that the equivalent InterestRate with *different*
            // compounding, and frequency is the *expected* InterestRate
            ir3 = ir.EquivalentRate(ir.DayCounter, i.Comp2, i.Freq2, d1, d2);
            expectedIR = new InterestRate(i.Expected, ir.DayCounter, i.Comp2, i.Freq2);
            if (ir3.DayCounter != expectedIR.DayCounter)
            {
                Assert.Fail(
                    "\n day counter error"
                    + $"\n    original interest rate: {ir3}"
                    + $"\n  equivalent interest rate: {expectedIR}");
            }

            if (ir3.Compounding != expectedIR.Compounding)
            {
                Assert.Fail(
                    "\n compounding error"
                    + $"\n    original interest rate: {ir3}"
                    + $"\n  equivalent interest rate: {expectedIR}");
            }

            if (ir3.Frequency != expectedIR.Frequency)
            {
                Assert.Fail(
                    "\n frequency error"
                    + $"\n    original interest rate: {ir3}"
                    + $"\n  equivalent interest rate: {expectedIR}");
            }
        }

        // check that SimpleThenCompounded and CompoundedThenSimple don't work

        ir = new InterestRate(0.03, new Actual360(), SimpleThenCompounded, Semiannual);
        ir2 = new InterestRate(0.03, new Actual360(), CompoundedThenSimple, Semiannual);

        foreach (InterestRate i in new[] { ir, ir2 })
        {
            CheckExpectedErrorMessage(() => i.DiscountFactor(1.0), "not supported");
            CheckExpectedErrorMessage(() => i.EquivalentRate(i.DayCounter, Simple, Annual, d1, d2), "not supported");
        }
    }

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    // BOOST_CHECK_EXCEPTION(..., Error, ExpectedErrorMessage(expected)): QuantLib's Error is an ArgumentException or a
    // NanoQLException in nanoql, and its message must contain the expected text.
    private static void CheckExpectedErrorMessage(Action action, string expected)
    {
        Exception? e = Record.Exception(action);
        if (e is not (ArgumentException or NanoQLException))
        {
            Assert.Fail(
                $"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
        }

        Assert.True(
            e.Message.Contains(expected, StringComparison.Ordinal),
            $"Error expected to contain: '{expected}'. Actual error is: '{e.Message}'.");
    }

    private readonly record struct InterestRateData(
        Rate R,
        Compounding Comp,
        Frequency Freq,
        double T,
        Compounding Comp2,
        Frequency Freq2,
        Rate Expected,
        Size Precision);
}
