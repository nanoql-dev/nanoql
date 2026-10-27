// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The coterminal swap market model's drifts on a curve state set from the step's first alive rate (the S-mm plan's
/// MR3 (xvi), fixed under R8). QuantLib's <c>SMMDriftCalculator::compute</c> reads every annuity from the first rate
/// (<c>smmdriftcalculator.cpp:86-89</c>), so a state set from its first alive rate on, all the drifts read, is refused
/// ("invalid index"; the planner's MP24, and this run's <c>probes\c1_facts.cpp</c>). nanoql reads them from the first
/// alive rate. Check: the drifts on the same curve set from the first rate, QuantLib's arithmetic (its vectors), bit
/// for bit, and QuantLib's own numbers there.
/// </summary>
public class SmmDriftAliveTests
{
    [Fact]
    public void TheDriftsOfAStateSetFromTheFirstAliveRateAreThoseOfTheWholeCurve()
    {
        double[] rateTimes = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0];
        double[] sr = [0.03, 0.031, 0.032, 0.033, 0.034];
        var pseudo = new Matrix(5, 5, 0.1);
        for (int i = 0; i < 5; ++i)
        {
            pseudo[i, i] = 0.2;
        }

        var calculator = new SMMDriftCalculator(pseudo, new double[5], Enumerable.Repeat(0.5, 5).ToArray(), 5, 2);
        var alive = new CoterminalSwapCurveState(rateTimes);
        alive.SetOnCoterminalSwapRates(sr, 2);
        double[] fromAlive = new double[5];
        calculator.Compute(alive, fromAlive);

        var whole = new CoterminalSwapCurveState(rateTimes);
        whole.SetOnCoterminalSwapRates(sr);
        double[] fromFirst = new double[5];
        calculator.Compute(whole, fromFirst);

        Assert.Equal(fromFirst, fromAlive);

        // QuantLib's drifts on the curve set from the first rate (this run's probes\c1_facts.cpp)
        Assert.Equal([0.0, 0.0, -0.001160309682994728, -0.00058998512642538439, 0.0], fromFirst);
    }
}
