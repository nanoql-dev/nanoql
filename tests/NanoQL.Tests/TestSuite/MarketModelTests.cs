// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2012 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/marketmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testAbcdVolatilityIntegration, testAbcdVolatilityFit, testDriftCalculator, testAbcdDegenerateCases, testCovariance (S-mm); testOneStepForwardsAndOptionlets, testOneStepNormalForwardsAndOptionlets, testInverseFloater, testAllMultiStepProducts, testPeriodAdapter, testCallableSwapNaif, testCallableSwapLS, testCallableSwapAnderson, testGreeks, testPathwiseGreeks, testPathwiseVegas, testPathwiseMarketVegas, testStochVolForwardsAndOptionlets (S-mmp)
// Not ported: testAbcdVolatilityCompare (it checks AbcdFunction against the legacy LIBOR model's LmExtLinearExponentialVolModel, which isn't ported)

using NanoQL.Models;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class MarketModelTests
{
    [Fact]
    public void TestIsInSubset()
    {
        // Performance test for isInSubset function (temporary)
        // Testing isInSubset function...
        const int dim = 100;
        double[] set = new double[dim], subset = new double[dim];
        for (int i = 0; i < dim; i++)
        {
            set[i] = i * 1.0;
        }

        for (int i = 0; i < dim; i++)
        {
            subset[i] = dim + (i * 1.0);
        }

        // QuantLib's case prints the result when its report is asked for, and asserts nothing.
        _ = UtilitiesFunctions.IsInSubset(set, subset);
    }
}
