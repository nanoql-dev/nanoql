// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/denglizhoubasketengine.hpp ql/pricingengines/basket/denglizhoubasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Numerics.ArrayFunctions;
using static NanoQL.Numerics.CholeskyDecompositionFunctions;
using static NanoQL.Numerics.PseudoSqrtFunctions;
using static NanoQL.NumericLimits;

namespace NanoQL.PricingEngines;

/// <summary>
/// Deng, Li and Zhou's engine for a European basket or spread option on several assets: S. Deng, M. Li and J. Zhou,
/// "Multi-asset Spread Option Pricing and Hedging" (2008), https://mpra.ub.uni-muenchen.de/8259/1/MPRA_paper_8259.pdf,
/// with the typo in formula (37) for J² corrected.
/// </summary>
/// <remarks>
/// <para>
/// The formula prices exactly one positive weight; several positive weights are lumped into one lognormal asset by C.
/// F. Lo, "WKB Approximation for the Sum of Two Correlated Lognormal Random Variables" (2013),
/// https://www.m-hikari.com/ams/ams-2013/ams-125-128-2013/loAMS125-128-2013.pdf. A negative strike enters as a
/// zero-volatility asset of weight 1. Every process must discount alike, as in QuantLib. Stateless.
/// </para>
/// <para>
/// The positive leg's standard deviation, the formula's only divisor among them, is floored at QL_EPSILON², Choi's
/// engine's floor, where QuantLib divides 0 by 0 at a zero volatility and returns 0
/// (<c>Divergences/DengLiZhouZeroVolatilityTests.cs</c>). At a negative strike with exactly one negative weight the
/// option is priced by its payoff's identity, the call on (w, K) the put on (−w, −K), in the formula's own setting of
/// one positive weight, where QuantLib lumps the strike with the positive leg
/// (<c>Divergences/DengLiZhouNegativeStrikeTests.cs</c>); QuantLib's branch prices every other negative strike.
/// </para>
/// </remarks>
public static class DengLiZhouBasketEngine
{
    private static readonly NormalDistribution Normal = new();
    private static readonly CumulativeNormalDistribution CumNormal = new();

