// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analytichestonengine.hpp ql/pricingengines/vanilla/analytichestonengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The damping exponent α that minimizes the Fourier integrand's size at the origin, for the engine's control variates:
/// Andersen and Lake's choice, QuantLib's <c>AnalyticHestonEngine::OptimalAlpha</c>.
/// </summary>
/// <remarks>
/// <para>
/// It searches α below −1 and above 0, each within the range where the characteristic function's moments exist (from
/// <see cref="M"/>'s explosion time), by Brent's minimization of the logarithm of the integrand at the origin, and
/// takes the smaller minimum. Immutable: each search returns its number of evaluations with its value, where QuantLib's
/// object counts them across its calls.
/// </para>
/// <para>
/// The date form reads the forward by the maturity date and the model's time from the earliest curve date, as the
/// engine's price does; QuantLib's form takes a time and reads each curve at it, the same only where the curves share
/// the risk-free curve's day counter and reference date (the S-eq plan's rulings of 2026-10-20 and 2026-10-21,
/// <c>Divergences/HestonForwardByDateTests.cs</c>, <c>Divergences/HestonMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
public sealed class AnalyticHestonEngineOptimalAlpha
{
    private readonly double t;
    private readonly double fwd;
    private readonly double kappa;
    private readonly double theta;
    private readonly double sigma;
    private readonly double rho;
    private readonly double eps;
    private readonly AnalyticHestonEngine engine;
    private readonly double km;
    private readonly double kp;

    /// <summary>Initializes a new instance of the <see cref="AnalyticHestonEngineOptimalAlpha"/> class at a maturity
    /// time, each curve read at it.</summary>
    /// <param name="t">The maturity.</param>
    /// <param name="engine">The engine whose model and characteristic function the search reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <see langword="null"/>.</exception>
    public AnalyticHestonEngineOptimalAlpha(double t, AnalyticHestonEngine engine)
        : this(t, ForwardAt(t, engine), engine)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AnalyticHestonEngineOptimalAlpha"/> class at a maturity
    /// date, the forward read by the date.</summary>
    /// <param name="maturity">The maturity.</param>
    /// <param name="engine">The engine whose model and characteristic function the search reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <see langword="null"/>.</exception>
    public AnalyticHestonEngineOptimalAlpha(Date maturity, AnalyticHestonEngine engine)
        : this(TimeOf(maturity, engine), ForwardOn(maturity, engine), engine)
    {
    }

    private AnalyticHestonEngineOptimalAlpha(double t, double fwd, AnalyticHestonEngine engine)
    {
        this.t = t;
        this.fwd = fwd;
        kappa = engine.Model.Kappa;
        theta = engine.Model.Theta;
        sigma = engine.Model.Sigma;
        rho = engine.Model.Rho;
        eps = Math.ScaleB(1.0, -(int)(0.5 * 53));
        this.engine = engine;
        km = K(0.0, -1);
        kp = K(0.0, 1);
    }

    /// <summary>The optimal α for a strike: the better of the searches below −1 and above 0, or −0.5 where either
    /// fails.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>α.</returns>
    public double Evaluate(double strike) => EvaluateWithStatistics(strike).Alpha;

    /// <summary>The optimal α for a strike, with the searches' number of evaluations.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>α, the better of the searches below −1 and above 0, or −0.5 where either fails; and the number of
    /// evaluations of the integrand both searches made.</returns>
    public (double Alpha, int Evaluations) EvaluateWithStatistics(double strike)
    {
        int evaluations = 0;
        try
        {
            (double Alpha, double Value, int Evaluations) minusOne = AlphaSmallerMinusOne(strike);
            evaluations += minusOne.Evaluations;
            (double Alpha, double Value, int Evaluations) greaterZero = AlphaGreaterZero(strike);
            evaluations += greaterZero.Evaluations;

            if (minusOne.Value < greaterZero.Value)
            {
                return (minusOne.Alpha, evaluations);
            }
            else
            {
                return (greaterZero.Alpha, evaluations);
            }
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // QuantLib's catch (const Error&)
            return (-0.5, evaluations);
        }
    }

    /// <summary>The search for α above 0, up to the moments' bound <see cref="AlphaMax"/>.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>α, the integrand's logarithm there, and the search's number of evaluations.</returns>
    /// <exception cref="NanoQLException">The bound can't be solved for, or isn't positive.</exception>
    public (double Alpha, double Value, int Evaluations) AlphaGreaterZero(double strike)
    {
        double alphaMax = AlphaMax(strike);

        return FindMinima(eps, Max(2 * eps, (1.0 - 1e-6) * alphaMax), strike);
    }

    /// <summary>The search for α below −1, down to the moments' bound <see cref="AlphaMin"/>.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>α, the integrand's logarithm there, and the search's number of evaluations.</returns>
    /// <exception cref="NanoQLException">The bound can't be solved for, or isn't below −1.</exception>
    public (double Alpha, double Value, int Evaluations) AlphaSmallerMinusOne(double strike)
    {
        double alphaMin = AlphaMin(strike);

        return FindMinima(Min(-1.0 - 1e-6, -1.0 + ((1.0 - 1e-6) * (alphaMin + 1.0))), -1.0 - eps, strike);
    }

