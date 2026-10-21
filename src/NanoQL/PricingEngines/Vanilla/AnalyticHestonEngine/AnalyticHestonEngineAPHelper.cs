// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analytichestonengine.hpp ql/pricingengines/vanilla/analytichestonengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The integrand of <see cref="AnalyticHestonEngine"/>'s control-variate forms and the control variate's value:
/// QuantLib's <c>AnalyticHestonEngine::AP_Helper</c>.
/// </summary>
/// <remarks>
/// The integrand is the damped difference between the control variate's characteristic function and the model's, on
/// the real line for Andersen and Piterbarg's forms and on an angled contour for the others; the price is the control
/// variate's value plus the forward over π times the integral, discounted. Immutable.
/// </remarks>
public sealed class AnalyticHestonEngineAPHelper
{
    private readonly double term;
    private readonly double fwd;
    private readonly double strike;
    private readonly double freq;
    private readonly AnalyticHestonEngineComplexLogFormula cpxLog;
    private readonly AnalyticHestonEngine engine;
    private readonly double alpha;
    private readonly double sAlpha;
    private readonly double vAvg;
    private readonly double tanPhi;
    private readonly Complex phi;
    private readonly Complex psi;

    /// <summary>Initializes a new instance of the <see cref="AnalyticHestonEngineAPHelper"/> class.</summary>
    /// <param name="term">The maturity.</param>
    /// <param name="fwd">The forward.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="cpxLog">The control variate: one of Andersen and Piterbarg's, the asymptotic expansion's or the
    /// angled contour's.</param>
    /// <param name="engine">The engine whose model and characteristic function the integrand reads.</param>
    /// <param name="alpha">The damping exponent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cpxLog"/> isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="cpxLog"/> is no control variate: Gatheral's form, the
    /// branch correction or the optimal choice.</exception>
    public AnalyticHestonEngineAPHelper(
        double term,
        double fwd,
        double strike,
        AnalyticHestonEngineComplexLogFormula cpxLog,
        AnalyticHestonEngine engine,
        double alpha = -0.5)
    {
        if (engine is null)
        {
            throw new ArgumentNullException(nameof(engine), "pricing engine required");
        }

        EnumArguments.Check(cpxLog, nameof(cpxLog));
        this.term = term;
        this.fwd = fwd;
        this.strike = strike;
        freq = Math.Log(fwd / strike);
        this.cpxLog = cpxLog;
        this.engine = engine;
        this.alpha = alpha;
        sAlpha = Math.Exp(alpha * freq);

        double v0 = engine.Model.V0;
        double kappa = engine.Model.Kappa;
        double theta = engine.Model.Theta;
        double sigma = engine.Model.Sigma;
        double rho = engine.Model.Rho;

        switch (this.cpxLog)
        {
            case AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg:
                vAvg = ((1 - Math.Exp(-kappa * term)) * (v0 - theta) / (kappa * term)) + theta;
                break;
            case AnalyticHestonEngineComplexLogFormula.AndersenPiterbargOptCV:
                vAvg = -8.0 * Math.Log(engine.ChF(new Complex(0, this.alpha), term).Real) / term;
                break;
            case AnalyticHestonEngineComplexLogFormula.AsymptoticChF:
            case AnalyticHestonEngineComplexLogFormula.AngledContour:
            case AnalyticHestonEngineComplexLogFormula.AngledContourNoCV:
                if (this.cpxLog == AnalyticHestonEngineComplexLogFormula.AsymptoticChF)
                {
                    phi = -(v0 + (term * kappa * theta)) / sigma * new Complex(Math.Sqrt(1 - (rho * rho)), rho);

                    psi = new Complex(
                        ((kappa - (0.5 * rho * sigma)) * (v0 + (term * kappa * theta)))
                        + (kappa * theta * Math.Log(4 * (1 - (rho * rho)))),
                        -((((0.5 * rho * rho * sigma) - (kappa * rho)) / Math.Sqrt(1 - (rho * rho)) * (v0 + (kappa * theta * term)))
                          - (2 * kappa * theta * Math.Atan(rho / Math.Sqrt(1 - (rho * rho))))))
                          / (sigma * sigma);
                }

                // QuantLib's fall-through: the asymptotic expansion and the angled contour take the average variance
                if (this.cpxLog != AnalyticHestonEngineComplexLogFormula.AngledContourNoCV)
                {
                    vAvg = ((1 - Math.Exp(-kappa * term)) * (v0 - theta) / (kappa * term)) + theta;
                }

                {
                    double r = rho - (sigma * freq / (v0 + (kappa * theta * term)));
                    tanPhi = Math.Tan(r * freq < 0.0 ? Math.PI / 12 * Math.Sign(freq) : 0.0);
                }

                break;
            default:
                throw new ArgumentException("unknown control variate", nameof(cpxLog));
        }
    }

