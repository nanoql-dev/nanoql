// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/hestonrndcalculator.hpp ql/methods/finitedifferences/utilities/hestonrndcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>The risk-neutral terminal density of the Heston model in the log spot, x = ln S.</summary>
/// <remarks>
/// <para>
/// Immutable. Its times are the risk-free curve's, the dividend curve read at the instant a time denotes on its own
/// clock, as <see cref="BSMRNDCalculator"/> reads it, where QuantLib reads it at the risk-free curve's time; a dividend
/// curve on the risk-free curve's day counter and reference date is read as QuantLib reads it. Where κt is below 1e-12
/// the density's κ is raised to 1e-12/t, the density moving by about κt relative there, below the tolerance, where
/// QuantLib's integration throws "Interval contains no more machine number" from κ = 1e-16.
/// </para>
/// <para>
/// Reference: A. Dragulescu, V. Yakovenko, Probability distribution of returns in the Heston model with stochastic
/// volatility, 2002, http://arxiv.org/pdf/cond-mat/0203046.pdf.
/// </para>
/// </remarks>
public sealed class HestonRNDCalculator : RiskNeutralDensityCalculator
{
    // the kappa t below which the density's kappa is raised to it over t
    private const double SmallKappaT = 1e-12;

    private readonly HestonProcess hestonProcess;
    private readonly double x0;
    private readonly double integrationEps;
    private readonly int maxIntegrationIterations;
    private readonly TermStructure clock;

    /// <summary>Initializes a new instance of the <see cref="HestonRNDCalculator"/> class.</summary>
    /// <param name="hestonProcess">The Heston process.</param>
    /// <param name="integrationEps">The integrations' accuracy.</param>
    /// <param name="maxIntegrationIterations">The integrations' limit on iterations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hestonProcess"/> is <see langword="null"/>.</exception>
    public HestonRNDCalculator(HestonProcess hestonProcess, double integrationEps = 1e-6, int maxIntegrationIterations = 10000)
        : this(hestonProcess, integrationEps, maxIntegrationIterations, hestonProcess?.RiskFreeRate!)
    {
    }

