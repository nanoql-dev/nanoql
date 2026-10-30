// SPDX-FileCopyrightText: Copyright (C) 2021 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/turnbullwakemanasianengine.hpp ql/pricingengines/asian/turnbullwakemanasianengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// Turnbull and Wakeman's engine for discrete arithmetic average-price Asian options: the average matched to a
/// log-normal by its first two moments, priced by Black's formula.
/// </summary>
/// <remarks>
/// <para>
/// The value, delta and gamma, and QuantLib's additional results ("accrued", "discount", "strike", "effective_strike",
/// "forward", "exp_A_2", "tte", "sigma", "times", "spotVols", "forwards"). An effective strike at or below zero is the
/// certain exercise, priced in closed form (Haug, "Option Pricing Formulas", 2nd ed., p. 193).
/// </para>
/// <para>
/// Documented divergences (the S-bar plan's BR5). With every fixing past, the known payoff discounted, as Choi's engine
/// prices it, where QuantLib's reads the last of no fixing times and crashes the process ((xxix),
/// <c>Divergences/TurnbullWakemanPastFixingsTests.cs</c>); QuantLib's master prices it so after 1.44 (#2954), its
/// branch ahead of the certain exercise's, as here. A seasoned option reads its variances and spot volatilities at the
/// future average's own strike, K_f = (K − A/m)·m/f for f of m fixings to come, so it prices as (f/m) times the
/// unseasoned option at K_f, where QuantLib reads them at K − A/m, +42% and +1,302% off on a smile varying with
/// maturity ((xxx),
/// <c>Divergences/SeasonedAsianSmileTests.cs</c>).
/// </para>
/// <para>
/// The last fixing on the evaluation date, at time 0, is priced with a zero standard deviation and σ, its delta
/// discount · α · E[A] / S, as QuantLib's master prices it after 1.44 (#2954,
/// <c>Divergences/AsianUpstreamFixesTests.cs</c>), where QuantLib 1.44 divides by the last time and refuses the NaN
/// standard deviation; the standard deviation is the master's, √log(E[A²]/E[A]²), and each spot volatility the
/// structure's volatility, so the price's last bits differ from 1.44's.
/// </para>
/// <para>
/// A fixing dated from the evaluation date to the latest structure's date, where the structures settle after the
/// evaluation date, is taken there, the spot with no variance and no discount accrued, so as a past fixing at the spot
/// (the S-bar code review's B3-1 and B3-2, <c>Divergences/AsianLateSettlementTests.cs</c>), where QuantLib reads its
/// negative time and refuses it.
/// </para>
/// </remarks>
public static class TurnbullWakemanAsianEngine
{
    /// <summary>Prices a discrete arithmetic average-price Asian option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value, delta, gamma and the additional results; the expired results when
    /// the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the average isn't arithmetic, the payoff isn't plain vanilla, or the option's past
    /// fixings fail its checks.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DiscreteAveragingAsianOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        // the latest structure's date: each structure is read on its own, so no read falls before its date
        Date origin = Latest(process);
        DiscreteAveragingAsianArguments arguments = option.Arguments(context).AtOrigin(
            context.EvaluationDate, d => d < origin, process.StateVariable);
        return option.FetchResults(Body(option, arguments, process));
    }

    private static OneAssetOptionResults Body(
        DiscreteAveragingAsianOption option, DiscreteAveragingAsianArguments arguments, GeneralizedBlackScholesProcess process)
    {
        // Enforce a few required things
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not a European Option");
        }

        if (!(arguments.AverageType == AverageType.Arithmetic))
        {
            throw new NanoQLException("must be Arithmetic Average::Type");
        }

        ImmutableSortedDictionary<string, object>.Builder additional = ImmutableSortedDictionary.CreateBuilder<string, object>();

        // Calculate the accrued portion
        int pastFixings = arguments.PastFixings;
        int futureFixings = arguments.FixingDates.Length;
        double accruedAverage = 0;
        if (pastFixings != 0)
        {
            accruedAverage = arguments.RunningAccumulator / (pastFixings + futureFixings);
        }

        additional["accrued"] = accruedAverage;

        double discount = process.RiskFreeRate.Discount(option.Exercise.LastDate());
        additional["discount"] = discount;

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        // We will read the volatility off the surface at the effective strike
        double effectiveStrike = payoff.Strike - accruedAverage;
        additional["strike"] = payoff.Strike;
        additional["effective_strike"] = effectiveStrike;

        // every fixing past: the known payoff discounted, QuantLib's master's (#2954) and Choi's engine's, where
        // QuantLib 1.44 reads the last of no fixing times (BR5 (xxix))
        if (futureFixings == 0)
        {
            if (!(pastFixings > 0))
            {
                throw new NanoQLException("no fixings given");
            }

            return new OneAssetOptionResults
            {
                NPV = discount * payoff.Evaluate(accruedAverage),
                Delta = 0,
                Gamma = 0,
                AdditionalResults = additional.ToImmutable(),
            };
        }

        // If the effective strike is negative, exercise resp. permanent OTM is guaranteed and the
        // valuation is made easy
        int m = futureFixings + pastFixings;
        if (effectiveStrike <= 0.0)
        {
            // For a reference, see "Option Pricing Formulas", Haug, 2nd ed, p. 193
            double value, delta;
            if (payoff.OptionType == OptionType.Call)
            {
                double spot0 = process.StateVariable;
                double sAHat = accruedAverage;
                foreach (Date fd in arguments.FixingDates)
                {
                    sAHat += spot0 * process.DividendYield.Discount(fd) / process.RiskFreeRate.Discount(fd) / m;
                }

                value = discount * (sAHat - payoff.Strike);
                delta = discount * (sAHat - accruedAverage) / spot0;
            }
            else
            {
                value = 0;
                delta = 0;
            }

            return new OneAssetOptionResults { NPV = value, Delta = delta, Gamma = 0, AdditionalResults = additional.ToImmutable() };
        }

        // the future average's own strike, where the smile is read (BR5 (xxx)), where QuantLib reads it at K − A/m
        double futureStrike = effectiveStrike * m / futureFixings;

        // Expected value of the non-accrued portion of the average prices
        // In general, m will equal n below if there is no accrued. If accrued, m > n.
        double ea = 0.0;
        ImmutableArray<double>.Builder forwards = ImmutableArray.CreateBuilder<double>(futureFixings);
        ImmutableArray<double>.Builder times = ImmutableArray.CreateBuilder<double>(futureFixings);
        var spotVars = new double[futureFixings];
        ImmutableArray<double>.Builder spotVolsVec = ImmutableArray.CreateBuilder<double>(futureFixings);
        double spot = process.StateVariable;

        int k = 0;
        foreach (Date fd in arguments.FixingDates)
        {
            double dividendDiscount = process.DividendYield.Discount(fd);
            double riskFreeDiscountForFwdEstimation = process.RiskFreeRate.Discount(fd);

            forwards.Add(spot * dividendDiscount / riskFreeDiscountForFwdEstimation);
            times.Add(process.BlackVolatility.TimeFromReference(fd));

            spotVars[k] = process.BlackVolatility.BlackVariance(times[^1], futureStrike);

            // QuantLib's master (#2954): the volatility itself, defined at time 0
            spotVolsVec.Add(process.BlackVolatility.BlackVol(times[^1], futureStrike));

            ea += forwards[^1];
            ++k;
        }

        ea /= m;

        // Expected value of A^2.
        double ea2 = 0.0;
        int n = forwards.Count;

        for (int i = 0; i < n; ++i)
        {
            ea2 += forwards[i] * forwards[i] * Math.Exp(spotVars[i]);
            for (int j = 0; j < i; ++j)
            {
                ea2 += 2 * forwards[i] * forwards[j] * Math.Exp(spotVars[j]);
            }
        }

        ea2 /= m * m;

        // Calculate value
        double tn = times[^1];

        // QuantLib's master (#2954): a zero standard deviation and σ where the last fixing is today
        double stdDev = tn > 0.0 ? Math.Sqrt(Math.Log(ea2 / (ea * ea))) : 0.0;
        double sigma = tn > 0.0 ? stdDev / Math.Sqrt(tn) : 0.0;

        // Populate results
        var black = new BlackCalculator(payoff.OptionType, effectiveStrike, ea, stdDev, discount);

        additional["forward"] = ea;
        additional["exp_A_2"] = ea2;
        additional["tte"] = tn;
        additional["sigma"] = sigma;
        additional["times"] = times.ToImmutable();
        additional["spotVols"] = spotVolsVec.ToImmutable();
        additional["forwards"] = forwards.ToImmutable();

        return new OneAssetOptionResults
        {
            NPV = black.Value,
            Delta = stdDev > 0.0 ? black.Delta(spot) : discount * black.Alpha * ea / spot,
            Gamma = black.Gamma(spot),
            AdditionalResults = additional.ToImmutable(),
        };
    }

    // the latest reference date of the structures the engine reads, each by its own date
    private static Date Latest(GeneralizedBlackScholesProcess process)
    {
        Date latest = process.RiskFreeRate.ReferenceDate;
        foreach (Date d in (Date[])[process.DividendYield.ReferenceDate, process.BlackVolatility.ReferenceDate])
        {
            if (d > latest)
            {
                latest = d;
            }
        }

        return latest;
    }
}
