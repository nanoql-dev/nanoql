// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The jump mesher's distribution and density at a finite time and its stationary distribution. QuantLib integrates its
/// density from 1e-100, which holds no mass while βt is below about 230, so at β 100, λ 4, η 2.5 its distribution meets
/// the closed form's to 6e-16 at t 1, misses 1.06e-4 at t 5, and from t 8 its integrator refuses, "accuracy not
/// reached"; nanoql integrates the closed form's route over the time since the last jump, its norm 1 − e^(−λt) by
/// <c>expm1</c>, which QuantLib's subtraction loses at small λt (it refuses at t 1e-12, "max number of iterations
/// reached"). QuantLib's density at t, a difference of regularised incomplete gammas, cancels as βt falls and where ηx
/// is large, and refuses at intensities from β, where it takes Γ(1 − λ/β) ("positive argument required", its harness at
/// β 4, λ 4, η 5 and β 0.2, λ 6, η 0.3); nanoql's is the route's derivative, which computes there too, its terms one
/// exponential so that none leaves the double range before its peak. QuantLib's stationary distribution holds the mass
/// to min(x, ε)/η where it means x, wrong with η below 1 and x below ε/η; there nanoql takes its series at ηx. The
/// references are the route at 40 digits, QuantLib's density formula continued in mpmath and the Gamma(λ/β, η) cdf;
/// QuantLib's values are its own and its harness's.
/// </summary>
public class ExponentialJump1dMesherTests
{
    private static readonly ExponentialJump1dMesher Mesher = new(10, 100.0, 4.0, 2.5);

    private static void Near(double exact, double actual, double tolerance = 1e-14) =>
        Assert.True(Math.Abs(actual - exact) <= tolerance, $"{actual:R} for {exact:R}");

    private static void NearRelative(double exact, double actual, double tolerance = 1e-13) =>
        Assert.True(Math.Abs(actual - exact) <= tolerance * Math.Abs(exact), $"{actual:R} for {exact:R}");

    [Fact]
    public void WhereQuantLibsCutHoldsNoMassItsValuesAreMet()
    {
        Near(0.60443994491647028, Mesher.JumpSizeDistribution(1e-6, 1.0));    // QuantLib 0.60443994491647091
        Near(0.80263895576642751, Mesher.JumpSizeDistribution(1e-3, 1.0));    // QuantLib 0.80263895576642785
        Near(0.95888988704005211, Mesher.JumpSizeDistribution(0.1, 1.0));     // QuantLib 0.95888988704005329
        Near(0.99412650129816199, Mesher.JumpSizeDistribution(0.5, 1.0));     // QuantLib 0.99412650129816271
    }

    [Fact]
    public void WhereEtaXReachesOneTheComplementKeepsTheDigits()
    {
        // at β 10, λ 2, η 1, x 20, t 2 the integrand stays within 2e-9 of 1: integrated whole, it lands 3.1e-12 off;
        // its complement's integral, 1.4e-14
        var mesher = new ExponentialJump1dMesher(10, 10.0, 2.0, 1.0);
        Near(0.99999999998014355, mesher.JumpSizeDistribution(20.0, 2.0), 1e-13);    // QuantLib 0.9999999999801579
    }

    [Fact]
    public void AtSmallTimesTheNormKeepsItsDigits()
    {
        Near(0.22119921693833015, Mesher.JumpSizeDistribution(0.1, 1e-12));   // QuantLib refuses
        Near(0.22119921692859524, Mesher.JumpSizeDistribution(0.1, 1e-17));   // QuantLib refuses
    }

    [Fact]
    public void BeyondItTheDistributionKeepsItsMass()
    {
        Near(0.61168487924381089, Mesher.JumpSizeDistribution(1e-6, 5.0));    // QuantLib 0.61157858263749598
        Near(0.80625374898397233, Mesher.JumpSizeDistribution(1e-3, 5.0));    // QuantLib 0.80614745237765784
        Near(0.95964284494051929, Mesher.JumpSizeDistribution(0.1, 5.0));     // QuantLib 0.95953654833420499
        Near(0.99423407816751386, Mesher.JumpSizeDistribution(0.5, 5.0));     // QuantLib 0.99412778156119952
        Near(0.61168488004418309, Mesher.JumpSizeDistribution(1e-6, 8.0));    // QuantLib refuses
        Near(0.99423407817939823, Mesher.JumpSizeDistribution(0.5, 8.0));     // QuantLib refuses
    }