    /// <summary>Prices a European basket or spread option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">
    /// The option, on an <see cref="AverageBasketPayoff"/> or a <see cref="SpreadBasketPayoff"/> over a plain vanilla
    /// payoff.
    /// </param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' processes.</param>
    /// <param name="rho">The assets' correlation matrix.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// No process is given, or the correlation matrix isn't square of the processes' count.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European; the payoff isn't an average or spread basket over a plain vanilla payoff or its
    /// weights aren't one per asset; the processes discount differently; no weight is positive or none negative; or
    /// the approximation loses validity.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        ImmutableArray<GeneralizedBlackScholesProcess> processes,
        Matrix rho)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rho);
        if (processes.IsDefault)
        {
            throw new ArgumentNullException(nameof(processes));
        }

        foreach (GeneralizedBlackScholesProcess p in processes)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(processes));
        }

        int n = processes.Length;
        if (!(n > 0))
        {
            throw new ArgumentException("No Black-Scholes process is given.", nameof(processes));
        }

        if (!(n == rho.Rows && rho.Rows == rho.Columns))
        {
            throw new ArgumentException("process and correlation matrix must have the same size.", nameof(rho));
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new MultiAssetOptionResults { NPV = Value(option, processes, rho) });
    }

    // QuantLib's calculate().
    private static double Value(
        BasketOption option, ImmutableArray<GeneralizedBlackScholesProcess> processes, Matrix rho_)
    {
        int n_ = processes.Length;
        if (option.Exercise is not EuropeanExercise exercise)
        {
            throw new NanoQLException("not an European exercise");
        }

        Date maturityDate = exercise.LastDate();

        AverageBasketPayoff avgPayoff = option.Payoff switch
        {
            AverageBasketPayoff average => average,
            SpreadBasketPayoff spread => new AverageBasketPayoff(spread.BasePayoff, [1.0, -1.0]),
            _ => throw new NanoQLException("average or spread basket payoff expected"),
        };

        ImmutableArray<double> weights = avgPayoff.Weights;
        if (!(n_ == weights.Length && n_ > 1))
        {
            throw new NanoQLException("wrong number of weights arguments in payoff");
        }

        var pExtractor = new VectorBsmProcessExtractor(processes);
        double[] s = pExtractor.GetSpot();
        double[] dq = pExtractor.GetDividendYieldDf(maturityDate);
        double[] v = pExtractor.GetBlackVariance(maturityDate);
        double dr0 = pExtractor.GetInterestRateDf(maturityDate);

        if (avgPayoff.BasePayoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain vanilla payoff given");
        }

        double[] w = [.. weights];

        // at a negative strike with exactly one negative weight: the call on (w, K) is the put on (-w, -K), and the
        // put the call, in the formula's setting of one positive weight
        if (payoff.Strike < 0.0 && w.Count(x => x < 0.0) == 1)
        {
            for (int i = 0; i < n_; ++i)
            {
                w[i] = -w[i];
            }

            OptionType flipped = payoff.OptionType == OptionType.Call ? OptionType.Put : OptionType.Call;
            return Price(w, -payoff.Strike, flipped, s, dq, v, dr0, rho_);
        }

        return Price(w, payoff.Strike, payoff.OptionType, s, dq, v, dr0, rho_);
    }

    // QuantLib's calculate() after the extraction, on the weights, the strike and the type given.
    private static double Price(
        double[] weights,
        double payoffStrike,
        OptionType optionType,
        double[] s,
        double[] dq,
        double[] v,
        double dr0,
        Matrix rho_)
    {
        int n_ = weights.Length;
        var p = new List<(double W, int I, double S, double Dq, double V)>(n_ + 1);

        for (int i = 0; i < n_; ++i)
        {
            p.Add((weights[i], i, s[i], dq[i], v[i]));
        }

        Matrix rho;
        if (payoffStrike < 0.0)
        {
            p.Add((1.0, n_, -payoffStrike, dr0, 0.0));
            rho = new Matrix(n_ + 1, n_ + 1);
            for (int i = 0; i < n_; ++i)
            {
                rho_.Row(i).CopyTo(rho.Row(i));
                rho[n_, i] = rho[i, n_] = 0.0;
            }

            rho[n_, n_] = 1.0;
        }
        else
        {
            rho = rho_;
        }

        double strike = 0.0 < payoffStrike ? payoffStrike : 0.0;

        // positive weights first: std::sort with std::greater over the tuples, lexicographic
        p.Sort((x, y) =>
        {
            int c = y.W.CompareTo(x.W);
            return c != 0 ? c : y.I.CompareTo(x.I);
        });

        int bigM = 0;
        while (bigM < p.Count && p[bigM].W > 0)
        {
            ++bigM;
        }

        if (!(bigM > 0))
        {
            throw new NanoQLException("at least one positive asset weight must be given");
        }

        if (!(bigM < p.Count))
        {
            throw new NanoQLException("at least one negative asset weight must be given");
        }

        int bigN = p.Count - bigM;

        var nRho = new Matrix(bigN + 1, bigN + 1);
        var s_ = new Vector(bigN + 1);
        var dq_ = new Vector(bigN + 1);
        var v_ = new Vector(bigN + 1);

        if (bigM > 1)
        {
            var bigF = new Vector(bigM);
            var vol = new Vector(bigM);
            for (int i = 0; i < bigM; ++i)
            {
                vol[i] = Math.Sqrt(p[i].V);
                bigF[i] = p[i].W * p[i].S * p[i].Dq / dr0;
            }

            double s0 = 0.0;
            for (int i = 0; i < bigM; ++i)
            {
                s0 += p[i].W * p[i].S;
            }

            double f0 = 0.0;
            for (int i = 0; i < bigM; ++i)
            {
                f0 += bigF[i];
            }

            double dq_S0 = f0 / s0 * dr0;

            double v_s = 0.0;
            for (int i = 0; i < bigM; ++i)
            {
                for (int j = 0; j < bigM; ++j)
                {
                    v_s += vol[i] * vol[j] * bigF[i] * bigF[j]
                        * rho[p[i].I, p[j].I];
                }
            }

            v_s /= f0 * f0;
            s_[0] = s0;
            dq_[0] = dq_S0;
            v_[0] = v_s;

            nRho[0, 0] = 1.0;

            for (int i = 0; i < bigN; ++i)
            {
                double rhoHat = 0.0;
                for (int j = 0; j < bigM; ++j)
                {
                    rhoHat += rho[p[bigM + i].I, p[j].I] * vol[j] * bigF[j];
                }

                nRho[i + 1, 0] = nRho[0, i + 1]
                      = Min(1.0, Max(-1.0, rhoHat / (Math.Sqrt(v_s) * f0)));
            }
        }
        else
        {
            s_[0] = Math.Abs(p[0].W * p[0].S);
            dq_[0] = p[0].Dq;
            v_[0] = p[0].V;
            for (int i = 0; i < bigN + 1; ++i)
            {
                nRho[0, i] = nRho[i, 0] = rho[p[i].I, p[0].I];
            }
        }

        for (int i = 0; i < bigN; ++i)
        {
            s_[i + 1] = Math.Abs(p[bigM + i].W * p[bigM + i].S);
            dq_[i + 1] = p[bigM + i].Dq;
            v_[i + 1] = p[bigM + i].V;

            int idx = p[bigM + i].I;
            for (int j = 0; j < bigN; ++j)
            {
                nRho[i + 1, j + 1] = rho[idx, p[bigM + j].I];
            }
        }

        double callValue = CalculateVanillaCall(Log(s_), dr0, dq_, v_, nRho, strike);

        if (optionType == OptionType.Call)
        {
            return Max(0.0, callValue);
        }
        else
        {
            double innerProduct = 0.0;
            for (int i = 1; i < bigN + 1; ++i)
            {
                innerProduct += s_[i] * dq_[i];
            }

            double fwd = (s_[0] * dq_[0]) - (dr0 * strike) - innerProduct;
            return Max(0.0, callValue - fwd);
        }
    }

    private static double I(double u, double tF2, Matrix d, Matrix df, int i)
    {
        double psi = 1.0 /
            (1.0 + InnerProduct(d.Row(i), d.Row(i)));
        double sqrtPsi = Math.Sqrt(psi);

        double n_uSqrtPsi = Normal.Evaluate(u * sqrtPsi);
        double j_0 = CumNormal.Evaluate(u * sqrtPsi);

        double vFv = InnerProduct(df.Row(i), d.Row(i));
        double j_1 = psi * sqrtPsi * ((psi * u * u) - 1.0) * vFv * n_uSqrtPsi;

        double vFFv = InnerProduct(df.Row(i), df.Row(i));
        double j_2 = u * psi * sqrtPsi * n_uSqrtPsi * ((2 * tF2) +
            (vFv * vFv * (Squared(Squared(psi * u)) - (10.0 * psi * psi * psi * u * u) + (15 * psi * psi))) +
            (vFFv * ((4 * psi * psi * u * u) - (12 * psi))));

        return j_0 + j_1 - (0.5 * j_2);
    }

    private static double CalculateVanillaCall(Vector x, double dr, Vector dq, Vector v, Matrix rho, double bigK)
    {
        Vector mu = x + Log(dq / dr) - (0.5 * v);
        Vector nu = Sqrt(v);

        // the positive leg's deviation, which the formula divides by, floored as Choi's engine floors it
        nu[0] = QL_EPSILON * QL_EPSILON < nu[0] ? nu[0] : QL_EPSILON * QL_EPSILON;

        double bigR = 0.0;
        for (int i = 1; i < mu.Size; ++i)
        {
            bigR += Math.Exp(mu[i]);
        }

        int bigN = x.Size - 1;

        var sig11 = new Matrix(bigN, bigN);
        for (int i = 0; i < bigN; ++i)
        {
            rho.Row(i + 1)[1..].CopyTo(sig11.Row(i));
        }

        var sig10 = new Vector(rho.Row(0)[1..]);

        Matrix sqSig11 = PseudoSqrt(sig11, SalvagingAlgorithmType.Principal);
        Vector sig11Inv10 = CholeskySolveFor(CholeskyDecomposition(sig11), sig10);

        double sig_xy = 1.0 - DotProduct(sig10, sig11Inv10);
        if (!(sig_xy > 0.0))
        {
            throw new NanoQLException("approximation loses validity");
        }

        double sqSig_xy = Math.Sqrt(sig_xy);

        double a = -0.5 / sqSig_xy;
        var bigE = new Matrix(bigN, bigN);
        for (int i = 1; i <= bigN; ++i)
        {
            for (int j = i; j <= bigN; ++j)
            {
                bigE[i - 1, j - 1] = bigE[j - 1, i - 1] =
                     a * (((i == j) ? Squared(nu[j]) * Math.Exp(mu[j]) / (nu[0] * (bigR + bigK)) : 0.0)
                        - (nu[i] * nu[j] * Math.Exp(mu[i] + mu[j]) / (nu[0] * Squared(bigR + bigK))));
            }
        }

        Matrix bigF = sqSig11 * bigE * sqSig11;

        double trF = 0, trF2 = 0;
        for (int i = 0; i < bigN; ++i)
        {
            trF += bigF[i, i];
            double sum = 0;
            ReadOnlySpan<double> row = bigF.Row(i);
            for (int k = i + 1; k < bigN; ++k)
            {
                sum += row[k] * row[k];
            }

            trF2 += Squared(bigF[i, i]) + (2.0 * sum);
        }

        double c = -(Math.Log(bigR + bigK) - mu[0]) / (nu[0] * sqSig_xy);

        Vector d = (sig11Inv10
            - (Exp(new Vector(mu.AsSpan()[1..])) * new Vector(nu.AsSpan()[1..]) / (nu[0] * (bigR + bigK)))) / sqSig_xy;

        Vector esig10 = bigE * sig10;
        Matrix esig11 = bigE * sig11;
        Vector sig11d = sig11 * d;

        var bigC = new Vector(bigN + 2);
        bigC[0] = c + trF + (nu[0] * sqSig_xy) + (nu[0] * DotProduct(sig10, d))
             + (Squared(nu[0]) * DotProduct(sig10, esig10));
        bigC[bigN + 1] = c + trF;

        for (int k = 1; k < bigN + 1; ++k)
        {
            bigC[k] = c + trF + (nu[k] * sig11d[k - 1]) + (Squared(nu[k])
                * InnerProduct(sig11.Row(k - 1), esig11.Column(k - 1).AsSpan()));
        }

        var bigD = new Vector[bigN + 2];
        bigD[0] = sqSig11 * (d + (2 * nu[0] * esig10));
        bigD[bigN + 1] = sqSig11 * d;
        for (int k = 1; k < bigN + 1; ++k)
        {
            bigD[k] = sqSig11 * (d + (2 * nu[k] * esig11.Column(k - 1)));
        }

        var dm = new Matrix(bigN + 2, bigN);
        for (int k = 0; k < bigN + 2; ++k)
        {
            bigD[k].AsSpan().CopyTo(dm.Row(k));
        }

        Matrix df = dm * bigF;

        double npv = (dr * Math.Exp(mu[0] + (0.5 * Squared(nu[0]))) * I(bigC[0], trF2, dm, df, 0))
            - (bigK * dr * I(bigC[bigN + 1], trF2, dm, df, bigN + 1));

        for (int k = 1; k <= bigN; ++k)
        {
            npv -= dr * Math.Exp(mu[k] + (0.5 * Squared(nu[k]))) * I(bigC[k], trF2, dm, df, k);
        }

        return npv;
    }

    // std::inner_product from 0.0
    private static double InnerProduct(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        double result = 0.0;
        for (int i = 0; i < a.Length; ++i)
        {
            result += a[i] * b[i];
        }

        return result;
    }

    private static double Squared(double x) => x * x;

    // std::max and std::min
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;
}
