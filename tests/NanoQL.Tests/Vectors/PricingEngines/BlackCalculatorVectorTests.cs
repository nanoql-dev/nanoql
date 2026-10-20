// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BlackCalculator vectors, <c>tests/vectors/pricingengines/blackcalculator.json</c>: the value and every
/// sensitivity over four payoffs, both types, five strikes, three forwards and six standard deviations, with the
/// refusals; the outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackCalculatorVectorTests
{
    [Fact]
    public void BlackCalculatorVector()
    {
        var file = VectorFile.Load("pricingengines/blackcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            StrikedTypePayoff payoff = PayoffOf(c.Input("payoff"));
            double forward = c.Double("forward"), stdDev = c.Double("stdDev"), discount = c.Double("discount");
            if (c.Expected.TryGetProperty("deltaAtZeroSpot", out _))
            {
                var r = new BlackCalculator(payoff, forward, stdDev, discount);
                check.Output(c, "deltaAtZeroSpot", Lazy(() => r.Delta(0.0)));
                check.Output(c, "gammaAtZeroSpot", Lazy(() => r.Gamma(0.0)));
                check.Output(c, "vannaAtZeroSpot", Lazy(() => r.Vanna(0.0, 1.0)));
                check.Output(c, "thetaAtNegativeMaturity", Lazy(() => r.Theta(100.0, -0.1)));
                check.Output(c, "vegaAtNegativeMaturity", Lazy(() => r.Vega(-0.1)));
                check.Output(c, "rhoAtNegativeMaturity", Lazy(() => r.Rho(-0.1)));
                check.Output(c, "dividendRhoAtNegativeMaturity", Lazy(() => r.DividendRho(-0.1)));
                check.Output(c, "vannaAtNegativeMaturity", Lazy(() => r.Vanna(100.0, -0.1)));
                check.Output(c, "volgaAtNegativeMaturity", Lazy(() => r.Volga(-0.1)));
                continue;
            }

            if (!c.Inputs.TryGetProperty("spot", out _))
            {
                check.Output(c, "value", Lazy(() => new BlackCalculator(payoff, forward, stdDev, discount).Value));
                continue;
            }

            var calc = new BlackCalculator(payoff, forward, stdDev, discount);
            double spot = c.Double("spot");
            double[] maturities = c.Doubles("maturities");
            check.Output(c, "value", calc.Value);
            check.Output(c, "alpha", calc.Alpha);
            check.Output(c, "beta", calc.Beta);
            check.Output(c, "gammaForward", calc.GammaForward);
            check.Output(c, "gamma", calc.Gamma(spot));
            check.Output(c, "vega", maturities.Select(t => (object?)calc.Vega(t)).ToArray());
            check.Output(c, "strikeGamma", calc.StrikeGamma);
            if (c.Expected.TryGetProperty("delta", out _))
            {
                check.Output(c, "deltaForward", calc.DeltaForward);
                check.Output(c, "elasticityForward", calc.ElasticityForward);
                check.Output(c, "delta", calc.Delta(spot));
                check.Output(c, "elasticity", calc.Elasticity(spot));
                check.Output(c, "theta", maturities.Select(t => (object?)calc.Theta(spot, t)).ToArray());
                check.Output(c, "thetaPerDay", maturities.Select(t => (object?)calc.ThetaPerDay(spot, t)).ToArray());
                check.Output(c, "rho", maturities.Select(t => (object?)calc.Rho(t)).ToArray());
                check.Output(c, "dividendRho", maturities.Select(t => (object?)calc.DividendRho(t)).ToArray());
                check.Output(c, "strikeSensitivity", calc.StrikeSensitivity);
            }

            if (c.Expected.TryGetProperty("itmCashProbability", out _))
            {
                check.Output(c, "itmCashProbability", calc.ItmCashProbability);
                check.Output(c, "itmAssetProbability", calc.ItmAssetProbability);
            }

            if (c.Expected.TryGetProperty("vanna", out _))
            {
                check.Output(c, "vanna", maturities.Select(t => (object?)calc.Vanna(spot, t)).ToArray());
                check.Output(c, "volga", calc.Volga(1.0));
            }
        }

        check.Complete();
    }
}
