// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticpdfhestonengine.hpp ql/pricingengines/vanilla/analyticpdfhestonengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Heston engine for European options on any payoff: the payoff integrated against
/// <see cref="HestonRNDCalculator"/>'s density of the log spot at expiry.
/// </summary>
/// <remarks>
/// <para>
/// An immutable object over its <see cref="HestonModel"/> and the integrations' accuracy and limit (the S-hes plan's
/// HD7); the static <c>Calculate</c> builds one. The model's time runs from the earliest curve date, each curve read at
/// the instant a time denotes (HR7 (xi)); at the model's time 0 the price is the forward's discounted payoff (HR7
/// (xxxi)), where QuantLib's window is zero-wide. The window is centred on the log forward, x₀ + ln(D_q/D_r), where
/// QuantLib centres it on x₀ + ln(D_r/D_q), the forward's mirror, and misses the density at high carry and long expiry
/// (HR7 (vii)); its half-width 8√(θt + (v₀ − θ)(1 − e^{−κt})/κ) takes 1 − e^{−κt} by −expm1(−κt) below κt = 1e-2,
/// where QuantLib's loses the (v₀ − θ) term (HR7 (xxv)).
/// </para>
/// <para>
/// The window misses the density's mass beyond it: on QuantLib's own markets, a half-year put at half the forward 3%
/// to 53% low, and at a vol of variance of 1 and correlation −0.9 puts up to 18% low. nanoql adds the tails' values
/// where they're worth more than the integrations' accuracy, QuantLib's price where they aren't: the payoff a line
/// between its strikes, each piece's value is exact from the spot's distribution under the risk-neutral measure and
/// under the share measure, a Heston distribution again, at the window's edges and the strikes; a payoff not linear on
/// a tail with mass beyond the accuracy is refused. The value added is the additional result <c>tailsValue</c> (the
/// code review's C2-B1-2; <c>Divergences/AnalyticPdfHestonTailsTests.cs</c>). A tail is left out where both its
/// probabilities, under the two measures, are within the accuracy, the distribution's own noise there; such a tail can
/// be worth up to (K + F) times the accuracy, so the tails' accuracy scales with the payoff, as the window's does
/// (C2-F3-1).
/// </para>
/// <para>
/// Reference: A. Dragulescu, V. Yakovenko, Probability distribution of returns in the Heston model with stochastic
/// volatility, 2002, http://arxiv.org/pdf/cond-mat/0203046.pdf.
/// </para>
/// </remarks>
public sealed class AnalyticPDFHestonEngine
{
    private readonly HestonModel model;
    private readonly double integrationEps;
    private readonly int maxIntegrationIterations;

    /// <summary>Initializes a new instance of the <see cref="AnalyticPDFHestonEngine"/> class.</summary>
    /// <param name="model">The Heston model.</param>
    /// <param name="gaussLobattoEps">The integrations' accuracy.</param>
    /// <param name="gaussLobattoIntegrationOrder">The integrations' limit on iterations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public AnalyticPDFHestonEngine(HestonModel model, double gaussLobattoEps = 1e-6, int gaussLobattoIntegrationOrder = 10000)
    {
        ArgumentNullException.ThrowIfNull(model);
        this.model = model;
        integrationEps = gaussLobattoEps;
        maxIntegrationIterations = gaussLobattoIntegrationOrder;
    }

