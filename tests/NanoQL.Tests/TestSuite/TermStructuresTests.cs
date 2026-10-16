// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/termstructures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testReferenceChange, testImplied, testImpliedObs, testFSpreaded, testFSpreadedObs, testZSpreaded, testZSpreadedObs, testCreateWithNullUnderlying, testLinearInterpolationSpreadedForwardRate, testForwardFlatInterpolationSpreadedForwardRate, testBackwardFlatInterpolationSpreadedForwardRate, testBackwardFlatInterpolationZeroRate, testLinkToNullUnderlying, testCompositeZeroYieldStructures

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class TermStructuresTests
{
    [Fact]
    public void TestNullTimeToReference()
    {
        // Testing zero-rate calculation for null time-to-reference...

        Rate rate = 0.02;
        var dayCount = new Thirty360(Thirty360Convention.BondBasis);
        var curve = new FlatForward(new Date(30, Month.August, 2023), rate, dayCount);

        // the time between August 30th and 31st is null for the 30/360 day count convention
        Rate expected = rate;
        Rate calculated = curve.ZeroRate(new Date(31, Month.August, 2023), dayCount, Compounding.Continuous).Rate;
        Real tolerance = 1.0e-10;

        if (Math.Abs(calculated - expected) > tolerance)
        {
            Assert.Fail(
                "unable to reproduce zero yield rate from curve\n"
                + $"    calculated: {calculated:F10}\n"
                + $"    expected:   {expected:F10}");
        }
    }
}
