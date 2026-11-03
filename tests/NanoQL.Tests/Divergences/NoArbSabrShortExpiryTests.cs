// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Doust's no-arbitrage SABR model at short expiries. QuantLib integrates the price, the digital and the forward error
/// over [K, U] on one Gauss-Lobatto interval, which misses the density's spike as the expiry falls: out of the money
/// from T 0.005 (2.18e-12 against Hagan's 2.18e-6), at the money from 0.003 (3.3e-9 against 1.97e-4), and at 0.001 its
/// forward adjustment fails too, the numerical forward 8.8e-4 off (QuantLib 1.44); nanoql splits each integral at the
/// forward ± 8 widths Fσ_I√T where σ_I√T is below 0.1, QuantLib's single interval at and above. Checks: Hagan's prices
/// (the SABR section) at strikes 0.9F to 1.1F within QuantLib's own consistency measure, 1e-5; the numerical forward
/// within 1e-6 of the forward.
/// </summary>
public class NoArbSabrShortExpiryTests
{
    // Doust's figure 3, QuantLib's testConsistencyWithHagan: σ_I = αF^(β − 1) = 0.1177
    private const double Alpha = 0.026, Beta = 0.5, Rho = -0.1, Nu = 0.4, Forward = 0.0488;

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.005)]
    [InlineData(0.003)]
    [InlineData(0.002)]
    [InlineData(0.001)]
    public void AtShortExpiriesThePricesAreHagans(double tau)
    {
        var sabr = new SabrSmileSection(tau, Forward, [Alpha, Beta, Nu, Rho]);
        var noArb = new NoArbSabrSmileSection(tau, Forward, [Alpha, Beta, Nu, Rho]);
        for (double m = 0.9; m <= 1.1 + 1e-12; m += 0.02)
        {
            double strike = Forward * m;
            double hagan = sabr.OptionPrice(strike), doust = noArb.OptionPrice(strike);
            Assert.True(Math.Abs(hagan - doust) <= 1e-5, $"T {tau}, strike {strike}: {doust} against Hagan's {hagan}");
        }
    }

    [Theory]
    [InlineData(0.003)]
    [InlineData(0.002)]
    [InlineData(0.001)]
    public void AtShortExpiriesTheNumericalForwardIsTheForward(double tau)
    {
        var model = new NoArbSabrModel(tau, Forward, Alpha, Beta, Nu, Rho);
        Assert.True(
            Math.Abs(model.NumericalForward - model.Forward) <= 1e-6,
            $"T {tau}: numerical forward {model.NumericalForward} against {model.Forward}");
    }
}
