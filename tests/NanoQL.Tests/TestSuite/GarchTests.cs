// SPDX-FileCopyrightText: Copyright (C) 2012 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/garch.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class GarchTests
{
    private static readonly Real[] ExpectedCalc =
    [
        0.452769, 0.513323, 0.530141, 0.5350841, 0.536558, 0.536999, 0.537132, 0.537171, 0.537183, 0.537187,
    ];

    // Adapted: nanoql's Garch11 is immutable, so each calibration's new model replaces the variable, as QuantLib's
    // calibrate changes the object; and the series is built from its dates and values, as nanoql's is immutable.
    [Fact]
    public void TestCalibration()
    {
        // Testing GARCH model calibration...
        var start = new Date(7, July, 1962);
        Date d = start;
        var garch = new Garch11(0.2, 0.3, 0.4);
        var rng = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(new MersenneTwisterUniformRng(48));

        Volatility r = 0.0, v = 0.0;
        var dates = new Date[50000];
        var values = new Volatility[50000];
        for (int i = 0; i < 50000; ++i, d += 1)
        {
            v = garch.Forecast(r, v);
            r = rng.Next().Value * Math.Sqrt(v);
            dates[i] = d;
            values[i] = r;
        }

        var ts = new TimeSeries<Volatility>(dates, values);

        // Default calibration; works fine in most cases
        var cgarch1 = new Garch11(ts);

        var calibrated = new Results(0.207592, 0.281979, 0.204647, -0.0217413);
        Check(calibrated, cgarch1);

        // Type 1 initial guess - no further optimization
        var cgarch2 = new Garch11(ts, Garch11Mode.MomentMatchingGuess);
        var m = new DummyOptimizationMethod();
        cgarch2 = cgarch2.Calibrate(ts, m, new EndCriteria(3, 2, 0.0, 0.0, 0.0));
        var expected1 = new Results(0.265749, 0.156956, 0.230964, -0.0227179);
        Check(expected1, cgarch2);

        // Optimization from this initial guess
        cgarch2 = cgarch2.Calibrate(ts);
        Check(calibrated, cgarch2);

        // Type 2 initial guess - no further optimization
        var cgarch3 = new Garch11(ts, Garch11Mode.GammaGuess);
        cgarch3 = cgarch3.Calibrate(ts, m, new EndCriteria(3, 2, 0.0, 0.0, 0.0));
        var expected2 = new Results(0.269896, 0.211373, 0.207534, -0.022798);
        Check(expected2, cgarch3);

        // Optimization from this initial guess
        cgarch3 = cgarch3.Calibrate(ts);
        Check(calibrated, cgarch3);

        // Double optimization using type 1 and 2 initial guesses
        var cgarch4 = new Garch11(ts, Garch11Mode.DoubleOptimization);
        cgarch4 = cgarch4.Calibrate(ts);
        Check(calibrated, cgarch4);

        // Alternative, gradient based optimization - usually gives worse results than simplex
        var lm = new LevenbergMarquardt();
        cgarch4 = cgarch4.Calibrate(ts, lm, new EndCriteria(100000, 500, 1e-8, 1e-8, 1e-8));
        var expected3 = new Results(0.265196, 0.277364, 0.678812, -0.216313);
        Check(expected3, cgarch4);
    }

    [Fact]
    public void TestCalculation()
    {
        // Testing GARCH model calculation...
        var d = new Date(7, July, 1962);
        var garch = new Garch11(0.2, 0.3, 0.4);

        Volatility r = 0.1;
        var dates = new Date[10];
        var values = new Volatility[10];
        for (int i = 0; i < 10; ++i, d += 1)
        {
            dates[i] = d;
            values[i] = r;
        }

        var ts = new TimeSeries<Volatility>(dates, values);
        TimeSeries<Volatility> tsout = garch.Calculate(ts);
        foreach (KeyValuePair<Date, Volatility> x in tsout)
        {
            CheckTs(x);
        }
    }

    private static void CheckTs(KeyValuePair<Date, Volatility> x)
    {
        if (x.Key.SerialNumber < 22835 || x.Key.SerialNumber > 22844)
        {
            Assert.Fail($"Failed to reproduce calculated GARCH time: \n    calculated: {x.Key.SerialNumber}\n    expected:   [22835, 22844]");
        }

        Real error = Math.Abs(x.Value - ExpectedCalc[x.Key.SerialNumber - 22835]);
        if (error > 1.0e-6)
        {
            Assert.Fail(
                $"Failed to reproduce calculated GARCH value at {x.Key.SerialNumber}: \n    calculated: {x.Value}\n    expected:   {ExpectedCalc[x.Key.SerialNumber - 22835]}");
        }
    }

    private static void Check(Results results, Garch11 garch)
    {
        Member(results.Alpha, garch.Alpha, "alpha");
        Member(results.Beta, garch.Beta, "beta");
        Member(results.Omega, garch.Omega, "omega");
        Member(results.LogLikelihood, garch.LogLikelihood, "logLikelihood");
    }

    private static void Member(Real expected, Real calculated, string member)
    {
        if (Math.Abs(expected - calculated) > 1.0e-6)
        {
            Assert.Fail($"Failed to reproduce expected {member}\n    calculated: {calculated}\n    expected:   {expected}");
        }
    }

    private sealed record Results(Real Alpha, Real Beta, Real Omega, Real LogLikelihood);

    private sealed class DummyOptimizationMethod : OptimizationMethod
    {
        protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria)
        {
            P.FunctionValue = P.Value(P.CurrentValue);
            return EndCriteriaType.None;
        }
    }
}
