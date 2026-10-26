// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BlackScholesCalculator vectors, <c>tests/vectors/cxx/pricingengines/blackscholescalculator.json</c>:
/// <see cref="BlackScholesCalculator"/>'s value and spot-based Greeks for plain-vanilla, cash-or-nothing,
/// asset-or-nothing and gap payoffs, against QuantLib's through the C++ harness, the plain vanillas through the
/// constructor taking their type and strike.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackScholesCalculatorCxxVectorTests
{
    [Fact]
    public void BlackScholesCalculatorCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/blackscholescalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement p = c.Input("payoff");
            var type = (OptionType)p[1].GetInt32();
            double strike = p[2].GetDouble(), extra = p[3].GetDouble();
            double spot = c.Double("spot"), growth = c.Double("growth"), stdDev = c.Double("stdDev");
            double discount = c.Double("discount");

            // a plain vanilla through the constructor taking its type and strike, QuantLib's own form of it
            BlackScholesCalculator bs = p[0].GetString() switch
            {
                "PlainVanilla" => new BlackScholesCalculator(type, strike, spot, growth, stdDev, discount),
                "CashOrNothing" => new BlackScholesCalculator(
                    new CashOrNothingPayoff(type, strike, extra), spot, growth, stdDev, discount),
                "AssetOrNothing" => new BlackScholesCalculator(
                    new AssetOrNothingPayoff(type, strike), spot, growth, stdDev, discount),
                _ => new BlackScholesCalculator(new GapPayoff(type, strike, extra), spot, growth, stdDev, discount),
            };
            double t = c.Double("maturity");
            check.Output(c, "value", () => bs.Value);
            check.Output(c, "delta", () => bs.Delta());
            check.Output(c, "elasticity", () => bs.Elasticity());
            check.Output(c, "gamma", () => bs.Gamma());
            check.Output(c, "theta", () => bs.Theta(t));
            check.Output(c, "thetaPerDay", () => bs.ThetaPerDay(t));
        }

        check.Complete();
    }
}
