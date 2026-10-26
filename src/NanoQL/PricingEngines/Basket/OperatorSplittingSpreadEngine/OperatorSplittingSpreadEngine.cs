// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/operatorsplittingspreadengine.hpp ql/pricingengines/basket/operatorsplittingspreadengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using static NanoQL.NumericLimits;

namespace NanoQL.PricingEngines;

/// <summary>
/// The operator-splitting engine for a European spread option on two futures, to first or second order: Kirk's price
/// with its corrections, from C. F. Lo, "Pricing Spread Options by the Operator Splitting Method",
/// https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2429696.
/// </summary>
/// <remarks>
/// <para>Immutable. A put is priced from the call by parity.</para>
/// <para>
/// A second forward and strike summing to zero or less is refused, where QuantLib takes the log of a negative number
/// and returns NaN (<c>Divergences/SpreadApproximationStrikeTests.cs</c>); at a zero volatility of the second asset
/// the corrections are their limit, 0, so the price is Kirk's, exact there, where QuantLib's are 0·∞ and the price NaN
/// (<c>Divergences/OperatorSplittingZeroVolatilityTests.cs</c>).
/// </para>
/// </remarks>
public sealed class OperatorSplittingSpreadEngine : SpreadBlackScholesVanillaEngine
{
    private static readonly CumulativeNormalDistribution CumNormal = new();
    private static readonly NormalDistribution Normal = new();

    private readonly OperatorSplittingSpreadEngineOrder order_;

    /// <summary>Initializes a new instance of the <see cref="OperatorSplittingSpreadEngine"/> class.</summary>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <param name="order">The approximation's order.</param>
    /// <exception cref="ArgumentNullException">A process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> isn't one of its values.</exception>
    public OperatorSplittingSpreadEngine(
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double correlation,
        OperatorSplittingSpreadEngineOrder order = OperatorSplittingSpreadEngineOrder.Second)
        : base(process1, process2, correlation)
    {
        EnumArguments.Check(order, nameof(order));
        order_ = order;
    }

