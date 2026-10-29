// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/everestoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class EverestOptionTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(28, Month.October, 2026) };

    [Fact]
    public void TestCached()
    {
        // Testing Everest option against cached values...
        Date today = Context.EvaluationDate;
        DayCounter dc = new Actual360();
        Date exerciseDate = today + 360;
        Exercise exercise = new EuropeanExercise(exerciseDate);

        double notional = 1.0;
        double guarantee = 0.0;
        var option = new EverestOption(notional, guarantee, exercise);

        YieldTermStructure riskFreeRate = FlatRate(today, 0.05, dc);
        const double dummyUnderlying = 1.0;
        var processes = new StochasticProcess1D[]
        {
            new BlackScholesMertonProcess(dummyUnderlying, FlatRate(today, 0.01, dc), riskFreeRate, FlatVol(today, 0.30, dc)),
            new BlackScholesMertonProcess(dummyUnderlying, FlatRate(today, 0.05, dc), riskFreeRate, FlatVol(today, 0.35, dc)),
            new BlackScholesMertonProcess(dummyUnderlying, FlatRate(today, 0.04, dc), riskFreeRate, FlatVol(today, 0.25, dc)),
            new BlackScholesMertonProcess(dummyUnderlying, FlatRate(today, 0.03, dc), riskFreeRate, FlatVol(today, 0.20, dc)),
        };
        var correlation = new Matrix(new[,]
        {
            { 1.00, 0.50, 0.30, 0.10 },
            { 0.50, 1.00, 0.20, 0.40 },
            { 0.30, 0.20, 1.00, 0.60 },
            { 0.10, 0.40, 0.60, 1.00 },
        });

        const long seed = 86421;
        const int fixedSamples = 1023;
        double minimumTol = 1.0e-2;

        var process = new StochasticProcessArray([.. processes], correlation);

        EverestOptionResults results = new MakeMCEverestEngine<PseudoRandom, GeneralStatistics>(process, seed)
            .WithStepsPerYear(1)
            .WithSamples(fixedSamples)
            .ToEngine()(option, Context);
        double value = results.NPV!.Value;
        double storedValue = 0.75784944;
        double tolerance = 1.0e-8;

        Assert.False(
            Math.Abs(value - storedValue) > tolerance,
            $"    calculated value: {value}\n    expected:         {storedValue}");

        tolerance = results.ErrorEstimate!.Value;
        tolerance = Math.Min(tolerance / 2.0, minimumTol * value);
        double accuracy = new MakeMCEverestEngine<PseudoRandom, GeneralStatistics>(process, seed)
            .WithStepsPerYear(1)
            .WithAbsoluteTolerance(tolerance)
            .ToEngine()(option, Context).ErrorEstimate!.Value;

        Assert.False(accuracy > tolerance, $"    reached accuracy: {accuracy}\n    expected:         {tolerance}");
    }

    // The test suite's flatRate: a flat continuously compounded curve from the reference date.
    private static YieldTermStructure FlatRate(Date today, double forward, DayCounter dc) =>
        new FlatForward(today, forward, dc);

    // The test suite's flatVol: a constant volatility from the reference date, on the null calendar.
    private static BlackVolTermStructure FlatVol(Date today, double vol, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), vol, dc);
}
