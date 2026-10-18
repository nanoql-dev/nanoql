// SPDX-FileCopyrightText: Copyright (C) 2013 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2015, 2024 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/blackformula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testBachelierImpliedVol, testChambersImpliedVol, testRadoicicStefanicaImpliedVol, testRadoicicStefanicaLowerBound, testImpliedVolAdaptiveSuccessiveOverRelaxation, testBlackFormulaForwardDerivative, testBlackFormulaForwardDerivativeWithZeroStrike, testBlackFormulaForwardDerivativeWithZeroVolatility, testBachelierBlackFormulaForwardDerivative, testBachelierBlackFormulaForwardDerivativeWithZeroVolatility, testItmProbabilitiesWithZeroVolatility, testBachelierImpliedVolAtIntrinsic, testImpliedStdDevApproximationRSStaysFinite, testImpliedStdDevLiRSAtIntrinsic, which need the Black formula's other functions (implied volatilities, derivatives, in-the-money probabilities) and wait for S6

using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BlackFormulaTests
{
    [Fact]
    public void TestDeepInTheMoneyPricesNotBelowIntrinsic()
    {
        // Testing that deep in-the-money Black and Bachelier prices are not below their intrinsic value...

        // With a small enough standard deviation the cumulative normal
        // saturates and the price has to reduce to the discounted intrinsic
        // value exactly; a result a few ulps below it is rejected by the
        // no-arbitrage checks of the implied-volatility inversions.
        (OptionType Type, Real Strike, Real Forward)[] cases =
        [
            (OptionType.Call, 50.0, 100.0), (OptionType.Put, 100.0, 50.0),
            (OptionType.Call, 0.03, 0.05), (OptionType.Put, 0.05, 0.03),
            (OptionType.Call, 1000.0, 1234.5), (OptionType.Put, 1234.5, 1000.0),
        ];

        foreach ((OptionType type, Real strike, Real forward) in cases)
        {
            Real theta = type == OptionType.Call ? 1.0 : -1.0;
            Real moneyness = theta * (forward - strike);
            foreach (Real discount in new[] { 1.0, 0.97 })
            {
                Real intrinsic = discount * Math.Max(moneyness, 0.0);

                // both d1 and d2 are above 20
                Real black = BlackFormula(type, strike, forward, 0.01, discount);
                if (black < intrinsic)
                {
                    Assert.Fail(
                        $"Black price for strike {strike}, forward {forward}, discount {discount} is below intrinsic by "
                        + $"{intrinsic - black}");
                }

                // (forward - strike)/stdDev is 20
                Real bachelier = BachelierBlackFormula(type, strike, forward, moneyness / 20.0, discount);
                if (bachelier < intrinsic)
                {
                    Assert.Fail(
                        $"Bachelier price for strike {strike}, forward {forward}, discount {discount} is below intrinsic "
                        + $"by {intrinsic - bachelier}");
                }
            }
        }
    }
}
