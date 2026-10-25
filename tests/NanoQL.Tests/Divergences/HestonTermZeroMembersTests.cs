// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The COS engine's members and the LPP expansions at a term of 0 (the S-hes plan's HR7 (xxxii), under R8). QuantLib's
/// cumulants at t = 0 (<c>coshestonengine.cpp:177-286</c>) leave rounding residue, c₂ −5.1e-19 and c₃ −2.2e-18 here,
/// and its skew c₃/c₂^{3/2} is NaN; its LPP2 and LPP3 coefficients (<c>hestonexpansionengine.cpp:128-720</c>) divide 0
/// by 0 at a term of 0, and their implied volatility is the 1e-8 floor. nanoql's members at t = 0 are their limits, the
/// cumulants, skew and kurtosis 0, and the expansions' coefficients theirs: the first three Forde, Jacquier and Lee's
/// leading order, √v₀, ρσ/(4√v₀) and (1 − 5ρ²/2)σ²/(24 v₀^{3/2}), LPP3's z₃ ρ(8ρ² − 5)σ³/(96 v₀^{5/2}). The check:
/// QuantLib's Forde expansion at a term of 0, whose variance is the square of that leading order, and QuantLib's z₃ in
/// Boost's 250-digit <c>cpp_bin_float</c> at a term of 1e-6 [executed at the run], next to its limit.
/// </summary>
public class HestonTermZeroMembersTests
{
    [Fact]
    public void AtTimeZeroTheCosMembersAreZero()
    {
        // QuantLib 1.44's c1 to c4 at t = 0: 0, -5.13992141030165e-19, -2.1930331350620376e-18 and 0; its skew NaN and
        // its kurtosis 0 [executed through the harness's COSHestonEngine::members]
        var today = new Date(15, Month.October, 2026);
        var engine = new COSHestonEngine(new HestonModel(new HestonProcess(
            new FlatForward(today, 0.03, new Actual365Fixed()), new FlatForward(today, 0.01, new Actual365Fixed()), 100.0, 0.04, 1.5, 0.04, 0.3, -0.5)));
        Assert.Equal(0.0, engine.C1(0.0));
        Assert.Equal(0.0, engine.C2(0.0));
        Assert.Equal(0.0, engine.C3(0.0));
        Assert.Equal(0.0, engine.C4(0.0));
        Assert.Equal(0.0, engine.Mu(0.0));
        Assert.Equal(0.0, engine.Var(0.0));
        Assert.Equal(0.0, engine.Skew(0.0));
        Assert.Equal(0.0, engine.Kurtosis(0.0));
    }

    [Fact]
    public void AtATermOfZeroTheExpansionsAreTheirLimits()
    {
        // kappa 1.5, theta 0.04, sigma 0.3, v0 0.04, rho -0.5: QuantLib 1.44's LPP2 and LPP3 give 1e-8 at each strike,
        // their coefficients NaN; its Forde expansion 0.22170641591117737, 0.2 and 0.18372614350092728 at strikes 90,
        // 100 and 110 on a forward of 100 [executed through the harness's HestonExpansion::impliedVolatility]
        var lpp2 = new LPP2HestonExpansion(1.5, 0.04, 0.3, 0.04, -0.5, 0.0);
        var lpp3 = new LPP3HestonExpansion(1.5, 0.04, 0.3, 0.04, -0.5, 0.0);
        double[] limits = [0.2, -0.1875, 0.17578125];
        for (int i = 0; i < 3; ++i)
        {
            Assert.Equal(limits[i], Coefficient(lpp2, i));
            Assert.Equal(limits[i], Coefficient(lpp3, i));
        }

        double z3 = Coefficient(lpp3, 3);
        Assert.Equal(1.318359375, z3);

        foreach ((double strike, double forde) in new[] { (90.0, 0.22170641591117737), (100.0, 0.2), (110.0, 0.18372614350092728) })
        {
            double x = Math.Log(strike / 100.0);
            Assert.Equal(forde, lpp2.ImpliedVolatility(strike, 100.0), 1e-15);
            Assert.Equal(forde, lpp3.ImpliedVolatility(strike, 100.0) - (z3 * x * x * x), 1e-15);
        }
    }

    [Fact]
    public void AtATermOfOneMillionthLpp3sZ3IsTheReferenceValueNextToItsLimit()
    {
        // kappa 1.5: QuantLib 1.44's z3 is 3.289529965664373e+21 and -2.2456483812272203e+20 [executed in the same
        // probe, QuantLib's expression in double]; the limits 1.318359375 and -1.712
        foreach ((double theta, double sigma, double v0, double rho, double digits, double limit) in new[]
                 {
                     (0.04, 0.3, 0.04, -0.5, 1.318357001955580227483752, 1.318359375),
                     (0.09, 0.5, 0.0625, 0.3, -1.71199489638558292225128, -1.712),
                 })
        {
            double z3 = Coefficient(new LPP3HestonExpansion(1.5, theta, sigma, v0, rho, 1e-6), 3);
            Assert.True(Math.Abs((z3 / digits) - 1.0) <= 1e-12, $"rho {rho}: {z3} against {digits}");
            Assert.Equal(limit, Coefficient(new LPP3HestonExpansion(1.5, theta, sigma, v0, rho, 0.0), 3), 1e-15);
            Assert.True(Math.Abs((z3 / limit) - 1.0) < 5e-6, $"rho {rho}: {z3} against the limit {limit}");
        }
    }

    // the coefficients, internal to the expansions
    private static double Coefficient(object expansion, int i)
    {
        MethodInfo coefficient = expansion.GetType().GetMethod("Coefficient", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (double)coefficient.Invoke(expansion, [i])!;
    }
}