    /// <summary>Prices a European option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="gaussLobattoEps">The integrations' accuracy.</param>
    /// <param name="gaussLobattoIntegrationOrder">The integrations' limit on iterations.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, or an integration fails.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        double gaussLobattoEps = 1e-6,
        int gaussLobattoIntegrationOrder = 10000) =>
        new AnalyticPDFHestonEngine(model, gaussLobattoEps, gaussLobattoIntegrationOrder).Calculate(option, context);

    /// <summary>Prices a European option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, or an integration fails.</exception>
    public OneAssetOptionResults Calculate(VanillaOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        (double npv, double tails) = Value(option);
        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = npv,
            AdditionalResults = tails != 0.0
                ? ImmutableSortedDictionary<string, object>.Empty.Add("tailsValue", tails)
                : ImmutableSortedDictionary<string, object>.Empty,
        });
    }

    /// <summary>The density of the log spot at a time, on the model's clock.</summary>
    /// <param name="x">The log spot.</param>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>The density.</returns>
    /// <exception cref="NanoQLException">The integration fails.</exception>
    public double Pv(double x, double t) => Calculator().Pdf(x, t);

    /// <summary>The cumulative distribution of the spot at a time, on the model's clock.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>The probability that the spot is at most <paramref name="s"/>.</returns>
    /// <exception cref="NanoQLException">The integration fails.</exception>
    public double Cdf(double s, double t) => Calculator().Cdf(Math.Log(s), t);

    private HestonRNDCalculator Calculator() =>
        new(model.Process, integrationEps, maxIntegrationIterations, model.Process.ModelClock());

    // QuantLib's calculate, its window centred on the log forward (HR7 (vii)) and its curves read on the model's clock,
    // and the tails outside the window where they're worth more than the integrations' accuracy (C2-B1-2): the price
    // and the tails' value, 0 where none is added
    private (double Npv, double Tails) Value(VanillaOption option)
    {
        // this is an European option pricer
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        HestonProcess process = model.Process;
        TermStructure clock = process.ModelClock();
        double t = process.ModelTime(option.Exercise.LastDate());
        double x0 = Math.Log(process.S0);
        double rD = process.RiskFreeRate.DiscountAt(clock, t);
        double qD = process.DividendYield.DiscountAt(clock, t);
        Payoff payoff = option.Payoff!;
        if (t == 0.0)
        {
            // no variance has accrued: the forward's discounted payoff (HR7 (xxxi))
            return (rD * payoff.Evaluate(process.S0 * qD / rD), 0.0);
        }

        // below kappa t = 1e-2, (1 - e^{-kappa t}) by -expm1(-kappa t), which keeps the (v0 - theta) term (HR7 (xxv))
        double kappa = process.Kappa;
        double decay = kappa * t < 1e-2 ? -Expm1Functions.StdExpm1(-kappa * t) : 1 - Math.Exp(-kappa * t);
        double xMax = 8.0 * Math.Sqrt((process.Theta * t) + ((process.V0 - process.Theta) * decay / kappa));
        double drift = x0 + Math.Log(qD / rD);

        HestonRNDCalculator calculator = Calculator();
        double window = new GaussLobattoIntegral(maxIntegrationIterations, integrationEps).Integrate(
            xT => WeightedPayoff(calculator, payoff, rD, xT, t), -xMax + drift, xMax + drift);

        double forward = process.S0 * qD / rD;
        double tails = rD * (LeftTail(calculator, payoff, Math.Exp(-xMax + drift), forward, t)
            + RightTail(calculator, payoff, Math.Exp(xMax + drift), forward, t));
        return Math.Abs(tails) > integrationEps ? (window + tails, tails) : (window, 0.0);
    }

    // The value below the window, undiscounted: the payoff a line a + bS on each piece between its strikes, so a
    // piece's value is a times its probability plus b F times its probability under the share measure; none where the
    // tail's probabilities under both measures are within the integrations' accuracy
    private double LeftTail(HestonRNDCalculator calculator, Payoff payoff, double end, double forward, double t)
    {
        double pEnd = calculator.Cdf(Math.Log(end), t), qEnd = calculator.ShareCdf(Math.Log(end), t);
        if (!(pEnd > integrationEps) && !(qEnd > integrationEps))
        {
            return 0.0;
        }

        double value = 0.0, lo = 0.0, pLo = 0.0, qLo = 0.0;
        foreach (double hi in Kinks(payoff).Where(k => k > 0.0 && k < end).Append(end))
        {
            double pHi = hi == end ? pEnd : calculator.Cdf(Math.Log(hi), t);
            double qHi = hi == end ? qEnd : calculator.ShareCdf(Math.Log(hi), t);
            (double a, double b) = Line(payoff, lo + (0.25 * (hi - lo)), lo + (0.5 * (hi - lo)), lo + (0.75 * (hi - lo)));
            value += (a * (pHi - pLo)) + (b * forward * (qHi - qLo));
            (lo, pLo, qLo) = (hi, pHi, qHi);
        }

        return value;
    }

    // The value above the window, undiscounted, as below it
    private double RightTail(HestonRNDCalculator calculator, Payoff payoff, double start, double forward, double t)
    {
        double pStart = 1.0 - calculator.Cdf(Math.Log(start), t), qStart = 1.0 - calculator.ShareCdf(Math.Log(start), t);
        if (!(pStart > integrationEps) && !(qStart > integrationEps))
        {
            return 0.0;
        }

        double value = 0.0, lo = start, pLo = pStart, qLo = qStart;
        foreach (double hi in Kinks(payoff).Where(k => k > start && double.IsFinite(k)))
        {
            double pHi = 1.0 - calculator.Cdf(Math.Log(hi), t), qHi = 1.0 - calculator.ShareCdf(Math.Log(hi), t);
            (double a, double b) = Line(payoff, lo + (0.25 * (hi - lo)), lo + (0.5 * (hi - lo)), lo + (0.75 * (hi - lo)));
            value += (a * (pLo - pHi)) + (b * forward * (qLo - qHi));
            (lo, pLo, qLo) = (hi, pHi, qHi);
        }

        (double aLast, double bLast) = Line(payoff, 2.0 * lo, 4.0 * lo, 3.0 * lo);
        return value + (aLast * pLo) + (bLast * forward * qLo);
    }

    // The strikes where nanoql's payoffs change their line, ascending
    private static IEnumerable<double> Kinks(Payoff payoff)
    {
        var kinks = new SortedSet<double>();
        if (payoff is StrikedTypePayoff striked)
        {
            kinks.Add(striked.Strike);
        }

        switch (payoff)
        {
            case GapPayoff gap:
                kinks.Add(gap.SecondStrike);
                break;
            case SuperFundPayoff fund:
                kinks.Add(fund.SecondStrike);
                break;
            case SuperSharePayoff share:
                kinks.Add(share.SecondStrike);
                break;
        }

        return kinks;
    }

    // The payoff's line through two points of a piece, checked at a third: a payoff not linear on a tail's piece is
    // refused, its value outside the window unknown
    private static (double A, double B) Line(Payoff payoff, double s1, double s2, double s3)
    {
        double f1 = payoff.Evaluate(s1), f2 = payoff.Evaluate(s2), f3 = payoff.Evaluate(s3);
        double b = (f2 - f1) / (s2 - s1);
        double a = f1 - (b * s1);
        double scale = Math.Max(Math.Max(Math.Abs(f1), Math.Abs(f2)), Math.Max(Math.Abs(f3), Math.Abs(b * s3)));
        if (!(Math.Abs(f3 - (a + (b * s3))) <= 1e-10 * scale))
        {
            throw new NanoQLException("the payoff isn't linear on a tail outside the integration's window");
        }

        return (a, b);
    }

    private static double WeightedPayoff(HestonRNDCalculator calculator, Payoff payoff, double rD, double xT, double t)
    {
        double sT = Math.Exp(xT);
        double value = payoff.Evaluate(sT);
        return (value != 0.0) ? value * calculator.Pdf(xT, t) * rD : 0.0;
    }
}
