// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Lorella Fatone
// SPDX-FileCopyrightText: Copyright (C) 2008 Francesca Mariani
// SPDX-FileCopyrightText: Copyright (C) 2008 Maria Cristina Recchioni
// SPDX-FileCopyrightText: Copyright (C) 2008 Francesco Zirilli
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/varianceoption/integralhestonvarianceoptionengine.hpp ql/experimental/varianceoption/integralhestonvarianceoptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The integral engine for variance options under Heston: the realized variance's law inverted by Bailey and
/// Swarztrauber's fractional transform (see &lt;http://www.econ.univpm.it/recchioni/finance/w4/&gt;).
/// </summary>
/// <remarks>
/// <para>
/// A call has QuantLib's one-dimensional integral. The engine reads
/// the risk-free curve on the model's clock, the earliest reference date of the process's curves on the risk-free day
/// counter, the maturity's time from it and the rate the curve accrues to the maturity per unit of that time, where
/// QuantLib measures the time from the evaluation date; on curves dated on the evaluation date QuantLib's numbers
/// stand.
/// </para>
/// <para>
/// Documented divergences (<c>Divergences/IntegralHestonVarianceOptionTests.cs</c>). A put is the call's integral
/// without its forward term, the integrated variance's mean less the strike, by parity, and other payoffs are refused,
/// where QuantLib sums any payoff but a call against the integrated variance's density on 4,096 points of spacing
/// √(2π)/64, about one standard deviation of it: QuantLib's put case prices 0.0466796 against 0.0465915, a
/// forward-start put 2.6% low, a cash-or-nothing put 13% to 24% low and above 1 at a strike past the distribution. The
/// process's dividend curve isn't read, as the integrated variance's law doesn't depend on the drift, where QuantLib
/// refuses a process whose dividend handle isn't empty. An option starting after the clock's origin is priced on the
/// variance integrated from its start: each integrand is affine in the variance at the start, so its expectation over
/// that variance's non-central χ² law from v₀ is the CIR transform, E[e^(Bv_s)] = (1 − 2cB)^(−d/2) exp(Bv₀e^(−κs)/(1 −
/// 2cB)), c = σ²(1 − e^(−κs))/(4κ), d = 4κθ/σ², and the mean term takes E[v_s] = θ + (v₀ − θ)e^(−κs), where QuantLib
/// prices every option as starting today; a seasoned option, its start before the evaluation date, is refused, as
/// QuantLib's header says.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class IntegralHestonVarianceOptionEngine
{
    /// <summary>Prices a variance option under Heston.</summary>
    /// <param name="option">The variance option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Heston process; its dividend curve isn't read.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't a plain-vanilla call or put ("this engine prices plain-vanilla calls and puts only"), the option
    /// starts before the evaluation date ("this engine does not manage seasoned variance options"), 2κθ/σ² − 1 isn't
    /// positive ("this parameter must be greater than zero"), or the integral leaves an imaginary part.
    /// </exception>
    public static InstrumentResults Calculate(VarianceOption option, EvaluationContext context, HestonProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new InstrumentResults { NPV = Value(option, context, process) });
    }

    private static double Value(VarianceOption option, EvaluationContext context, HestonProcess process)
    {
        // QuantLib's two-dimensional integral for other payoffs isn't ported
        if (option.Payoff is not PlainVanillaPayoff plainPayoff)
        {
            throw new NanoQLException("this engine prices plain-vanilla calls and puts only");
        }

        if (option.StartDate < context.EvaluationDate)
        {
            throw new NanoQLException("this engine does not manage seasoned variance options");
        }

        YieldTermStructure riskFreeRate = process.RiskFreeRate;
        double epsilon = process.Sigma;
        double chi = process.Kappa;
        double theta = process.Theta;
        double rho = process.Rho;
        double v0 = process.V0;

        // the model's clock: the maturity's time and the risk-free rate per unit of it
        double tau = process.ModelTime(option.MaturityDate);
        double r = riskFreeRate.ZeroRate(option.MaturityDate, riskFreeRate.DayCounter, Compounding.Continuous).Rate;
        double curveTime = riskFreeRate.AccrualTimeTo(option.MaturityDate);
        if (curveTime != tau)
        {
            r = tau == 0.0 ? r : (curveTime == 0.0 ? 0.0 : r * (curveTime / tau));
        }

        // a forward start: the variance integrated over [s, T] from the variance's law at s
        double s = Math.Min(process.ModelTime(option.StartDate), tau);
        StartLaw? start = s > 0.0 ? new StartLaw(epsilon, chi, theta, v0, s) : null;

        // a specialization for Call options is available; a put by parity
        double strike = plainPayoff.Strike;
        bool call = plainPayoff.OptionType == OptionType.Call;
        return IvopOneDim(epsilon, chi, theta, rho, v0, strike, tau, r, start, call) * option.Notional;
    }

    // QuantLib's one-dimensional integral for a call on the realized variance at strike eprice; a put is the integral
    // without the forward term vero
    private static double IvopOneDim(
        double eps, double chi, double theta, double rho, double v0, double eprice, double tau, double rtax, StartLaw? start,
        bool call)
    {
        _ = rho;
        double ss;
        double nris;
        int j, mm;
        double pi, pi2;
        double dstep;
        double optionValue, impart;
        Complex xi;
        var ui = new Complex(0.0, 1.0);
        Complex beta, zita, gamma, csum, vero;
        Complex contrib, caux, caux1, caux2, caux3;

        // i0: initial integrated variance i0=0
        const double i0 = 0.0;

        pi = 3.14159265358979324;
        pi2 = 2.0 * pi;
        double s = (2.0 * chi * theta / (eps * eps)) - 1.0;

        // Note that s must be greater than zero
        if (s <= 0)
        {
            throw new NanoQLException($"this parameter must be greater than zero-> {s}");
        }

        ss = s + 1;

        // the variance's horizon: from the start where the option starts forward
        double horizon = start is null ? tau : tau - start.Time;

        dstep = 256.0;
        nris = Math.Sqrt(pi2) / dstep;
        mm = (int)(pi2 / (nris * nris));
        var xiv = new double[mm + 1];
        var ff = new Complex[mm + 1];

        for (j = 0; j <= mm - 1; j++)
        {
            xiv[j + 1] = (j - (mm / 2.0)) * nris;
        }

        for (j = 0; j <= mm - 1; j++)
        {
            xi = xiv[j + 1];
            caux = chi * chi;
            caux1 = 2.0 * eps * eps;
            caux1 *= xi;
            caux1 *= ui;
            caux2 = caux1 + caux;
            zita = 0.5 * Complex.Sqrt(caux2);
            caux1 = ComplexFunctions.Exp(-2.0 * horizon * zita);
            beta = (0.5 * chi) + zita;
            beta += caux1 * (zita - (0.5 * chi));
            gamma = 1.0 - caux1;
            caux = -ss * horizon;
            caux2 = caux * (zita - (0.5 * chi));
            caux = ss * Complex.Log(2.0 * (zita / beta));
            if (start is null)
            {
                caux3 = -v0 * ui * xi * (gamma / beta);
                caux += caux3;
                caux += caux2;
                ff[j + 1] = ComplexFunctions.Exp(caux);
            }
            else
            {
                caux += caux2;
                ff[j + 1] = ComplexFunctions.Exp(caux) * start.Transform(-ui * xi * (gamma / beta));
            }

            if (Math.Sqrt((xi.Imaginary * xi.Imaginary) + (xi.Real * xi.Real)) > 1.0e-06)
            {
                contrib = -eprice / (ui * xi);
                caux = ui * xi;
                caux *= eprice;
                caux = ComplexFunctions.Exp(caux);
                caux -= 1.0;
                caux2 = ui * xi * ui * xi;
                contrib += caux / caux2;
            }
            else
            {
                contrib = eprice * eprice * 0.5;
            }

            ff[j + 1] *= contrib;
        }

        csum = 0.0;
        for (j = 0; j <= mm - 1; j++)
        {
            caux = Math.Pow(-1.0, j);
            caux2 = -2.0 * pi * mm * j * 0.5 / mm;
            caux3 = ui * caux2;
            csum += ff[j + 1] * caux * ComplexFunctions.Exp(caux3);
        }

        csum = csum * Math.Sqrt(Math.Pow(-1.0, mm)) * nris / pi2;
        if (call)
        {
            double vStart = start is null ? v0 : start.MeanVariance;
            vero = i0 - eprice + (theta * horizon) + ((1.0 - Math.Exp(-chi * horizon)) * (vStart - theta) / chi);
            csum += vero;
        }

        optionValue = Math.Exp(-rtax * tau) * csum.Real;
        impart = csum.Imaginary;
        if (!(impart <= 1e-12))
        {
            throw new NanoQLException($"imaginary part option (must be zero) = {impart}");
        }

        return optionValue;
    }

    // The CIR law of the variance at the start s from v₀: its transform E[e^(Bv_s)] and its mean
    private sealed class StartLaw(double sigma, double kappa, double theta, double v0, double s)
    {
        private readonly double c = sigma * sigma * (1.0 - Math.Exp(-kappa * s)) / (4.0 * kappa);
        private readonly double d = 4.0 * kappa * theta / (sigma * sigma);
        private readonly double decay = Math.Exp(-kappa * s);

        public double Time => s;

        public double MeanVariance => theta + ((v0 - theta) * decay);

        public Complex Transform(Complex b)
        {
            Complex denominator = 1.0 - (2.0 * c * b);
            return ComplexFunctions.Pow(denominator, -d / 2.0) * ComplexFunctions.Exp(b * v0 * decay / denominator);
        }
    }
}
