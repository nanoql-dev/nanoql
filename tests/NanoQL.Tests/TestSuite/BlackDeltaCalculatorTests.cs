// SPDX-FileCopyrightText: Copyright (C) 2010 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/blackdeltacalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BlackDeltaCalculatorTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    private static readonly DeltaData[] DeltaValues =
    [
        // Values taken from parallel implementation in R
        new(OptionType.Call, DeltaVolQuoteDeltaType.Spot, 1.421, 0.997306, 0.992266, 0.1180654, 1.608080, 0.15),
        new(OptionType.Call, DeltaVolQuoteDeltaType.PaSpot, 1.421, 0.997306, 0.992266, 0.1180654, 1.600545, 0.15),
        new(OptionType.Call, DeltaVolQuoteDeltaType.Fwd, 1.421, 0.997306, 0.992266, 0.1180654, 1.609029, 0.15),
        new(OptionType.Call, DeltaVolQuoteDeltaType.PaFwd, 1.421, 0.997306, 0.992266, 0.1180654, 1.601550, 0.15),
        new(OptionType.Call, DeltaVolQuoteDeltaType.Spot, 122.121, 0.9695434, 0.9872347, 0.0887676, 119.8031, 0.67),
        new(OptionType.Call, DeltaVolQuoteDeltaType.PaSpot, 122.121, 0.9695434, 0.9872347, 0.0887676, 117.7096, 0.67),
        new(OptionType.Call, DeltaVolQuoteDeltaType.Fwd, 122.121, 0.9695434, 0.9872347, 0.0887676, 120.0592, 0.67),
        new(OptionType.Call, DeltaVolQuoteDeltaType.PaFwd, 122.121, 0.9695434, 0.9872347, 0.0887676, 118.0532, 0.67),
        new(OptionType.Put, DeltaVolQuoteDeltaType.Spot, 3.4582, 0.99979, 0.9250616, 0.3199034, 4.964924, -0.821),
        new(OptionType.Put, DeltaVolQuoteDeltaType.PaSpot, 3.4582, 0.99979, 0.9250616, 0.3199034, 3.778327, -0.821),
        new(OptionType.Put, DeltaVolQuoteDeltaType.Fwd, 3.4582, 0.99979, 0.9250616, 0.3199034, 4.51896, -0.821),
        new(OptionType.Put, DeltaVolQuoteDeltaType.PaFwd, 3.4582, 0.99979, 0.9250616, 0.3199034, 3.65728, -0.821),

        // JPYUSD Data taken from Castagnas "FX Options and Smile Risk" (Wiley 2009)
        new(OptionType.Put, DeltaVolQuoteDeltaType.Spot, 103.00, 0.99482, 0.98508, 0.07247845, 97.47, -0.25),
        new(OptionType.Put, DeltaVolQuoteDeltaType.PaSpot, 103.00, 0.99482, 0.98508, 0.07247845, 97.22, -0.25),
    ];

    [Fact]
    public void TestDeltaValues()
    {
        // Testing delta calculator values...

        for (int i = 0; i < DeltaValues.Length; i++)
        {
            DeltaData v = DeltaValues[i];
            var myCalc = new BlackDeltaCalculator(v.Ot, v.Dt, v.Spot, v.DDf, v.FDf, v.StdDev);

            double tolerance = 1.0e-3;
            double expected = v.Value;
            double calculated = myCalc.DeltaFromStrike(v.Strike);
            double error = Math.Abs(calculated - expected);
            Assert.True(
                error <= tolerance,
                $"Delta-from-strike calculation failed for delta. Iteration: {i}, calculated {calculated}, "
                + $"expected {expected}, error {error}");

            // tolerance not that small, but sufficient for strikes in
            // particular since they might be results of a numerical
            // procedure
            tolerance = 1.0e-2;
            expected = v.Strike;
            calculated = myCalc.StrikeFromDelta(v.Value);
            error = Math.Abs(calculated - expected);
            Assert.True(
                error <= tolerance,
                $"Strike-from-delta calculation failed for delta. Iteration: {i}, calculated {calculated}, "
                + $"expected {expected}, error {error}");
        }
    }

    [Fact]
    public void TestDeltaPriceConsistency()
    {
        // Testing premium-adjusted delta price consistency...

        // This function tests for price consistencies with the standard
        // Black Scholes calculator, since premium adjusted deltas can be calculated
        // from spot deltas by adding/subtracting the premium.

        // actually, value and tol won't be needed for testing
        EuropeanOptionData[] values =
        [
            // type, strike, spot, q, r, t, vol, value, tol (QuantLib's columns read rd, rf for q, r)
            new(OptionType.Call, 0.9123, 1.2212, 0.0231, 0.0000, 0.25, 0.301, 0.0, 0.0),
            new(OptionType.Call, 0.9234, 1.2212, 0.0231, 0.0000, 0.35, 0.111, 0.0, 0.0),
            new(OptionType.Call, 0.9783, 1.2212, 0.0231, 0.0000, 0.45, 0.071, 0.0, 0.0),
            new(OptionType.Call, 1.0000, 1.2212, 0.0231, 0.0000, 0.55, 0.082, 0.0, 0.0),
            new(OptionType.Call, 1.1230, 1.2212, 0.0231, 0.0000, 0.65, 0.012, 0.0, 0.0),
            new(OptionType.Call, 1.2212, 1.2212, 0.0231, 0.0000, 0.75, 0.129, 0.0, 0.0),
            new(OptionType.Call, 1.3212, 1.2212, 0.0231, 0.0000, 0.85, 0.034, 0.0, 0.0),
            new(OptionType.Call, 1.3923, 1.2212, 0.0131, 0.2344, 0.95, 0.001, 0.0, 0.0),
            new(OptionType.Call, 1.3455, 1.2212, 0.0000, 0.0000, 1.00, 0.127, 0.0, 0.0),
            new(OptionType.Put, 0.9123, 1.2212, 0.0231, 0.0000, 0.25, 0.301, 0.0, 0.0),
            new(OptionType.Put, 0.9234, 1.2212, 0.0231, 0.0000, 0.35, 0.111, 0.0, 0.0),
            new(OptionType.Put, 0.9783, 1.2212, 0.0231, 0.0000, 0.45, 0.071, 0.0, 0.0),
            new(OptionType.Put, 1.0000, 1.2212, 0.0231, 0.0000, 0.55, 0.082, 0.0, 0.0),
            new(OptionType.Put, 1.1230, 1.2212, 0.0231, 0.0000, 0.65, 0.012, 0.0, 0.0),
            new(OptionType.Put, 1.2212, 1.2212, 0.0231, 0.0000, 0.75, 0.129, 0.0, 0.0),
            new(OptionType.Put, 1.3212, 1.2212, 0.0231, 0.0000, 0.85, 0.034, 0.0, 0.0),
            new(OptionType.Put, 1.3923, 1.2212, 0.0131, 0.2344, 0.95, 0.001, 0.0, 0.0),
            new(OptionType.Put, 1.3455, 1.2212, 0.0000, 0.0000, 1.00, 0.127, 0.0, 0.0),

            // extreme case: zero vol
            new(OptionType.Put, 1.3455, 1.2212, 0.0000, 0.0000, 0.50, 0.000, 0.0, 0.0),

            // extreme case: zero strike
            new(OptionType.Put, 0.0000, 1.2212, 0.0000, 0.0000, 1.50, 0.133, 0.0, 0.0),

            // extreme case: zero strike+zero vol
            new(OptionType.Put, 0.0000, 1.2212, 0.0000, 0.0000, 1.00, 0.133, 0.0, 0.0),
        ];

        DayCounter dc = new Actual360();
        Calendar calendar = new TARGET();
        Date today = Date.TodaysDate();

        double tolerance = 1.0e-10;

        foreach (EuropeanOptionData value in values)
        {
            StrikedTypePayoff payoff = new PlainVanillaPayoff(value.Type, value.Strike);
            Date exDate = today + TimeToDays(value.T);
            Exercise exercise = new EuropeanExercise(exDate);

            // QuantLib's test builds both curves on the dividend quote (its rHandle wraps qQuote)
            YieldTermStructure qTS = new FlatForward(today, value.Q, dc);
            YieldTermStructure rTS = new FlatForward(today, value.Q, dc);
            BlackVolTermStructure volTS = new BlackConstantVol(today, calendar, value.V, dc);

            double discDom = rTS.Discount(exDate);
            double discFor = qTS.Discount(exDate);
            double implVol = Math.Sqrt(volTS.BlackVariance(exDate, 0.0));

            var myCalc = new BlackDeltaCalculator(
                value.Type, DeltaVolQuoteDeltaType.PaSpot, value.S, discDom, discFor, implVol);

            var stochProcess = new BlackScholesMertonProcess(value.S, qTS, rTS, volTS);
            var option = new EuropeanOption(payoff, exercise);
            OneAssetOptionResults results = AnalyticEuropeanEngine.Calculate(option, RunnerContext, stochProcess);

            double calculatedVal = myCalc.DeltaFromStrike(value.Strike);

            double delta = 0.0;
            if (implVol > 0.0)
            {
                delta = results.Delta!.Value;
            }
            else
            {
                double fwd = value.S * discFor / discDom;
                if (payoff.OptionType == OptionType.Call && fwd > payoff.Strike)
                {
                    delta = 1.0;
                }
                else if (payoff.OptionType == OptionType.Put && fwd < payoff.Strike)
                {
                    delta = -1.0;
                }
            }

            double expectedVal = delta - (results.NPV!.Value / value.S);
            double error = Math.Abs(expectedVal - calculatedVal);
            Assert.True(
                error <= tolerance,
                $"Premium-adjusted spot delta test failed. Calculated Delta: {calculatedVal}, "
                + $"Expected Value: {expectedVal}, Error: {error}");

            myCalc = myCalc.SetDeltaType(DeltaVolQuoteDeltaType.PaFwd);

            calculatedVal = myCalc.DeltaFromStrike(value.Strike);
            expectedVal = expectedVal / discFor; // Premium adjusted Fwd Delta is PA spot without discount
            error = Math.Abs(expectedVal - calculatedVal);
            Assert.True(
                error <= tolerance,
                $"Premium-adjusted forward delta test failed. Calculated Delta: {calculatedVal}, "
                + $"Expected Value: {expectedVal}, Error: {error}");

            // Test consistency with BlackScholes Calculator for Spot Delta
            myCalc = myCalc.SetDeltaType(DeltaVolQuoteDeltaType.Spot);

            calculatedVal = myCalc.DeltaFromStrike(value.Strike);
            expectedVal = delta;
            error = Math.Abs(calculatedVal - expectedVal);
            Assert.True(
                error <= tolerance,
                $"spot delta in BlackDeltaCalculator differs from delta in BlackScholesCalculator. "
                + $"Calculated Value: {calculatedVal}, Expected Value: {expectedVal}, Error: {error}");
        }
    }

    [Fact]
    public void TestPutCallParity()
    {
        // Testing put-call parity for deltas...

        // Test for put call parity between put and call deltas.

        /* The data below are from
           "Option pricing formulas", E.G. Haug, McGraw-Hill 1998
           pag 11-16
        */
        EuropeanOptionData[] values =
        [
            // pag 2-8
            // type, strike, spot, q, r, t, vol, value, tol
            new(OptionType.Call, 65.00, 60.00, 0.00, 0.08, 0.25, 0.30, 2.1334, 1.0e-4),
            new(OptionType.Put, 95.00, 100.00, 0.05, 0.10, 0.50, 0.20, 2.4648, 1.0e-4),
            new(OptionType.Put, 19.00, 19.00, 0.10, 0.10, 0.75, 0.28, 1.7011, 1.0e-4),
            new(OptionType.Call, 19.00, 19.00, 0.10, 0.10, 0.75, 0.28, 1.7011, 1.0e-4),
            new(OptionType.Call, 1.60, 1.56, 0.08, 0.06, 0.50, 0.12, 0.0291, 1.0e-4),
            new(OptionType.Put, 70.00, 75.00, 0.05, 0.10, 0.50, 0.35, 4.0870, 1.0e-4),

            // pag 24
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.10, 0.15, 0.0205, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.10, 0.15, 1.8734, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.10, 0.15, 9.9413, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.10, 0.25, 0.3150, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.10, 0.25, 3.1217, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.10, 0.25, 10.3556, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.10, 0.35, 0.9474, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.10, 0.35, 4.3693, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.10, 0.35, 11.1381, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.50, 0.15, 0.8069, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.50, 0.15, 4.0232, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.50, 0.15, 10.5769, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.50, 0.25, 2.7026, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.50, 0.25, 6.6997, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.50, 0.25, 12.7857, 1.0e-4),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.50, 0.35, 4.9329, 1.0e-4),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.50, 0.35, 9.3679, 1.0e-4),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.50, 0.35, 15.3086, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.10, 0.15, 9.9210, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.10, 0.15, 1.8734, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.10, 0.15, 0.0408, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.10, 0.25, 10.2155, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.10, 0.25, 3.1217, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.10, 0.25, 0.4551, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.10, 0.35, 10.8479, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.10, 0.35, 4.3693, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.10, 0.35, 1.2376, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.50, 0.15, 10.3192, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.50, 0.15, 4.0232, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.50, 0.15, 1.0646, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.50, 0.25, 12.2149, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.50, 0.25, 6.6997, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.50, 0.25, 3.2734, 1.0e-4),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.50, 0.35, 14.4452, 1.0e-4),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.50, 0.35, 9.3679, 1.0e-4),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.50, 0.35, 5.7963, 1.0e-4),

            // pag 27
            new(OptionType.Call, 40.00, 42.00, 0.08, 0.04, 0.75, 0.35, 5.0975, 1.0e-4),
        ];

        DayCounter dc = new Actual360();
        Calendar calendar = new TARGET();
        Date today = Date.TodaysDate();

        double tolerance = 1.0e-10;

        foreach (EuropeanOptionData value in values)
        {
            Date exDate = today + TimeToDays(value.T);

            // QuantLib's test builds both curves on the dividend quote (its rHandle wraps qQuote)
            YieldTermStructure qTS = new FlatForward(today, value.Q, dc);
            YieldTermStructure rTS = new FlatForward(today, value.Q, dc);
            BlackVolTermStructure volTS = new BlackConstantVol(today, calendar, value.V, dc);

            double discDom = rTS.Discount(exDate);
            double discFor = qTS.Discount(exDate);
            double implVol = Math.Sqrt(volTS.BlackVariance(exDate, 0.0));
            double forward = value.S * discFor / discDom;

            var myCalc = new BlackDeltaCalculator(
                OptionType.Call, DeltaVolQuoteDeltaType.Spot, value.S, discDom, discFor, implVol);

            double deltaCall = myCalc.DeltaFromStrike(value.Strike);
            double deltaPut = myCalc.SetOptionType(OptionType.Put).DeltaFromStrike(value.Strike);

            double expectedDiff = discFor;
            double calculatedDiff = deltaCall - deltaPut;
            double error = Math.Abs(expectedDiff - calculatedDiff);
            Assert.True(
                error <= tolerance,
                $"Put-call parity failed for spot delta. Calculated Call Delta: {deltaCall}, "
                + $"Calculated Put Delta: {deltaPut}, Expected Difference: {expectedDiff}, "
                + $"Calculated Difference: {calculatedDiff}");

            myCalc = myCalc.SetDeltaType(DeltaVolQuoteDeltaType.Fwd);

            deltaCall = myCalc.DeltaFromStrike(value.Strike);
            deltaPut = myCalc.SetOptionType(OptionType.Put).DeltaFromStrike(value.Strike);

            expectedDiff = 1.0;
            calculatedDiff = deltaCall - deltaPut;
            error = Math.Abs(expectedDiff - calculatedDiff);
            Assert.True(
                error <= tolerance,
                $"Put-call parity failed for forward delta. Calculated Call Delta: {deltaCall}, "
                + $"Calculated Put Delta: {deltaPut}, Expected Difference: {expectedDiff}, "
                + $"Calculated Difference: {calculatedDiff}");

            myCalc = myCalc.SetDeltaType(DeltaVolQuoteDeltaType.PaSpot);

            deltaCall = myCalc.DeltaFromStrike(value.Strike);
            deltaPut = myCalc.SetOptionType(OptionType.Put).DeltaFromStrike(value.Strike);

            expectedDiff = discFor * value.Strike / forward;
            calculatedDiff = deltaCall - deltaPut;
            error = Math.Abs(expectedDiff - calculatedDiff);
            Assert.True(
                error <= tolerance,
                $"Put-call parity failed for premium-adjusted spot delta. Calculated Call Delta: {deltaCall}, "
                + $"Calculated Put Delta: {deltaPut}, Expected Difference: {expectedDiff}, "
                + $"Calculated Difference: {calculatedDiff}");

            myCalc = myCalc.SetDeltaType(DeltaVolQuoteDeltaType.PaFwd);

            deltaCall = myCalc.DeltaFromStrike(value.Strike);
            deltaPut = myCalc.SetOptionType(OptionType.Put).DeltaFromStrike(value.Strike);

            expectedDiff = value.Strike / forward;
            calculatedDiff = deltaCall - deltaPut;
            error = Math.Abs(expectedDiff - calculatedDiff);
            Assert.True(
                error <= tolerance,
                $"Put-call parity failed for premium-adjusted forward delta. Calculated Call Delta: {deltaCall}, "
                + $"Calculated Put Delta: {deltaPut}, Expected Difference: {expectedDiff}, "
                + $"Calculated Difference: {calculatedDiff}");
        }
    }

    [Fact]
    public void TestAtmCalcs()
    {
        // Testing delta-neutral ATM quotations...

        DeltaData[] values =
        [
            .. DeltaValues,

            // Extreme case: zero vol, ATM Fwd strike
            new(OptionType.Call, DeltaVolQuoteDeltaType.Fwd, 103.00, 0.99482, 0.98508, 0.0, 101.0013, 0.5),
            new(OptionType.Call, DeltaVolQuoteDeltaType.Spot, 103.00, 0.99482, 0.98508, 0.0, 101.0013, 0.99482 * 0.5),
        ];

        double tolerance = 1.0e-2; // not that small, but sufficient for strikes

        for (int i = 0; i < values.Length; i++)
        {
            DeltaData v = values[i];
            double currFwd = v.Spot * v.FDf / v.DDf;

            var myCalc = new BlackDeltaCalculator(OptionType.Call, v.Dt, v.Spot, v.DDf, v.FDf, v.StdDev);

            foreach ((DeltaVolQuoteDeltaType deltaType, string name) in new[]
            {
                (v.Dt, "spot"),
                (DeltaVolQuoteDeltaType.Fwd, "forward"),
                (DeltaVolQuoteDeltaType.PaSpot, "premium-adjusted spot"),
                (DeltaVolQuoteDeltaType.PaFwd, "premium-adjusted forward"),
            })
            {
                myCalc = myCalc.SetDeltaType(deltaType);
                double currAtmStrike = myCalc.AtmStrike(DeltaVolQuoteAtmType.AtmDeltaNeutral);
                double currCallDelta = myCalc.DeltaFromStrike(currAtmStrike);
                double currPutDelta = myCalc.SetOptionType(OptionType.Put).DeltaFromStrike(currAtmStrike);

                double expected = 0.0;
                double calculated = currCallDelta + currPutDelta;
                double error = Math.Abs(calculated - expected);
                Assert.True(
                    error <= tolerance,
                    $"Delta neutrality failed for {name} delta in Delta Calculator. Iteration: {i}, "
                    + $"Calculated Delta Sum: {calculated}, Expected Delta Sum: {expected}, Error: {error}");
            }

            // Test ATM forward Calculations
            {
                double calculated = myCalc.AtmStrike(DeltaVolQuoteAtmType.AtmFwd);
                double expected = currFwd;
                double error = Math.Abs(expected - calculated);
                Assert.True(
                    error <= tolerance,
                    $"Atm forward test failed. Calculated Value: {calculated}, Expected Value: {expected}, Error: {error}");
            }

            // Test ATM 0.50 delta calculations
            {
                myCalc = myCalc.SetDeltaType(DeltaVolQuoteDeltaType.Fwd);
                double atmFiftyStrike = myCalc.AtmStrike(DeltaVolQuoteAtmType.AtmPutCall50);
                double calculated = Math.Abs(myCalc.DeltaFromStrike(atmFiftyStrike));
                double expected = 0.50;
                double error = Math.Abs(expected - calculated);
                Assert.True(
                    error <= tolerance,
                    $"Atm 0.50 delta strike test failed. Iteration: {i}, Calculated Value: {calculated}, "
                    + $"Expected Value: {expected}, Error: {error}");
            }
        }
    }

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static int TimeToDays(double t, int daysPerYear = 360) =>
        (int)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private sealed record DeltaData(
        OptionType Ot,
        DeltaVolQuoteDeltaType Dt,
        double Spot,
        double DDf,
        double FDf,
        double StdDev,
        double Strike,
        double Value);

    private sealed record EuropeanOptionData(
        OptionType Type,
        double Strike,
        double S,
        double Q,
        double R,
        double T,
        double V,
        double Result,
        double Tol);
}
