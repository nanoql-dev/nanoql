// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="BlackCalculator"/>'s vanna and volga. QuantLib's volga, vega × d1 × d2 / (σ√T), divides by the standard
/// deviation where the derivative in σ divides by σ (<c>blackcalculator.cpp:511-523</c>), so it is the volga over √T:
/// against the difference of QuantLib's own vega in σ, a call struck at 100 on a forward of 104 at 25% reads half of it
/// at T = 0.25 and twice it at T = 4. And QuantLib's vanna and volga use the plain vanilla's closed form, through d1
/// and d2, for every payoff (<c>:494-523</c>): a cash-or-nothing call reads a vanna of 0.0054 where its vega's
/// difference in the spot is −0.60, and a volga of −0.15 where it is 18.9. nanoql multiplies the volga by √T, and for
/// payoffs other than the plain vanilla differentiates QuantLib's vega through the payoff's alpha and beta; the plain
/// vanilla's are QuantLib's formulas, and QuantLib's own check is its vega's differences.
/// </summary>
public class BlackCalculatorVannaVolgaTests
{
    [Fact]
    public void TheVolgaIsTheVegasDerivativeInTheVolatility()
    {
        const double sigma = 0.25, h = 1e-4;
        foreach (double maturity in new[] { 0.25, 1.0, 4.0 })
        {
            var calc = new BlackCalculator(OptionType.Call, 100.0, 104.0, sigma * Math.Sqrt(maturity));
            double up = new BlackCalculator(OptionType.Call, 100.0, 104.0, (sigma + h) * Math.Sqrt(maturity)).Vega(maturity);
            double down = new BlackCalculator(OptionType.Call, 100.0, 104.0, (sigma - h) * Math.Sqrt(maturity)).Vega(maturity);
            double difference = (up - down) / (2 * h);

            Assert.Equal(difference, calc.Volga(maturity), 1e-6 * Math.Max(1.0, Math.Abs(difference)));
        }
    }

    [Fact]
    public void ADigitalsVannaAndVolgaAreItsVegasDerivatives()
    {
        StrikedTypePayoff[] payoffs =
        [
            new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0),
            new CashOrNothingPayoff(OptionType.Put, 120.0, 10.0),
            new AssetOrNothingPayoff(OptionType.Call, 95.0),
            new AssetOrNothingPayoff(OptionType.Put, 105.0),
            new GapPayoff(OptionType.Call, 100.0, 90.0),
            new GapPayoff(OptionType.Put, 100.0, 110.0),
        ];
        foreach (StrikedTypePayoff payoff in payoffs)
        {
            foreach (double maturity in new[] { 0.25, 1.0, 4.0 })
            {
                const double sigma = 0.25, spot = 100.0, growth = 1.04, discount = 0.95;
                double sd = sigma * Math.Sqrt(maturity);
                var calc = new BlackCalculator(payoff, spot * growth, sd, discount);
                double Vega(double s, double v) => new BlackCalculator(payoff, s * growth, v * Math.Sqrt(maturity), discount).Vega(maturity);
                double vanna = Richardson(s => Vega(s, sigma), spot, 0.4);
                double volga = Richardson(v => Vega(spot, v), sigma, 2e-3);

                Assert.Equal(vanna, calc.Vanna(spot, maturity), 1e-7 * Math.Max(1.0, Math.Abs(vanna)));
                Assert.Equal(volga, calc.Volga(maturity), 1e-7 * Math.Max(1.0, Math.Abs(volga)));
            }
        }
    }

    // A central difference with one Richardson step, as the general formulas were measured against.
    private static double Richardson(Func<double, double> f, double x, double h)
    {
        double d1 = (f(x + h) - f(x - h)) / (2 * h);
        double d2 = (f(x + (h / 2)) - f(x - (h / 2))) / h;
        return ((4 * d2) - d1) / 3;
    }
}
