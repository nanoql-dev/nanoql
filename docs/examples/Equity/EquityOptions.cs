// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: Examples/EquityOption/EquityOption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Equity;

/// <summary>
/// Equity options: QuantLib's EquityOption example, one put valued European, Bermudan and American by each engine
/// family.
/// </summary>
public static class EquityOptions
{
    /// <summary>
    /// The Longstaff-Schwartz row's name, marked: its calibration seed is the one QuantLib documents, the pricing seed
    /// plus an offset, where QuantLib's code takes its null (the divergences page's LongstaffSchwartzCalibrationSeedTests).
    /// </summary>
    public const string LongstaffSchwartz = "Monte Carlo, Longstaff-Schwartz †";

    /// <summary>Gets the example's evaluation context: 15 May 1998.</summary>
    public static EvaluationContext Context { get; } = new() { EvaluationDate = new Date(15, Month.May, 1998) };

    /// <summary>The example's outputs.</summary>
    /// <returns>The table of values, a row per method.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        var table = new Table("Method", ">European", ">Bermudan", ">American");
        foreach (var (method, european, bermudan, american) in Run())
        {
            table.Add(method, Cell(european), Cell(bermudan), Cell(american));
        }

        yield return ("equity-options", table);
    }

    /// <summary>Values the example's put by each method.</summary>
    /// <returns>Each method's European, Bermudan and American values, null where it doesn't apply.</returns>
    public static IReadOnlyList<(string Method, double? European, double? Bermudan, double? American)> Run()
    {
        // <setup>
        var settlement = new Date(17, Month.May, 1998);
        var maturity = new Date(17, Month.May, 1999);
        double underlying = 36, strike = 40, dividendYield = 0.0, riskFreeRate = 0.06, volatility = 0.20;
        DayCounter dayCounter = new Actual365Fixed();

        var riskFree = new FlatForward(settlement, riskFreeRate, dayCounter);
        var dividend = new FlatForward(settlement, dividendYield, dayCounter);
        var process = new BlackScholesMertonProcess(underlying, dividend, riskFree,
            new BlackConstantVol(settlement, new TARGET(), volatility, dayCounter));

        var payoff = new PlainVanillaPayoff(OptionType.Put, strike);
        var european = new VanillaOption(payoff, new EuropeanExercise(maturity));
        var american = new VanillaOption(payoff, new AmericanExercise(settlement, maturity));
        Date[] exerciseDates = [.. Enumerable.Range(1, 4).Select(i => settlement + new Period(3 * i, TimeUnit.Months))];
        var bermudan = new VanillaOption(payoff, new BermudanExercise(exerciseDates));
        // </setup>
        EvaluationContext context = Context;
        var rows = new List<(string, double?, double?, double?)>();

        // <analytic>
        rows.Add(("Black-Scholes", AnalyticEuropeanEngine.Calculate(european, context, process).NPV, null, null));
        rows.Add(("Black-Vasicek", AnalyticBlackVasicekEngine.Calculate(
            european, context, process, new Vasicek(riskFreeRate, 0.3, 0.3, 0.15, 0.0), 0.5).NPV, null, null));
        var heston = new HestonModel(new HestonProcess(
            riskFree, dividend, underlying, volatility * volatility, 1.0, volatility * volatility, 0.001, 0.0));
        rows.Add(("Heston, semi-analytic", AnalyticHestonEngine.Calculate(european, context, heston).NPV, null, null));
        var bates = new BatesModel(new BatesProcess(
            riskFree, dividend, underlying, volatility * volatility, 1.0, volatility * volatility, 0.001, 0.0,
            1e-14, 1e-14, 1e-14));
        rows.Add(("Bates, semi-analytic", BatesEngine.Calculate(european, context, bates).NPV, null, null));
        rows.Add(("Barone-Adesi-Whaley", null, null,
            BaroneAdesiWhaleyApproximationEngine.Calculate(american, context, process).NPV));
        rows.Add(("Bjerksund-Stensland", null, null,
            BjerksundStenslandApproximationEngine.Calculate(american, context, process).NPV));
        rows.Add(("QD+ fixed point, fast", null, null,
            QdFpAmericanEngine.Calculate(american, context, process, QdFpAmericanEngine.FastScheme()).NPV));
        rows.Add(("QD+ fixed point, accurate", null, null,
            QdFpAmericanEngine.Calculate(american, context, process, QdFpAmericanEngine.AccurateScheme()).NPV));
        rows.Add(("QD+ fixed point, high precision", null, null,
            QdFpAmericanEngine.Calculate(american, context, process, QdFpAmericanEngine.HighPrecisionScheme()).NPV));
        rows.Add(("Integral", IntegralEngine.Calculate(european, context, process).NPV, null, null));
        // </analytic>

        // <lattices>
        int timeSteps = 801;
        rows.Add(("Finite differences",
            FdBlackScholesVanillaEngine.Calculate(european, context, process, tGrid: timeSteps, xGrid: timeSteps - 1).NPV,
            FdBlackScholesVanillaEngine.Calculate(bermudan, context, process, tGrid: timeSteps, xGrid: timeSteps - 1).NPV,
            FdBlackScholesVanillaEngine.Calculate(american, context, process, tGrid: timeSteps, xGrid: timeSteps - 1).NPV));
        (string, double?, double?, double?) Tree<TTree>(string name)
            where TTree : Tree, IBinomialTree<TTree> => (name,
            BinomialVanillaEngine.Calculate<TTree>(european, context, process, timeSteps).NPV,
            BinomialVanillaEngine.Calculate<TTree>(bermudan, context, process, timeSteps).NPV,
            BinomialVanillaEngine.Calculate<TTree>(american, context, process, timeSteps).NPV);
        rows.Add(Tree<JarrowRudd>("Binomial, Jarrow-Rudd"));
        rows.Add(Tree<CoxRossRubinstein>("Binomial, Cox-Ross-Rubinstein"));
        rows.Add(Tree<AdditiveEQPBinomialTree>("Binomial, additive equiprobabilities"));
        rows.Add(Tree<Trigeorgis>("Binomial, Trigeorgis"));
        rows.Add(Tree<Tian>("Binomial, Tian"));
        rows.Add(Tree<LeisenReimer>("Binomial, Leisen-Reimer"));
        rows.Add(Tree<Joshi4>("Binomial, Joshi"));
        // </lattices>

        // <montecarlo>
        int seed = 42;
        rows.Add(("Monte Carlo, crude", new MakeMCEuropeanEngine<PseudoRandom, GeneralStatistics>(process, seed)
            .WithSteps(1).WithAbsoluteTolerance(0.02).ToEngine()(european, context).NPV, null, null));
        // QuantLib's example gives the Sobol engine no seed; a one-dimensional Sobol sequence reads none, and nanoql's
        // seeds are explicit.
        rows.Add(("Quasi-Monte Carlo, Sobol", new MakeMCEuropeanEngine<LowDiscrepancy, GeneralStatistics>(process, seed)
            .WithSteps(1).WithSamples(32768).ToEngine()(european, context).NPV, null, null));
        rows.Add((LongstaffSchwartz, null, null,
            new MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom>(process, seed)
                .WithSteps(100).WithAntitheticVariate().WithCalibrationSamples(4096).WithAbsoluteTolerance(0.02)
                .ToEngine()(american, context).NPV));
        // </montecarlo>
        return rows;
    }

    private static string Cell(double? value) => value is { } v ? Table.N(v, 6) : "";
}
