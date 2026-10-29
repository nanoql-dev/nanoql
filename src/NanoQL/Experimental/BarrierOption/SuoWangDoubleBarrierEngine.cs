// SPDX-FileCopyrightText: Copyright (C) 2013 Yue Tian
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/suowangdoublebarrierengine.hpp ql/experimental/barrieroption/suowangdoublebarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// Suo and Wang's analytic engine for European double barrier options ("Barrier Option Pricing"): the knock-out as an
/// image series, the knock-in as the European less it.
/// </summary>
/// <remarks>
/// <para>
/// The value, with QuantLib's additional results: "vanilla", the analytic European; "barrierOut" and "barrierIn", the
/// knock-out and knock-in without their rebate; "rebateIn", QuantLib's own sum for a knock-in's rebate, unread by the
/// value, as in QuantLib.
/// </para>
/// <para>
/// Documented divergences (the S-xeq plan's XER5 (iii)–(vii), each leaving QuantLib's value and bits where it doesn't
/// apply). The strike limits are clipped to the corridor on both sides, K↑ = max(min(H, K), L) and K↓ = min(max(L, K),
/// H), where QuantLib clips one side and prices a knock-out call struck above H or a put struck below L negative
/// (#2881's class, S-bar's BR6 (ii)); the image series runs from −<c>series</c> to <c>series</c> and on while the next
/// pair of terms exceeds machine epsilon of the sum, where QuantLib sums from −<c>series</c> to <c>series</c> − 1, ten
/// terms, unconverged on narrow corridors (S-bar's BR5 (xliii)); QuantLib's sum stands wherever it agrees with that
/// converged series within 1e-12 on the value's scale, max(|value|, 1), QuantLib's cases among them; where that sum
/// isn't finite or disagrees with the analytic engine's knock-out within 1e-12 on the value's scale, as at low
/// volatility, where QuantLib returns NaN once a term's power overflows and, at volatilities of 1% to 3%, sums that
/// cancel, the knock-out is the analytic engine's, its terms taken in logs and its deterministic limit below
/// machine-epsilon variance (BR5 (xvi)), the two one image series; and the rebate is priced, a
/// knock-out's at the first touch of either barrier and a knock-in's at expiry where neither is touched, the analytic
/// engine's value (BR5 (xlvi)), where QuantLib's engine reads none (<c>Divergences/SuoWangDoubleBarrierTests.cs</c>).
/// </para>
/// <para>
/// Each rate and discount is read to the residual time on the volatility's clock, the volatility at the strike, as
/// the analytic engine reads them (BR5 (i)), where QuantLib reads every structure at the risk-free curve's time:
/// equivalent-constant reads, as S-bar's BR4 (d) keeps for the analytic engine, no exact closed form existing for a
/// first passage under general deterministic term structures (XER5 (vii)).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class SuoWangDoubleBarrierEngine
{
    // the relative agreement under which QuantLib's ten terms stand
    private const double QuantLibAgreement = 1e-12;

    // the most pairs of terms the converged series adds past series: a safety net, the terms decaying as a Gaussian in n
    private const int MaxExtraPairs = 10000;

    private static readonly CumulativeNormalDistribution F = new();

    /// <summary>Prices a double barrier option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="series">The number of terms either side of 0 the series takes at least; QuantLib's default
    /// 5.</param>
    /// <returns>The option's results: the value and QuantLib's additional results; the expired results when the option
    /// has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="series"/> is negative.</exception>
    /// <exception cref="NanoQLException"> The payoff isn't plain vanilla or its strike isn't positive, the spot isn't
    /// positive or lies on or beyond a barrier ("barrier touched"), the type is KIKO or KOKI, the exercise isn't
    /// European, or the series doesn't converge. </exception>
    public static OneAssetOptionResults Calculate(
        DoubleBarrierOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, int series = 5)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegative(series);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process, series));
    }

    internal static OneAssetOptionResults Body(DoubleBarrierOption option, GeneralizedBlackScholesProcess process, int series)
    {
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        if (!(payoff.Strike > 0.0))
        {
            throw new NanoQLException("strike must be positive");
        }

        double k = payoff.Strike;
        double s = process.X0;
        if (!(s > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        if (option.Triggered(s))
        {
            throw new NanoQLException("barrier touched");
        }

        DoubleBarrierType barrierType = option.BarrierType;
        if (!(barrierType is DoubleBarrierType.KnockOut or DoubleBarrierType.KnockIn))
        {
            throw new NanoQLException("only KnockIn and KnockOut options supported");
        }

        double l = option.BarrierLo;
        double h = option.BarrierHi;

        // the reads on the volatility's clock (XER5 (vii), BR5 (i)), as the analytic engine's
        Date lastDate = option.Exercise.LastDate();
        BlackVolTermStructure volatility = process.BlackVolatility;
        double t = volatility.TimeFromReference(lastDate);
        double rd = AnalyticBarrierEngine.RateOnVolatilityClock(process.RiskFreeRate, volatility, lastDate);
        double dd = process.RiskFreeRate.AccruedDiscount(lastDate);
        double rf = AnalyticBarrierEngine.RateOnVolatilityClock(process.DividendYield, volatility, lastDate);
        double df = process.DividendYield.AccruedDiscount(lastDate);
        double vol = volatility.BlackVol(lastDate, k);
        double mu = rd - rf - (vol * vol / 2.0);
        double sgn = mu > 0 ? 1.0 : (mu < 0 ? -1.0 : 0.0);

        // rebate
        double rL = option.Rebate;
        double rH = option.Rebate;

        // european option
        double european = AnalyticEuropeanEngine.Body(
            new VanillaOption(payoff, option.Exercise), process, process.RiskFreeRate).NPV!.Value;

        var terms = new Terms(payoff.OptionType, s, k, l, h, t, dd, df, vol, mu);

        // QuantLib's ten terms on its one-sided limits
        double kUp = Math.Min(h, k);
        double kDown = Math.Max(l, k);
        double quantLibOut = 0;
        double rebateIn = 0;
        for (int n = -series; n < series; n++)
        {
            quantLibOut += terms.Out(n, kUp, kDown);

            double v1 = D(h / s * Math.Pow(h / l, 2.0 * n), -mu, vol, t);
            double v2 = D(h / s * Math.Pow(h / l, 2.0 * n), mu, vol, t);
            double v3 = D(s / l * Math.Pow(h / l, 2.0 * n), -mu, vol, t);
            double v4 = D(s / l * Math.Pow(h / l, 2.0 * n), mu, vol, t);
            rebateIn += (dd * rH * sgn * ((Math.Pow(l / h, 2.0 * n * mu / (vol * vol)) * F.Evaluate(sgn * v1))
                    - (Math.Pow(h / s, 2.0 * mu / (vol * vol)) * F.Evaluate(-sgn * v2))))
                + (dd * rL * sgn * ((Math.Pow(l / s, 2.0 * mu / (vol * vol)) * F.Evaluate(-sgn * v3))
                    - (Math.Pow(h / l, 2.0 * n * mu / (vol * vol)) * F.Evaluate(sgn * v4))));
        }

        // the limits clipped to the corridor on both sides (XER5 (iii)), the series converged (XER5 (iv)); the analytic
        // engine's knock-out, its terms in logs, wherever this sum isn't finite or disagrees with it within 1e-12 on the
        // value's scale, as at low volatility where the powers overflow or the sums cancel (XER5 (vi); the code
        // review's O1-3)
        double converged = terms.Converged(series, Math.Max(Math.Min(h, k), l), Math.Min(Math.Max(l, k), h));
        double analyticOut = AnalyticDoubleBarrierEngine.KnockOutValue(option, payoff, process, series);
        double ownOut = double.IsFinite(converged) ? Agreed(quantLibOut, converged) : double.NaN;
        double barrierOut = Math.Abs(ownOut - analyticOut) <= QuantLibAgreement * Math.Max(Math.Abs(analyticOut), 1.0)
            ? ownOut
            : analyticOut;

        // the rebate as the analytic engine prices it (XER5 (v)): a knock-out's at the first touch, a knock-in's at
        // expiry where neither barrier is touched
        double rebate = option.Rebate != 0.0
            ? AnalyticDoubleBarrierEngine.RebateValue(option, payoff, process, series)
            : 0.0;

        double value = barrierType == DoubleBarrierType.KnockOut ? barrierOut + rebate : european - barrierOut + rebate;
        return new OneAssetOptionResults
        {
            NPV = value,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty
                .Add("vanilla", european)
                .Add("barrierOut", barrierOut)
                .Add("barrierIn", european - barrierOut)
                .Add("rebateIn", rebateIn),
        };
    }

    // QuantLib's sum where it agrees with the converged series within 1e-12 on the value's scale, max(|value|, 1); the
    // converged series elsewhere
    private static double Agreed(double quantLib, double converged) =>
        double.IsFinite(quantLib)
        && Math.Abs(quantLib - converged) <= QuantLibAgreement * Math.Max(Math.Abs(converged), 1.0)
            ? quantLib
            : converged;

    private static double D(double x, double lambda, double sigma, double t) =>
        (Math.Log(x) + (lambda * t)) / (sigma * Math.Sqrt(t));

    // The knock-out series' terms, QuantLib's loop body for one n at given strike limits.
    private sealed class Terms(
        OptionType type, double s, double k, double l, double h, double t, double dd, double df, double vol, double mu)
    {
        internal double Out(int n, double kUp, double kDown)
        {
            double sd = vol * Math.Sqrt(t);
            double d1 = D(s / h * Math.Pow(l / h, 2.0 * n), (vol * vol) + mu, vol, t);
            double d2 = d1 - sd;
            double g1 = D(h / s * Math.Pow(l / h, (2.0 * n) - 1.0), (vol * vol) + mu, vol, t);
            double g2 = g1 - sd;
            double h1 = D(s / h * Math.Pow(l / h, (2.0 * n) - 1.0), (vol * vol) + mu, vol, t);
            double h2 = h1 - sd;
            double k1 = D(l / s * Math.Pow(l / h, (2.0 * n) - 1.0), (vol * vol) + mu, vol, t);
            double k2 = k1 - sd;
            double d1Down = D(s / kDown * Math.Pow(l / h, 2.0 * n), (vol * vol) + mu, vol, t);
            double d2Down = d1Down - sd;
            double d1Up = D(s / kUp * Math.Pow(l / h, 2.0 * n), (vol * vol) + mu, vol, t);
            double d2Up = d1Up - sd;
            double k1Down = D(h * h / (kDown * s) * Math.Pow(l / h, 2.0 * n), (vol * vol) + mu, vol, t);
            double k2Down = k1Down - sd;
            double k1Up = D(h * h / (kUp * s) * Math.Pow(l / h, 2.0 * n), (vol * vol) + mu, vol, t);
            double k2Up = k1Up - sd;

            return type switch
            {
                OptionType.Call => Math.Pow(l / h, 2.0 * n * mu / (vol * vol))
                    * ((df * s * Math.Pow(l / h, 2.0 * n) * (F.Evaluate(d1Down) - F.Evaluate(d1)))
                        - (dd * k * (F.Evaluate(d2Down) - F.Evaluate(d2)))
                        - (df * Math.Pow(l / h, 2.0 * n) * h * h / s * Math.Pow(h / s, 2.0 * mu / (vol * vol))
                            * (F.Evaluate(k1Down) - F.Evaluate(k1)))
                        + (dd * k * Math.Pow(h / s, 2.0 * mu / (vol * vol)) * (F.Evaluate(k2Down) - F.Evaluate(k2)))),
                OptionType.Put => Math.Pow(l / h, 2.0 * n * mu / (vol * vol))
                    * ((dd * k * (F.Evaluate(h2) - F.Evaluate(d2Up)))
                        - (df * s * Math.Pow(l / h, 2.0 * n) * (F.Evaluate(h1) - F.Evaluate(d1Up)))
                        - (dd * k * Math.Pow(h / s, 2.0 * mu / (vol * vol)) * (F.Evaluate(g2) - F.Evaluate(k2Up)))
                        + (df * Math.Pow(l / h, 2.0 * n) * h * h / s * Math.Pow(h / s, 2.0 * mu / (vol * vol))
                            * (F.Evaluate(g1) - F.Evaluate(k1Up)))),
                _ => throw new NanoQLException("option type not recognized"),
            };
        }

        // the series from −series to series, then pairs while the next exceeds machine epsilon of the sum (S-bar's BR5
        // (xliii)); NaN where a term isn't finite
        internal double Converged(int series, double kUp, double kDown)
        {
            double sum = 0.0;
            for (int n = -series; n <= series; ++n)
            {
                sum += Out(n, kUp, kDown);
            }

            if (!double.IsFinite(sum))
            {
                return double.NaN;
            }

            for (int j = series + 1; j <= series + MaxExtraPairs; ++j)
            {
                double pair = Out(-j, kUp, kDown) + Out(j, kUp, kDown);
                if (!double.IsFinite(pair))
                {
                    return double.NaN;
                }

                if (!(Math.Abs(pair) > NumericLimits.QL_EPSILON * Math.Abs(sum)))
                {
                    return sum + pair;
                }

                sum += pair;
            }

            throw new NanoQLException("double-barrier image series did not converge");
        }
    }
}