    // The analytic PDF engine's form: its times on the model's clock (HestonProcess.ModelClock), each curve read at the
    // instant a time denotes.
    internal HestonRNDCalculator(HestonProcess hestonProcess, double integrationEps, int maxIntegrationIterations, TermStructure clock)
    {
        ArgumentNullException.ThrowIfNull(hestonProcess);
        this.hestonProcess = hestonProcess;
        x0 = Math.Log(hestonProcess.S0);
        this.integrationEps = integrationEps;
        this.maxIntegrationIterations = maxIntegrationIterations;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public override double Pdf(double x, double t) =>
        new GaussLobattoIntegral(maxIntegrationIterations, 0.1 * integrationEps).Integrate(
            new CpxPvHelper(Params(t), XT(x, t), t).Evaluate, 0.0, 1.0) / (2.0 * Math.PI);

    /// <inheritdoc/>
    public override double Cdf(double x, double t)
    {
        var helper = new CpxPvHelper(Params(t), XT(x, t), t);
        return (new GaussLobattoIntegral(maxIntegrationIterations, 0.1 * integrationEps).Integrate(helper.P0, 0.0, 1.0) / (2.0 * Math.PI)) + 0.5;
    }

    // The distribution of the log spot under the share measure, the spot the numeraire: there -ln S is a Heston log
    // spot again, its mean reversion kappa - rho sigma, its level kappa theta/(kappa - rho sigma), its correlation -rho
    // and its forward 1/F, so the risk-neutral distribution's integral at the reflected point. E[S; S <= e^x] = F times
    // it.
    internal double ShareCdf(double x, double t)
    {
        HestonParams p = Params(t);
        double kappa = p.Kappa - (p.Rho * p.Sigma);
        if (Math.Abs(kappa) * t < SmallKappaT)
        {
            kappa = SmallKappaT / t;
        }

        var share = new HestonParams(p.V0, kappa, p.Kappa * p.Theta / kappa, p.Sigma, -p.Rho);
        var helper = new CpxPvHelper(share, -XT(x, t), t);
        return 0.5 - (new GaussLobattoIntegral(maxIntegrationIterations, 0.1 * integrationEps).Integrate(helper.P0, 0.0, 1.0) / (2.0 * Math.PI));
    }

    /// <inheritdoc/>
    public override double InvCdf(double p, double t)
    {
        double v0 = hestonProcess.V0;

        // the density's kappa, floored as Params floors it
        double kappa = Params(t).Kappa;
        double theta = hestonProcess.Theta;
        double expVol = Math.Sqrt(theta + ((v0 - theta) * (1 - Math.Exp(-kappa * t)) / (t * kappa)));

        var bsmProcess = new BlackScholesMertonProcess(
            hestonProcess.S0,
            hestonProcess.DividendYield,
            hestonProcess.RiskFreeRate,
            new BlackConstantVol(clock.ReferenceDate, new NullCalendar(), expVol, clock.DayCounter));

        double guess = new BSMRNDCalculator(bsmProcess).InvCdf(p, t);
        return new InvCdfHelper(this, guess, 0.1 * integrationEps, maxIntegrationIterations).InverseCdf(p, t);
    }

    // the log spot's distance from its forward at t, the curves read at the instant t denotes on the clock
    private double XT(double x, double t)
    {
        double dr = hestonProcess.RiskFreeRate.DiscountAt(clock, t);
        double dq = hestonProcess.DividendYield.DiscountAt(clock, t);
        return x - x0 + Math.Log(dr / dq);
    }

    // the model's parameters, kappa raised to 1e-12/t where kappa t is below 1e-12
    private HestonParams Params(double t)
    {
        double kappa = hestonProcess.Kappa;
        if (kappa * t < SmallKappaT)
        {
            kappa = SmallKappaT / t;
        }

        return new HestonParams(hestonProcess.V0, kappa, hestonProcess.Theta, hestonProcess.Sigma, hestonProcess.Rho);
    }

    private readonly record struct HestonParams(double V0, double Kappa, double Theta, double Sigma, double Rho);

    // QuantLib's CpxPv_Helper: the density's and the distribution's integrands after the substitution u = -ln(x)/c_inf
    private sealed class CpxPvHelper
    {
        // -ln(QL_EPSILON): the integrands return 0 below u = QL_EPSILON, so the integral stops at x = this / c_inf
        private static readonly double CutExponent = -Math.Log(NumericLimits.QL_EPSILON);

        private readonly HestonParams p;
        private readonly double t;
        private readonly double x;
        private readonly double cInf;

        internal CpxPvHelper(HestonParams p, double x, double t)
        {
            this.p = p;
            this.t = t;
            this.x = x;
            // QuantLib's c_inf, capped where its cut x = -ln(QL_EPSILON)/c_inf comes before the characteristic
            // function's decay, about e^{-V x^2/2} with V the expected integrated variance, reaches QL_EPSILON: at a
            // small vol of variance over long terms, and at short terms, QuantLib's cut comes early and the PDF engine
            // is off by up to 600%; QuantLib's constant, and so its bits, where it already cuts below QL_EPSILON
            double quantLib = Min(10.0, Max(0.0001, Math.Sqrt(1.0 - (p.Rho * p.Rho)) / p.Sigma)) * (p.V0 + (p.Kappa * p.Theta * t));
            cInf = Min(quantLib, Math.Sqrt(CutExponent * ExpectedVariance(p, t) / 2.0));
        }

        // theta t + (v0 - theta)(1 - e^{-kappa t})/kappa, the PDF engine's window variance, 1 - e^{-kappa t} by
        // -expm1(-kappa t) below kappa t = 1e-2
        private static double ExpectedVariance(HestonParams p, double t)
        {
            double kt = p.Kappa * t;
            double decay = kt < 1e-2 ? -Expm1Functions.StdExpm1(-kt) : 1 - Math.Exp(-kt);
            return (p.Theta * t) + ((p.V0 - p.Theta) * decay / p.Kappa);
        }

        internal double Evaluate(double u) => TransformPhi(u).Real;

        internal double P0(double pX)
        {
            if (pX < NumericLimits.QL_EPSILON)
            {
                return 0.0;
            }

            double uX = Max(NumericLimits.QL_EPSILON, -Math.Log(pX) / cInf);
            return (Phi(uX) / (pX * cInf * new Complex(0.0, uX))).Real;
        }

        // std::max and std::min, which return their first argument where neither is less than the other
        private static double Max(double a, double b) => a < b ? b : a;

        private static double Min(double a, double b) => b < a ? b : a;

        private static Complex Gamma(HestonParams p, double pX) => new(p.Kappa, p.Rho * p.Sigma * pX);

        private static Complex Omega(HestonParams p, double pX)
        {
            Complex g = Gamma(p, pX);
            return Complex.Sqrt((g * g) + (p.Sigma * p.Sigma * new Complex(pX * pX, -pX)));
        }

        private Complex TransformPhi(double u)
        {
            if (u < NumericLimits.QL_EPSILON)
            {
                return new Complex(0.0, 0.0);
            }

            double uX = -Math.Log(u) / cInf;
            return Phi(uX) / (u * cInf);
        }

        private Complex Phi(double pX)
        {
            double sigma2 = p.Sigma * p.Sigma;
            Complex g = Gamma(p, pX);
            Complex o = Omega(p, pX);
            Complex gamma = (g - o) / (g + o);
            return 2.0 * ComplexFunctions.Exp(
                new Complex(0.0, pX * x)
                - (p.V0 * new Complex(pX * pX, -pX) / (g + (o * (1.0 + ComplexFunctions.Exp(-o * t)) / (1.0 - ComplexFunctions.Exp(-o * t)))))
                + (p.Kappa * p.Theta / sigma2 * (((g - o) * t) - (2.0 * Complex.Log((1.0 - (gamma * ComplexFunctions.Exp(-o * t))) / (1.0 - gamma))))));
        }
    }
}
