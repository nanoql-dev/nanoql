// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/rounding.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;
using static NanoQL.Numerics.ComparisonFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class RoundingTests
{
    private static readonly TestCase[] TestData =
    [
        new(0.86313513, 5, 0.86314, 0.86314, 0.86313, 0.86314, 0.86313),
        new(0.86313, 5, 0.86313, 0.86313, 0.86313, 0.86313, 0.86313),
        new(-7.64555346, 1, -7.6, -7.7, -7.6, -7.6, -7.6),
        new(0.13961605, 2, 0.14, 0.14, 0.13, 0.14, 0.13),
        new(0.14344179, 4, 0.1434, 0.1435, 0.1434, 0.1434, 0.1434),
        new(-4.74315016, 2, -4.74, -4.75, -4.74, -4.74, -4.74),
        new(-7.82772074, 5, -7.82772, -7.82773, -7.82772, -7.82772, -7.82772),
        new(2.74137947, 3, 2.741, 2.742, 2.741, 2.741, 2.741),
        new(2.13056714, 1, 2.1, 2.2, 2.1, 2.1, 2.1),
        new(-1.06228670, 1, -1.1, -1.1, -1.0, -1.0, -1.1),
        new(8.29234094, 4, 8.2923, 8.2924, 8.2923, 8.2923, 8.2923),
        new(7.90185598, 2, 7.90, 7.91, 7.90, 7.90, 7.90),
        new(-0.26738058, 1, -0.3, -0.3, -0.2, -0.2, -0.3),
        new(1.78128713, 1, 1.8, 1.8, 1.7, 1.8, 1.7),
        new(4.23537260, 1, 4.2, 4.3, 4.2, 4.2, 4.2),
        new(3.64369953, 4, 3.6437, 3.6437, 3.6436, 3.6437, 3.6436),
        new(6.34542470, 2, 6.35, 6.35, 6.34, 6.35, 6.34),
        new(-0.84754962, 4, -0.8475, -0.8476, -0.8475, -0.8475, -0.8475),
        new(4.60998652, 1, 4.6, 4.7, 4.6, 4.6, 4.6),
        new(6.28794223, 3, 6.288, 6.288, 6.287, 6.288, 6.287),
        new(7.89428221, 2, 7.89, 7.90, 7.89, 7.89, 7.89),
    ];

    [Fact]
    public void TestClosest()
    {
        // Testing closest decimal rounding...

        foreach (TestCase i in TestData)
        {
            Integer digits = i.Precision;
            var closest = new ClosestRounding(digits);
            Real calculated = closest.Evaluate(i.X);
            Real expected = i.Closest;
            if (!Close(calculated, expected, 1))
            {
                Assert.Fail(Message(i.X, digits, expected, calculated));
            }
        }
    }

    [Fact]
    public void TestUp()
    {
        // Testing upward decimal rounding...

        foreach (TestCase i in TestData)
        {
            Integer digits = i.Precision;
            var up = new UpRounding(digits);
            Real calculated = up.Evaluate(i.X);
            Real expected = i.Up;
            if (!Close(calculated, expected, 1))
            {
                Assert.Fail(Message(i.X, digits, expected, calculated));
            }
        }
    }

    [Fact]
    public void TestDown()
    {
        // Testing downward decimal rounding...

        foreach (TestCase i in TestData)
        {
            Integer digits = i.Precision;
            var down = new DownRounding(digits);
            Real calculated = down.Evaluate(i.X);
            Real expected = i.Down;
            if (!Close(calculated, expected, 1))
            {
                Assert.Fail(Message(i.X, digits, expected, calculated));
            }
        }
    }

    [Fact]
    public void TestFloor()
    {
        // Testing floor decimal rounding...

        foreach (TestCase i in TestData)
        {
            Integer digits = i.Precision;
            var floor = new FloorTruncation(digits);
            Real calculated = floor.Evaluate(i.X);
            Real expected = i.Floor;
            if (!Close(calculated, expected, 1))
            {
                Assert.Fail(Message(i.X, digits, expected, calculated));
            }
        }
    }

    [Fact]
    public void TestCeiling()
    {
        // Testing ceiling decimal rounding...

        foreach (TestCase i in TestData)
        {
            Integer digits = i.Precision;
            var ceiling = new CeilingTruncation(digits);
            Real calculated = ceiling.Evaluate(i.X);
            Real expected = i.Ceiling;
            if (!Close(calculated, expected, 1))
            {
                Assert.Fail(Message(i.X, digits, expected, calculated));
            }
        }
    }

    // std::fixed, with 8 decimals for the original number and the case's precision for the others.
    private static string Message(double x, Integer digits, Real expected, Real calculated) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Original number: {x:F8}\n"
            + $"Expected:        {expected.ToString("F" + digits, CultureInfo.InvariantCulture)}\n"
            + $"Calculated:      {calculated.ToString("F" + digits, CultureInfo.InvariantCulture)}");

    private readonly record struct TestCase(
        double X,
        Integer Precision,
        double Closest,
        double Up,
        double Down,
        double Floor,
        double Ceiling);
}
