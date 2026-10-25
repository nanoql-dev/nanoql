// SPDX-FileCopyrightText: Copyright (C) 2008 Yee Man Chan
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticgjrgarchengine.hpp ql/pricingengines/vanilla/analyticgjrgarchengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Concurrent;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic GJR-GARCH(1,1) engine for European options: Duan, Gauthier, Simonato and Sasseville's Edgeworth
/// approximation, from the first four moments of the log return over the option's days.
/// </summary>
/// <remarks>
/// <para>
/// An immutable object over its <see cref="GJRGARCHModel"/> (the S-hes plan's HD7). It keeps the moments of each
/// maturity it prices in a memo keyed by the day count and the rate they read, each entry QuantLib's arithmetic on the
/// engine's model, whose parameters and curves are fixed with it, so a price is a fresh engine's bit for bit whatever
/// the engine priced before and on whichever thread (HR4 (iv)); QuantLib's engine keeps one maturity's moments and its
/// key leaves out the rate, so a model relinked to another curve can price on the old rate.
/// </para>
/// <para>
/// QuantLib's prices are corrected where they're wrong (R8): the model's time runs from the earliest curve date, as
/// <see cref="GJRGARCHProcess"/>'s (HR7 (xviii)); a maturity of no day prices the forward's discounted intrinsic value,
/// where QuantLib's moments are 0/0 (HR7 (xvii)); with a dividend yield the call is D_q times QuantLib's
/// approximation and the put D_q·C + K·D_r − S·D_q, where QuantLib returns the price over D_q (HR7 (xxix)); and a
/// payoff that isn't a plain vanilla is refused, where QuantLib returns the call's or put's value by its option type
/// (HR12).
/// </para>
/// <para>
/// Two of QuantLib's defects are kept, its prices returned (C3-B1-1 and C3-B1-2 of the S-hes run record, the user's
/// rulings). Its fourth moment is wrong in the variance's own terms: where the variance is deterministic (α = γ = 0)
/// the log return is normal, but the engine's kurtosis is about 3 − Σh, Σh the total variance, so at a daily variance
/// of 2e-4 a year's calls at 0.9, 1 and 1.1 of the spot price 0.12%, 0.23% and 0.29% above Black-Scholes at the same
/// variance. And its Edgeworth expansion is a density only for moderate skewness and kurtosis: against a Monte Carlo of
/// the discrete model it approximates, the day's shock moving the log return and then the next day's variance
/// (QuantLib's gjrgarchmodel test model from its stationary variance, spot 50, a flat 4% curve, calls at 45, 50 and 55,
/// 180 days and a year; 2,560,000 paths on QuantLib's Mersenne twister), it errs from −3.2% to +0.3% at λ 0, down to
/// −8.8% at λ 0.1 and from −8.3% to −32% at λ 0.2 and 180 days, and at λ 0.2 and a year its calls are negative, −0.63
/// to −6.60 where the model's are 10.67 to 5.55 (QuantLib's own Monte Carlo engine, daily steps of its continuous-time
/// process, gives the model's prices within 1.3%). Where the variance's second or third moment grows without bound its
/// prices run off: −41,067 at two years on that model at λ 0.2 (m₂ 1.0022, m₃ 1.0257), and −3.4e19 at two years at 252
/// days a year with α 0.05, β 0.85, γ 0.1 and λ 0.3 (m₂ 1.0135, m₃ 1.1208).
/// </para>
/// <para>
/// Where two of the variance's growth factors m₁, m₂ and m₃, or one of them and 1, are equal (α = β = γ = 0; an
/// integrated GARCH), QuantLib's divided differences of their powers are 0/0 and its price NaN; here each is taken at
/// its limit, the same polynomial in the factors, the limit of QuantLib's prices as the factors meet (C3-B1-1's
/// ruling). QuantLib's own prices, kept bit for bit off the meeting point, lose their accuracy within about 1e-7 of all
/// three factors meeting at 1 (α = γ = 0, β near 1), where its nested difference quotients cancel: a year's call there
/// moves by as much as 12%.
/// </para>
/// <para>
/// Reference: Jin-Chuan Duan, Genevieve Gauthier, Jean-Guy Simonato, Caroline Sasseville, 2006. Approximating the
/// GJR-GARCH and EGARCH option pricing models analytically. Journal of Computational Finance, Volume 9, Number 3,
/// Spring 2006.
/// </para>
/// </remarks>
public sealed class AnalyticGJRGARCHEngine
{
    private static readonly CumulativeNormalDistribution CumNormal = new();

