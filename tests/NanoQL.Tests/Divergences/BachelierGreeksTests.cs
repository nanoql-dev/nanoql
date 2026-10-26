// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Bachelier calculator's theta, vanna and volga. QuantLib's theta reads the spot gamma where the time decay reads
/// the forward gamma, missing (S/F)²; its vanna, the vega's sensitivity to the spot by its documentation, misses D·F/S;
/// its volga divides by σ√T where the derivative in σ divides by σ, missing √T (<c>bacheliercalculator.cpp:263-271</c>,
/// <c>:350-370</c>). At F/S 1.04 and T 2 they are −1.9078, −0.016563 and 0.0052986 where the price bumped gives −1.8295,
/// −0.015586 and 0.0074933; QuantLib's cases run at T 1, which hides the volga's, and record the theta and vanna at F/S
/// 1.02, −4.3159316452046594 and 0.0048333514600356151 (their checks moved here from
/// <c>testBachelierCalculatorGreeks</c>). nanoql takes each missing factor (the S-exo plan's ER5 (xxviii)), the vanna
/// taking the spot as <see cref="BlackCalculator.Vanna"/> does. The checks: Bachelier's price in a spot model,
/// F = S·e^{(r−q)T}, D = e^{−rT}, standard deviation σ_N·√T, bumped in T, in S and σ_N, and in σ_N twice.
/// </summary>
public class BachelierGreeksTests
{
    // QuantLib's testBachelierCalculatorGreeks: K 105, F 100, σ√T 20, D 0.95, spot 98, T 1
    private static readonly Market QuantLibsCase = new(105.0, 98.0, 1.0, 20.0, -Math.Log(0.95), -Math.Log(0.95) - Math.Log(100.0 / 98.0));

    // the plan's EP21: K 100, spot 100, T 2, σ_N 8, r 5%, F/S 1.04
    private static readonly Market PlansCase = new(100.0, 100.0, 2.0, 8.0, 0.05, 0.05 - (Math.Log(1.04) / 2.0));

    [Fact]
    public void TheThetaReadsTheForwardGamma()
    {
        foreach (Market m in new[] { QuantLibsCase, PlansCase })
        {
            const double h = 1e-5;
            double bumped = -(m.Price(m.Spot, m.T + h, m.SigmaN) - m.Price(m.Spot, m.T - h, m.SigmaN)) / (2.0 * h);
            Assert.Equal(bumped, m.Calculator().Theta(m.Spot, m.T), 1e-6);

            // QuantLib's theta per day, the theta over 365
            Assert.Equal(m.Calculator().Theta(m.Spot, m.T) / 365.0, m.Calculator().ThetaPerDay(m.Spot, m.T));
        }

        // QuantLib's recorded theta, its spot gamma's
        Assert.NotEqual(-4.3159316452046594, QuantLibsCase.Calculator().Theta(QuantLibsCase.Spot, QuantLibsCase.T), 1e-3);
    }

    [Fact]
    public void TheVannaIsTheVegasSensitivityToTheSpot()
    {
        foreach (Market m in new[] { QuantLibsCase, PlansCase })
        {
            const double h = 1e-3;
            double bumped = (m.Price(m.Spot + h, m.T, m.SigmaN + h) - m.Price(m.Spot + h, m.T, m.SigmaN - h)
                - m.Price(m.Spot - h, m.T, m.SigmaN + h) + m.Price(m.Spot - h, m.T, m.SigmaN - h)) / (4.0 * h * h);
            Assert.Equal(bumped, m.Calculator().Vanna(m.Spot, m.T), 1e-6);
        }

        // QuantLib's recorded vanna, short of D·F/S
        Assert.NotEqual(0.0048333514600356151, QuantLibsCase.Calculator().Vanna(QuantLibsCase.Spot, QuantLibsCase.T), 1e-4);
    }

    [Fact]
    public void TheVolgaIsTheVegasSensitivityToTheVolatility()
    {
        foreach (Market m in new[] { QuantLibsCase, PlansCase })
        {
            const double h = 1e-3;
            double bumped = (m.Price(m.Spot, m.T, m.SigmaN + h) - (2.0 * m.Price(m.Spot, m.T, m.SigmaN))
                + m.Price(m.Spot, m.T, m.SigmaN - h)) / (h * h);
            Assert.Equal(bumped, m.Calculator().Volga(m.T), 1e-6);
        }
    }

    // A call in a spot model: the strike, the spot, the maturity, the normal volatility, r and q.
    private sealed record Market(double Strike, double Spot, double T, double SigmaN, double R, double Q)
    {
        public BachelierCalculator Calculator() =>
            new(OptionType.Call, Strike, Spot * Math.Exp((R - Q) * T), SigmaN * Math.Sqrt(T), Math.Exp(-R * T));

        // Bachelier's call: D·((F − K)·N(d) + σ√T·n(d))
        public double Price(double spot, double t, double sigmaN)
        {
            double f = spot * Math.Exp((R - Q) * t), sd = sigmaN * Math.Sqrt(t), d = (f - Strike) / sd;
            return Math.Exp(-R * t) * (((f - Strike) * new CumulativeNormalDistribution().Evaluate(d))
                + (sd * new NormalDistribution().Evaluate(d)));
        }
    }
}
