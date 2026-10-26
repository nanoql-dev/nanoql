// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/varianceswaps.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class VarianceSwapsTests
{
    [Fact]
    public void TestReplicatingVarianceSwap()
    {
        // Testing variance swap with replicating cost engine...

        ReplicatingVarianceSwapData[] values =
        [
            // data from "A Guide to Volatility and Variance Swaps",
            //   Derman, Kamal & Zou, 1999
            //   with maturity t corrected from 0.25 to 0.246575
            //   corresponding to Jan 1, 1999 to Apr 1, 1999

            // type, varStrike, nominal,     s,    q,    r,        t,    v,  result, tol
            new(PositionType.Long, 0.04, 50000, 100.0, 0.00, 0.05, 0.246575, 0.20, 0.04189, 1.0e-4),
        ];

        Datum[] replicatingOptionData =
        [
            // data from "A Guide to Volatility and Variance Swaps",
            //   Derman, Kamal & Zou, 1999

            // Option::Type, strike, v
            new(OptionType.Put, 50, 0.30),
            new(OptionType.Put, 55, 0.29),
            new(OptionType.Put, 60, 0.28),
            new(OptionType.Put, 65, 0.27),
            new(OptionType.Put, 70, 0.26),
            new(OptionType.Put, 75, 0.25),
            new(OptionType.Put, 80, 0.24),
            new(OptionType.Put, 85, 0.23),
            new(OptionType.Put, 90, 0.22),
            new(OptionType.Put, 95, 0.21),
            new(OptionType.Put, 100, 0.20),
            new(OptionType.Call, 100, 0.20),
            new(OptionType.Call, 105, 0.19),
            new(OptionType.Call, 110, 0.18),
            new(OptionType.Call, 115, 0.17),
            new(OptionType.Call, 120, 0.16),
            new(OptionType.Call, 125, 0.15),
            new(OptionType.Call, 130, 0.14),
            new(OptionType.Call, 135, 0.13),
        ];

        DayCounter dc = new Actual365Fixed();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (ReplicatingVarianceSwapData value in values)
        {
            Date exDate = today + TimeToDays(value.T, 365);
            Date[] dates = [exDate];

            YieldTermStructure qTS = new FlatForward(today, value.Q, dc);
            YieldTermStructure rTS = new FlatForward(today, value.R, dc);

            int options = replicatingOptionData.Length;
            List<Real> callStrikes = [], putStrikes = [], callVols = [], putVols = [];

            // Assumes ascending strikes and same min call and max put strikes
            int j;
            for (j = 0; j < options; j++)
            {
                if (replicatingOptionData[j].Type == OptionType.Call)
                {
                    callStrikes.Add(replicatingOptionData[j].Strike);
                    callVols.Add(replicatingOptionData[j].V);
                }
                else if (replicatingOptionData[j].Type == OptionType.Put)
                {
                    putStrikes.Add(replicatingOptionData[j].Strike);
                    putVols.Add(replicatingOptionData[j].V);
                }
                else
                {
                    throw new NanoQLException("unknown option type");
                }
            }

            var vols = new Matrix(options - 1, 1);
            List<Real> strikes = [];
            for (j = 0; j < putVols.Count; j++)
            {
                vols[j, 0] = putVols[j];
                strikes.Add(putStrikes[j]);
            }

            for (int k = 1; k < callVols.Count; k++)
            {
                int jj = putVols.Count - 1;
                vols[jj + k, 0] = callVols[k];
                strikes.Add(callStrikes[k]);
            }

            BlackVolTermStructure volTS = new BlackVarianceSurface(today, new NullCalendar(), dates, strikes, vols, dc);

            var stochProcess = new BlackScholesMertonProcess(value.S, qTS, rTS, volTS);

            var varianceSwap = new VarianceSwap(value.Type, value.VarStrike, value.Nominal, today, exDate);

            Real calculated = ReplicatingVarianceSwapEngine.Calculate(
                varianceSwap, context, stochProcess, 5.0, [.. callStrikes], [.. putStrikes]).Variance!.Value;
            Real expected = value.Result;
            Real error = Math.Abs(calculated - expected);
            Assert.False(
                error > value.Tol,
                $" variance swap with     underlying value: {value.S}\n    strike:           {value.VarStrike}\n"
                + $"    nominal:          {value.Nominal}\n    dividend yield:   {value.Q}\n"
                + $"    risk-free rate:   {value.R}\n    reference date:   {today}\n    maturity:         {exDate}\n"
                + $"    volatility:       {value.V}\n\n    expected   value: {expected}\n"
                + $"    calculated value: {calculated}\n    error:            {error}\n    tolerance:        {value.Tol}");
        }
    }

    [Fact]
    public void TestMCVarianceSwap()
    {
        // Testing variance swap with Monte Carlo engine...

        MCVarianceSwapData[] values =
        [
            // data from "A Guide to Volatility and Variance Swaps",
            //   Derman, Kamal & Zou, 1999
            //   with maturity t corrected from 0.25 to 0.246575
            //   corresponding to Jan 1, 1999 to Apr 1, 1999

            // exercising code using BlackVarianceCurve because BlackVarianceSurface is unreliable
            // Result should be v*v for arbitrary t1 and v1 (as long as 0<=t1<t and 0<=v1<v)

            // type, varStrike, nominal,     s,    q,    r,  t1,     t,     v1,    v, result, tol
            new(PositionType.Long, 0.04, 50000, 100.0, 0.00, 0.05, 0.1, 0.246575, 0.1, 0.20, 0.04, 3.0e-4),
        ];

        DayCounter dc = new Actual365Fixed();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (MCVarianceSwapData value in values)
        {
            Date exDate = today + TimeToDays(value.T, 365);
            Date intermDate = today + TimeToDays(value.T1, 365);
            Date[] dates = [intermDate, exDate];
            Volatility[] vols = [value.V1, value.V];

            YieldTermStructure qTS = new FlatForward(today, value.Q, dc);
            YieldTermStructure rTS = new FlatForward(today, value.R, dc);
            BlackVolTermStructure volTS = new BlackVarianceCurve(today, dates, vols, dc, true);
            var stochProcess = new BlackScholesMertonProcess(value.S, qTS, rTS, volTS);

            Func<VarianceSwap, EvaluationContext, VarianceSwapResults> engine =
                new MakeMCVarianceSwapEngine<PseudoRandom, GeneralStatistics>(stochProcess, 42)
                    .WithStepsPerYear(250)
                    .WithSamples(1023)
                    .ToEngine();

            var varianceSwap = new VarianceSwap(value.Type, value.VarStrike, value.Nominal, today, exDate);

            Real calculated = engine(varianceSwap, context).Variance!.Value;
            Real expected = value.Result;
            Real error = Math.Abs(calculated - expected);
            Assert.False(
                error > value.Tol,
                $" variance swap with     underlying value: {value.S}\n    strike:           {value.VarStrike}\n"
                + $"    nominal:          {value.Nominal}\n    dividend yield:   {value.Q}\n"
                + $"    risk-free rate:   {value.R}\n    reference date:   {today}\n    maturity:         {exDate}\n"
                + $"    volatility:       {value.V}\n\n    expected   value: {expected}\n"
                + $"    calculated value: {calculated}\n    error:            {error}\n    tolerance:        {value.Tol}");
        }
    }

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private readonly record struct ReplicatingVarianceSwapData(
        PositionType Type, Real VarStrike, Real Nominal, Real S, Rate Q, Rate R, double T, Volatility V, Real Result, Real Tol);

    private readonly record struct Datum(OptionType Type, Real Strike, Volatility V);

    private readonly record struct MCVarianceSwapData(
        PositionType Type,
        Real VarStrike,
        Real Nominal,
        Real S,
        Rate Q,
        Rate R,
        double T1,
        double T,
        Volatility V1,
        Volatility V,
        Real Result,
        Real Tol);
}
