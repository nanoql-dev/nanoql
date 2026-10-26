// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BachelierCalculator vectors, <c>tests/vectors/pricingengines/bacheliercalculator.json</c>:
/// <see cref="BachelierCalculator"/>'s value and Greeks for plain-vanilla calls and puts on a price level and on a rate
/// with strikes below zero, and QuantLib's checks of its inputs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BachelierCalculatorVectorTests
{
    [Fact]
    public void BachelierCalculatorVector()
    {
        var file = VectorFile.Load("pricingengines/bacheliercalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("calculator");
            double spot = s.GetProperty("spot").GetDouble(), maturity = s.GetProperty("maturity").GetDouble();
            var calculator = new Lazy<BachelierCalculator>(() => new BachelierCalculator(
                new PlainVanillaPayoff((OptionType)s.GetProperty("type").GetInt32(), s.GetProperty("strike").GetDouble()),
                s.GetProperty("forward").GetDouble(),
                s.GetProperty("stdDev").GetDouble(),
                s.GetProperty("discount").GetDouble()));
            check.Output(c, "value", () => calculator.Value.Value);
            check.Output(c, "deltaForward", () => calculator.Value.DeltaForward);
            check.Output(c, "gammaForward", () => calculator.Value.GammaForward);
            check.Output(c, "elasticityForward", () => calculator.Value.ElasticityForward);
            check.Output(c, "itmCashProbability", () => calculator.Value.ItmCashProbability);
            check.Output(c, "itmAssetProbability", () => calculator.Value.ItmAssetProbability);
            check.Output(c, "strikeSensitivity", () => calculator.Value.StrikeSensitivity);
            check.Output(c, "strikeGamma", () => calculator.Value.StrikeGamma);
            check.Output(c, "alpha", () => calculator.Value.Alpha);
            check.Output(c, "beta", () => calculator.Value.Beta);
            check.Output(c, "delta", () => calculator.Value.Delta(spot));
            check.Output(c, "gamma", () => calculator.Value.Gamma(spot));
            check.Output(c, "elasticity", () => calculator.Value.Elasticity(spot));
            check.Output(c, "vega", () => calculator.Value.Vega(maturity));
            check.Output(c, "rho", () => calculator.Value.Rho(maturity));
            check.Output(c, "dividendRho", () => calculator.Value.DividendRho(maturity));
        }

        check.Complete();
    }
}