    /// <summary>Prices a European spread option by operator splitting.</summary>
    /// <param name="option">The option, on a <see cref="SpreadBasketPayoff"/> over a plain vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <param name="order">The approximation's order.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> isn't one of its values.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't a spread payoff over a plain vanilla payoff, or the second forward
    /// and the strike sum to zero or less.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double correlation,
        OperatorSplittingSpreadEngineOrder order = OperatorSplittingSpreadEngineOrder.Second) =>
        new OperatorSplittingSpreadEngine(process1, process2, correlation, order).Calculate(option, context);

    /// <inheritdoc/>
    protected override double Calculate(
        double f1, double f2, double k, OptionType optionType, double variance1, double variance2, double df)
    {
        // the logs and ratios of f2 + k below, where QuantLib's are NaN
        if (f2 + k <= 0.0)
        {
            throw new NanoQLException("f2 + strike must be positive for this approximation");
        }

        double CallPutParityPrice(double callPrice)
        {
            if (optionType == OptionType.Call)
            {
                return callPrice;
            }
            else
            {
                return callPrice - (df * (f1 - f2 - k));
            }
        }

        double vol1 = Math.Sqrt(variance1);
        double vol2 = Math.Sqrt(variance2);
        double sig2 = vol2 * f2 / (f2 + k);
        double sig_m = Math.Sqrt(variance1 + (sig2 * (sig2 - (2 * Rho * vol1))));
        double d1 = ((Math.Log(f1) - Math.Log(f2 + k)) / sig_m) + (0.5 * sig_m);
        double d2 = d1 - sig_m;
        double kirkCallNPV = df * ((f1 * CumNormal.Evaluate(d1)) - ((f2 + k) * CumNormal.Evaluate(d2)));

        // a zero volatility of the second asset: the corrections' limit, 0, so Kirk's price, exact there, where
        // QuantLib's corrections are 0·∞ (NaN)
        if (vol2 == 0.0)
        {
            return CallPutParityPrice(kirkCallNPV);
        }

        double vs = vol2 / (sig_m * sig_m);
        double rs = Squared((Rho * vol1) - sig2);
        double oPlt = -sig2 * sig2 * k * df * Normal.Evaluate(d2) * vs * ((-d2 * rs / sig2) - (0.5 * vs * sig_m * k /
            (f2 + k) * ((rs * d1 * d2) + ((1 - (Rho * Rho)) * variance1))));

        if (order_ == OperatorSplittingSpreadEngineOrder.First)
        {
            return CallPutParityPrice(kirkCallNPV + (0.5 * oPlt));
        }

        /*
           In the original paper, the second-order approximation was computed using numerical differentiation.
           The following Mathematica scripts calculates the approximation to the n'th order.

           vol2Hat[R2_] := vol2*(R2 - K)/R2
           volMinusHat[R2_] := Sqrt[vol1^2 - 2*rho*vol1*vol2Hat[R2] + vol2Hat[R2]^2]
           zeta1[R1_, R2_] := 1/(volMinusHat[R2]*Sqrt[t])*(Log[R1] + volMinusHat[R2]^2*t/2)
           zeta2[R1_, R2_] := zeta1[R1, R2] - volMinusHat[R2]*Sqrt[t]
           pLT[R1_, R2_] := Exp[-r*t]*R2*(R1*CDF[NormalDistribution[0, 1], zeta1[R1, R2]]
                                           - CDF[NormalDistribution[0, 1], zeta2[R1, R2]])
           opt[R1_, R2_] := (1/2*vol2Hat[R2]^2*R2^2*D[#, {R2, 2}] + (rho*vol1 - vol2Hat[R2])*vol2Hat[R2]*R1*R2*
                               D[#, R1, R2] - (rho*vol1 - vol2Hat[R2])*vol2Hat[R2]*R1*D[#, R1]) &
           pStrange1[R1_, R2_] := pLT[R1, R2] + (t/2)^1/Factorial[1]*opt[R1, R2][pLT[R1, R2]]
           pStrange2[R1_, R2_] := pStrange1[R1, R2] + (t/2)^2/Factorial[2]*opt[R1, R2][opt[R1, R2][pLT[R1, R2]]]
         */

        double bigR2 = f2 + k;
        double bigR1 = f1 / bigR2;
        double vol12 = vol1 * vol1;
        double vol22 = vol2 * vol2;
        double vol23 = vol22 * vol2;

        double ooPlt;
        if (rs < Math.Pow(QL_EPSILON, 0.625))
        {
            double vol24 = vol22 * vol22;
            double vol26 = vol22 * vol24;
            double k2 = k * k;
            double bigR22 = bigR2 * bigR2;
            double bigR24 = bigR22 * bigR22;
            double kmR22 = Squared(k - bigR2);
            double lnR1 = Math.Log(bigR1);
            ooPlt = -0.0625 * (k2 * kmR22 * vol26 * ((-8 * bigR22 * bigR24 * ((7 * k2) - (7 * k * bigR2) +
                bigR22) * vol12 * vol12) + (kmR22 * bigR24 * vol12 * ((-112 * k * bigR2) + (16 * bigR22) + (k2 * (124 +
                (3 * vol12)))) * vol22) - (2 * kmR22 * kmR22 * bigR22 * ((-28 * k * bigR2) + (4 * bigR22) + (k2 * (34 +
                (3 * vol12)))) * vol24) + (3 * k2 * kmR22 * kmR22 * kmR22 * vol26) - (4 * k * (k - bigR2) * bigR24 *
                lnR1 * ((-4 * bigR22 * vol12) + (4 * kmR22 * vol22) + (3 * k * (k - bigR2) * vol22 * lnR1))))) /
                (Math.Exp(Squared(-(bigR22 * vol12) + (kmR22 * vol22) + (2 * bigR22 * lnR1)) / ((8 * bigR24 * vol12) -
                (8 * kmR22 * bigR22 * vol22))) * MathConstants.M_SQRTPI * MathConstants.M_SQRT2 * bigR22 * bigR24 *
                bigR2 * Squared((bigR22 * vol12) - (kmR22 * vol22)) * Math.Sqrt(vol12 - (kmR22 * vol22 / bigR22)));
        }
        else
        {
            double bigF2 = f2;
            double bigF22 = bigF2 * bigF2;
            double bigF23 = bigF22 * bigF2;
            double bigF24 = bigF22 * bigF22;
            double iR2 = 1.0 / bigR2;
            double iR22 = iR2 * iR2;
            double iR23 = iR22 * iR2;
            double iR24 = iR22 * iR22;
            double a = vol12 - (2 * bigF2 * iR2 * Rho * vol1 * vol2) + (bigF22 * iR22 * vol22);
            double a2 = a * a;
            double b = (a / 2) + Math.Log(bigR1);
            double b2 = b * b;
            double c = Math.Sqrt(a);
            double d = b / c;
            double e = (Rho * vol1) - (bigF2 * iR2 * vol2);
            double e2 = e * e;
            double f = d - c;
            double g = (-2 * iR2 * Rho * vol1 * vol2) + (2 * bigF2 * iR22 * Rho * vol1 * vol2) + (2 * bigF2 * iR22 *
                vol22) - (2 * bigF22 * iR23 * vol22);
            double h = Rho * Rho;
            double j = 1 - h;
            double iat = 1 / c;
            double l = (b * iat) - c;
            double m = (f * (1 - (bigR2 * Rho * vol1 / (bigF2 * vol2)))) - (e * iR2 * k * ((d * l) + (j * vol12 / (e *
                e))) * vol2 / (2.0 * c));
            double n = (iat * (1 - (bigR2 * Rho * vol1 / (bigF2 * vol2))) / bigR1) - (e * iR2 * k * ((f * iat / bigR1) +
                (b / (a * bigR1))) * vol2 / (2.0 * c));
            double o = df * Math.Exp(-0.5 * f * f);
            double p = (d * l) + (j * vol12 / (e * e));
            double q = -2 * j * vol12 * (-(iR2 * vol2) + (bigF2 * iR22 * vol2)) / (e * e * e);
            double s = q - (b2 * g / (2.0 * a2)) - (b * f * g / (2.0 * a * c)) + (f * g / (2.0 * c));
            double u = f * (-(Rho * vol1 / (bigF2 * vol2)) + (bigR2 * Rho * vol1 / (bigF22 * vol2)));
            double v = -0.5 * (b * g * (1 - (bigR2 * Rho * vol1 / (bigF2 * vol2)))) / (a * c);
            double w = (3 * g * g / (4.0 * a2 * c)) - (((4 * iR22 * Rho * vol1 * vol2) - (4 * bigF2 * iR23 * Rho * vol1 *
                vol2) + (2 * iR22 * vol22) - (8 * bigF2 * iR23 * vol22) + (6 * bigF22 * iR24 * vol22)) / (2.0 * a * c));
            double x = u + v + (e * g * iR2 * k * p * vol2 / (4.0 * a * c)) + (e * iR22 * k * p * vol2 / (2.0 * c)) - (e *
                iR2 * k * s * vol2 / (2.0 * c)) - (iR2 * k * p * vol2 * (-(iR2 * vol2) + (bigF2 * iR22 * vol2)) / (2.0 *
                c));
            double y = (((4 * iR22) - (4 * bigF2 * iR23)) * Rho * vol1 * vol2) + (((2 * iR22) - (8 * bigF2 * iR23) + (6 *
                bigF22 * iR24)) * vol22);
            double z = (4 * iR22 * Rho * vol1 * vol2) - (4 * bigF2 * iR23 * Rho * vol1 * vol2) + (2 * iR22 * vol22) - (8 *
                bigF2 * iR23 * vol22) + (6 * bigF22 * iR24 * vol22);
            ooPlt = k * o * vol23 * ((-2 * c * b2 * e2 * e * (-1 + (f * f)) * bigF23 * bigF24 * g * g * iR22 * m *
                vol23) + (2 * b2 * e2 * e2 * bigF23 * bigF24 * g * g * iR2 * iR22 * k * vol22 * vol22) + (2 * a * b * e2 *
                e * bigF23 * bigF22 * g * iR22 * vol2 * ((-8 * e2 * bigF2 * iR2 * k * vol22) + (7 * f * bigF22 * g * m *
                vol22))) - (a * c * e2 * e * bigF23 * bigF22 * g * iR22 * vol2 * ((4 * e * bigF2 * vol2 * ((-2 * b * (-1 +
                (f * f)) * m) + (e * f * iR2 * k * vol2))) + (bigF22 * g * ((16 * m) + (e * ((2 * f) + (3 * b * iat)) *
                iR2 * k * vol2)) * vol22))) - (4 * a2 * a * c * e2 * ((e2 * bigF22 * vol2 * ((4 * bigF22 * iat * iR22 *
                bigR2 * Rho * vol1) + (8 * bigF23 * iR22 * n * bigR1 * vol2) - (4 * bigF24 * 3 * iR23 * n * bigR1 *
                vol2) - (bigF23 * iR22 * ((4 * iat * Rho * vol1) + (bigF22 * iR2 * k * p * vol23 * w))))) + (4 * bigF23 *
                bigF22 * vol22 * vol22 * ((iR22 * ((-2 * bigF2 * iR2) + (3 * bigF22 * iR22)) * m) + (bigF22 * ((2 * iR2) -
                (3 * bigF2 * iR22)) * iR23 * m) + (bigF22 * iR22 * (-iR2 + (bigF2 * iR22)) * x))) + (2 * e * bigF22 *
                ((2 * bigF24 * bigF2 * iR24 * n * bigR1 * vol23) + (2 * f * bigF2 * bigF22 * iR22 * Rho * vol1 * vol22) -
                (2 * f * bigF22 * iR22 * bigR2 * Rho * vol1 * vol22) - (b * bigF24 * iR22 * bigR2 * Rho * vol1 * vol22 *
                w) - (2 * bigF24 * vol2 * ((iR23 * n * bigR1 * vol22) + (4 * iR23 * m * vol22) - (2 * iR22 * vol22 *
                x))) + (bigF23 * ((2 * iR22 * m * vol23) + (6 * bigF22 * iR24 * m * vol23) + (b * bigF22 * iR22 * vol23 *
                w) - (4 * bigF22 * iR23 * vol23 * x))))))) + (2 * a2 * c * e2 * bigF23 * bigF22 * vol2 * ((8 * bigF22 *
                g * iR22 * (-iR2 + (bigF2 * iR22)) * m * vol23) + (e2 * iR22 * vol2 * ((8 * bigF2 * g * n * bigR1) + (b *
                bigF22 * iat * iR2 * k * vol22 * (y - z)))) + (4 * e * vol22 * ((4 * bigF2 * g * iR22 * m) + (bigF22 *
                ((-4 * g * iR23 * m) + (2 * g * iR22 * x) + (iR22 * m * z))))))) + (2 * a2 * a * bigF22 * ((-4 * e2 * e2 *
                e * f * bigF24 * iat * iR24 * k * vol23) + (8 * e * bigF2 * bigF24 * iR23 * (-iR22 + (bigF2 * iR23)) * j *
                k * vol12 * vol23 * vol22) + (12 * bigF2 * bigF24 * iR23 * Squared(iR2 - (bigF2 * iR22)) * j * k * vol12 *
                vol23 * vol23) + (e2 * e2 * bigF2 * vol22 * ((2 * bigF24 * iR22 * k * vol22 * ((2 * ((iR23 * p) - (iR22 *
                s))) + (b2 * iat * iR2 * w))) + (f * ((4 * bigF22 * iR22 * ((4 * m) + (bigF22 * iat * iR2 * iR22 * k *
                vol22))) - (4 * bigF23 * ((6 * iR23 * m) + (iat * iR24 * k * vol22) - (2 * iR22 * x))) + (bigF24 * iR23 *
                k * vol22 * ((2 * b * w) + (iat * y))))))) - (2 * e2 * e * bigF22 * iR22 * ((4 * f * bigF22 * (iR2 -
                (bigF2 * iR22)) * m * vol23) + (bigF22 * vol22 * ((bigF2 * vol2 * ((2 * k * ((bigF2 * iR24 * p) + (bigF2 *
                iR24 * p) + (iR22 * s) - (iR23 * ((2 * p) + (bigF2 * s)))) * vol22) + y - z)) + (bigR2 * Rho * vol1 *
                (-y + z)))))))) - (2 * a2 * e2 * bigF23 * ((2 * e2 * e * bigF23 * iR22 * k * ((2 * b * iR22) + (g * (-1 +
                (f * iat)) * iR2)) * vol23) + (4 * b * f * bigF22 * bigF22 * g * iR22 * (-iR2 + (bigF2 * iR22)) * m *
                vol22 * vol22) + (2 * e2 * bigF22 * iR22 * vol2 * ((2 * b * bigF2 * iR2 * (iR2 - (bigF2 * iR22)) * k *
                vol23) + (g * ((2 * bigR2 * Rho * vol1) + (2 * bigF2 * (-1 + (3 * f * m) + (b * f * n * bigR1)) * vol2) +
                (bigF22 * k * (-(iR22 * p) + (iR2 * s)) * vol23))))) + (e * vol22 * ((bigF2 * bigF22 * g * iR22 * ((g *
                bigR2 * Rho * vol1) + (bigF2 * g * (-1 + (f * m)) * vol2) + (2 * bigF2 * iR2 * (-iR2 + (bigF2 * iR22)) *
                k * p * vol23))) + (2 * b * ((2 * bigF2 * bigF22 * g * iR22 * Rho * vol1) - (2 * bigF22 * g * iR22 *
                bigR2 * Rho * vol1) + (4 * f * bigF23 * g * iR22 * m * vol2) + (f * bigF24 * vol2 * ((-4 * g * iR23 * m) +
                (2 * g * iR22 * x) + (iR22 * m * z)))))))))) / (16.0 * a2 * a2 * c * e2 * bigF23 * MathConstants.M_SQRT2 *
                MathConstants.M_SQRTPI * vol2);
        }

        return CallPutParityPrice(kirkCallNPV + (0.5 * oPlt) + (0.125 * ooPlt));
    }

    private static double Squared(double x) => x * x;
}