    private readonly GJRGARCHModel model;
    private readonly Coefficients coefficients;
    private readonly ConcurrentDictionary<(long Days, long Rate), Moments> memo = new();

    /// <summary>Initializes a new instance of the <see cref="AnalyticGJRGARCHEngine"/> class.</summary>
    /// <param name="model">The model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public AnalyticGJRGARCHEngine(GJRGARCHModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        this.model = model;
        GJRGARCHProcess process = model.Process;
        coefficients = new Coefficients(process.Beta, process.Alpha, process.Gamma, process.Lambda);
    }

    /// <summary>Gets the model.</summary>
    public GJRGARCHModel Model => model;

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <returns>The option's results: its value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The payoff isn't plain vanilla ("non-plain payoff given").</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, or the underlying isn't positive.</exception>
    public static OneAssetOptionResults Calculate(VanillaOption option, EvaluationContext context, GJRGARCHModel model) =>
        new AnalyticGJRGARCHEngine(model).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results: its value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The payoff isn't plain vanilla ("non-plain payoff given").</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, or the underlying isn't positive.</exception>
    public OneAssetOptionResults Calculate(VanillaOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = Value(option) });
    }

    // QuantLib's calculate (analyticgjrgarchengine.cpp:42-283)
    private double Value(VanillaOption option)
    {
        // this is a european option pricer
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        // plain vanilla, as the Monte Carlo engine requires (HR12)
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new ArgumentException("non-plain payoff given", nameof(option));
        }

        GJRGARCHProcess process = model.Process;
        Date lastDate = option.Exercise.LastDate();
        double riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);
        double dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        double spotPrice = process.S0;
        if (!(spotPrice > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        double strikePrice = payoff.Strike;
        double term = process.ModelTime(lastDate);
        long days = (long)Math.Round(process.DaysPerYear * term, MidpointRounding.AwayFromZero);
        if (days == 0)
        {
            // no day's variance: the forward's discounted intrinsic value (HR7 (xvii))
            return riskFreeDiscount * payoff.Evaluate(spotPrice * dividendDiscount / riskFreeDiscount);
        }

        double r = -Math.Log(riskFreeDiscount / dividendDiscount) / (process.DaysPerYear * term);
        Moments moments = memo.GetOrAdd(
            (days, BitConverter.DoubleToInt64Bits(r)),
            static (key, state) => state.Engine.ComputeMoments(state.Days, state.R),
            (Engine: this, Days: days, R: r));

        // compute call option price
        double ex = moments.Ex, sigma = moments.Sigma, k3 = moments.K3, k4 = moments.K4;
        double s = spotPrice;
        double x = strikePrice;
        double t = days;
        double stdev = Math.Sqrt(sigma);
        double del = (ex - (r * t) + (sigma / 2)) / stdev;
        double d = (Math.Log(s / x) + ((r * t) + (sigma / 2))) / stdev;
        double d_ = d + del;
        double c = (s * Math.Exp(del * stdev) * CumNormal.Evaluate(d_))
            - (x * Math.Exp(-r * t) * CumNormal.Evaluate(d_ - stdev));
        double a3 = s * Math.Exp(del * stdev) * stdev * ((((2 * stdev) - d_)
            * Math.Exp(-d_ * d_ / 2) / Math.Sqrt(2 * Math.PI))
            + (sigma * CumNormal.Evaluate(d_))) / 6;
        double a4 = s * Math.Exp(del * stdev) * stdev * (
            (((d_ * d_) - 1 - (3 * stdev * (d_ - stdev))) * Math.Exp(-d_ * d_ / 2) / Math.Sqrt(2 * Math.PI))
            - (sigma * stdev * CumNormal.Evaluate(d_))) / 24;
        double capp = c + (k3 * a3) + ((k4 - 3) * a4);

        // QuantLib's price is the price over the dividend discount (HR7 (xxix))
        return payoff.OptionType == OptionType.Call
            ? dividendDiscount * capp
            : (dividendDiscount * capp) + (strikePrice * riskFreeDiscount) - (spotPrice * dividendDiscount);
    }

    // QuantLib's first four moments of the log return over the days (analyticgjrgarchengine.cpp:133-250)
    private Moments ComputeMoments(long days, double r)
    {
        GJRGARCHProcess process = model.Process;
        double h1 = process.V0;
        double b0 = process.Omega;
        int tDays = checked((int)days);
        double m1 = coefficients.M1, m2 = coefficients.M2, m3 = coefficients.M3;
        double v1 = coefficients.V1, v2 = coefficients.V2, z1 = coefficients.Z1, x1 = coefficients.X1;
        if (m1 == m2 || m1 == m3 || m2 == m3 || m1 == 1.0 || m2 == 1.0 || m3 == 1.0)
        {
            return ConfluentMoments(days, r);
        }

        double sEh = 0.0, sEh2 = 0.0, sEhh = 0.0, sEh1_2eh = 0.0;
        double sEhhh = 0.0, sEh2h = 0.0, sEhh2 = 0.0, sEh3 = 0.0;
        double sEh1_2eh2 = 0.0, sEh3_2eh = 0.0, sEh1_2ehh = 0.0, sEhh1_2eh = 0.0;
        double sEhe2h = 0.0, sEh1_2eh1_2eh = 0.0;
        double sEh3_2e3h = 0.0;

        var m1ai = new double[tDays];
        var m2ai = new double[tDays];
        var m3ai = new double[tDays];
        m1ai[0] = m2ai[0] = m3ai[0] = 1.0;
        for (int i = 1; i < tDays; ++i)
        {
            m1ai[i] = m1ai[i - 1] * m1;
            m2ai[i] = m2ai[i - 1] * m2;
            m3ai[i] = m3ai[i - 1] * m3;
        }

        for (int i = 0; i < tDays; ++i)
        {
            double m1i = m1ai[i];
            double m2i = m2ai[i];
            double m3i = m3ai[i];

            double m1im2i = m1i - m2i, m1im3i = m1i - m3i, m2im3i = m2i - m3i;
            double eh = (b0 * (1 - m1i) / (1 - m1)) + (m1i * h1);
            double eh2 = (b0 * b0 * (((1 + m1) * (1 - m2i) / (1 - m2))
                                     - (2 * m1 * m1im2i / (m1 - m2))) / (1 - m1))
                + (2 * b0 * m1 * m1im2i * h1 / (m1 - m2))
                + (m2i * h1 * h1);
            double eh3 = (Math.Pow(b0, 3) * (
                    ((1 - m3i) / (1 - m3))
                    + (3 * m2 * (((1 - m3i) / (1 - m3)) - (m2im3i / (m2 - m3))) / (1 - m2))
                    + (3 * m1 * (((1 - m3i) / (1 - m3)) - (m1im3i / (m1 - m3))) / (1 - m1))
                    + (6 * m1 * m2 * (
                           ((((1 - m3i) / (1 - m3)) - (m2im3i / (m2 - m3))) / (1 - m2))
                           + (((m2im3i / (m2 - m3)) - (m1im3i / (m1 - m3))) / (m1 - m2))) / (1 - m1))))
                + (3 * b0 * b0 * m1 * h1 * ((m1im3i / (m1 - m3))
                                            + (2 * m2 * ((m1im3i / (m1 - m3)) - (m2im3i / (m2 - m3))) / (m1 - m2))))
                + (3 * b0 * m2 * h1 * h1 * m2im3i / (m2 - m3))
                + (m3i * h1 * h1 * h1);
            double eh3_2 = (.375 * Math.Pow(eh, -0.5) * eh2) + (.625 * Math.Pow(eh, 1.5));
            double eh5_2 = (1.875 * Math.Pow(eh, 0.5) * eh2) - (.875 * Math.Pow(eh, 2.5));
            sEh += eh;
            sEh2 += eh2;
            sEh3 += eh3;
            for (int j = 0; j < tDays - i - 1; ++j)
            {
                double ehh = (b0 * eh * (1 - m1ai[j + 1]) / (1 - m1)) + (eh2 * m1ai[j + 1]);
                double ehh2 = (b0 * b0 * eh * (((1 + m1) * (1 - m2ai[j + 1]) / (1 - m2))
                                               - (2 * m1 * (m1ai[j + 1] - m2ai[j + 1]) / (m1 - m2))) / (1 - m1))
                    + (2 * b0 * m1 * eh2 * (m1ai[j + 1] - m2ai[j + 1]) / (m1 - m2))
                    + (m2ai[j + 1] * eh3);
                double eh2h = (b0 * eh2 * (1 - m1ai[j + 1]) / (1 - m1))
                    + (m1ai[j + 1] * eh3);
                double eh1_2eh = v1 * m1ai[j] * eh3_2;
                double eh1_2eh2 = (2 * b0 * v1 * (m1ai[j + 1] - m2ai[j + 1]) * eh3_2 / (m1 - m2))
                    + (v2 * m2ai[j] * eh5_2);
                double ehij = (b0 * (1 - m1ai[i + j + 1]) / (1 - m1))
                    + (m1ai[i + j + 1] * h1);
                double ehh3_2 = (0.375 * ehh2 / Math.Sqrt(ehij))
                    + (0.75 * Math.Sqrt(ehij) * ehh)
                    - (0.125 * Math.Pow(ehij, 1.5) * eh);
                double eh3_2eh = v1 * m1ai[j] * eh5_2;
                double eh3_2e3h = x1 * m1ai[j] * eh5_2;
                double eh1_2eh3_2 = (0.375 * eh1_2eh2 / Math.Sqrt(ehij))
                    + (0.75 * Math.Sqrt(ehij) * eh1_2eh);
                sEhh += ehh;
                sEh1_2eh += eh1_2eh;
                sEhh2 += ehh2;
                sEh2h += eh2h;
                sEh1_2eh2 += eh1_2eh2;
                sEh3_2eh += eh3_2eh;
                sEhe2h += (b0 * eh * (1 - m1ai[j + 1]) / (1 - m1))
                    + (z1 * m1ai[j] * eh2);
                sEh3_2e3h += eh3_2e3h;
                for (int k = 0; k < tDays - i - j - 2; ++k)
                {
                    double ehhh = (b0 * ehh * (1 - m1ai[k + 1]) / (1 - m1))
                        + (m1ai[k + 1] * ehh2);
                    double eh1_2ehh = (b0 * eh1_2eh * (1 - m1ai[k + 1]) / (1 - m1))
                        + (m1ai[k + 1] * eh1_2eh2);
                    sEhhh += ehhh;
                    sEh1_2ehh += eh1_2ehh;
                    sEhh1_2eh += v1 * m1ai[k] * ehh3_2;
                    sEh1_2eh1_2eh += v1 * m1ai[k] * eh1_2eh3_2;
                }
            }
        }

        return MomentsOf(
            days,
            r,
            new Sums(sEh, sEh2, sEhh, sEh1_2eh, sEhhh, sEh2h, sEhh2, sEh3, sEh1_2eh2, sEh3_2eh, sEh1_2ehh, sEhh1_2eh, sEhe2h, sEh1_2eh1_2eh, sEh3_2e3h));
    }

    // QuantLib's moments where its divided differences of the growth factors' powers are 0/0, two of m1, m2 and m3,
    // or one of them and 1, equal (C3-B1-1 of the S-hes run record): each divided difference written as one and taken
    // at its limit, the same polynomial in the factors, so the moments are continuous with QuantLib's on either side
    private Moments ConfluentMoments(long days, double r)
    {
        GJRGARCHProcess process = model.Process;
        double h1 = process.V0;
        double b0 = process.Omega;
        int tDays = checked((int)days);
        double m1 = coefficients.M1, m2 = coefficients.M2, m3 = coefficients.M3;
        double v1 = coefficients.V1, v2 = coefficients.V2, z1 = coefficients.Z1, x1 = coefficients.X1;

        // f[nodes](n), the divided difference of x^n over the nodes, for each power the loops read: (1 - m1^n)/(1 - m1)
        // is d1m1[n], (m1^n - m2^n)/(m1 - m2) dm1m2[n], and so on
        double[] d1m1 = Differences(tDays, 1.0, m1), d1m2 = Differences(tDays, 1.0, m2), d1m3 = Differences(tDays, 1.0, m3);
        double[] dm1m2 = Differences(tDays, m1, m2), dm1m3 = Differences(tDays, m1, m3), dm2m3 = Differences(tDays, m2, m3);
        double[] d1m1m2 = Differences(tDays, 1.0, m1, m2), d1m1m3 = Differences(tDays, 1.0, m1, m3);
        double[] d1m2m3 = Differences(tDays, 1.0, m2, m3), dm1m2m3 = Differences(tDays, m1, m2, m3);
        double[] d1m1m2m3 = Differences(tDays, 1.0, m1, m2, m3);

        double sEh = 0.0, sEh2 = 0.0, sEhh = 0.0, sEh1_2eh = 0.0;
        double sEhhh = 0.0, sEh2h = 0.0, sEhh2 = 0.0, sEh3 = 0.0;
        double sEh1_2eh2 = 0.0, sEh3_2eh = 0.0, sEh1_2ehh = 0.0, sEhh1_2eh = 0.0;
        double sEhe2h = 0.0, sEh1_2eh1_2eh = 0.0;
        double sEh3_2e3h = 0.0;

        var m1ai = new double[tDays];
        var m2ai = new double[tDays];
        var m3ai = new double[tDays];
        m1ai[0] = m2ai[0] = m3ai[0] = 1.0;
        for (int i = 1; i < tDays; ++i)
        {
            m1ai[i] = m1ai[i - 1] * m1;
            m2ai[i] = m2ai[i - 1] * m2;
            m3ai[i] = m3ai[i - 1] * m3;
        }

        for (int i = 0; i < tDays; ++i)
        {
            double m1i = m1ai[i];
            double m2i = m2ai[i];
            double m3i = m3ai[i];

            // QuantLib's ((1 + m1) f[1, m2] - 2 m1 f[m1, m2])/(1 - m1) is f[1, m2] + 2 m1 f[1, m1, m2]
            double eh = (b0 * d1m1[i]) + (m1i * h1);
            double eh2 = (b0 * b0 * (d1m2[i] + (2 * m1 * d1m1m2[i])))
                + (2 * b0 * m1 * dm1m2[i] * h1)
                + (m2i * h1 * h1);
            double eh3 = (Math.Pow(b0, 3) * (d1m3[i] + (3 * m2 * d1m2m3[i]) + (3 * m1 * d1m1m3[i]) + (6 * m1 * m2 * d1m1m2m3[i])))
                + (3 * b0 * b0 * m1 * h1 * (dm1m3[i] + (2 * m2 * dm1m2m3[i])))
                + (3 * b0 * m2 * h1 * h1 * dm2m3[i])
                + (m3i * h1 * h1 * h1);
            double eh3_2 = (.375 * Math.Pow(eh, -0.5) * eh2) + (.625 * Math.Pow(eh, 1.5));
            double eh5_2 = (1.875 * Math.Pow(eh, 0.5) * eh2) - (.875 * Math.Pow(eh, 2.5));
            sEh += eh;
            sEh2 += eh2;
            sEh3 += eh3;
            for (int j = 0; j < tDays - i - 1; ++j)
            {
                double ehh = (b0 * eh * d1m1[j + 1]) + (eh2 * m1ai[j + 1]);
                double ehh2 = (b0 * b0 * eh * (d1m2[j + 1] + (2 * m1 * d1m1m2[j + 1])))
                    + (2 * b0 * m1 * eh2 * dm1m2[j + 1])
                    + (m2ai[j + 1] * eh3);
                double eh2h = (b0 * eh2 * d1m1[j + 1]) + (m1ai[j + 1] * eh3);
                double eh1_2eh = v1 * m1ai[j] * eh3_2;
                double eh1_2eh2 = (2 * b0 * v1 * dm1m2[j + 1] * eh3_2) + (v2 * m2ai[j] * eh5_2);
                double ehij = (b0 * d1m1[i + j + 1]) + (m1ai[i + j + 1] * h1);
                double ehh3_2 = (0.375 * ehh2 / Math.Sqrt(ehij))
                    + (0.75 * Math.Sqrt(ehij) * ehh)
                    - (0.125 * Math.Pow(ehij, 1.5) * eh);
                double eh3_2eh = v1 * m1ai[j] * eh5_2;
                double eh3_2e3h = x1 * m1ai[j] * eh5_2;
                double eh1_2eh3_2 = (0.375 * eh1_2eh2 / Math.Sqrt(ehij))
                    + (0.75 * Math.Sqrt(ehij) * eh1_2eh);
                sEhh += ehh;
                sEh1_2eh += eh1_2eh;
                sEhh2 += ehh2;
                sEh2h += eh2h;
                sEh1_2eh2 += eh1_2eh2;
                sEh3_2eh += eh3_2eh;
                sEhe2h += (b0 * eh * d1m1[j + 1]) + (z1 * m1ai[j] * eh2);
                sEh3_2e3h += eh3_2e3h;
                for (int k = 0; k < tDays - i - j - 2; ++k)
                {
                    double ehhh = (b0 * ehh * d1m1[k + 1]) + (m1ai[k + 1] * ehh2);
                    double eh1_2ehh = (b0 * eh1_2eh * d1m1[k + 1]) + (m1ai[k + 1] * eh1_2eh2);
                    sEhhh += ehhh;
                    sEh1_2ehh += eh1_2ehh;
                    sEhh1_2eh += v1 * m1ai[k] * ehh3_2;
                    sEh1_2eh1_2eh += v1 * m1ai[k] * eh1_2eh3_2;
                }
            }
        }

        return MomentsOf(
            days,
            r,
            new Sums(sEh, sEh2, sEhh, sEh1_2eh, sEhhh, sEh2h, sEhh2, sEh3, sEh1_2eh2, sEh3_2eh, sEh1_2ehh, sEhh1_2eh, sEhe2h, sEh1_2eh1_2eh, sEh3_2e3h));
    }

    // f[nodes](n) for each n below count: the divided difference of x^n over the nodes
    private static double[] Differences(int count, params double[] nodes)
    {
        double[] d = new double[count];
        for (int n = 0; n < count; ++n)
        {
            d[n] = PowerDividedDifference(n, nodes);
        }

        return d;
    }

    // The divided difference of x^n over the nodes, a node repeated taken at its limit: symmetric in the nodes, it is
    // the difference quotient over two that differ, and over k + 1 equal nodes x the k-th derivative's
    // C(n, k) x^(n - k)
    private static double PowerDividedDifference(int n, double[] nodes)
    {
        int k = nodes.Length - 1;
        if (k == 0)
        {
            return Math.Pow(nodes[0], n);
        }

        for (int a = 0; a < k; ++a)
        {
            for (int b = a + 1; b <= k; ++b)
            {
                if (nodes[a] != nodes[b])
                {
                    double[] ordered = new double[k + 1];
                    ordered[0] = nodes[a];
                    ordered[k] = nodes[b];
                    for (int i = 0, m = 1; i <= k; ++i)
                    {
                        if (i != a && i != b)
                        {
                            ordered[m++] = nodes[i];
                        }
                    }

                    return (PowerDividedDifference(n, ordered[..k]) - PowerDividedDifference(n, ordered[1..]))
                        / (ordered[0] - ordered[k]);
                }
            }
        }

        if (n < k)
        {
            return 0.0;
        }

        double binomial = 1.0;
        for (int i = 0; i < k; ++i)
        {
            binomial = binomial * (n - i) / (i + 1);
        }

        return binomial * Math.Pow(nodes[0], n - k);
    }

    // QuantLib's mean, variance, skewness and kurtosis from the sums of the variance's moments over the days
    private static Moments MomentsOf(long days, double r, in Sums s)
    {
        double t = days;
        double ex = (t * r) - (0.5 * s.Eh);
        double sd1 = (2 * s.Ehh) + s.Eh2;
        double sd2 = s.Eh;
        double sd3 = s.Eh1_2eh;
        double ex2 = (t * t * r * r) - (t * r * s.Eh) + (0.25 * sd1) + sd2 - sd3;
        double st1 = (6 * s.Ehhh) + ((3 * s.Ehh2) + ((3 * s.Eh2h) + s.Eh3));
        double st2 = 3 * s.Eh1_2eh;
        double st3 = (2 * s.Ehh1_2eh) + ((2 * s.Eh1_2ehh) + ((2 * s.Eh3_2eh) + s.Eh1_2eh2));
        double st4 = s.Ehe2h + (s.Ehh + (s.Eh2 + (2 * s.Eh1_2eh1_2eh)));
        double ex3 = Math.Pow(t * r, 3) - (1.5 * t * t * r * r * s.Eh)
            + (3 * t * r * ((sd1 / 4) + sd2 - sd3)) + (st2 - (st1 / 8) + (3 * st3 / 4) - (3 * st4 / 2));
        double sq2 = (6 * s.Ehe2h) + ((12 * s.Eh1_2eh1_2eh) + (3 * s.Eh2));
        double sq4 = (2 * s.Ehhh) + (2 * s.Ehh2);
        double sq5 = (3 * s.Ehh1_2eh) + (3 * s.Eh1_2ehh) + (3 * s.Eh3_2eh)
            + (3 * s.Eh1_2eh2) + s.Eh3_2e3h;
        double ex4 = Math.Pow(t * r, 4) - (2 * Math.Pow(t * r, 3) * s.Eh)
            + (6 * t * t * r * r * ((sd1 / 4) + sd2 - sd3)) + (t * r * ((4 * st2) - (st1 / 2) + (3 * st3) - (6 * st4)))
            + (sq2 + (3 * sq4 / 2) - (2 * sq5));

        // compute variance, skewness, kurtosis
        double sigma = ex2 - (ex * ex);

        // 3rd central moment mu3
        double k3 = ex3 - (3 * sigma * ex) - (ex * ex * ex);

        // 4th central moment mu4
        double k4 = ex4 + (6 * ex * ex * ex2) - (3 * ex * ex * ex * ex) - (4 * ex * ex3);
        k3 /= Math.Pow(sigma, 1.5); // 3rd standardized moment, ie skewness
        k4 /= Math.Pow(sigma, 2); // 4th standardized moment, ie kurtosis
        return new Moments(ex, sigma, k3, k4);
    }

    // the sums over the days of the variance's moments QuantLib's moments read, named as its sEh, sEh2, ...
    private readonly record struct Sums(
        double Eh,
        double Eh2,
        double Ehh,
        double Eh1_2eh,
        double Ehhh,
        double Eh2h,
        double Ehh2,
        double Eh3,
        double Eh1_2eh2,
        double Eh3_2eh,
        double Eh1_2ehh,
        double Ehh1_2eh,
        double Ehe2h,
        double Eh1_2eh1_2eh,
        double Eh3_2e3h);

    // the log return's mean, variance, skewness and kurtosis over a maturity's days
    private readonly record struct Moments(double Ex, double Sigma, double K3, double K4);

    // QuantLib's coefficients of the model's parameters (analyticgjrgarchengine.cpp:89-124), the same for every maturity
    private sealed class Coefficients
    {
        internal Coefficients(double b1, double b2, double b3, double la)
        {
            double n = Math.Exp(-la * la / 2) / (MathConstants.M_SQRTPI * MathConstants.M_SQRT2);
            double cn = CumNormal.Evaluate(la);
            M1 = b1 + ((b2 + (b3 * cn)) * (1 + (la * la))) + (b3 * la * n);
            M2 = (b1 * b1) + (b2 * b2 * (Math.Pow(la, 4) + (6 * la * la) + 3))
                + (((b3 * b3) + (2 * b2 * b3)) * ((Math.Pow(la, 4) * cn)
                                                  + (Math.Pow(la, 3) * n) + (6 * la * la * cn) + (5 * la * n) + (3 * cn)))
                + (2 * b1 * b2 * (1 + (la * la))) + (2 * b3 * b1 * ((la * la * cn) + (la * n) + cn));
            M3 = Math.Pow(b1, 3)
                + (((3 * b3 * b3 * b1) + (6 * b1 * b2 * b3)) * ((Math.Pow(la, 3) * n) + (5 * la * n) + (3 * cn)
                                                               + (Math.Pow(la, 4) * cn) + (6 * la * la * cn)))
                + (Math.Pow(b2, 3) * (15 + Math.Pow(la, 6) + (15 * Math.Pow(la, 4)) + (45 * la * la)))
                + ((Math.Pow(b3, 3) + (3 * b2 * b2 * b3) + (3 * b3 * b3 * b2))
                   * ((Math.Pow(la, 5) * n) + (14 * Math.Pow(la, 3) * n) + (33 * la * n) + (15 * cn)
                      + (15 * Math.Pow(la, 4) * cn) + (45 * la * la * cn) + (Math.Pow(la, 6) * cn)))
                + (3 * b1 * b1 * b2 * (1 + (la * la))) + (3 * b1 * b1 * b3 * ((la * n) + cn + (la * la * cn)))
                + (3 * b1 * b2 * b2 * (3 + Math.Pow(la, 4) + (6 * la * la)));
            V1 = (-2 * b2 * la) - (2 * b3 * (n + (la * cn)));
            V2 = (-4 * b2 * b2 * ((3 * la) + Math.Pow(la, 3)))
                - (((4 * b3 * b3) + (8 * b2 * b3)) * ((la * la * n) + (2 * n) + (Math.Pow(la, 3) * cn) + (3 * la * cn)))
                - (4 * b1 * b2 * la) - (4 * b3 * b1 * (n + (la * cn)));
            Z1 = b1 + (b2 * (3 + (la * la))) + (b3 * ((la * n) + (3 * cn) + (la * la * cn)));
            X1 = (-6 * b2 * la) - (2 * b3 * ((4 * n) + (3 * la * cn)));
        }

        internal double M1 { get; }

        internal double M2 { get; }

        internal double M3 { get; }

        internal double V1 { get; }

        internal double V2 { get; }

        internal double Z1 { get; }

        internal double X1 { get; }
    }
}
