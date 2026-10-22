// SPDX-FileCopyrightText: Copyright (C) 2022 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/svivolatility.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SviVolatilityTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestSviSmileSection()
    {
        // Testing SviSmileSection construction...

        Date today = Context.EvaluationDate;

        // Test time based constructor
        double tte = 11.0 / 365;
        double forward = 123.45;
        double a = -0.0666;
        double b = 0.229;
        double sigma = 0.337;
        double rho = 0.439;
        double m = 0.193;
        double[] sviParameters = [a, b, sigma, rho, m];

        // Compute the strike that yields x (log-moneyness) equal to m,
        // this simplifies the variance expression to a+b*sigma so we can test the correctness
        // against the input parameters
        double strike = forward * Math.Exp(m);

        var timeSection = new SviSmileSection(tte, forward, sviParameters);
        Assert.Equal(forward, timeSection.AtmLevel());
        CheckClose(timeSection.Variance(strike), a + (b * sigma), 1E-10);

        // Test date based constructor
        Date date = today + new Period(11, TimeUnit.Days);

        SviSmileSection dateSection = SviSmileSection.Create(date, forward, sviParameters, Context);
        Assert.Equal(forward, dateSection.AtmLevel());
        CheckClose(dateSection.Variance(strike), a + (b * sigma), 1E-10);
    }

    // BOOST_CHECK_CLOSE: the difference within the tolerance, in percent, of both values.
    private static void CheckClose(double a, double b, double percent)
    {
        double diff = Math.Abs(a - b);
        Assert.True(
            diff <= Math.Abs(a) * percent / 100.0 && diff <= Math.Abs(b) * percent / 100.0,
            $"difference between {a} and {b} exceeds {percent}%");
    }
}
