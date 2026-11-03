// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/exponentialjump1dmesher.hpp ql/methods/finitedifferences/meshers/exponentialjump1dmesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// A mesher for an exponential jump process with high mean reversion and low jump intensity, dY = −βY dt + J dN, the
/// jumps exponential of mean 1/η (Hambly, Howison and Kluge, "Modelling spikes and pricing swing options in
/// electricity markets"): its points at the stationary jump-size distribution's quantiles, to 1 − ε.
/// </summary>
/// <remarks>
/// Immutable. The distribution at a finite time, <see cref="JumpSizeDistribution(double, double)"/>, is the
/// distribution of the last jump's decayed size, ∫₀ᵗ λe^(−λu)/(1 − e^(−λt))·(1 − e^(−ηx·e^(βu))) du, integrated over u
/// and split where ηx·e^(βu) crosses 1, or as 1 less its complement's integral where ηx is at least 1. QuantLib
/// integrates its density over x from 1e-100 instead, which holds no mass while βt is below about 230; beyond, it
/// misses mass (1.06e-4 at βt 500) and from βt 800 its integrator refuses (a documented divergence). Where its cut
/// holds no mass the two meet within 4.4e-13, each within 4.5e-13 absolute of a 40-digit evaluation, the tolerance of
/// both integrals being absolute; at an intensity at or above β, where QuantLib's density at t refuses, the route
/// computes. Both routes take 1 − e^(−λt) by <c>expm1</c>, which QuantLib's subtraction loses at small λt.
/// <para>
/// The density at a finite time, <see cref="JumpSizeDensity(double, double)"/>, is the route's derivative, integrated
/// by tanh-sinh to a relative tolerance and split at the same point. QuantLib's closed form, Γ(1 − λ/β)·(P(1 − λ/β,
/// ηx·e^(βt)) − P(1 − λ/β, ηx)) over its other factors, cancels as βt falls (9.9e-5 relative at β 10, λ 2, η 1, t
/// 1e-12) and where ηx is large, both gammas near 1 (1.1e-7 relative at x 20, βt 0.01; 0 for 4e-20 to 7e-18 where they
/// underflow), and refuses at an intensity at or above β (its Γ's argument) and from βt about 750; it is replaced (a
/// fix, a documented divergence), within 5e-12 relative of a 40-digit evaluation (tanh-sinh's √ε), where the value is
/// in the double range: it is refused where it leaves it at x 0 (e^((β − λ)t) beyond it), 0 below it, and at subnormal
/// sizes, where an integrand value leaves the double range, it is refused or not reliable. Both integrands take
/// ηx·e^(βu) as one exponential, so neither leaves the double range before their peak. A time at or below 0, where
/// QuantLib's density divides by 0, is refused; at x at or below 0 the distribution is QuantLib's 0, and below 0 the
/// density is 0. The stationary forms are 0 below 0 too, where QuantLib's powers of a negative size give NaN, or at λ =
/// β a negative probability.
/// </para>
/// <para>
/// The stationary distribution, <see cref="JumpSizeDistribution(double)"/>, is QuantLib's: a series below a cut and the
/// stationary density's integral above it. QuantLib's series at min(x, ε) holds the mass to min(x, ε)/η, so with η
/// below 1 and x below ε/η its integral is empty and it returns the mass to ε/η or x/η, not x (4.9% to 151% off); there
/// the series is taken at ηx (a fix, a documented divergence), QuantLib's bits elsewhere.
/// </para>
/// </remarks>
public sealed class ExponentialJump1dMesher : Fdm1dMesher
{
    private readonly double beta;
    private readonly double jumpIntensity;
    private readonly double eta;

    /// <summary>Initializes a new instance of the <see cref="ExponentialJump1dMesher"/> class.</summary>
    /// <param name="steps">The number of points, at least two.</param>
    /// <param name="beta">The mean reversion β.</param>
    /// <param name="jumpIntensity">The jump intensity λ.</param>
    /// <param name="eta">The jumps' rate η, their mean 1/η.</param>
    /// <param name="eps">The probability above the last point, in (0, 1).</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="eps"/> is outside (0, 1), or fewer than two steps.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    public ExponentialJump1dMesher(int steps, double beta, double jumpIntensity, double eta, double eps = 1e-3)
        : base(steps)
    {
        this.beta = beta;
        this.jumpIntensity = jumpIntensity;
        this.eta = eta;

        if (!(eps > 0.0 && eps < 1.0))
        {
            throw new ArgumentException("eps > 0.0 and eps < 1.0", nameof(eps));
        }

        if (!(steps > 1))
        {
            throw new ArgumentException("minimum number of steps is two", nameof(steps));
        }

        const double start = 0.0;
        double end = 1.0 - eps;
        double dx = (end - start) / (steps - 1);
        double scale = 1 / (1 - Math.Exp(-beta / jumpIntensity));

        for (int i = 0; i < steps; ++i)
        {
            double p = start + (i * dx);
            locations[i] = scale * (-1.0 / eta * Math.Log(1.0 - p));
        }

        for (int i = 0; i < steps - 1; ++i)
        {
            dminus[i + 1] = dplus[i] = locations[i + 1] - locations[i];
        }

        dplus[^1] = dminus[0] = NullReal;
    }

