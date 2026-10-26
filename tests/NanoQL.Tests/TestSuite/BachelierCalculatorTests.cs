// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/bacheliercalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Moved: testBachelierCalculatorGreeks line 173 (Divergences/BachelierGreeksTests.cs); testBachelierCalculatorGreeks line 213 (Divergences/BachelierGreeksTests.cs)

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BachelierCalculatorTests
{
    [Fact]
    public void TestBachelierCalculatorBasicValues()
    {
        // Testing BachelierCalculator basic option values...
        BachelierCalculatorTestData[] values =
        [
            // type, strike, forward, stdDev (absolute), discount, tolerance
            new(OptionType.Call, 100.0, 100.0, 20.0, 1.0, 1e-8, 7.9788456080286538), // ATM Call
            new(OptionType.Put, 100.0, 100.0, 20.0, 1.0, 1e-8, 7.9788456080286538), // ATM Put
            new(OptionType.Call, 90.0, 100.0, 20.0, 1.0, 1e-8, 13.955931148026121), // ITM Call
            new(OptionType.Put, 110.0, 100.0, 20.0, 1.0, 1e-8, 13.955931148026121), // ITM Put
            new(OptionType.Call, 110.0, 100.0, 20.0, 1.0, 1e-8, 3.9559311480261217), // OTM Call
            new(OptionType.Put, 90.0, 100.0, 20.0, 1.0, 1e-8, 3.9559311480261217), // OTM Put
            new(OptionType.Call, 100.0, 100.0, 0.0, 1.0, 1e-8, 0.0), // Zero vol Call
            new(OptionType.Put, 100.0, 100.0, 0.0, 1.0, 1e-8, 0.0), // Zero vol Put
            new(OptionType.Call, 0.0, 100.0, 20.0, 1.0, 1e-8, 100.00000106923312), // Zero strike
        ];

        foreach (BachelierCalculatorTestData data in values)
        {
            // Test constructor with Option::Type
            var calc1 = new BachelierCalculator(data.Type, data.Strike, data.Forward, data.StdDev, data.Discount);
            Real value1 = calc1.Value;

            // Test constructor with Payoff
            var payoff = new PlainVanillaPayoff(data.Type, data.Strike);
            var calc2 = new BachelierCalculator(payoff, data.Forward, data.StdDev, data.Discount);
            Real value2 = calc2.Value;

            // Both constructors should give the same result
            Real error = Math.Abs(value1 - value2);
            Assert.False(
                error > data.Tolerance,
                $"BachelierCalculator constructor mismatch for {data.Type} strike={data.Strike} forward={data.Forward}"
                + $" stdDev={data.StdDev} discount={data.Discount} value1={value1} value2={value2} error={error}");

            Real error2 = Math.Abs(value1 - data.RefValue);
            Assert.False(
                error2 > data.Tolerance,
                $"BachelierCalculator constructor rf value error for {data.Type} strike={data.Strike} forward={data.Forward}"
                + $" stdDev={data.StdDev} discount={data.Discount} value1={value1} value2={value2} error={error}");

            // Basic sanity checks
            if (data.StdDev == 0.0)
            {
                // With zero volatility, option value should be intrinsic value
                Real intrinsic = data.Discount * Math.Max(
                    0.0, data.Type == OptionType.Call ? data.Forward - data.Strike : data.Strike - data.Forward);
                Assert.False(
                    Math.Abs(value1 - intrinsic) > data.Tolerance,
                    $"BachelierCalculator zero volatility test failed for {data.Type} expected={intrinsic} calculated={value1}");
            }

            // Option value should be non-negative
            Assert.False(value1 < -data.Tolerance, $"BachelierCalculator negative option value: {value1}");
        }
    }

    [Fact]
    public void TestBachelierCalculatorGreeks()
    {
        // Testing BachelierCalculator Greeks calculations...
        Real forward = 100.0;
        Real strike = 105.0;
        Real stdDev = 20.0; // Absolute volatility
        Real discount = 0.95;
        Real spot = 98.0;
        Real maturity = 1.0;
        Real tolerance = 1e-6;
        Real refDelta = 0.38900917408288;
        Real refDeltaFwd = 0.38122899060122245;
        Real refGamma = 0.019124047842706517;
        Real refGammaFwd = 0.018366735548135338;
        Real refVega = 0.36733471096270676;
        Real refRho = 32.682349793874224;
        Real refElasticity = 7.0071783554334042;
        Real refElasticityFwd = 7.0071783554334051;
        Real refItmCashProb = 0.4012936743170763;
        Real refItmAssetProb = 0.4012936743170763;
        Real refDividendRho = -38.122899060122243;
        Real refStrikeSensitivity = -0.38122899060122245;
        Real refStrikeGamma = 0.018366735548135338;
        Real refVolga = 0.0011479209717584586;

        var calc = new BachelierCalculator(OptionType.Call, strike, forward, stdDev, discount);

        Real delta = calc.Delta(spot);
        Real deltaForward = calc.DeltaForward;
        Real gamma = calc.Gamma(spot);
        Real gammaForward = calc.GammaForward;
        Real vega = calc.Vega(maturity);
        Real rho = calc.Rho(maturity);
        Real elasticity = calc.Elasticity(spot);
        Real elasticityForward = calc.ElasticityForward;
        Real itmCashProb = calc.ItmCashProbability;
        Real itmAssetProb = calc.ItmAssetProbability;
        Real dividendRho = calc.DividendRho(maturity);
        Real strikeSensitivity = calc.StrikeSensitivity;
        Real strikeGamma = calc.StrikeGamma;
        Real volga = calc.Volga(maturity);

        Assert.False(Math.Abs(deltaForward - refDeltaFwd) > tolerance, "BachelierCalculator call fwd delta error");
        Assert.False(Math.Abs(delta - refDelta) > tolerance, "BachelierCalculator call delta error");
        Assert.False(Math.Abs(gammaForward - refGammaFwd) > tolerance, "BachelierCalculator call fwd gamma error");
        Assert.False(Math.Abs(gamma - refGamma) > tolerance, "BachelierCalculator call gamma error");
        Assert.False(Math.Abs(vega - refVega) > tolerance, "BachelierCalculator call vega error");
        Assert.False(Math.Abs(rho - refRho) > tolerance, "BachelierCalculator call rho error");
        Assert.False(
            Math.Abs(elasticityForward - refElasticityFwd) > tolerance, "BachelierCalculator call fwd elasticity error");
        Assert.False(Math.Abs(elasticity - refElasticity) > tolerance, "BachelierCalculator call elasticity error");
        Assert.False(
            Math.Abs(itmCashProb - refItmCashProb) > tolerance, "BachelierCalculator call itm cash probability error");
        Assert.False(
            Math.Abs(itmAssetProb - refItmAssetProb) > tolerance, "BachelierCalculator call itm asset probability error");
        Assert.False(Math.Abs(dividendRho - refDividendRho) > tolerance, "BachelierCalculator call dividend rho error");
        Assert.False(
            Math.Abs(strikeSensitivity - refStrikeSensitivity) > tolerance,
            "BachelierCalculator call strike sensitivity error");
        Assert.False(Math.Abs(strikeGamma - refStrikeGamma) > tolerance, "BachelierCalculator call strike gamma error");
        Assert.False(Math.Abs(volga - refVolga) > tolerance, "BachelierCalculator call volga error");
    }

    [Fact]
    public void TestBachelierCalculatorPutCallParity()
    {
        // Testing BachelierCalculator put-call parity...
        Real forward = 100.0;
        Real strike = 105.0;
        Real stdDev = 25.0; // Absolute volatility
        Real discount = 0.95;
        Real tolerance = 1e-10;

        var callCalc = new BachelierCalculator(OptionType.Call, strike, forward, stdDev, discount);
        var putCalc = new BachelierCalculator(OptionType.Put, strike, forward, stdDev, discount);

        Real callValue = callCalc.Value;
        Real putValue = putCalc.Value;

        // Put-Call parity: C - P = discount * (F - K)
        Real paritylhs = callValue - putValue;
        Real parityrhs = discount * (forward - strike);
        Real parityError = Math.Abs(paritylhs - parityrhs);
        Assert.False(
            parityError > tolerance,
            $"BachelierCalculator put-call parity violation: C-P={paritylhs} discount*(F-K)={parityrhs} error={parityError}");
    }

    [Fact]
    public void TestBachelierCalculatorEdgeCases()
    {
        // Testing BachelierCalculator edge cases...
        Real tolerance = 1e-8;

        // Test zero volatility
        {
            var calc = new BachelierCalculator(OptionType.Call, 100.0, 100.0, 0.0, 1.0);
            Real value = calc.Value;
            Real refValue = 0.0;
            Assert.False(Math.Abs(value - refValue) > tolerance, $"BachelierCalculator failed for zero volatility: {value}");
        }

        // Test very high volatility
        {
            var calc = new BachelierCalculator(OptionType.Call, 100.0, 100.0, 200.0, 1.0);
            Real value = calc.Value;
            Real refValue = 79.788456080286537;
            Assert.False(
                Math.Abs(value - refValue) > tolerance, $"BachelierCalculator failed for very high volatility: {value}");
        }

        // Test negative strikes (valid in Bachelier model)
        {
            var calc = new BachelierCalculator(OptionType.Call, -50.0, 100.0, 20.0, 1.0);
            Real value = calc.Value;
            Real intrinsicValue = 100.0 - -50.0; // Should be close to intrinsic
            Assert.False(
                value < intrinsicValue - 10.0, // Allow some time value
                $"BachelierCalculator negative strike call unreasonable: {value} vs intrinsic {intrinsicValue}");
        }

        // Test negative forward (valid in Bachelier model)
        {
            var calc = new BachelierCalculator(OptionType.Call, 50.0, -100.0, 20.0, 1.0);
            Real value = calc.Value;
            Real intrinsicValue = -100.0 - 50.0; // Should be close to intrinsic
            Assert.False(
                value < intrinsicValue + 10.0, // Allow some time value
                $"BachelierCalculator negative strike call unreasonable: {value} vs intrinsic {intrinsicValue}");
        }

        // Test deep ITM call
        {
            var calc = new BachelierCalculator(OptionType.Call, 50.0, 100.0, 20.0, 1.0);
            Real value = calc.Value;
            Real intrinsicValue = 100.0 - 50.0; // Should be close to intrinsic
            Assert.False(
                value < intrinsicValue - tolerance, $"BachelierCalculator deep ITM call below intrinsic: {value} vs {intrinsicValue}");
        }

        // Test deep OTM call
        {
            var calc = new BachelierCalculator(OptionType.Call, 150.0, 100.0, 20.0, 1.0);
            Real value = calc.Value;
            Real refValue = 0.040082743582562863;
            Assert.False(Math.Abs(value - refValue) > tolerance, $"BachelierCalculator deep OTM call negative: {value}");
        }
    }

    [Fact]
    public void TestBachelierCalculatorNumericalDerivatives()
    {
        // Testing BachelierCalculator numerical derivative consistency...
        Real forward = 100.0;
        Real strike = 100.0;
        Real stdDev = 20.0; // Absolute volatility
        Real discount = 0.95;
        Real maturity = 1.0;
        Real bump = 1e-4;
        Real tolerance = 1e-3;

        var calc = new BachelierCalculator(OptionType.Call, strike, forward, stdDev, discount);

        // Test delta via finite differences
        var calcUp = new BachelierCalculator(OptionType.Call, strike, forward + bump, stdDev, discount);
        var calcDown = new BachelierCalculator(OptionType.Call, strike, forward - bump, stdDev, discount);

        Real analyticalDelta = calc.DeltaForward;
        Real numericalDelta = (calcUp.Value - calcDown.Value) / (2.0 * bump);
        Real deltaError = Math.Abs(analyticalDelta - numericalDelta);
        Assert.False(
            deltaError > tolerance,
            $"BachelierCalculator delta finite difference test failed: analytical={analyticalDelta} numerical={numericalDelta}"
            + $" error={deltaError}");

        // Test gamma via finite differences
        Real analyticalGamma = calc.GammaForward;
        Real numericalGamma = (calcUp.DeltaForward - calcDown.DeltaForward) / (2.0 * bump);
        Real gammaError = Math.Abs(analyticalGamma - numericalGamma);
        Assert.False(
            gammaError > tolerance,
            $"BachelierCalculator gamma finite difference test failed: analytical={analyticalGamma} numerical={numericalGamma}"
            + $" error={gammaError}");

        // Test vega via finite differences
        var calcVolUp = new BachelierCalculator(OptionType.Call, strike, forward, stdDev + bump, discount);
        var calcVolDown = new BachelierCalculator(OptionType.Call, strike, forward, stdDev - bump, discount);

        Real analyticalVega = calc.Vega(maturity);
        Real numericalVega = (calcVolUp.Value - calcVolDown.Value) / (2.0 * bump);
        Real vegaError = Math.Abs(analyticalVega - (numericalVega * Math.Sqrt(maturity)));
        Assert.False(
            vegaError > tolerance,
            $"BachelierCalculator vega finite difference test failed: analytical={analyticalVega}"
            + $" numerical={numericalVega * Math.Sqrt(maturity)} error={vegaError}");

        // --- Vanna and Volga tests ---
        // Analytical values; adapted: the vanna takes the spot, here the forward, where the sensitivity to the spot is
        // the sensitivity to the forward QuantLib's finite difference reads
        Real vanna = calc.Vanna(forward, maturity);
        Real volga = calc.Volga(maturity);

        // Finite difference for vanna: dVega/dForward
        var calcFwdUp = new BachelierCalculator(OptionType.Call, strike, forward + bump, stdDev, discount);
        var calcFwdDown = new BachelierCalculator(OptionType.Call, strike, forward - bump, stdDev, discount);
        Real vegaFwdUp = calcFwdUp.Vega(maturity);
        Real vegaFwdDown = calcFwdDown.Vega(maturity);
        Real vannaFD = (vegaFwdUp - vegaFwdDown) / (2.0 * bump);
        Assert.False(
            Math.Abs(vanna - vannaFD) > tolerance,
            $"BachelierCalculator call vanna error: analytical={vanna} finite-diff={vannaFD} error={vanna - vannaFD}");

        // Finite difference for volga: dVega/dVol
        Real vegaVolUp = calcVolUp.Vega(maturity);
        Real vegaVolDown = calcVolDown.Vega(maturity);
        Real volgaFD = (vegaVolUp - vegaVolDown) / (2.0 * bump);
        Assert.False(
            Math.Abs(volga - volgaFD) > tolerance,
            $"BachelierCalculator call volga error: analytical={volga} finite-diff={volgaFD} error={volga - volgaFD}");
    }

    [Fact]
    public void TestBachelierCalculatorAgainstAnalyticalFormula()
    {
        // Testing BachelierCalculator against analytical Bachelier formula...
        Real forward = 100.0;
        Real strike = 95.0;
        Real stdDev = 15.0; // Absolute volatility
        Real discount = 0.98;
        Real tolerance = 1e-10;

        var calc = new BachelierCalculator(OptionType.Call, strike, forward, stdDev, discount);
        Real calculatedValue = calc.Value;

        // Analytical Bachelier formula: C = (F-K)*N(d) + σ*n(d)
        // where d = (F-K)/σ
        Real d = (forward - strike) / stdDev;
        var n = new CumulativeNormalDistribution();
        var nd = new NormalDistribution();
        Real analyticalValue = discount * (((forward - strike) * n.Evaluate(d)) + (stdDev * nd.Evaluate(d)));

        Real error = Math.Abs(calculatedValue - analyticalValue);
        Assert.False(
            error > tolerance,
            $"BachelierCalculator analytical formula test failed: calculated={calculatedValue} analytical={analyticalValue}"
            + $" error={error}");
    }

    [Fact]
    public void TestBachelierCalculatorZeroVolatilityGreeks()
    {
        // Testing BachelierCalculator Greeks with zero volatility...
        Real tolerance = 1e-10;
        Real forward = 100.0;
        Real discount = 1.0;
        Real spot = 98.0;
        Real maturity = 1.0;
        Real stdDev = 0.0; // Zero absolute volatility

        // Test different moneyness scenarios
        ZeroVolTestCase[] testCases =
        [
            // ITM options should have delta = 1 for calls, -1 for puts (approximately)
            new(OptionType.Call, 90.0, "ITM Call"),
            new(OptionType.Put, 110.0, "ITM Put"),

            // ATM options in Bachelier model
            new(OptionType.Call, 100.0, "ATM Call"),
            new(OptionType.Put, 100.0, "ATM Put"),

            // OTM options should have delta = 0
            new(OptionType.Call, 90.0, "OTM Call"),
            new(OptionType.Put, 110.0, "OTM Put"),

            // Test negative strikes (valid in Bachelier model)
            new(OptionType.Call, -10.0, "Negative Strike Call"),
            new(OptionType.Put, 200.0, "High Strike Put"),
        ];

        foreach (ZeroVolTestCase testCase in testCases)
        {
            var calc = new BachelierCalculator(testCase.Type, testCase.Strike, forward, stdDev, discount);

            Real deltaForward = calc.DeltaForward;
            Real delta = calc.Delta(spot);
            Real gammaForward = calc.GammaForward;
            Real gamma = calc.Gamma(spot);
            Real vega = calc.Vega(maturity);
            Real theta = calc.Theta(spot, maturity);
            Real rho = calc.Rho(maturity);
            Real dividendRho = calc.DividendRho(maturity);

            // All Greeks should be finite (not NaN or infinite)
            Assert.False(
                !double.IsFinite(deltaForward) || !double.IsFinite(delta) || !double.IsFinite(gammaForward)
                || !double.IsFinite(gamma) || !double.IsFinite(vega) || !double.IsFinite(theta) || !double.IsFinite(rho)
                || !double.IsFinite(dividendRho),
                $"BachelierCalculator {testCase.Description} produced non-finite Greeks with zero volatility");

            // Gamma should be zero (no convexity with zero vol)
            Assert.False(
                Math.Abs(gammaForward) > tolerance || Math.Abs(gamma) > tolerance,
                $"BachelierCalculator {testCase.Description} gamma should be zero with zero volatility: "
                + $"gammaForward={gammaForward} gamma={gamma}");

            // Vega should be zero (no vol sensitivity)
            Assert.False(
                Math.Abs(vega) > tolerance,
                $"BachelierCalculator {testCase.Description} vega should be zero with zero volatility: {vega}");

            // For clearly ITM/OTM cases, check delta bounds
            if (testCase.Strike < forward - 5.0)
            {
                // Clearly ITM call
                Assert.False(
                    testCase.Type == OptionType.Call && (deltaForward < 0.99 || deltaForward > 1.01),
                    $"BachelierCalculator ITM call deltaForward should be ~1.0 with zero vol: {deltaForward}");
            }

            if (testCase.Strike > forward + 5.0)
            {
                // Clearly OTM call
                Assert.False(
                    testCase.Type == OptionType.Call && Math.Abs(deltaForward) > tolerance,
                    $"BachelierCalculator OTM call deltaForward should be ~0.0 with zero vol: {deltaForward}");
            }

            // Strike sensitivities should be finite
            Real strikeSens = calc.StrikeSensitivity;
            Real strikeGamma = calc.StrikeGamma;
            Assert.False(
                !double.IsFinite(strikeSens) || !double.IsFinite(strikeGamma),
                $"BachelierCalculator {testCase.Description} strike sensitivities should be finite with zero volatility");

            // Test ITM probabilities
            Real itmCashProb = calc.ItmCashProbability;
            Real itmAssetProb = calc.ItmAssetProbability;
            Assert.False(
                !double.IsFinite(itmCashProb) || !double.IsFinite(itmAssetProb),
                $"BachelierCalculator {testCase.Description} ITM probabilities should be finite with zero volatility");

            // In Bachelier model with zero vol, ITM probabilities should be 0 or 1
            Real expectedProb = (testCase.Type == OptionType.Call)
                ? (forward > testCase.Strike ? 1.0 : (forward == testCase.Strike ? 0.5 : 0.0))
                : (forward < testCase.Strike ? 1.0 : (forward == testCase.Strike ? 0.5 : 0.0));
            Assert.False(
                Math.Abs(itmCashProb - expectedProb) > tolerance,
                $"BachelierCalculator {testCase.Description} ITM cash probability incorrect with zero vol: expected="
                + $"{expectedProb} actual={itmCashProb}");

            // adapted: the vanna takes the spot
            Real vanna = calc.Vanna(spot, maturity);
            Real volga = calc.Volga(maturity);
            Assert.False(
                Math.Abs(vanna) > tolerance,
                $"BachelierCalculator {testCase.Description} vanna should be zero with zero volatility: {vanna}");
            Assert.False(
                Math.Abs(volga) > tolerance,
                $"BachelierCalculator {testCase.Description} volga should be zero with zero volatility: {volga}");
        }
    }

    [Fact]
    public void TestBachelierVsBlackConvergence()
    {
        // Testing BachelierCalculator convergence to BlackCalculator for small relative volatilities...
        Real forward = 100.0;
        Real strike = 100.0;
        Real relativeVol = 0.01; // 1% relative volatility
        Real absoluteVol = relativeVol * forward; // Convert to absolute
        Real discount = 1.0;
        Real tolerance = 1e-2; // Looser tolerance for convergence test

        var bachelierCalc = new BachelierCalculator(OptionType.Call, strike, forward, absoluteVol, discount);
        var blackCalc = new BlackCalculator(OptionType.Call, strike, forward, relativeVol, discount);

        Real bachelierValue = bachelierCalc.Value;
        Real blackValue = blackCalc.Value;

        // For small relative volatilities, Bachelier should approximate Black-Scholes
        Real error = Math.Abs(bachelierValue - blackValue);
        Real relativeError = error / blackValue;
        Assert.False(
            relativeError > tolerance,
            $"BachelierCalculator vs BlackCalculator convergence test failed: bachelier={bachelierValue} black={blackValue}"
            + $" relative error={relativeError}");
    }

    private readonly record struct BachelierCalculatorTestData(
        OptionType Type, Real Strike, Real Forward, Real StdDev, Real Discount, Real Tolerance, Real RefValue);

    private readonly record struct ZeroVolTestCase(OptionType Type, Real Strike, string Description);
}
