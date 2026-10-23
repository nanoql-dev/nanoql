// SPDX-FileCopyrightText: Copyright (C) 2006 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/g2process.hpp ql/processes/g2process.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Models.G2Functions;

namespace NanoQL.Processes;

/// <summary>
/// The forward G2 stochastic process: <see cref="G2Process"/>'s in the forward measure of a time T, its state again
/// shifted so that z₁ + z₂ = r(t), with the T-forward corrections to the drift and the conditional expectation.
/// </summary>
/// <remarks>
/// <para>
/// The drift's corrections, QuantLib's −(σ²/a)(1 − e^(−a(T−t))) − (ρση/b)(1 − e^(−b(T−t))) and its mirror, are written
/// as −σ²τf(aτ) − ρσητf(bτ), τ = T − t, f(x) = (1 − e^(−x))/x; the expectation's, QuantLib's Mx_T and My_T, as
/// σ²τ₁f(aτ₁)[τ₂f(aτ₂) + ½e^(−aτ₂)τ₁f(aτ₁)] + ρσητ₁[τ₁D(aτ₁, bτ₁) + τ₂f(bτ₂)f((a+b)τ₁)], τ₁ = t − s, τ₂ = T − t,
/// D(u, v) = (f(u) − f(u+v))/v, and their mirrors, every term positive; the correlation and φ as
/// <see cref="G2Process"/>'s (the S-sr plan's SSR6 (iii) and (xix)): a divergence, QuantLib's terms cancelling at small
/// reversion.
/// </para>
/// <para>
/// QuantLib's constructor leaves the forward measure time unset; <see cref="WithForwardMeasureTime"/> gives a copy at
/// one, and the drift and the expectation refuse to read an unset one.
/// </para>
/// </remarks>
public sealed class G2ForwardProcess : ForwardMeasureProcess
{
    private readonly double x0 = 0.0, y0 = 0.0;
    private readonly OrnsteinUhlenbeckProcess xProcess;
    private readonly OrnsteinUhlenbeckProcess yProcess;

    /// <summary>Initializes a new instance of the <see cref="G2ForwardProcess"/> class, with no forward measure time.</summary>
    /// <param name="a">The first factor's mean reversion.</param>
    /// <param name="sigma">The first factor's volatility, not negative.</param>
    /// <param name="b">The second factor's mean reversion.</param>
    /// <param name="eta">The second factor's volatility, not negative.</param>
    /// <param name="rho">The factors' correlation.</param>
    /// <param name="termStructure">The curve the shift fits; <see langword="null"/>, QuantLib's empty handle, for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sigma"/> or <paramref name="eta"/> is negative or NaN.</exception>
    public G2ForwardProcess(double a, double sigma, double b, double eta, double rho, YieldTermStructure? termStructure = null)
    {
        A = a;
        Sigma = sigma;
        B = b;
        Eta = eta;
        Rho = rho;
        xProcess = new OrnsteinUhlenbeckProcess(a, sigma, 0.0);
        yProcess = new OrnsteinUhlenbeckProcess(b, eta, 0.0);
        TermStructure = termStructure;
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <summary>Gets the first factor's mean reversion a.</summary>
    public double A { get; }

    /// <summary>Gets the first factor's volatility σ.</summary>
    public double Sigma { get; }

    /// <summary>Gets the second factor's mean reversion b.</summary>
    public double B { get; }

    /// <summary>Gets the second factor's volatility η.</summary>
    public double Eta { get; }

    /// <summary>Gets the factors' correlation ρ.</summary>
    public double Rho { get; }

    /// <summary>Gets the curve the shift fits, or <see langword="null"/> for none.</summary>
    public YieldTermStructure? TermStructure { get; }

    /// <inheritdoc/>
    public override Vector InitialValues()
    {
        double z1_0 = TermStructure is null ? x0 : Phi(0.0);
        return new Vector([z1_0, y0]);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The process has no forward measure time.</exception>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        double shiftDrift = 0.0;
        if (TermStructure is not null)
        {
            const double h = 1.0e-4;
            double phi_t = Phi(t);
            double phi_th = Phi(t + h);
            double phiPrime = (phi_th - phi_t) / h;
            shiftDrift = (A * phi_t) + phiPrime;
        }

        double T = ForwardMeasureTime;
        return new Vector(
        [
            xProcess.Drift(t, x[0]) + XForwardDrift(t, T) + shiftDrift,
            yProcess.Drift(t, x[1]) + YForwardDrift(t, T),
        ]);
    }

    /// <inheritdoc/>
    public override Matrix Diffusion(double t, Vector x)
    {
        var tmp = new Matrix(2, 2);
        double sigma1 = Sigma;
        double sigma2 = Eta;
        tmp[0, 0] = sigma1;
        tmp[0, 1] = 0.0;
        tmp[1, 0] = Rho * sigma1;
        tmp[1, 1] = Math.Sqrt(1.0 - (Rho * Rho)) * sigma2;
        return tmp;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The process has no forward measure time.</exception>
    public override Vector Expectation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
        double shiftExp = 0.0;
        if (TermStructure is not null)
        {
            shiftExp = Phi(t0 + dt) - (Phi(t0) * Math.Exp(-A * dt));
        }

        double T = ForwardMeasureTime;
        return new Vector(
        [
            xProcess.Expectation(t0, x0[0], dt) - Mx_T(t0, t0 + dt, T) + shiftExp,
            yProcess.Expectation(t0, x0[1], dt) - My_T(t0, t0 + dt, T),
        ]);
    }

    /// <inheritdoc/>
    public override Matrix StdDeviation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
        return G2Process.G2ProcessStdDeviation(
            xProcess.StdDeviation(t0, x0[0], dt), yProcess.StdDeviation(t0, x0[1], dt), A, B, Rho, dt);
    }

