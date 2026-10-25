// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exponential fitting engine's node table is QuantLib's <c>values4</c>, generated from the pin into an embedded
/// resource and read once (the S-hes plan's HD15; G4 (6) regenerates it and compares the bytes): 147 rows of 129
/// doubles, each row's moneyness, 64 nodes and 64 weights, the moneyness increasing. QuantLib's first and last rows'
/// leading values, from <c>exponentialfittinghestonengine.cpp:34</c> and <c>:180</c>. Read through reflection, the
/// table being internal.
/// </summary>
public class ExponentialFittingTableTests
{
    [Fact]
    public void TheTableIsQuantLibs()
    {
        Type table = typeof(ExponentialFittingHestonEngine).Assembly.GetType(
            "NanoQL.PricingEngines.ExponentialFittingHestonEngineValues", throwOnError: true)!;
        var values = (double[][])table.GetProperty("Values", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.Equal(147, values.Length);
        Assert.All(values, row => Assert.Equal(129, row.Length));
        Assert.Equal([0.0, 234.8095791713261, 218.0318519353285], values[0][..3]);
        Assert.Equal([49.48416805066332, 3.660862802298221, 3.596993646112191], values[^1][..3]);
        for (int i = 1; i < values.Length; ++i)
        {
            Assert.True(values[i][0] > values[i - 1][0], $"moneyness not increasing at row {i}");
        }
    }
}
