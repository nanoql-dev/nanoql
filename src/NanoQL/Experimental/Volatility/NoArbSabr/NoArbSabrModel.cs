// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/noarbsabr.hpp ql/experimental/volatility/noarbsabr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// Doust's no-arbitrage SABR model (Paul Doust, "No-arbitrage SABR", The Journal of Computational Finance, 2012): the
/// SABR density with the absorption at zero read off a simulated table, its option and digital prices the density's
/// integrals, the forward adjusted so the density reprices it.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: QuantLib's forward solve, which changes its forward and normalization as it searches, runs over a working
/// state at construction, the found forward and normalization stored. The model's domain is QuantLib's: an expiry in
/// (0, 30], β in [0.01, 0.99], σ_I = αF^(β − 1) in [0.05, 1], ν in [0.01, 0.8], ρ in [−0.99, 0.99]. As QuantLib's header
/// says, the forward adjustment can fail at extreme parameters, which <see cref="Forward"/> against
/// <see cref="NumericalForward"/> shows: at an expiry of 30, ρ 0.9, ν 0.8 the adjustment is 0.0157 off and the call flat
/// in strike; at β 0.9, ν 0.8, |ρ| 0.7 the density beyond QuantLib's integration domain carries a strike-independent call
/// value, 4.0e-6 at an expiry of 1 (σ_I 0.3) and 3.5e-4 at 4 (σ_I 0.05). Out-of-the-money prices below the integrator's
/// 1e-7 carry no relative accuracy, so implied volatilities read there are unreliable.
/// </para>
/// <para>
/// Documented divergence (<c>Divergences/NoArbSabrShortExpiryTests.cs</c>): where σ_I√T is below 0.1, each of the
/// model's integrals (the normalization and the price inside the forward solve, the price, the digital) is split at
/// the density's peak, the current forward ± 8w with w = Fσ_I√T, where QuantLib's single interval misses the spike: out
/// of the money from T 0.005, at the money from 0.003, the forward adjustment failing at 0.001; at and above 0.1
/// QuantLib's single interval and bits stand.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class NoArbSabrModel
{
    // QuantLib's detail::NoArbSabrModel constants
    internal const double BetaMin = 0.01;
    internal const double BetaMax = 0.99;
    internal const double ExpiryTimeMax = 30.0;
    internal const double SigmaIMin = 0.05;
    internal const double SigmaIMax = 1.00;
    internal const double NuMin = 0.01;
    internal const double NuMax = 0.80;
    internal const double RhoMin = -0.99;
    internal const double RhoMax = 0.99;
    internal const double PhiByTauCutoff = 124.587;
    internal const double Nsim = 2500000.0;
    internal const double TinyProb = 1E-5;
    internal const double StrikeMin = 1E-6;
    internal const double IAccuracy = 1E-7;
    internal const int IMaxIterations = 10000;
    internal const double ForwardAccuracy = 1E-6;
    internal const double ForwardSearchStep = 0.0010;
    internal const double DensityLowerBound = 1E-50;
    internal const double DensityThreshold = 1E-100;

    // the split at the density's peak below this σ_I√T, at this many widths Fσ_I√T either side
    private const double SplitBelow = 0.1;
    private const double SplitWidths = 8.0;

    private readonly double fmin;
    private readonly double fmax;
    private readonly double sigmaIRootT;
    private readonly double forward;
    private readonly double numericalIntegralOverP;
    private readonly GaussLobattoIntegral integrator;

    /// <summary>Initializes a new instance of the <see cref="NoArbSabrModel"/> class.</summary>
    /// <param name="expiryTime">The expiry time, in (0, 30].</param>
    /// <param name="forward">The forward, positive.</param>
    /// <param name="alpha">The SABR α.</param>
    /// <param name="beta">The SABR β, in [0.01, 0.99].</param>
    /// <param name="nu">The SABR ν, in [0.01, 0.8].</param>
    /// <param name="rho">The SABR ρ, in [−0.99, 0.99].</param>
    /// <exception cref="ArgumentException">A parameter is out of the model's domain.</exception>
    /// <exception cref="NanoQLException">No integration domain is found ("could not find a reasonable integration
    /// domain"), or an integral fails.</exception>
    public NoArbSabrModel(double expiryTime, double forward, double alpha, double beta, double nu, double rho)
    {
        ExpiryTime = expiryTime;
        Forward = forward;
        Alpha = alpha;
        Beta = beta;
        Nu = nu;
        Rho = rho;
        if (!(expiryTime > 0.0 && expiryTime <= ExpiryTimeMax))
        {
            throw new ArgumentException($"expiryTime ({Show(expiryTime)}) out of bounds", nameof(expiryTime));
        }

        if (!(forward > 0.0))
        {
            throw new ArgumentException($"forward ({Show(forward)}) must be positive", nameof(forward));
        }

        if (!(beta >= BetaMin && beta <= BetaMax))
        {
            throw new ArgumentException($"beta ({Show(beta)}) out of bounds", nameof(beta));
        }

        double sigmaI = alpha * Math.Pow(forward, beta - 1.0);
        if (!(sigmaI >= SigmaIMin && sigmaI <= SigmaIMax))
        {
            throw new ArgumentException(
                $"sigmaI = alpha*forward^(beta-1.0) ({Show(sigmaI)}) out of bounds, alpha={Show(alpha)} beta={Show(beta)} "
                + $"forward={Show(forward)}",
                nameof(alpha));
        }

        if (!(nu >= NuMin && nu <= NuMax))
        {
            throw new ArgumentException($"nu ({Show(nu)}) out of bounds", nameof(nu));
        }

        if (!(rho >= RhoMin && rho <= RhoMax))
        {
            throw new ArgumentException($"rho ({Show(rho)}) out of bounds", nameof(rho));
        }

        sigmaIRootT = sigmaI * Math.Sqrt(expiryTime);

        // QuantLib's integration domain, on the external forward
        double lo = forward, hi = forward;
        for (double tmp = P(hi, forward); tmp > Math.Max(IAccuracy / Math.Max(1.0, hi - lo), DensityThreshold); tmp = P(hi, forward))
        {
            hi *= 2.0;
        }

        for (double tmp = P(lo, forward); tmp > Math.Max(IAccuracy / Math.Max(1.0, hi - lo), DensityThreshold); tmp = P(lo, forward))
        {
            lo *= 0.5;
        }

        fmin = Math.Max(StrikeMin, lo);
        fmax = hi;
        if (!(fmax > fmin))
        {
            throw new NanoQLException("could not find a reasonable integration domain");
        }

        integrator = new GaussLobattoIntegral(IMaxIterations, IAccuracy);
        AbsorptionProbability = new NoArbSabrD0Interpolator(forward, expiryTime, alpha, beta, nu, rho).Value();

        // QuantLib's forward solve: forwardError sets the forward and the normalization it then prices on
        double currentForward = forward, currentIntegralOverP = 0.0;
        double ForwardError(double x)
        {
            currentForward = (x * x) + StrikeMin;
            double f = currentForward;
            currentIntegralOverP = Integrate(u => P(u, f), fmin, fmax, f);
            return OptionPrice(0.0, f, currentIntegralOverP) - Forward;
        }

        try
        {
            var b = new Brent();
            double start = Math.Sqrt(Forward - StrikeMin);
            double tmp = b.Solve(ForwardError, ForwardAccuracy, start, Math.Min(ForwardSearchStep, start / 2.0));
            currentForward = (tmp * tmp) + StrikeMin;
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // QuantLib's catch of any error: the external forward
            currentForward = Forward;
        }

        double d = ForwardError(Math.Sqrt(currentForward - StrikeMin));
        this.forward = currentForward;
        numericalIntegralOverP = currentIntegralOverP;
        NumericalForward = d + Forward;
    }

    /// <summary>Gets the expiry time.</summary>
    public double ExpiryTime { get; }

    /// <summary>Gets the external forward, the one given.</summary>
    public double Forward { get; }

    /// <summary>Gets the forward the density reprices, which differs from <see cref="Forward"/> where the adjustment
    /// fails.</summary>
    public double NumericalForward { get; }

    /// <summary>Gets the SABR α.</summary>
    public double Alpha { get; }

    /// <summary>Gets the SABR β.</summary>
    public double Beta { get; }

    /// <summary>Gets the SABR ν.</summary>
    public double Nu { get; }

    /// <summary>Gets the SABR ρ.</summary>
    public double Rho { get; }

    /// <summary>Gets the absorption probability at zero.</summary>
    public double AbsorptionProbability { get; }

    /// <summary>The undiscounted call price at a strike.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The price.</returns>
    /// <exception cref="NanoQLException">The integral fails.</exception>
    public double OptionPrice(double strike) => OptionPrice(strike, forward, numericalIntegralOverP);

    /// <summary>The undiscounted digital call price at a strike.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The price.</returns>
    /// <exception cref="NanoQLException">The integral fails.</exception>
    public double DigitalOptionPrice(double strike)
    {
        if (strike < NumericLimits.QL_MIN_POSITIVE_REAL)
        {
            return 1.0;
        }

        if (P(Math.Max(forward, strike), forward) < DensityThreshold)
        {
            return 0.0;
        }

        return (1.0 - AbsorptionProbability)
            * (Integrate(f => P(f, forward), strike, Math.Max(fmax, 2.0 * strike), forward) / numericalIntegralOverP);
    }

    /// <summary>The density at a strike, the absorption at zero left out.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The density.</returns>
    public double Density(double strike) => P(strike, forward) * (1 - AbsorptionProbability) / numericalIntegralOverP;

    private static string Show(double x) => DataFormattersFunctions.General(x);

    private double OptionPrice(double strike, double f, double integralOverP)
    {
        if (P(Math.Max(f, strike), f) < DensityThreshold)
        {
            return 0.0;
        }

        return (1.0 - AbsorptionProbability)
            * (Integrate(u => Math.Max(u - strike, 0.0) * P(u, f), strike, Math.Max(fmax, 2.0 * strike), f) / integralOverP);
    }

    // QuantLib's integral over [a, b]; below σ_I√T of 0.1 split at the forward ± 8 widths
    private double Integrate(Func<double, double> g, double a, double b, double f)
    {
        if (!(sigmaIRootT < SplitBelow))
        {
            return integrator.Integrate(g, a, b);
        }

        double w = f * sigmaIRootT;
        double left = Math.Min(Math.Max(f - (SplitWidths * w), a), b);
        double right = Math.Min(Math.Max(f + (SplitWidths * w), a), b);
        double sum = 0.0;
        if (left > a)
        {
            sum += integrator.Integrate(g, a, left);
        }

        if (right > left)
        {
            sum += integrator.Integrate(g, left, right);
        }

        if (b > right)
        {
            sum += integrator.Integrate(g, right, b);
        }

        return sum;
    }

    // QuantLib's p(f): the unnormalized density at f for the given forward
    private double P(double f, double fwd)
    {
        if (f < DensityLowerBound || fwd < DensityLowerBound)
        {
            return 0.0;
        }

        double fOmB = Math.Pow(f, 1.0 - Beta);
        double fwdOmB = Math.Pow(fwd, 1.0 - Beta);
        double zf = fOmB / (Alpha * (1.0 - Beta));
        double zF = fwdOmB / (Alpha * (1.0 - Beta));
        double z = zF - zf;
        double jmzf = Math.Sqrt(1.0 + (2.0 * Rho * Nu * zf) + (Nu * Nu * zf * zf));
        double jz = Math.Sqrt(1.0 - (2.0 * Rho * Nu * z) + (Nu * Nu * z * z));
        double xz = Math.Log((jz - Rho + (Nu * z)) / (1.0 - Rho)) / Nu;
        double bpB = Beta / fwdOmB;
        double kappa1 = (0.125 * Nu * Nu * (2.0 - (3.0 * Rho * Rho))) - (0.25 * Rho * Nu * Alpha * bpB);
        double gamma = 1.0 / (2.0 * (1.0 - Beta));
        double sqrtOmR = Math.Sqrt(1.0 - (Rho * Rho));
        double h = 0.5 * Beta * Rho / ((1.0 - Beta) * jmzf * jmzf)
            * ((Nu * zf * Math.Log(zf * jz / zF))
                + ((1 + (Rho * Nu * zf)) / sqrtOmR * (Math.Atan(((Nu * z) - Rho) / sqrtOmR) + Math.Atan(Rho / sqrtOmR))));
        return Math.Pow(jz, -1.5) / (Alpha * Math.Pow(f, Beta) * ExpiryTime)
            * Math.Pow(zf, 1.0 - gamma) * Math.Pow(zF, gamma)
            * Math.Exp((-(xz * xz) / (2.0 * ExpiryTime)) + (h + (kappa1 * ExpiryTime)))
            * ModifiedBesselFunctions.ModifiedBesselFunction_i_exponentiallyWeighted(gamma, zF * zf / ExpiryTime);
    }
}
