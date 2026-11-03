// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticcevengine.hpp ql/pricingengines/vanilla/analyticcevengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The undiscounted value of a European option on the constant elasticity of variance (CEV) process
/// df = α·f^β·dW with an absorbing boundary at f = 0, by the non-central chi-squared distribution (D. R. Brecher and
/// A. E. Lindsay, "Results on the CEV Process, Past and Present").
/// </summary>
/// <remarks>
/// <para>
/// Near β = 1 the formula fails: δ = (1 − 2β)/(1 − β) divides by zero at β = 1, and around it Boost's non-central
/// chi-squared refuses or runs for seconds. For |β − 1| &lt; 1e-3 the value is Black's formula at Hagan's
/// CEV-equivalent volatility, the SABR volatility at ν = 0 (<see cref="SabrFunctions.UnsafeSabrLogNormalVolatility"/>),
/// which is α at β = 1 and within 4.6e-7 of the CEV formula at the band's edge, where QuantLib throws or hangs. A
/// documented divergence (<c>Divergences/CevNearOneTests.cs</c>).
/// </para>
/// <para>
/// Where a tail QuantLib takes as 1 − cdf (or Γ-ratio − cdf, <c>analyticcevengine.cpp:50-80</c>) is below 1e-3, the
/// difference loses most of its digits: a 36-day call at a strike of 150 comes out about 20% low. There the tail is
/// Boost's complement <c>cdf</c> (and <c>gamma_q</c>); QuantLib's arithmetic and bits elsewhere. A documented divergence
/// (<c>Divergences/CevTailTests.cs</c>).
/// </para>
/// <para>
/// At a time of 0 the formula's non-centrality x₀/t is infinite, which Boost refuses; the value there is the payoff at
/// f0, the limit QuantLib's own values approach as the time shrinks. A documented divergence (a run find,
/// <c>Divergences/CevAtExpiryTests.cs</c>).
/// </para>
/// </remarks>
public sealed class CEVCalculator
{
    // the band around β = 1 where Black's formula at Hagan's volatility replaces the CEV formula
    private const double NearOne = 1e-3;

    // the tail below which the complement replaces QuantLib's 1 − cdf
    private const double SmallTail = 1e-3;

    private readonly double delta;
    private readonly double x0;

    /// <summary>Initializes a new instance of the <see cref="CEVCalculator"/> class.</summary>
    /// <param name="f0">The forward's value today.</param>
    /// <param name="alpha">The volatility parameter α.</param>
    /// <param name="beta">The elasticity β.</param>
    public CEVCalculator(double f0, double alpha, double beta)
    {
        F0 = f0;
        Alpha = alpha;
        Beta = beta;
        delta = (1.0 - (2.0 * beta)) / (1.0 - beta);
        x0 = X(f0);
    }

    /// <summary>Gets the forward's value today.</summary>
    public double F0 { get; }

    /// <summary>Gets the volatility parameter α.</summary>
    public double Alpha { get; }

    /// <summary>Gets the elasticity β.</summary>
    public double Beta { get; }

    /// <summary>The option's undiscounted value.</summary>
    /// <param name="optionType">The option's type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="t">The time to expiry; at 0, the payoff at f0.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">Boost's non-central chi-squared refuses its arguments (a time that isn't
    /// positive, a negative forward), or a series runs out of terms.</exception>
    public double Value(OptionType optionType, double strike, double t)
    {
        EnumArguments.Check(optionType, nameof(optionType));

        if (Math.Abs(Beta - 1.0) < NearOne)
        {
            // Black's formula at Hagan's CEV-equivalent volatility
            double vol = SabrFunctions.UnsafeSabrLogNormalVolatility(strike, F0, t, Alpha, Beta, 0.0, 0.0);
            return BlackFormulaFunctions.BlackFormula(optionType, strike, F0, vol * Math.Sqrt(t));
        }

        if (t == 0.0)
        {
            // the formula's limit at expiry, where its non-centrality x0/t is infinite
            return Math.Max(((int)optionType) * (F0 - strike), 0.0);
        }

        double kTilde = X(strike);

        // QuantLib's two distributions are built before either cdf is read, as MSVC orders the expression's temporaries:
        // a refused parameter is refused before a refused variate
        if (delta < 2.0)
        {
            NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(4.0 - delta, x0 / t);
            NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(2.0 - delta, kTilde / t);
        }
        else
        {
            NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(delta - 2.0, kTilde / t);
            NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(delta, x0 / t);
        }

        if (optionType == OptionType.Call)
        {
            if (delta < 2.0)
            {
                double tail = 1.0 - Cdf(4.0 - delta, x0 / t, kTilde / t);
                if (tail < SmallTail)
                {
                    tail = CdfComplement(4.0 - delta, x0 / t, kTilde / t);
                }

                return (F0 * tail) - (strike * Cdf(2.0 - delta, kTilde / t, x0 / t));
            }
            else
            {
                double g = GammaFunctions.GammaP((0.5 * delta) - 1.0, x0 / (2.0 * t));

                double tail = g - Cdf(delta - 2.0, kTilde / t, x0 / t);
                if (tail < SmallTail)
                {
                    tail = CdfComplement(delta - 2.0, kTilde / t, x0 / t)
                        - GammaFunctions.GammaQ((0.5 * delta) - 1.0, x0 / (2.0 * t));
                }

                return (F0 * tail) - (strike * Cdf(delta, x0 / t, kTilde / t));
            }
        }
        else
        {
            if (delta < 2.0)
            {
                double tail = 1.0 - Cdf(2.0 - delta, kTilde / t, x0 / t);
                if (tail < SmallTail)
                {
                    tail = CdfComplement(2.0 - delta, kTilde / t, x0 / t);
                }

                return (-F0 * Cdf(4.0 - delta, x0 / t, kTilde / t)) + (strike * tail);
            }
            else
            {
                double tail = 1.0 - Cdf(delta, x0 / t, kTilde / t);
                if (tail < SmallTail)
                {
                    tail = CdfComplement(delta, x0 / t, kTilde / t);
                }

                return (-F0 * Cdf(delta - 2.0, kTilde / t, x0 / t)) + (strike * tail);
            }
        }
    }

    // boost::math::cdf(non_central_chi_squared_distribution(k, l), x), the distribution's constructor checked by Value
    private static double Cdf(double k, double l, double x) => NonCentralChiSquaredFunctions.Cdf(k, l, x);

    // boost::math::cdf(complement(non_central_chi_squared_distribution(k, l), x))
    private static double CdfComplement(double k, double l, double x) => NonCentralChiSquaredFunctions.CdfComplement(k, l, x);

    private double X(double f) => Math.Pow(f, 2.0 * (1.0 - Beta)) / Squared(Alpha * (1.0 - Beta));

    private static double Squared(double x) => x * x;
}
