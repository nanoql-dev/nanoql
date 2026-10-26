// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/lookbackoptions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// QuantLib's options hold their engines and its quotes notify them; here each pricing passes the engine's arguments,
// and a quote set anew is a process built at the new value.
[Trait("Oracle", "QuantLib")]
public class LookbackOptionsTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestAnalyticContinuousFloatingLookback()
    {
        // Testing analytic continuous floating-strike lookback options...

        LookbackOptionData[] values =
        [
            // data from "Option Pricing Formulas", Haug, 1998, pg.61-62
            new(OptionType.Call, 0.0, 100.0, 120.0, 0.06, 0.1, 0.5, 0.3, 0.0, 0.0, 25.3533, 0.0001),
            // data from "Connecting discrete and continuous path-dependent options",
            // Broadie, Glasserman & Kou, 1999, pg.70-74
            new(OptionType.Call, 0.0, 100.0, 100.0, 0.0, 0.05, 1.0, 0.3, 0.0, 0.0, 23.7884, 0.0001),
            new(OptionType.Call, 0.0, 100.0, 100.0, 0.0, 0.05, 0.2, 0.3, 0.0, 0.0, 10.719, 0.0001),
            new(OptionType.Call, 0.0, 100.0, 110.0, 0.0, 0.05, 0.2, 0.3, 0.0, 0.0, 14.4597, 0.0001),
            new(OptionType.Put, 0.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.3, 0.0, 0.0, 15.3526, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 100.0, 0.0, 0.1, 0.5, 0.3, 0.0, 0.0, 16.8468, 0.0001),
            new(OptionType.Put, 0.0, 120.0, 100.0, 0.0, 0.1, 0.5, 0.3, 0.0, 0.0, 21.0645, 0.0001),
        ];

        foreach (LookbackOptionData value in values)
        {
            (Exercise exercise, GeneralizedBlackScholesProcess process) = Market(value);
            var payoff = new FloatingTypePayoff(value.Type);
            var option = new ContinuousFloatingLookbackOption(value.Minmax, payoff, exercise);
            double calculated = AnalyticContinuousFloatingLookbackEngine.Calculate(option, RunnerContext, process).NPV!.Value;
            Check(value, calculated);
        }
    }

    [Fact]
    public void TestAnalyticContinuousFixedLookback()
    {
        // Testing analytic continuous fixed-strike lookback options...

        LookbackOptionData[] values =
        [
            // data from "Option Pricing Formulas", Haug, 1998, pg.63-64
            new(OptionType.Call, 95.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.1, 0.0, 0.0, 13.2687, 0.0001),
            new(OptionType.Call, 95.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.2, 0.0, 0.0, 18.9263, 0.0001),
            new(OptionType.Call, 95.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.3, 0.0, 0.0, 24.9857, 0.0001),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.1, 0.0, 0.0, 8.5126, 0.0001),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.2, 0.0, 0.0, 14.1702, 0.0001),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.3, 0.0, 0.0, 20.2296, 0.0001),
            new(OptionType.Call, 105.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.1, 0.0, 0.0, 4.3908, 0.0001),
            new(OptionType.Call, 105.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.2, 0.0, 0.0, 9.8905, 0.0001),
            new(OptionType.Call, 105.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.3, 0.0, 0.0, 15.8512, 0.0001),
            new(OptionType.Call, 95.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.1, 0.0, 0.0, 18.3241, 0.0001),
            new(OptionType.Call, 95.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.2, 0.0, 0.0, 26.0731, 0.0001),
            new(OptionType.Call, 95.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.3, 0.0, 0.0, 34.7116, 0.0001),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.1, 0.0, 0.0, 13.8, 0.0001),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.2, 0.0, 0.0, 21.5489, 0.0001),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.3, 0.0, 0.0, 30.1874, 0.0001),
            new(OptionType.Call, 105.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.1, 0.0, 0.0, 9.5445, 0.0001),
            new(OptionType.Call, 105.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.2, 0.0, 0.0, 17.2965, 0.0001),
            new(OptionType.Call, 105.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.3, 0.0, 0.0, 25.9002, 0.0001),
            new(OptionType.Put, 95.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.1, 0.0, 0.0, 0.6899, 0.0001),
            new(OptionType.Put, 95.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.2, 0.0, 0.0, 4.4448, 0.0001),
            new(OptionType.Put, 95.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.3, 0.0, 0.0, 8.9213, 0.0001),
            new(OptionType.Put, 100.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.1, 0.0, 0.0, 3.3917, 0.0001),
            new(OptionType.Put, 100.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.2, 0.0, 0.0, 8.3177, 0.0001),
            new(OptionType.Put, 100.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.3, 0.0, 0.0, 13.1579, 0.0001),
            new(OptionType.Put, 105.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.1, 0.0, 0.0, 8.1478, 0.0001),
            new(OptionType.Put, 105.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.2, 0.0, 0.0, 13.0739, 0.0001),
            new(OptionType.Put, 105.0, 100.0, 100.0, 0.0, 0.1, 0.5, 0.3, 0.0, 0.0, 17.914, 0.0001),
            new(OptionType.Put, 95.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.1, 0.0, 0.0, 1.0534, 0.0001),
            new(OptionType.Put, 95.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.2, 0.0, 0.0, 6.2813, 0.0001),
            new(OptionType.Put, 95.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.3, 0.0, 0.0, 12.2376, 0.0001),
            new(OptionType.Put, 100.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.1, 0.0, 0.0, 3.8079, 0.0001),
            new(OptionType.Put, 100.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.2, 0.0, 0.0, 10.1294, 0.0001),
            new(OptionType.Put, 100.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.3, 0.0, 0.0, 16.3889, 0.0001),
            new(OptionType.Put, 105.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.1, 0.0, 0.0, 8.3321, 0.0001),
            new(OptionType.Put, 105.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.2, 0.0, 0.0, 14.6536, 0.0001),
            new(OptionType.Put, 105.0, 100.0, 100.0, 0.0, 0.1, 1.0, 0.3, 0.0, 0.0, 20.913, 0.0001),
        ];

        foreach (LookbackOptionData value in values)
        {
            (Exercise exercise, GeneralizedBlackScholesProcess process) = Market(value);
            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);
            var option = new ContinuousFixedLookbackOption(value.Minmax, payoff, exercise);
            double calculated = AnalyticContinuousFixedLookbackEngine.Calculate(option, RunnerContext, process).NPV!.Value;
            Check(value, calculated);
        }
    }

    [Fact]
    public void TestAnalyticContinuousPartialFloatingLookback()
    {
        // Testing analytic continuous partial floating-strike lookback options...

        LookbackOptionData[] values =
        [
            // data from "Option Pricing Formulas, Second Edition", Haug, 2006, pg.146
            new(OptionType.Call, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.25, 8.6524, 0.0001),
            new(OptionType.Call, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.5, 9.2128, 0.0001),
            new(OptionType.Call, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.75, 9.5567, 0.0001),
            new(OptionType.Call, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.25, 10.5751, 0.0001),
            new(OptionType.Call, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.5, 11.2601, 0.0001),
            new(OptionType.Call, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.75, 11.6804, 0.0001),
            new(OptionType.Call, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.25, 13.3402, 0.0001),
            new(OptionType.Call, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.5, 14.5121, 0.0001),
            new(OptionType.Call, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.75, 15.314, 0.0001),
            new(OptionType.Call, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.25, 16.3047, 0.0001),
            new(OptionType.Call, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.5, 17.737, 0.0001),
            new(OptionType.Call, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.75, 18.7171, 0.0001),
            new(OptionType.Call, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.25, 17.9831, 0.0001),
            new(OptionType.Call, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.5, 19.6618, 0.0001),
            new(OptionType.Call, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.75, 20.8493, 0.0001),
            new(OptionType.Call, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.25, 21.9793, 0.0001),
            new(OptionType.Call, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.5, 24.0311, 0.0001),
            new(OptionType.Call, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.75, 25.4825, 0.0001),
            new(OptionType.Put, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.25, 2.7189, 0.0001),
            new(OptionType.Put, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.5, 3.4639, 0.0001),
            new(OptionType.Put, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.75, 4.1912, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.25, 3.3231, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.5, 4.2336, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.1, 1.0, 0.75, 5.1226, 0.0001),
            new(OptionType.Put, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.25, 7.9153, 0.0001),
            new(OptionType.Put, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.5, 9.5825, 0.0001),
            new(OptionType.Put, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.75, 11.0362, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.25, 9.6743, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.5, 11.7119, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.2, 1.0, 0.75, 13.4887, 0.0001),
            new(OptionType.Put, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.25, 13.4719, 0.0001),
            new(OptionType.Put, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.5, 16.1495, 0.0001),
            new(OptionType.Put, 0.0, 90.0, 90.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.75, 18.4071, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.25, 16.4657, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.5, 19.7383, 0.0001),
            new(OptionType.Put, 0.0, 110.0, 110.0, 0.0, 0.06, 1.0, 0.3, 1.0, 0.75, 22.4976, 0.0001),
        ];

        Date today = Date.TodaysDate();
        foreach (LookbackOptionData value in values)
        {
            (Exercise exercise, GeneralizedBlackScholesProcess process) = Market(value);
            var payoff = new FloatingTypePayoff(value.Type);
            Date lookbackEnd = today + TimeToDays(value.T1);
            var option = new ContinuousPartialFloatingLookbackOption(value.Minmax, value.L, lookbackEnd, payoff, exercise);
            double calculated = AnalyticContinuousPartialFloatingLookbackEngine.Calculate(option, RunnerContext, process).NPV!.Value;
            Check(value, calculated);
        }
    }

    [Fact]
    public void TestAnalyticContinuousPartialFixedLookback()
    {
        // Testing analytic continuous fixed-strike lookback options...

        LookbackOptionData[] values =
        [
            // data from "Option Pricing Formulas, Second Edition", Haug, 2006, pg.148
            new(OptionType.Call, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.25, 20.2845, 0.0001),
            new(OptionType.Call, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.5, 19.6239, 0.0001),
            new(OptionType.Call, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.75, 18.6244, 0.0001),
            new(OptionType.Call, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.25, 4.0432, 0.0001),
            new(OptionType.Call, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.5, 3.958, 0.0001),
            new(OptionType.Call, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.75, 3.7015, 0.0001),
            new(OptionType.Call, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.25, 27.5385, 0.0001),
            new(OptionType.Call, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.5, 25.8126, 0.0001),
            new(OptionType.Call, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.75, 23.4957, 0.0001),
            new(OptionType.Call, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.25, 11.4895, 0.0001),
            new(OptionType.Call, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.5, 10.8995, 0.0001),
            new(OptionType.Call, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.75, 9.8244, 0.0001),
            new(OptionType.Call, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.25, 35.4578, 0.0001),
            new(OptionType.Call, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.5, 32.7172, 0.0001),
            new(OptionType.Call, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.75, 29.1473, 0.0001),
            new(OptionType.Call, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.25, 19.725, 0.0001),
            new(OptionType.Call, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.5, 18.4025, 0.0001),
            new(OptionType.Call, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.75, 16.2976, 0.0001),
            new(OptionType.Put, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.25, 0.4973, 0.0001),
            new(OptionType.Put, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.5, 0.4632, 0.0001),
            new(OptionType.Put, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.75, 0.3863, 0.0001),
            new(OptionType.Put, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.25, 12.6978, 0.0001),
            new(OptionType.Put, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.5, 10.9492, 0.0001),
            new(OptionType.Put, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.1, 0.0, 0.75, 9.1555, 0.0001),
            new(OptionType.Put, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.25, 4.5863, 0.0001),
            new(OptionType.Put, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.5, 4.1925, 0.0001),
            new(OptionType.Put, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.75, 3.5831, 0.0001),
            new(OptionType.Put, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.25, 19.0255, 0.0001),
            new(OptionType.Put, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.5, 16.9433, 0.0001),
            new(OptionType.Put, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.2, 0.0, 0.75, 14.6505, 0.0001),
            new(OptionType.Put, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.25, 9.9348, 0.0001),
            new(OptionType.Put, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.5, 9.1111, 0.0001),
            new(OptionType.Put, 90.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.75, 7.9267, 0.0001),
            new(OptionType.Put, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.25, 25.2112, 0.0001),
            new(OptionType.Put, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.5, 22.8217, 0.0001),
            new(OptionType.Put, 110.0, 0.0, 100.0, 0.0, 0.06, 1.0, 0.3, 0.0, 0.75, 20.0566, 0.0001),
        ];

        Date today = Date.TodaysDate();
        foreach (LookbackOptionData value in values)
        {
            (Exercise exercise, GeneralizedBlackScholesProcess process) = Market(value);
            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);
            Date lookbackStart = today + TimeToDays(value.T1);
            var option = new ContinuousPartialFixedLookbackOption(lookbackStart, payoff, exercise);
            double calculated = AnalyticContinuousPartialFixedLookbackEngine.Calculate(option, RunnerContext, process).NPV!.Value;
            Check(value, calculated);
        }
    }

    [Fact]
    public void TestMonteCarloLookback()
    {
        // Testing Monte Carlo engines for lookback options...

        double tolerance = 0.1;

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        double strike = 90;
        double t = 1;
        double t1 = 0.25;

        Date exDate = today + TimeToDays(t);
        var exercise = new EuropeanExercise(exDate);

        var stochProcess = new BlackScholesMertonProcess(
            100, FlatRate(today, 0, dc), FlatRate(today, 0.06, dc), FlatVol(today, 0.1, dc));

        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            var payoff = new PlainVanillaPayoff(type, strike);

            // Partial Fixed
            Date lookbackStart = today + TimeToDays(t1);
            var partialFixedLookback = new ContinuousPartialFixedLookbackOption(lookbackStart, payoff, exercise);
            double analytical =
                AnalyticContinuousPartialFixedLookbackEngine.Calculate(partialFixedLookback, RunnerContext, stochProcess).NPV!.Value;
            double monteCarlo = new MakeMCLookbackEngine<ContinuousPartialFixedLookbackOption, PseudoRandom, GeneralStatistics>(stochProcess, 1)
                .WithSteps(2000)
                .WithAntitheticVariate()
                .WithAbsoluteTolerance(tolerance)
                .ToEngine()(partialFixedLookback, RunnerContext).NPV!.Value;
            CheckMc("Partial Fixed", type, analytical, monteCarlo, tolerance);

            // Fixed
            double minMax = 100;
            var fixedLookback = new ContinuousFixedLookbackOption(minMax, payoff, exercise);
            analytical = AnalyticContinuousFixedLookbackEngine.Calculate(fixedLookback, RunnerContext, stochProcess).NPV!.Value;
            monteCarlo = new MakeMCLookbackEngine<ContinuousFixedLookbackOption, PseudoRandom, GeneralStatistics>(stochProcess, 1)
                .WithSteps(2000)
                .WithAntitheticVariate()
                .WithAbsoluteTolerance(tolerance)
                .ToEngine()(fixedLookback, RunnerContext).NPV!.Value;
            CheckMc("Fixed", type, analytical, monteCarlo, tolerance);

            // Partial Floating
            double lambda = 1;
            Date lookbackEnd = today + TimeToDays(t1);
            var floatingPayoff = new FloatingTypePayoff(type);
            var partialFloating = new ContinuousPartialFloatingLookbackOption(minMax, lambda, lookbackEnd, floatingPayoff, exercise);
            analytical =
                AnalyticContinuousPartialFloatingLookbackEngine.Calculate(partialFloating, RunnerContext, stochProcess).NPV!.Value;
            monteCarlo = new MakeMCLookbackEngine<ContinuousPartialFloatingLookbackOption, PseudoRandom, GeneralStatistics>(stochProcess, 1)
                .WithSteps(2000)
                .WithAntitheticVariate()
                .WithAbsoluteTolerance(tolerance)
                .ToEngine()(partialFloating, RunnerContext).NPV!.Value;
            CheckMc("Partial Floating", type, analytical, monteCarlo, tolerance);

            // Floating
            var floating = new ContinuousFloatingLookbackOption(minMax, floatingPayoff, exercise);
            analytical = AnalyticContinuousFloatingLookbackEngine.Calculate(floating, RunnerContext, stochProcess).NPV!.Value;
            monteCarlo = new MakeMCLookbackEngine<ContinuousFloatingLookbackOption, PseudoRandom, GeneralStatistics>(stochProcess, 1)
                .WithSteps(2000)
                .WithAntitheticVariate()
                .WithAbsoluteTolerance(tolerance)
                .ToEngine()(floating, RunnerContext).NPV!.Value;
            CheckMc("Floating", type, analytical, monteCarlo, tolerance);
        }
    }

    // The test's market: flat rates and volatility on Act/360 from the runner's date, the exercise at t.
    private static (Exercise Exercise, GeneralizedBlackScholesProcess Process) Market(LookbackOptionData value)
    {
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var exercise = new EuropeanExercise(today + TimeToDays(value.T));
        var process = new BlackScholesMertonProcess(
            value.S, FlatRate(today, value.Q, dc), FlatRate(today, value.R, dc), FlatVol(today, value.V, dc));
        return (exercise, process);
    }

    private static void Check(LookbackOptionData value, double calculated)
    {
        double error = Math.Abs(calculated - value.Result);
        Assert.True(
            error <= value.Tol,
            $"{value.Type} lookback option: expected {value.Result}, calculated {calculated}, error {error}, tolerance {value.Tol}");
    }

    private static void CheckMc(string lookbackType, OptionType type, double analytical, double monteCarlo, double tolerance)
    {
        double diff = Math.Abs(analytical - monteCarlo);
        Assert.True(
            diff <= tolerance,
            $"Analytical and MC {lookbackType} {type} values differed by more than tolerance: analytical {analytical}, "
            + $"Monte Carlo {monteCarlo}, tolerance {tolerance}, difference {diff}");
    }

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static int TimeToDays(double t, int daysPerYear = 360) =>
        (int)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    // The test suite's flatRate: a flat continuously compounded curve from the reference date.
    private static YieldTermStructure FlatRate(Date today, double forward, DayCounter dc) =>
        new FlatForward(today, forward, dc);

    // The test suite's flatVol: a constant volatility from the reference date, on the null calendar.
    private static BlackVolTermStructure FlatVol(Date today, double vol, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), vol, dc);

    private sealed record LookbackOptionData(
        OptionType Type,
        double Strike,
        double Minmax,
        double S, // spot
        double Q, // dividend
        double R, // risk-free rate
        double T, // time to maturity
        double V, // volatility

        // Partial-time lookback options:
        double L, // level above/below actual extremum
        double T1, // time to start of lookback period
        double Result, // result
        double Tol); // tolerance
}