    /// <summary>The integrand at <paramref name="u"/>.</summary>
    /// <param name="u">The integration variable.</param>
    /// <returns>The integrand's value.</returns>
    /// <exception cref="NanoQLException">The engine adds a term to the characteristic function: the control variates
    /// serve the Heston model alone.</exception>
    public double Evaluate(double u)
    {
        if (!(engine.AddOnTermOf(u, term, 1) == new Complex(0.0, 0.0)
              && engine.AddOnTermOf(u, term, 2) == new Complex(0.0, 0.0)))
        {
            throw new NanoQLException("only Heston model is supported");
        }

        var i = new Complex(0, 1);

        if (cpxLog == AnalyticHestonEngineComplexLogFormula.AngledContour
            || cpxLog == AnalyticHestonEngineComplexLogFormula.AngledContourNoCV
            || cpxLog == AnalyticHestonEngineComplexLogFormula.AsymptoticChF)
        {
            var h_u = new Complex(u, (u * tanPhi) - alpha);
            Complex hPrime = h_u - i;

            Complex phiBS = new(0.0, 0.0);
            if (cpxLog == AnalyticHestonEngineComplexLogFormula.AngledContour)
            {
                phiBS = Complex.Exp(
                    -0.5 * vAvg * term * ((hPrime * hPrime) + new Complex(-hPrime.Imaginary, hPrime.Real)));
            }
            else if (cpxLog == AnalyticHestonEngineComplexLogFormula.AsymptoticChF)
            {
                phiBS = Complex.Exp((u * new Complex(1, tanPhi) * phi) + psi);
            }

            return Math.Exp(-u * tanPhi * freq)
                   * (Complex.Exp(new Complex(0.0, u * freq))
                      * new Complex(1, tanPhi)
                      * (phiBS - engine.ChF(hPrime, term)) / (h_u * hPrime)).Real * sAlpha;
        }
        else if (cpxLog == AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg
                 || cpxLog == AnalyticHestonEngineComplexLogFormula.AndersenPiterbargOptCV)
        {
            var z = new Complex(u, -alpha);
            var zPrime = new Complex(u, -alpha - 1);
            Complex phiBS = Complex.Exp(
                -0.5 * vAvg * term * ((zPrime * zPrime) + new Complex(-zPrime.Imaginary, zPrime.Real)));

            return (Complex.Exp(new Complex(0.0, u * freq))
                    * (phiBS - engine.ChF(zPrime, term)) / (z * zPrime)).Real * sAlpha;
        }
        else
        {
            throw new NanoQLException("unknown control variate");
        }
    }

    /// <summary>The control variate's value, undiscounted.</summary>
    /// <returns>The value: a Black price at the average variance for Andersen and Piterbarg's forms and the angled
    /// contour, the asymptotic expansion's closed form, or the residue term without a control variate.</returns>
    /// <exception cref="NanoQLException">The asymptotic expansion's damping exponent isn't −0.5.</exception>
    public double ControlVariateValue()
    {
        if (cpxLog == AnalyticHestonEngineComplexLogFormula.AngledContour
            || cpxLog == AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg
            || cpxLog == AnalyticHestonEngineComplexLogFormula.AndersenPiterbargOptCV)
        {
            return new BlackCalculator(OptionType.Call, strike, fwd, Math.Sqrt(vAvg * term)).Value;
        }
        else if (cpxLog == AnalyticHestonEngineComplexLogFormula.AsymptoticChF)
        {
            if (!(alpha == -0.5))
            {
                throw new NanoQLException("alpha must be equal to -0.5");
            }

            var phiFreq = new Complex(phi.Real, phi.Imaginary + freq);

            return fwd - (Math.Sqrt(strike * fwd) / Math.PI
                          * (Complex.Exp(psi)
                             * ((-2.0 * ExponentialIntegralsFunctions.Ci(-0.5 * phiFreq) * Complex.Sin(0.5 * phiFreq))
                                + (Complex.Cos(0.5 * phiFreq) * (Math.PI + (2.0 * ExponentialIntegralsFunctions.Si(0.5 * phiFreq)))))).Real);
        }
        else if (cpxLog == AnalyticHestonEngineComplexLogFormula.AngledContourNoCV)
        {
            return (alpha <= 0.0 ? fwd : 0.0)
                   - (alpha <= -1.0 ? strike : 0.0)
                   - (0.5 * (alpha == 0.0 ? fwd : 0.0))
                   + (0.5 * (alpha == -1.0 ? strike : 0.0));
        }
        else
        {
            throw new NanoQLException("unknown control variate");
        }
    }
}
