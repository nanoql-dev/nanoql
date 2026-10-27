// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/curvestates.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CurveStatesTests
{
    [Fact]
    public void TestLMMCurveState()
    {
        // Testing Libor-market-model curve state...
        var vars = new CommonVars();

        var lmmDriftcalculator = new LMMDriftCalculator(vars.Pseudo, vars.Displacements, vars.Taus, vars.Numeraire, vars.FirstAliveRates[0]);
        var lmmCs = new LMMCurveState(vars.RateTimes);
        lmmCs.SetOnForwardRates(vars.TodaysForwards);

        double[] lmmDrifts = new double[vars.N];
        lmmDriftcalculator.Compute(lmmCs, lmmDrifts);

        for (int i = 0; i < vars.N; ++i)
        {
            Assert.True(Math.Abs(lmmDrifts[i] - CommonVars.ExpectedDrifts[i]) <= CommonVars.Tol, $"LMM drifts mismatched: {lmmDrifts[i]}\t\t{CommonVars.ExpectedDrifts[i]}");
            Assert.True(
                Math.Abs(lmmCs.DiscountRatio(i, vars.N) - CommonVars.ExpectedDiscountRatios[i]) <= CommonVars.Tol,
                $"LMM discount ratio mismatch: {lmmCs.DiscountRatio(i, vars.N)}\t\t{CommonVars.ExpectedDiscountRatios[i]}");
            Assert.True(
                Math.Abs(lmmCs.ForwardRate(i) - CommonVars.ExpectedForwardRates[i]) <= CommonVars.Tol,
                $"LMM forward rate mismatch: {lmmCs.ForwardRate(i)}\t\t{CommonVars.ExpectedForwardRates[i]}");
        }
    }

    [Fact]
    public void TestCoterminalSwapCurveState()
    {
        // Testing coterminal-swap-market-model curve state...
        var vars = new CommonVars();

        // Coterminal swap rates & annuities
        double[] todaysCoterminalSwapRates = new double[vars.N];
        double[] coterminalAnnuity = new double[vars.N];
        double floatingLeg = 0.0;
        for (int i = 1; i <= vars.N; ++i)
        {
            if (i == 1)
            {
                coterminalAnnuity[vars.N - 1] = vars.Accruals[vars.N - 1] * vars.TodaysDiscounts[vars.N];
            }
            else
            {
                coterminalAnnuity[vars.N - i] = coterminalAnnuity[vars.N - i + 1] + (vars.Accruals[vars.N - i] * vars.TodaysDiscounts[vars.N - i + 1]);
            }

            floatingLeg = vars.TodaysDiscounts[vars.N - i] - vars.TodaysDiscounts[vars.N];
            todaysCoterminalSwapRates[vars.N - i] = floatingLeg / coterminalAnnuity[vars.N - i];
        }

        double[] evolutionTimes = vars.RateTimes[..^1];
        var evolution = new EvolutionDescription(vars.RateTimes, evolutionTimes);
        double[] taus = [.. evolution.RateTaus];

        var smmDriftcalculator = new SMMDriftCalculator(vars.Pseudo, vars.Displacements, taus, vars.Numeraire, vars.FirstAliveRates[0]);
        var cotCs = new CoterminalSwapCurveState(vars.RateTimes);
        cotCs.SetOnCoterminalSwapRates(todaysCoterminalSwapRates);

        double[] cotDrifts = new double[vars.N];
        smmDriftcalculator.Compute(cotCs, cotDrifts);

        for (int i = 0; i < vars.N; ++i)
        {
            Assert.True(Math.Abs(cotDrifts[i] - CommonVars.ExpectedCotDrifts[i]) <= CommonVars.Tol, $"COT drifts mismatched: {cotDrifts[i]}\t\t{CommonVars.ExpectedCotDrifts[i]}");
            Assert.True(
                Math.Abs(cotCs.DiscountRatio(i, vars.N) - CommonVars.ExpectedCotDiscountRatios[i]) <= CommonVars.Tol,
                $"COT discount ratio mismatch: {cotCs.DiscountRatio(i, vars.N)}\t\t{CommonVars.ExpectedCotDiscountRatios[i]}");
            Assert.True(
                Math.Abs(cotCs.ForwardRate(i) - CommonVars.ExpectedForwardRates[i]) <= CommonVars.Tol,
                $"COT forward rate mismatch: {cotCs.ForwardRate(i)}\t\t{CommonVars.ExpectedForwardRates[i]}");

            // Swap rate should be the same as Forward Rates
            Assert.True(
                Math.Abs(cotCs.CoterminalSwapRate(i) - todaysCoterminalSwapRates[i]) <= CommonVars.Tol,
                $"COT swap rate mismatch: {cotCs.CoterminalSwapRate(i)}\t\t{todaysCoterminalSwapRates[i]}");
            Assert.True(
                Math.Abs(cotCs.CoterminalSwapAnnuity(vars.Numeraire, i) - CommonVars.ExpectedCotSwapAnnuity[i]) <= CommonVars.Tol,
                $"COT swap annunity mismatch: {cotCs.CoterminalSwapAnnuity(vars.Numeraire, i)}\t\t{CommonVars.ExpectedCotSwapAnnuity[i]}");
        }
    }

    [Fact]
    public void TestCMSwapCurveState()
    {
        // Testing constant-maturity-swap-market-model curve state...
        var vars = new CommonVars();

        var cmsDriftcalculator = new CMSMMDriftCalculator(
            vars.Pseudo, vars.Displacements, vars.Taus, vars.Numeraire, vars.FirstAliveRates[0], CommonVars.SpanningFwds);

        var cmsCs = new CMSwapCurveState(vars.RateTimes, CommonVars.SpanningFwds);
        cmsCs.SetOnCMSwapRates(vars.TodaysForwards);
        double[] cmsDrifts = new double[vars.N];
        cmsDriftcalculator.Compute(cmsCs, cmsDrifts);

        for (int i = 0; i < vars.N; ++i)
        {
            Assert.True(Math.Abs(cmsDrifts[i] - CommonVars.ExpectedDrifts[i]) <= CommonVars.Tol, $"CMS drifts mismatched: {cmsDrifts[i]}\t\t{CommonVars.ExpectedDrifts[i]}");
            Assert.True(
                Math.Abs(cmsCs.DiscountRatio(i, vars.N) - CommonVars.ExpectedDiscountRatios[i]) <= CommonVars.Tol,
                $"CMS discount ratio mismatch: {cmsCs.DiscountRatio(i, vars.N)}\t\t{CommonVars.ExpectedDiscountRatios[i]}");
            Assert.True(
                Math.Abs(cmsCs.ForwardRate(i) - CommonVars.ExpectedForwardRates[i]) <= CommonVars.Tol,
                $"CMS forward rate mismatch: {cmsCs.ForwardRate(i)}\t\t{CommonVars.ExpectedForwardRates[i]}");

            // Swap rate should be the same as Forward Rates
            Assert.True(
                Math.Abs(cmsCs.CmSwapRate(i, CommonVars.SpanningFwds) - CommonVars.ExpectedForwardRates[i]) <= CommonVars.Tol,
                $"CMS swap rate mismatch: {cmsCs.CmSwapRate(i, CommonVars.SpanningFwds)}\t\t{CommonVars.ExpectedForwardRates[i]}");
            Assert.True(
                Math.Abs(cmsCs.CmSwapAnnuity(vars.Numeraire, i, CommonVars.SpanningFwds) - CommonVars.ExpectedSwapAnnuity[i]) <= CommonVars.Tol,
                $"CMS swap annunity mismatch: {cmsCs.CmSwapAnnuity(vars.Numeraire, i, CommonVars.SpanningFwds)}\t\t{CommonVars.ExpectedSwapAnnuity[i]}");
        }
    }

    private sealed class CommonVars
    {
        public const double Tol = 1.0e-4;
        public const int SpanningFwds = 1;

        public static readonly double[] ExpectedDrifts =
        [
            -0.0825792, -0.0787625, -0.0748546, -0.0708555, -0.0667655, -0.0625846, -0.0583128,
            -0.0539504, -0.0494972, -0.0449536, -0.0403194, -0.0355949, -0.0307801, -0.025875,
            -0.0208799, -0.0157948, -0.0106197, -0.00535471, 0,
        ];

        public static readonly double[] ExpectedDiscountRatios =
        [
            1.58379, 1.55274, 1.52154, 1.49025, 1.45888, 1.42748, 1.39607, 1.36468, 1.33335, 1.3021,
            1.27096, 1.23996, 1.20913, 1.17848, 1.14806, 1.11788, 1.08796, 1.05833, 1.029,
        ];

        public static readonly double[] ExpectedForwardRates =
        [
            0.04, 0.041, 0.042, 0.043, 0.044, 0.045, 0.046, 0.047, 0.048, 0.049,
            0.05, 0.051, 0.052, 0.053, 0.054, 0.055, 0.056, 0.057, 0.058,
        ];

        public static readonly double[] ExpectedSwapAnnuity =
        [
            0.776368, 0.760772, 0.745125, 0.729442, 0.713739, 0.698034, 0.68234,
            0.666673, 0.651048, 0.635479, 0.619979, 0.604563, 0.589242, 0.574031,
            0.558939, 0.54398, 0.529163, 0.5145, 0.5,
        ];

        public static readonly double[] ExpectedCotDrifts =
        [
            -0.0472372, -0.0447452, -0.042233, -0.0397016, -0.0371516, -0.034584, -0.0319995, -0.0293991,
            -0.0267836, -0.0241539, -0.0215109, -0.0188555, -0.0161887, -0.0135113, -0.0108244,
            -0.00812878, -0.00542554, -0.00271562, 0,
        ];

        public static readonly double[] ExpectedCotDiscountRatios =
        [
            1.58379, 1.55274, 1.52154, 1.49025, 1.45888, 1.42748, 1.39607, 1.36468, 1.33335, 1.3021,
            1.27096, 1.23996, 1.20913, 1.17848, 1.14806, 1.11788, 1.08796, 1.05833, 1.029,
        ];

        public static readonly double[] ExpectedCotSwapAnnuity =
        [
            12.0934, 11.317, 10.5563, 9.81115, 9.08171, 8.36797, 7.66994, 6.9876, 6.32092, 5.66988,
            5.0344, 4.41442, 3.80986, 3.22061, 2.64658, 2.08764, 1.54366, 1.0145, 0.5,
        ];

        public CommonVars()
        {
            // Times
            Calendar calendar = new NullCalendar();
            var context = new EvaluationContext { EvaluationDate = new Date(16, Month.September, 2015) };
            Date todaysDate = context.EvaluationDate;
            Date endDate = todaysDate + new Period(10, TimeUnit.Years);
            var dates = new Schedule(
                todaysDate,
                endDate,
                new Period(Frequency.Semiannual),
                calendar,
                BusinessDayConvention.Following,
                BusinessDayConvention.Following,
                DateGenerationRule.Backward,
                false,
                context);
            RateTimes = new double[dates.Size - 1];

            // init
            N = RateTimes.Length - 1;
            Accruals = new double[N];
            Numeraire = N;
            Pseudo = new Matrix(N, N, 0.1);

            DayCounter dayCounter = new SimpleDayCounter();
            for (int i = 1; i < dates.Size; ++i)
            {
                RateTimes[i - 1] = dayCounter.YearFraction(todaysDate, dates[i]);
            }

            for (int i = 1; i < RateTimes.Length; ++i)
            {
                Accruals[i - 1] = RateTimes[i] - RateTimes[i - 1];
            }

            // Rates & displacement
            TodaysForwards = new double[N];
            Displacements = new double[N];
            for (int i = 0; i < TodaysForwards.Length; ++i)
            {
                TodaysForwards[i] = 0.04 + (0.0010 * i);
            }

            // Discounts
            TodaysDiscounts = new double[RateTimes.Length];
            TodaysDiscounts[0] = 0.95;
            for (int i = 1; i < RateTimes.Length; ++i)
            {
                TodaysDiscounts[i] = TodaysDiscounts[i - 1] / (1.0 + (TodaysForwards[i - 1] * Accruals[i - 1]));
            }

            // taus & first alive rates
            double[] evolutionTimes = RateTimes[..^1];
            var evolution = new EvolutionDescription(RateTimes, evolutionTimes);
            Taus = [.. evolution.RateTaus];
            FirstAliveRates = [.. evolution.FirstAliveRate];
        }

        public double[] RateTimes { get; }

        public double[] Taus { get; }

        public double[] Accruals { get; }

        public double[] TodaysForwards { get; }

        public double[] Displacements { get; }

        public double[] TodaysDiscounts { get; }

        public int N { get; }

        public int Numeraire { get; }

        public Matrix Pseudo { get; }

        public int[] FirstAliveRates { get; }
    }
}
