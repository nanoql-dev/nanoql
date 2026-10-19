// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swaptionvolatilitymatrix.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testSwaptionVolMatrixObservability (Vectors/TermStructures/SwaptionVolMatrixVectorTests.cs)

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.TestSuite.SwaptionVolStructuresUtilitiesTests;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SwaptionVolatilityMatrixTests
{
    // Adapted: the two matrices whose reference date floats (QuantLib's calendar-first constructors) are built by
    // Create from the context; the engine set on the at-the-money swaption by withPricingEngine (line 203) prices it,
    // called on the swaption (line 228).
    [Fact]
    public void TestSwaptionVolMatrixCoherence()
    {
        // Testing swaption volatility matrix...

        var vars = new CommonVars();

        SwaptionVolatilityMatrix vol;
        string description;

        // floating reference date, floating market data
        description = "floating reference date, floating market data";
        vol = SwaptionVolatilityMatrix.Create(
            vars.Conventions.Calendar,
            vars.Conventions.OptionBdc,
            vars.Atm.Tenors.Options,
            vars.Atm.Tenors.Swaps,
            vars.Atm.Vols,
            vars.Conventions.DayCounter,
            vars.Context);
        vars.MakeCoherenceTest(description, vol);

        // fixed reference date, floating market data
        description = "fixed reference date, floating market data";
        vol = new SwaptionVolatilityMatrix(
            vars.Context.EvaluationDate,
            vars.Conventions.Calendar,
            vars.Conventions.OptionBdc,
            vars.Atm.Tenors.Options,
            vars.Atm.Tenors.Swaps,
            vars.Atm.Vols,
            vars.Conventions.DayCounter);
        vars.MakeCoherenceTest(description, vol);

        // floating reference date, fixed market data
        description = "floating reference date, fixed market data";
        vol = SwaptionVolatilityMatrix.Create(
            vars.Conventions.Calendar,
            vars.Conventions.OptionBdc,
            vars.Atm.Tenors.Options,
            vars.Atm.Tenors.Swaps,
            vars.Atm.Vols,
            vars.Conventions.DayCounter,
            vars.Context);
        vars.MakeCoherenceTest(description, vol);

        // fixed reference date, fixed market data
        description = "fixed reference date, fixed market data";
        vol = new SwaptionVolatilityMatrix(
            vars.Context.EvaluationDate,
            vars.Conventions.Calendar,
            vars.Conventions.OptionBdc,
            vars.Atm.Tenors.Options,
            vars.Atm.Tenors.Swaps,
            vars.Atm.Vols,
            vars.Conventions.DayCounter);
        vars.MakeCoherenceTest(description, vol);
    }

    // The fixture without makeObservabilityTest, whose case is replaced.
    private sealed class CommonVars
    {
        public CommonVars()
        {
            Conventions.SetConventions();
            Atm.SetMarketData();
            Context = new EvaluationContext { EvaluationDate = Conventions.Calendar.Adjust(Date.TodaysDate()) };
            AtmVolMatrix = SwaptionVolatilityMatrix.Create(
                Conventions.Calendar,
                Conventions.OptionBdc,
                Atm.Tenors.Options,
                Atm.Tenors.Swaps,
                Atm.Vols,
                Conventions.DayCounter,
                Context);
            TermStructure = FlatForward.Create(0, Conventions.Calendar, 0.05, new Actual365Fixed(), Context);
        }

        // global data
        public SwaptionMarketConventions Conventions { get; } = new();

        public AtmVolatility Atm { get; } = new();

        public EvaluationContext Context { get; }

        public YieldTermStructure TermStructure { get; }

        public SwaptionVolatilityStructure AtmVolMatrix { get; }

        // utilities
        public void MakeCoherenceTest(string description, SwaptionVolatilityDiscrete vol)
        {
            for (int i = 0; i < Atm.Tenors.Options.Count; ++i)
            {
                Date optionDate = vol.OptionDateFromTenor(Atm.Tenors.Options[i]);
                Assert.True(
                    optionDate == vol.OptionDates[i],
                    $"optionDateFromTenor failure for {description}: option tenor {Atm.Tenors.Options[i]}, actual "
                    + $"option date {optionDate}, exp. option date {vol.OptionDates[i]}");
                double optionTime = vol.TimeFromReference(optionDate);
                Assert.True(
                    ComparisonFunctions.Close(optionTime, vol.OptionTimes[i]),
                    $"timeFromReference failure for {description}: option tenor {Atm.Tenors.Options[i]}, option "
                    + $"date {optionDate}, actual option time {optionTime}, exp. option time {vol.OptionTimes[i]}");
            }

            InstrumentResults Engine(Swaption swaption, EvaluationContext context) =>
                BlackSwaptionEngine.Calculate(swaption, context, TermStructure, vol);

            for (int j = 0; j < Atm.Tenors.Swaps.Count; j++)
            {
                double swapLength = vol.SwapLength(Atm.Tenors.Swaps[j]);
                Assert.True(
                    ComparisonFunctions.Close(swapLength, PeriodFunctions.Years(Atm.Tenors.Swaps[j])),
                    $"convertSwapTenor failure for {description}: swap tenor {Atm.Tenors.Swaps[j]}, actual swap "
                    + $"length {swapLength}, exp. swap length {PeriodFunctions.Years(Atm.Tenors.Swaps[j])}");

                SwapIndex swapIndex = new EuriborSwapIsdaFixA(Atm.Tenors.Swaps[j], TermStructure);

                for (int i = 0; i < Atm.Tenors.Options.Count; ++i)
                {
                    double error, tolerance = 1.0e-16;
                    double actVol, expVol = Atm.Vols[i, j];

                    actVol = vol.Volatility(Atm.Tenors.Options[i], Atm.Tenors.Swaps[j], 0.05, true);
                    error = Math.Abs(expVol - actVol);
                    Assert.True(
                        error <= tolerance,
                        $"recovery of atm vols failed for {description}: option tenor = {Atm.Tenors.Options[i]}, "
                        + $"swap length = {Atm.Tenors.Swaps[j]}, expected vol = {expVol}, actual vol = {actVol}, "
                        + $"error = {error}, tolerance = {tolerance}");

                    Date optionDate = vol.OptionDateFromTenor(Atm.Tenors.Options[i]);
                    actVol = vol.Volatility(optionDate, Atm.Tenors.Swaps[j], 0.05, true);
                    error = Math.Abs(expVol - actVol);
                    Assert.True(
                        error <= tolerance,
                        $"recovery of atm vols failed for {description}: option tenor {Atm.Tenors.Options[i]}, "
                        + $"option date {optionDate}, swap tenor {Atm.Tenors.Swaps[j]}, exp. vol {expVol}, actual vol "
                        + $"{actVol}, error {error}, tolerance {tolerance}");

                    double optionTime = vol.TimeFromReference(optionDate);
                    actVol = vol.Volatility(optionTime, swapLength, 0.05, true);
                    error = Math.Abs(expVol - actVol);
                    Assert.True(
                        error <= tolerance,
                        $"recovery of atm vols failed for {description}: option tenor {Atm.Tenors.Options[i]}, "
                        + $"option time {optionTime}, swap tenor {Atm.Tenors.Swaps[j]}, swap length {swapLength}, "
                        + $"exp. vol {expVol}, actual vol {actVol}, error {error}, tolerance {tolerance}");

                    // ATM swaption
                    Swaption swaption = new MakeSwaption(swapIndex, Atm.Tenors.Options[i])
                        .WithPricingEngine(Engine)
                        .ToSwaption(Context);

                    Date exerciseDate = swaption.Exercise.Dates[0];
                    Assert.True(
                        exerciseDate == vol.OptionDates[i],
                        $"optionDateFromTenor mismatch for {description}: option tenor {Atm.Tenors.Options[i]}, "
                        + $"actual option date {exerciseDate}, exp. option date {vol.OptionDates[i]}");

                    Date start = swaption.Underlying.StartDate();
                    Date end = swaption.Underlying.MaturityDate();
                    double swapLength2 = vol.SwapLength(start, end);
                    Assert.True(
                        ComparisonFunctions.Close(swapLength2, swapLength),
                        $"swapLength failure for {description}: exp. swap length {swapLength}, actual swap length "
                        + $"{swapLength2}, swap tenor {Atm.Tenors.Swaps[j]}, swap index tenor {swapIndex.Tenor}, "
                        + $"option date {exerciseDate}, start date {start}, maturity date {end}");

                    double npv = Engine(swaption, Context).NPV!.Value;
                    actVol = swaption.ImpliedVolatility(
                        npv, TermStructure, expVol * 0.98, Context, 1e-6, 100, 10.0e-7, 4.0, VolatilityType.ShiftedLognormal, 0.0);
                    error = Math.Abs(expVol - actVol);
                    double tolerance2 = 0.000001;
                    Assert.True(
                        error <= tolerance2,
                        $"recovery of atm vols through BlackSwaptionEngine failed for {description}: option tenor "
                        + $"{Atm.Tenors.Options[i]}, option time {optionTime}, swap tenor {Atm.Tenors.Swaps[j]}, swap "
                        + $"length {swapLength}, exp. vol {expVol}, actual vol {actVol}, error {error}, tolerance "
                        + $"{tolerance2}");
                }
            }
        }
    }
}
