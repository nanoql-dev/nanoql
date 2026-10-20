// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/blackcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BlackCalculatorTests
{
    [Fact]
    public void TestBlackCalculatorBasicValues()
    {
        // Testing BlackCalculator basic option values...

        (OptionType Type, Real Strike, Real Forward, Real StdDev, Real Discount, Real Tolerance, Real RefValue)[] values =
        [
            // type, strike, forward, stdDev, discount, tolerance
            (OptionType.Call, 100.0, 100.0, 0.20, 1.0, 1e-8, 7.9655674554058038), // ATM Call
            (OptionType.Put, 100.0, 100.0, 0.20, 1.0, 1e-8, 7.9655674554058038), // ATM Put
            (OptionType.Call, 90.0, 100.0, 0.20, 1.0, 1e-8, 13.589108116054803), // ITM Call
            (OptionType.Put, 110.0, 100.0, 0.20, 1.0, 1e-8, 14.292010941409899), // ITM Put
            (OptionType.Call, 110.0, 100.0, 0.20, 1.0, 1e-8, 4.2920109414098846), // OTM Call
            (OptionType.Put, 90.0, 100.0, 0.20, 1.0, 1e-8, 3.5891081160548062), // OTM Put
            (OptionType.Call, 100.0, 100.0, 0.0, 1.0, 1e-8, 0.0), // Zero vol Call
            (OptionType.Put, 100.0, 100.0, 0.0, 1.0, 1e-8, 0.0), // Zero vol Put
        ];

        foreach (var data in values)
        {
            // Test constructor with Option::Type
            var calc1 = new BlackCalculator(data.Type, data.Strike, data.Forward, data.StdDev, data.Discount);
            Real value1 = calc1.Value;

            // Test constructor with Payoff
            StrikedTypePayoff payoff = new PlainVanillaPayoff(data.Type, data.Strike);
            var calc2 = new BlackCalculator(payoff, data.Forward, data.StdDev, data.Discount);
            Real value2 = calc2.Value;

            // Both constructors should give the same result
            Real error = Math.Abs(value1 - value2);
            if (error > data.Tolerance)
            {
                Assert.Fail(
                    $"BlackCalculator constructor mismatch for {data.Type} strike={data.Strike} forward={data.Forward} "
                    + $"stdDev={data.StdDev} discount={data.Discount} value1={value1} value2={value2} error={error}");
            }

            Real error2 = Math.Abs(value1 - data.RefValue);
            if (error2 > data.Tolerance)
            {
                Assert.Fail(
                    $"BlackCalculator constructor rf value error for {data.Type} strike={data.Strike} "
                    + $"forward={data.Forward} stdDev={data.StdDev} discount={data.Discount} value1={value1} "
                    + $"value2={value2} error={error}");
            }

            // Basic sanity checks
            if (data.StdDev == 0.0)
            {
                // With zero volatility, option value should be intrinsic value
                Real intrinsic = data.Discount * Math.Max(
                    0.0, data.Type == OptionType.Call ? data.Forward - data.Strike : data.Strike - data.Forward);
                if (Math.Abs(value1 - intrinsic) > data.Tolerance)
                {
                    Assert.Fail(
                        $"BlackCalculator zero volatility test failed for {data.Type} expected={intrinsic} "
                        + $"calculated={value1}");
                }
            }

            // Option value should be non-negative
            if (value1 < -data.Tolerance)
            {
                Assert.Fail($"BlackCalculator negative option value: {value1}");
            }
        }
    }

    [Fact]
    public void TestBlackCalculatorGreeks()
    {
        // Testing BlackCalculator Greeks calculations...

        Real forward = 100.0;
        Real strike = 105.0;
        Real stdDev = 0.20;
        Real discount = 0.95;
        Real spot = 98.0;
        Real maturity = 1.0;
        Real tolerance = 1e-6;
        Real refDelta = 0.42921547913932068;
        Real refDeltaFwd = 0.42063116955653351;
        Real refGamma = 0.019527733248736884;
        Real refGammaFwd = 0.018754435012086908;
        Real refTheta = -4.31290436588883;
        Real refVega = 37.508870024173795;
        Real refRho = 36.452803157675653;
        Real refElasticity = 7.4974624362037323;
        Real refElasticityFwd = 7.4974624362037199;
        Real refItmCashProb = 0.36544163566592136;
        Real refItmAssetProb = 0.44276965216477238;
        Real refDividendRho = -42.063116955653371;
        Real refStrikeSensitivity = -0.34716955388262527;
        Real refStrikeGamma = 0.017010825407788574;
        Real refVanna = 0.65822482825836837;
        Real refVolga = 9.2856964243177753;

        var calc = new BlackCalculator(OptionType.Call, strike, forward, stdDev, discount);

        Real delta = calc.Delta(spot);
        Real deltaForward = calc.DeltaForward;
        Real gamma = calc.Gamma(spot);
        Real gammaForward = calc.GammaForward;
        Real theta = calc.Theta(spot, maturity);
        Real vega = calc.Vega(maturity);
        Real rho = calc.Rho(maturity);
        Real elasticity = calc.Elasticity(spot);
        Real elasticityForward = calc.ElasticityForward;
        Real itmCashProb = calc.ItmCashProbability;
        Real itmAssetProb = calc.ItmAssetProbability;
        Real dividendRho = calc.DividendRho(maturity);
        Real strikeSensitivity = calc.StrikeSensitivity;
        Real strikeGamma = calc.StrikeGamma;
        Real vanna = calc.Vanna(spot, maturity);
        Real volga = calc.Volga(maturity);

        Check(deltaForward, refDeltaFwd, "fwd delta");
        Check(delta, refDelta, "delta");
        Check(gammaForward, refGammaFwd, "fwd gamma");
        Check(gamma, refGamma, "gamma");
        Check(theta, refTheta, "theta");
        Check(vega, refVega, "vega");
        Check(rho, refRho, "rho");
        Check(elasticityForward, refElasticityFwd, "fwd elasticity");
        Check(elasticity, refElasticity, "elasticity");
        Check(itmCashProb, refItmCashProb, "itm cash probability");
        Check(itmAssetProb, refItmAssetProb, "itm asset probability");
        Check(dividendRho, refDividendRho, "dividend rho");
        Check(strikeSensitivity, refStrikeSensitivity, "strike sensitivity");
        Check(strikeGamma, refStrikeGamma, "strike gamma");
        Check(vanna, refVanna, "vanna");
        Check(volga, refVolga, "volga");

        void Check(Real calculated, Real expected, string name)
        {
            if (Math.Abs(calculated - expected) > tolerance)
            {
                Assert.Fail($"BlackCalculator call {name} error");
            }
        }
    }

    [Fact]
    public void TestBlackCalculatorPutCallParity()
    {
        // Testing BlackCalculator put-call parity...

        Real forward = 100.0;
        Real strike = 105.0;
        Real stdDev = 0.25;
        Real discount = 0.95;
        Real tolerance = 1e-10;

        var callCalc = new BlackCalculator(OptionType.Call, strike, forward, stdDev, discount);
        var putCalc = new BlackCalculator(OptionType.Put, strike, forward, stdDev, discount);

        Real callValue = callCalc.Value;
        Real putValue = putCalc.Value;

        // Put-Call parity: C - P = discount * (F - K)
        Real paritylhs = callValue - putValue;
        Real parityrhs = discount * (forward - strike);
        Real parityError = Math.Abs(paritylhs - parityrhs);

        if (parityError > tolerance)
        {
            Assert.Fail(
                $"BlackCalculator put-call parity violation: C-P={paritylhs} discount*(F-K)={parityrhs} "
                + $"error={parityError}");
        }
    }

    [Fact]
    public void TestBlackCalculatorEdgeCases()
    {
        // Testing BlackCalculator edge cases...

        Real tolerance = 1e-10;

        // Test zero volatility
        {
            var calc = new BlackCalculator(OptionType.Call, 100.0, 100.0, 0.0, 1.0);
            Real value = calc.Value;
            Real refValue = 0.0;
            if (Math.Abs(value - refValue) > tolerance)
            {
                Assert.Fail($"BlackCalculator failed for zero volatility: {value}");
            }
        }

        // Test very high volatility
        {
            var calc = new BlackCalculator(OptionType.Call, 100.0, 100.0, 2.0, 1.0);
            Real value = calc.Value;
            Real refValue = 68.268949213708595;
            if (Math.Abs(value - refValue) > tolerance)
            {
                Assert.Fail($"BlackCalculator failed for very high volatility: {value}");
            }
        }

        // Test deep ITM call
        {
            var calc = new BlackCalculator(OptionType.Call, 50.0, 100.0, 0.20, 1.0);
            Real value = calc.Value;
            Real intrinsicValue = 100.0 - 50.0; // Should be close to intrinsic
            if (value < intrinsicValue - tolerance)
            {
                Assert.Fail($"BlackCalculator deep ITM call below intrinsic: {value} vs {intrinsicValue}");
            }
        }

        // Test deep OTM call
        {
            var calc = new BlackCalculator(OptionType.Call, 150.0, 100.0, 0.20, 1.0);
            Real value = calc.Value;
            if (value < 0 || value > 10.0)
            {
                // Should be small positive value
                Assert.Fail($"BlackCalculator deep OTM call unreasonable: {value}");
            }
        }
    }

    [Fact]
    public void TestBlackCalculatorNumericalDerivatives()
    {
        // Testing BlackCalculator numerical derivative consistency...

        Real forward = 100.0;
        Real strike = 100.0;
        Real stdDev = 0.20;
        Real discount = 0.95;
        Real maturity = 1.0;
        Real bump = 1e-4;
        Real tolerance = 1e-3;

        var calc = new BlackCalculator(OptionType.Call, strike, forward, stdDev, discount);

        // Test delta via finite differences
        var calcUp = new BlackCalculator(OptionType.Call, strike, forward + bump, stdDev, discount);
        var calcDown = new BlackCalculator(OptionType.Call, strike, forward - bump, stdDev, discount);

        Real analyticalDelta = calc.DeltaForward;
        Real numericalDelta = (calcUp.Value - calcDown.Value) / (2.0 * bump);
        Real deltaError = Math.Abs(analyticalDelta - numericalDelta);

        if (deltaError > tolerance)
        {
            Assert.Fail(
                $"BlackCalculator delta finite difference test failed: analytical={analyticalDelta} "
                + $"numerical={numericalDelta} error={deltaError}");
        }

        // Test gamma via finite differences
        Real analyticalGamma = calc.GammaForward;
        Real numericalGamma = (calcUp.DeltaForward - calcDown.DeltaForward) / (2.0 * bump);
        Real gammaError = Math.Abs(analyticalGamma - numericalGamma);

        if (gammaError > tolerance)
        {
            Assert.Fail(
                $"BlackCalculator gamma finite difference test failed: analytical={analyticalGamma} "
                + $"numerical={numericalGamma} error={gammaError}");
        }

        Real vanna = calc.Vanna(forward, maturity);
        Real volga = calc.Volga(maturity);

        // Finite difference for vanna: dVega/dForward
        Real vegaFwdUp = calcUp.Vega(maturity);
        Real vegaFwdDown = calcDown.Vega(maturity);
        Real vannaFD = (vegaFwdUp - vegaFwdDown) / (2.0 * bump);

        if (Math.Abs(vanna - vannaFD) > tolerance)
        {
            Assert.Fail($"BlackCalculator call vanna error: analytical={vanna} finite-diff={vannaFD} error={vanna - vannaFD}");
        }

        // Finite difference for volga: dVega/dVol
        var calcVolUp = new BlackCalculator(OptionType.Call, strike, forward, stdDev + bump, discount);
        var calcVolDown = new BlackCalculator(OptionType.Call, strike, forward, stdDev - bump, discount);
        Real vegaVolUp = calcVolUp.Vega(maturity);
        Real vegaVolDown = calcVolDown.Vega(maturity);
        Real volgaFD = (vegaVolUp - vegaVolDown) / (2.0 * bump);

        if (Math.Abs(volga - volgaFD) > tolerance)
        {
            Assert.Fail($"BlackCalculator call volga error: analytical={volga} finite-diff={volgaFD} error={volga - volgaFD}");
        }
    }

    [Fact]
    public void TestBlackCalculatorZeroVolatilityGreeks()
    {
        // Testing BlackCalculator Greeks with zero volatility...

        Real tolerance = 1e-10;
        Real forward = 100.0;
        Real discount = 1.0;
        Real spot = 98.0;
        Real maturity = 1.0;
        Real stdDev = 0.0; // Zero volatility

        // Test different moneyness scenarios
        (OptionType Type, Real Strike, string Description, Real ExpectedDelta, Real ExpectedGamma, Real ExpectedVega,
            Real ExpectedTheta)[] testCases =
        [
            // ITM options should have delta = 1 for calls, -1 for puts (approximately)
            (OptionType.Call, 90.0, "ITM Call", 1.0, 0.0, 0.0, 0.0),
            (OptionType.Put, 110.0, "ITM Put", -1.0, 0.0, 0.0, 0.0),

            // ATM options have undefined behavior at zero vol, but should be finite
            (OptionType.Call, 100.0, "ATM Call", 0.5, 0.0, 0.0, 0.0),
            (OptionType.Put, 100.0, "ATM Put", -0.5, 0.0, 0.0, 0.0),

            // OTM options should have delta = 0
            (OptionType.Call, 110.0, "OTM Call", 0.0, 0.0, 0.0, 0.0),
            (OptionType.Put, 90.0, "OTM Put", 0.0, 0.0, 0.0, 0.0),
        ];

        foreach (var testCase in testCases)
        {
            var calc = new BlackCalculator(testCase.Type, testCase.Strike, forward, stdDev, discount);

            Real deltaForward = calc.DeltaForward;
            Real delta = calc.Delta(spot);
            Real gammaForward = calc.GammaForward;
            Real gamma = calc.Gamma(spot);
            Real vega = calc.Vega(maturity);
            Real theta = calc.Theta(spot, maturity);
            Real rho = calc.Rho(maturity);
            Real dividendRho = calc.DividendRho(maturity);

            // All Greeks should be finite (not NaN or infinite)
            if (!double.IsFinite(deltaForward) || !double.IsFinite(delta)
                || !double.IsFinite(gammaForward) || !double.IsFinite(gamma)
                || !double.IsFinite(vega) || !double.IsFinite(theta)
                || !double.IsFinite(rho) || !double.IsFinite(dividendRho))
            {
                Assert.Fail($"BlackCalculator {testCase.Description} produced non-finite Greeks with zero volatility");
            }

            // Gamma should be zero (no convexity with zero vol)
            if (Math.Abs(gammaForward) > tolerance || Math.Abs(gamma) > tolerance)
            {
                Assert.Fail(
                    $"BlackCalculator {testCase.Description} gamma should be zero with zero volatility: "
                    + $"gammaForward={gammaForward} gamma={gamma}");
            }

            // Vega should be zero (no vol sensitivity)
            if (Math.Abs(vega) > tolerance)
            {
                Assert.Fail($"BlackCalculator {testCase.Description} vega should be zero with zero volatility: {vega}");
            }

            // For clearly ITM/OTM cases, check delta bounds
            if (testCase.Strike < forward * 0.95)
            {
                // Clearly ITM call
                if (testCase.Type == OptionType.Call && (deltaForward < 0.99 || deltaForward > 1.01))
                {
                    Assert.Fail($"BlackCalculator ITM call deltaForward should be ~1.0 with zero vol: {deltaForward}");
                }
            }

            if (testCase.Strike > forward * 1.05)
            {
                // Clearly OTM call
                if (testCase.Type == OptionType.Call && Math.Abs(deltaForward) > tolerance)
                {
                    Assert.Fail($"BlackCalculator OTM call deltaForward should be ~0.0 with zero vol: {deltaForward}");
                }
            }

            // Strike sensitivities should be finite
            Real strikeSens = calc.StrikeSensitivity;
            Real strikeGamma = calc.StrikeGamma;

            if (!double.IsFinite(strikeSens) || !double.IsFinite(strikeGamma))
            {
                Assert.Fail(
                    $"BlackCalculator {testCase.Description} strike sensitivities should be finite with zero volatility");
            }

            Real vanna = calc.Vanna(spot, maturity);
            Real volga = calc.Volga(maturity);

            if (Math.Abs(vanna) > tolerance)
            {
                Assert.Fail($"BlackCalculator {testCase.Description} vanna should be zero with zero volatility: {vanna}");
            }

            if (Math.Abs(volga) > tolerance)
            {
                Assert.Fail($"BlackCalculator {testCase.Description} volga should be zero with zero volatility: {volga}");
            }
        }

        // Test specific edge case: very small but non-zero volatility to ensure it still works
        Real smallVol = 1e-12;
        var calcSmallVol = new BlackCalculator(OptionType.Call, 100.0, forward, smallVol, discount);

        Real deltaSmallVol = calcSmallVol.DeltaForward;
        Real gammaSmallVol = calcSmallVol.GammaForward;
        Real vegaSmallVol = calcSmallVol.Vega(maturity);

        // These should be finite and reasonable
        if (!double.IsFinite(deltaSmallVol) || !double.IsFinite(gammaSmallVol) || !double.IsFinite(vegaSmallVol))
        {
            Assert.Fail("BlackCalculator failed for very small volatility");
        }

        // Delta should be close to 0.5 for ATM
        if (Math.Abs(deltaSmallVol - (discount * 0.5)) > 0.1)
        {
            Assert.Fail($"BlackCalculator ATM delta with small vol unreasonable: {deltaSmallVol}");
        }
    }
}