    [Fact]
    public void TheDensityAtSmallAndLargeTimesKeepsItsDigits()
    {
        var mesher = new ExponentialJump1dMesher(10, 10.0, 2.0, 1.0);
        NearRelative(0.90483745875364431, mesher.JumpSizeDensity(0.1, 1e-8));   // QuantLib 0.9048374606580513
        NearRelative(0.90483741804003134, mesher.JumpSizeDensity(0.1, 1e-12));  // QuantLib 9.9e-5 off
        NearRelative(1.9470026878043428, Mesher.JumpSizeDensity(0.1, 1e-8));    // QuantLib 1.9470026869358201
        NearRelative(0.022459110003169174, Mesher.JumpSizeDensity(0.5, 7.5));   // QuantLib refuses
    }

    [Fact]
    public void TheDensityComputesWhereItsTermsLeaveTheDoubleRange()
    {
        // e^(βu) leaves the double range before the integrand's peak: at x 0 past βt 709.78 ηx·e^(βu) was 0·∞, NaN,
        // refused; at 1e-308 the tail past it was cut, 11.7% low
        NearRelative(6.0, new ExponentialJump1dMesher(10, 50.0, 60.0, 1.0).JumpSizeDensity(0.0, 20.0), 1e-11);
        NearRelative(5.848825642133745e245, new ExponentialJump1dMesher(10, 10.0, 2.0, 1.0).JumpSizeDensity(1e-308, 100.0), 1e-11);
    }

    [Fact]
    public void WhereEtaXIsLargeQuantLibsDensityCancels()
    {
        // both incomplete gammas near 1: the densityAt vectors keep such outputs, which pass on the harness's scale of
        // 1
        NearRelative(7.0319884334592194e-18, new ExponentialJump1dMesher(10, 5.0, 1.0, 2.0).JumpSizeDensity(20.0, 0.002));    // QuantLib 0
        NearRelative(1.8766401230606645e-9, new ExponentialJump1dMesher(10, 10.0, 2.0, 1.0).JumpSizeDensity(20.0, 0.001));    // QuantLib 1.8766399151459895e-09
    }

    [Fact]
    public void BelowEpsOverEtaTheStationarySeriesTakesEtaX()
    {
        // with η below 1, QuantLib's series at min(x, ε) holds the mass to min(x, ε)/η, and its integral is empty; the
        // references are the Gamma(λ/β, η) cdf at 40 digits
        var low = new ExponentialJump1dMesher(10, 100.0, 4.0, 0.3);
        Near(0.20349392557321053, low.JumpSizeDistribution(1e-17));                                                            // QuantLib 0.213533785670037
        NearRelative(0.00068719105251949573, new ExponentialJump1dMesher(10, 5.0, 1.0, 0.01).JumpSizeDistribution(1e-14));    // QuantLib 0.0008060566054634075

        // above ε/η, QuantLib's bits
        Assert.Equal(0.24465350937735283, low.JumpSizeDistribution(1e-15));
    }

    [Fact]
    public void BelowZeroTheStationaryFormsAreZero()
    {
        // QuantLib's powers of a negative size: at λ = β, the stationary law exponential of rate η, the distribution
        // −0.105 and the density 1.105 at x −0.1; NaN at λ 4, β 100
        var exponential = new ExponentialJump1dMesher(10, 2.0, 2.0, 1.0);
        Assert.Equal(0.0, exponential.JumpSizeDistribution(-0.1));
        Assert.Equal(0.0, exponential.JumpSizeDensity(-0.1));
        Assert.Equal(0.0, Mesher.JumpSizeDistribution(-1e-20));
        Assert.Equal(0.0, Mesher.JumpSizeDensity(-1e-20));
    }

    [Fact]
    public void AtIntensitiesFromBetaTheDistributionAndDensityCompute()
    {
        var equal = new ExponentialJump1dMesher(10, 4.0, 4.0, 5.0);
        var above = new ExponentialJump1dMesher(10, 0.2, 6.0, 0.3);
        Near(0.10702288137168959, equal.JumpSizeDistribution(0.01, 0.5), 1e-12);
        Near(0.98019565243329692, equal.JumpSizeDistribution(0.5, 2.0), 1e-12);
        Near(0.99999997905762045, equal.JumpSizeDistribution(3.0, 0.5), 1e-12);
        Near(0.0030986225013415884, above.JumpSizeDistribution(0.01, 2.0), 1e-12);
        Near(0.14299413868940747, above.JumpSizeDistribution(0.5, 0.5), 1e-12);
        Near(0.60565462906825020, above.JumpSizeDistribution(3.0, 2.0), 1e-12);

        // QuantLib's density at t refuses both, its Γ(1 − λ/β)
        NearRelative(0.14407270759707791, equal.JumpSizeDensity(0.5, 0.5));
        NearRelative(0.26447837668642871, above.JumpSizeDensity(0.5, 0.5));
    }
}
