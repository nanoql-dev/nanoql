// SPDX-FileCopyrightText: Copyright (C) 2006 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/g2process.hpp ql/processes/g2process.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Models.G2Functions;

namespace NanoQL.Processes;

/// <summary>
/// The G2 stochastic process: the two-factor G2++ process with its state shifted so that the two components sum to
/// the short rate, (z₁, z₂) = (x + φ(t), y), x and y the zero-mean Ornstein-Uhlenbeck factors and φ the deterministic
/// offset that fits the initial curve; without a curve, the two zero-mean processes (φ ≡ 0).
/// </summary>
/// <remarks>
/// <para>
/// A path generated on it satisfies r(tᵢ) = z₁ᵢ + z₂ᵢ, its expectation φ(tᵢ). The step's conditional correlation,
/// QuantLib's ρση(1 − e^(−(a+b)Δt))/(a + b) over ½ση√((1 − e^(−2aΔt))(1 − e^(−2bΔt))/(ab)), is written as
/// ρ·f((a+b)Δt)/√(f(2aΔt)·f(2bΔt)), f(x) = (1 − e^(−x))/x, and held in [−1, 1], ρ itself where a = b; φ's terms as
/// G2's: a divergence, QuantLib's √(1 − ρ²) of a correlation an ulp past ±1 being NaN at ρ = ±1 with a = b
/// (<c>Divergences/G2ProcessCorrelationBoundTests.cs</c>), and its terms cancelling at small reversion.
/// </para>
/// </remarks>
public sealed class G2Process : StochasticProcess
{
    private readonly double x0 = 0.0, y0 = 0.0;
    private readonly OrnsteinUhlenbeckProcess xProcess;
    private readonly OrnsteinUhlenbeckProcess yProcess;

    /// <summary>Initializes a new instance of the <see cref="G2Process"/> class.</summary>
    /// <param name="a">The first factor's mean reversion.</param>
    /// <param name="sigma">The first factor's volatility, not negative.</param>
    /// <param name="b">The second factor's mean reversion.</param>
    /// <param name="eta">The second factor's volatility, not negative.</param>
    /// <param name="rho">The factors' correlation.</param>
    /// <param name="termStructure">The curve the shift fits; <see langword="null"/>, QuantLib's empty handle, for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sigma"/> or <paramref name="eta"/> is negative or NaN.</exception>
    public G2Process(double a, double sigma, double b, double eta, double rho, YieldTermStructure? termStructure = null)
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

    /// <summary>Gets the first component's initial value: φ(0) on a curve, 0 without one.</summary>
    public double X0 => TermStructure is null ? x0 : Phi(0.0);

    /// <summary>Gets the second component's initial value, 0.</summary>
    public double Y0 => y0;

    /// <inheritdoc/>
    public override Vector InitialValues()
    {
        double z1_0 = TermStructure is null ? x0 : Phi(0.0);
        return new Vector([z1_0, y0]);
    }

    /// <inheritdoc/>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);

        // Drift in shifted coordinates z1 = x + phi(t), z2 = y:
        //   dz1 = (-a*z1 + a*phi(t) + phi'(t)) dt + sigma dW1
        //   dz2 = -b*z2 dt + eta dW2
        double shiftDrift = 0.0;
        if (TermStructure is not null)
        {
            const double h = 1.0e-4;
            double phi_t = Phi(t);
            double phi_th = Phi(t + h);
            double phiPrime = (phi_th - phi_t) / h;
            shiftDrift = (A * phi_t) + phiPrime;
        }