    /// <summary>
    /// The density of the jump size at time t, the derivative in x of
    /// <see cref="JumpSizeDistribution(double, double)"/>: ∫₀ᵗ λe^(−λu)/(1 − e^(−λt))·ηe^(βu)·e^(−ηx·e^(βu)) du.
    /// </summary>
    /// <param name="x">The size.</param>
    /// <param name="t">The time, positive.</param>
    /// <returns>The density, 0 below 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="t"/> isn't positive.</exception>
    public double JumpSizeDensity(double x, double t)
    {
        if (!(t > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(t), t, "the time must be positive");
        }

        if (x < 0.0)
        {
            return 0.0;
        }

        double norm = -Expm1Functions.StdExpm1(-jumpIntensity * t);
        double D(double u)
        {
            // ηx·e^(βu) and the exponents as one, so neither e^(βu) nor ηx leaves the double range before the peak
            double s = Math.Exp((beta * u) + Math.Log(eta * x));
            return jumpIntensity * eta / norm * Math.Exp(((beta - jumpIntensity) * u) - s);
        }

        // the integrand's peak, where ηx·e^(βu) crosses 1; the quadrature's tolerance is relative, as the density
        // has no scale
        double knee = Math.Log(1.0 / (eta * x)) / beta;
        var integrator = new TanhSinhIntegral();
        if (knee > 0.0 && knee < t)
        {
            return integrator.Integrate(D, 0.0, knee) + integrator.Integrate(D, knee, t);
        }

        return integrator.Integrate(D, 0.0, t);
    }

    /// <summary>The stationary density of the jump size, as t → ∞.</summary>
    /// <param name="x">The size.</param>
    /// <returns>The density, 0 below 0.</returns>
    public double JumpSizeDensity(double x)
    {
        if (x < 0.0)
        {
            return 0.0;
        }

        double a = 1.0 - (jumpIntensity / beta);
        double gammaValue = Math.Exp(new GammaFunction().LogValue(jumpIntensity / beta));
        return Math.Exp(-x * eta) * Math.Pow(x, -a) * Math.Pow(eta, 1.0 - a) / gammaValue;
    }

    /// <summary>The distribution of the jump size at time t: the last jump's decayed size.</summary>
    /// <param name="x">The size.</param>
    /// <param name="t">The time, positive.</param>
    /// <returns>The probability of a size at most <paramref name="x"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="t"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The integration doesn't reach its accuracy.</exception>
    public double JumpSizeDistribution(double x, double t)
    {
        if (!(t > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(t), t, "the time must be positive");
        }

        if (!(x > 0.0))
        {
            return 0.0;
        }

        double norm = -Expm1Functions.StdExpm1(-jumpIntensity * t);
        double F(double u) =>
            jumpIntensity * Math.Exp(-jumpIntensity * u) / norm
            * -Expm1Functions.StdExpm1(-Math.Exp((beta * u) + Math.Log(eta * x)));

        // the integrand's knee, where ηx·e^(βu) crosses 1
        double knee = Math.Log(1.0 / (eta * x)) / beta;
        var integrator = new GaussLobattoIntegral(1000000, 1e-12);
        if (knee <= 0.0)
        {
            // ηx at or above 1: the integrand stays near 1, so its small complement is integrated, the weights' own
            // integral being 1
            double G(double u) =>
                jumpIntensity * Math.Exp(-jumpIntensity * u) / norm * Math.Exp(-eta * x * Math.Exp(beta * u));
            return 1.0 - integrator.Integrate(G, 0.0, t);
        }

        if (knee < t)
        {
            return integrator.Integrate(F, 0.0, knee) + integrator.Integrate(F, knee, t);
        }

        return integrator.Integrate(F, 0.0, t);
    }

    /// <summary>The stationary distribution of the jump size, as t → ∞.</summary>
    /// <param name="x">The size.</param>
    /// <returns>The probability of a size at most <paramref name="x"/>, 0 below 0.</returns>
    /// <exception cref="NanoQLException">The integration doesn't reach its accuracy.</exception>
    public double JumpSizeDistribution(double x)
    {
        if (x < 0.0)
        {
            return 0.0;
        }

        double a = jumpIntensity / beta;
        double xmin = Math.Min(x, NumericLimits.QL_EPSILON);
        double gammaValue = Math.Exp(new GammaFunction().LogValue(jumpIntensity / beta));

        // below xmin/η, which η under 1 puts above x, the series at ηx itself: QuantLib's, at xmin, holds the mass to
        // xmin/η, and its integral is empty
        if (x < xmin / eta)
        {
            double z = eta * x;
            return ((Math.Pow(z, a) / a) - (Math.Pow(z, a + 1) / (a + 1))) / gammaValue;
        }

        double lowerEps = ((Math.Pow(xmin, a) / a) - (Math.Pow(xmin, a + 1) / (a + 1))) / gammaValue;

        return lowerEps + new GaussLobattoIntegral(10000, 1e-12).Integrate(
            JumpSizeDensity, xmin / eta, Math.Max(x, xmin / eta));
    }
}
