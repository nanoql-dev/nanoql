// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/volatilitymodels.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class VolatilityModelsTests
{
    [Fact]
    public void TestConstruction()
    {
        // Testing volatility model construction...
        var ts = new TimeSeries<Real>([new(25, March, 2005), new(29, March, 2005), new(15, March, 2005)], [1.2, 2.3, 0.3]);

        var sle = new SimpleLocalEstimator(1 / 360.0);
        TimeSeries<Volatility> locale = sle.Calculate(ts);

        var ce = new ConstantEstimator(1);
        TimeSeries<Volatility> sv = ce.Calculate(locale);
        _ = sv.First();
    }
}