        return new Vector([xProcess.Drift(t, x[0]) + shiftDrift, yProcess.Drift(t, x[1])]);
    }

    /// <inheritdoc/>
    public override Matrix Diffusion(double t, Vector x)
    {
        /* the correlation matrix is
           |  1   rho |
           | rho   1  |
           whose square root (which is used here) is
           |  1          0       |
           | rho   sqrt(1-rho^2) |
        */
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
    public override Vector Expectation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);

        // E[z1(t0+dt) | z1(t0)] = z1(t0)*exp(-a*dt)
        //                       + phi(t0+dt) - phi(t0)*exp(-a*dt)
        // E[z2(t0+dt) | z2(t0)] = z2(t0)*exp(-b*dt)
        double shiftExp = 0.0;
        if (TermStructure is not null)
        {
            shiftExp = Phi(t0 + dt) - (Phi(t0) * Math.Exp(-A * dt));
        }

        return new Vector([xProcess.Expectation(t0, x0[0], dt) + shiftExp, yProcess.Expectation(t0, x0[1], dt)]);
    }

    /// <inheritdoc/>
    public override Matrix StdDeviation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
        return G2ProcessStdDeviation(
            xProcess.StdDeviation(t0, x0[0], dt), yProcess.StdDeviation(t0, x0[1], dt), A, B, Rho, dt);
    }

    /// <inheritdoc/>
    public override Matrix Covariance(double t0, Vector x0, double dt)
    {
        Matrix sigma = StdDeviation(t0, x0, dt);
        Matrix result = sigma * MatrixFunctions.Transpose(sigma);
        return result;
    }

    /// <summary>
    /// The shift φ(t) = f(t) + ½(σ(1 − e^(−at))/a)² + ½(η(1 − e^(−bt))/b)² + ρ(σ(1 − e^(−at))/a)(η(1 − e^(−bt))/b),
    /// f the curve's instantaneous forward.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>φ(t).</returns>
    /// <exception cref="NanoQLException">The process has no curve.</exception>
    public double Phi(double t)
    {
        YieldTermStructure termStructure = TermStructure ?? throw new NanoQLException("no term structure given to G2Process");
        double forward = termStructure.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double temp1 = Sigma * t * OneMinusExpOverX(A * t);
        double temp2 = Eta * t * OneMinusExpOverX(B * t);
        return (0.5 * temp1 * temp1) + (0.5 * temp2 * temp2) + (Rho * temp1 * temp2) + forward;
    }

    /// <summary>The short rate at the state: z₁ + z₂, the state already including φ(t).</summary>
    /// <param name="t">The time, unused.</param>
    /// <param name="z1">The first component.</param>
    /// <param name="z2">The second component.</param>
    /// <returns>The short rate.</returns>
    public double ShortRate(double t, double z1, double z2) => z1 + z2;

    /// <summary>
    /// The step's standard deviation: the factors' over Δt, correlated by ρ·f((a+b)Δt)/√(f(2aΔt)·f(2bΔt)), held in
    /// [−1, 1] and ρ itself where a = b. Shared with <see cref="G2ForwardProcess"/>.
    /// </summary>
    /// <param name="sigma1">The first factor's standard deviation over the step.</param>
    /// <param name="sigma2">The second factor's standard deviation over the step.</param>
    /// <param name="a">The first factor's mean reversion.</param>
    /// <param name="b">The second factor's mean reversion.</param>
    /// <param name="rho">The factors' correlation.</param>
    /// <param name="dt">The step.</param>
    /// <returns>The lower-triangular standard deviation.</returns>
    internal static Matrix G2ProcessStdDeviation(double sigma1, double sigma2, double a, double b, double rho, double dt)
    {
        var tmp = new Matrix(2, 2);

        // QuantLib's (rho*sigma*eta)/(a+b)*(1-expa*expb) over (0.5*sigma*eta)*sqrt((1-expa^2)*(1-expb^2)/(a*b))
        double newRho;
        if (a == b)
        {
            newRho = rho;
        }
        else
        {
            newRho = rho * OneMinusExpOverX((a + b) * dt)
                / Math.Sqrt(OneMinusExpOverX(2.0 * a * dt) * OneMinusExpOverX(2.0 * b * dt));
            newRho = newRho > 1.0 ? 1.0 : newRho < -1.0 ? -1.0 : newRho;
        }

        tmp[0, 0] = sigma1;
        tmp[0, 1] = 0.0;
        tmp[1, 0] = newRho * sigma2;
        tmp[1, 1] = Math.Sqrt(1.0 - (newRho * newRho)) * sigma2;
        return tmp;
    }
}