    /// <summary>The explosion time of the characteristic function's moment of order <paramref name="k"/>.</summary>
    /// <param name="k">The moment's order.</param>
    /// <returns>The time.</returns>
    public double M(double k)
    {
        double beta = kappa - (sigma * rho * k);

        if (k >= km && k <= kp)
        {
            double d = Math.Sqrt((beta * beta) - (sigma * sigma * k * (k - 1)));
            return Math.Log((beta - d) / (beta + d)) / d;
        }
        else
        {
            double dImag = Math.Sqrt(-((beta * beta) - (sigma * sigma * k * (k - 1))));

            return 2 / dImag * ((beta > 0.0 ? Math.PI : 0.0) - Math.Atan(dImag / beta));
        }
    }

    /// <summary>The moment's order at which the explosion time's argument is <paramref name="x"/>, on the side
    /// <paramref name="sgn"/> gives.</summary>
    /// <param name="x">The argument.</param>
    /// <param name="sgn">The side, −1 or 1.</param>
    /// <returns>The order.</returns>
    public double K(double x, int sgn) =>
        ((sigma - (2 * rho * kappa))
         + (sgn * Math.Sqrt(Squared(sigma - (2 * rho * kappa)) + (4 * ((kappa * kappa) + (x * x / (t * t))) * (1 - (rho * rho))))))
        / (2 * sigma * (1 - (rho * rho)));

    /// <summary>The smallest α below −1 at which the moments exist up to the maturity.</summary>
    /// <param name="strike">The strike, which QuantLib's bound doesn't read.</param>
    /// <returns>α.</returns>
    /// <exception cref="NanoQLException">The bound can't be solved for, or isn't below −1.</exception>
    public double AlphaMin(double strike)
    {
        _ = strike;
        double km2Pi = K(2 * Math.PI, -1);

        double alphaMin = new Brent().Solve(
            Cm, eps, 0.5 * (km2Pi + km), (1 - 1e-8) * km2Pi, (1 + 1e-8) * km) - 1.0;

        if (!(alphaMin <= -1.0))
        {
            throw new NanoQLException("alpha min must be smaller than minus one");
        }

        return alphaMin;
    }

    /// <summary>The largest α above 0 at which the moments exist up to the maturity.</summary>
    /// <param name="strike">The strike, which QuantLib's bound doesn't read.</param>
    /// <returns>α.</returns>
    /// <exception cref="NanoQLException">The bound can't be solved for, or isn't positive.</exception>
    public double AlphaMax(double strike)
    {
        _ = strike;
        const double eps = 1e-8;

        double alphaMax;
        double adx = kappa - (sigma * rho);
        if (adx > 0.0)
        {
            double kp2Pi = K(2 * Math.PI, 1);

            alphaMax = new Brent().Solve(
                Cm, this.eps, 0.5 * (kp + kp2Pi), (1 + eps) * kp, (1 - eps) * kp2Pi) - 1.0;
        }
        else if (adx < 0.0)
        {
            double tCut = -2 / (kappa - (sigma * rho * kp));
            if (t < tCut)
            {
                double kpPi = K(Math.PI, 1);
                alphaMax = new Brent().Solve(
                    Cm, this.eps, 0.5 * (kp + kpPi), (1 + eps) * kp, (1 - eps) * kpPi) - 1.0;
            }
            else
            {
                alphaMax = new Brent().Solve(
                    Cm, this.eps, 0.5 * (1.0 + kp), 1 + eps, (1 - eps) * kp) - 1.0;
            }
        }
        else
        {
            // adx == 0.0
            double kpPi = K(Math.PI, 1);
            alphaMax = new Brent().Solve(
                Cm, this.eps, 0.5 * (kp + kpPi), (1 + eps) * kp, (1 - eps) * kpPi) - 1.0;
        }

        if (!(alphaMax >= 0.0))
        {
            throw new NanoQLException("alpha max must be larger than zero");
        }

        return alphaMax;
    }

    private static double ForwardAt(double t, AnalyticHestonEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        HestonProcess process = engine.Model.Process;
        return process.S0 * process.DividendYield.Discount(t) / process.RiskFreeRate.Discount(t);
    }

    // the engine's model time and forward for the date (AnalyticHestonEngine.PriceVanillaPayoffWithStatistics)
    private static double TimeOf(Date maturity, AnalyticHestonEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return engine.Model.Process.ModelTime(maturity);
    }

    private static double ForwardOn(Date maturity, AnalyticHestonEngine engine)
    {
        HestonProcess process = engine.Model.Process;
        return process.S0 * process.DividendYield.AccruedDiscount(maturity) / process.RiskFreeRate.AccruedDiscount(maturity);
    }

    // std::max and std::min, which return their first argument where neither is less than the other
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    private static double Squared(double x) => x * x;

    // the explosion time's distance from the maturity, whose root bounds α
    private double Cm(double k) => M(k) - t;

    private (double Alpha, double Value, int Evaluations) FindMinima(double lower, double upper, double strike)
    {
        double freq = Math.Log(fwd / strike);
        int evaluations = 0;

        (double x, double fx) = MinimaFunctions.BrentFindMinima(
            alpha =>
            {
                ++evaluations;
                var z = new Complex(0, -(alpha + 1));
                return engine.LnChF(z, t).Real - Math.Log(alpha * (alpha + 1)) + (alpha * freq);
            },
            lower,
            upper,
            (int)(0.5 * 53));

        return (x, fx, evaluations);
    }
}