    /// <inheritdoc/>
    public override Matrix Covariance(double t0, Vector x0, double dt)
    {
        Matrix sigma = StdDeviation(t0, x0, dt);
        Matrix result = sigma * MatrixFunctions.Transpose(sigma);
        return result;
    }

    /// <summary>A copy of the process at another forward measure time.</summary>
    /// <param name="forwardMeasureTime">The forward measure time T.</param>
    /// <returns>The copy; the receiver doesn't change.</returns>
    public override G2ForwardProcess WithForwardMeasureTime(double forwardMeasureTime) =>
        (G2ForwardProcess)base.WithForwardMeasureTime(forwardMeasureTime);

    /// <summary>The shift φ(t), as <see cref="G2Process.Phi"/>'s.</summary>
    /// <param name="t">The time.</param>
    /// <returns>φ(t).</returns>
    /// <exception cref="NanoQLException">The process has no curve.</exception>
    public double Phi(double t)
    {
        YieldTermStructure termStructure = TermStructure ?? throw new NanoQLException("no term structure given to G2ForwardProcess");
        double forward = termStructure.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double temp1 = Sigma * t * OneMinusExpOverX(A * t);
        double temp2 = Eta * t * OneMinusExpOverX(B * t);
        return (0.5 * temp1 * temp1) + (0.5 * temp2 * temp2) + (Rho * temp1 * temp2) + forward;
    }

    /// <summary>The short rate at the state: z₁ + z₂.</summary>
    /// <param name="t">The time, unused.</param>
    /// <param name="z1">The first component.</param>
    /// <param name="z2">The second component.</param>
    /// <returns>The short rate.</returns>
    public double ShortRate(double t, double z1, double z2) => z1 + z2;

    // -(sigma^2/a)*(1 - exp(-a(T-t))) - (rho*sigma*eta/b)*(1 - exp(-b(T-t)))
    private double XForwardDrift(double t, double T)
    {
        double tau = T - t;
        return -(Sigma * Sigma * tau * OneMinusExpOverX(A * tau)) - (Rho * Sigma * Eta * tau * OneMinusExpOverX(B * tau));
    }

    // -(eta^2/b)*(1 - exp(-b(T-t))) - (rho*sigma*eta/a)*(1 - exp(-a(T-t)))
    private double YForwardDrift(double t, double T)
    {
        double tau = T - t;
        return -(Eta * Eta * tau * OneMinusExpOverX(B * tau)) - (Rho * Sigma * Eta * tau * OneMinusExpOverX(A * tau));
    }

    // QuantLib's Mx_T: (sigma^2/a^2 + rho*sigma*eta/(a*b))*(1 - exp(-a(t-s)))
    // - sigma^2/(2a^2)*(exp(-a(T-t)) - exp(-a(T+t-2s))) - rho*sigma*eta/(b(a+b))*(exp(-b(T-t)) - exp(-bT-at+(a+b)s))
    private double Mx_T(double s, double t, double T) => M(Sigma, Eta, A, B, s, t, T);

    // QuantLib's My_T, its mirror
    private double My_T(double s, double t, double T) => M(Eta, Sigma, B, A, s, t, T);

    // The mirrored terms with tau1 = t - s and tau2 = T - t: sigma1^2*tau1*f(a1*tau1)*(tau2*f(a1*tau2)
    // + 0.5*exp(-a1*tau2)*tau1*f(a1*tau1)) + rho*sigma1*sigma2*tau1*(tau1*D(a1*tau1, a2*tau1)
    // + tau2*f(a2*tau2)*f((a1+a2)*tau1))
    private double M(double sigma1, double sigma2, double a1, double a2, double s, double t, double T)
    {
        double tau1 = t - s, tau2 = T - t;
        double f1 = OneMinusExpOverX(a1 * tau1);
        double own = sigma1 * sigma1 * tau1 * f1 * ((tau2 * OneMinusExpOverX(a1 * tau2)) + (0.5 * Math.Exp(-a1 * tau2) * tau1 * f1));
        double cross = Rho * sigma1 * sigma2 * tau1
            * ((tau1 * DecayRatioSlope(a1 * tau1, a2 * tau1)) + (tau2 * OneMinusExpOverX(a2 * tau2) * OneMinusExpOverX((a1 + a2) * tau1)));
        return own + cross;
    }
}
