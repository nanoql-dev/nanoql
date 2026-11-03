// SPDX-FileCopyrightText: Copyright (C) 2025 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/choiasianengine.hpp ql/pricingengines/asian/choiasianengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// Choi's engine for discrete arithmetic average-price Asian options: the average replicated by a basket of the future
/// fixings, priced by Choi's basket engine ("Sum of all Black-Scholes-Merton Models: An efficient Pricing Method for
/// Spread, Basket and Asian Options", 2018).
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's. The basket's processes are QuantLib's: each fixing's forward as the spot, zero rates,
/// and a constant volatility scaled to the last fixing's time; the basket priced by <see cref="ChoiBasketEngine"/>
/// with QuantLib's λ and integration steps.
/// </para>
/// <para>
/// Documented divergences. A seasoned option reads its correlations and volatilities at the future average's own
/// strike, K_f = (K − A/m)·m/f for f of m fixings to come, so it prices as (f/m) times the unseasoned option at K_f,
/// where QuantLib reads its correlations at K − A/m and its volatilities at K, −2.6% to +306% off on a smile varying
/// with maturity (<c>Divergences/SeasonedAsianSmileTests.cs</c>). An effective strike below zero is the certain
/// exercise, priced as Turnbull and Wakeman's engine prices it, its value alone, where QuantLib refuses it
/// (<c>Divergences/ChoiCertainExerciseTests.cs</c>). Its times run on the engine's clock,
/// <see cref="FdmBlackScholesOp.Clock"/>, where QuantLib takes them from the process.
/// </para>
/// <para>
/// A fixing dated from the evaluation date to the latest structure's date, where the structures settle after the
/// evaluation date, is taken there, the spot with no variance and no discount accrued, so as a past fixing at the spot
/// (<c>Divergences/AsianLateSettlementTests.cs</c>), where QuantLib refuses it as past.
/// </para>
/// </remarks>
public static class ChoiAsianEngine
{
    /// <summary>Prices a discrete arithmetic average-price Asian option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="lambda">The basket engine's λ.</param>
    /// <param name="maxNrIntegrationSteps">The basket engine's most integration steps.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The average isn't arithmetic, the exercise isn't European, the payoff isn't plain vanilla, no fixing is given, a
    /// fixing date is past the exercise or in the past, two fixing dates are the same, or the option's past fixings fail
    /// its checks.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DiscreteAveragingAsianOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        double lambda = 15,
        int maxNrIntegrationSteps = 2 << 21)
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
        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = Value(option, arguments, context, process, lambda, maxNrIntegrationSteps),
        });
    }

    private static double Value(
        DiscreteAveragingAsianOption option,
        DiscreteAveragingAsianArguments arguments,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        double lambda,
        int maxNrIntegrationSteps)
    {
        if (!(arguments.AverageType == AverageType.Arithmetic))
        {
            throw new NanoQLException("must be Average::Type Arithmetic ");
        }

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not a European Option");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non plain vanilla payoff given");
        }

        List<Date> fixingDates = [.. arguments.FixingDates];
        int futureFixings = fixingDates.Count;
        int pastFixings = arguments.PastFixings;
        double runningAccumulator = arguments.RunningAccumulator;

        Date exerciseDate = option.Exercise.LastDate();
        YieldTermStructure rTS = process.RiskFreeRate;
        TermStructure clock = FdmBlackScholesOp.Clock(process);

        if (futureFixings > 0 && clock.TimeFromReference(fixingDates[0]) == 0.0)
        {
            // push today fixing to past fixings
            fixingDates.RemoveAt(0);
            futureFixings--;
            pastFixings++;
            runningAccumulator += process.X0;
        }

        if (futureFixings == 0)
        {
            if (!(pastFixings > 0))
            {
                throw new NanoQLException("no past fixings given");
            }

            return payoff.Evaluate(runningAccumulator / pastFixings) * rTS.Discount(exerciseDate);
        }

        if (!(fixingDates[^1] <= exerciseDate))
        {
            throw new NanoQLException("last fixing date must be before exercise date");
        }

        if (!(clock.TimeFromReference(fixingDates[0]) >= 0.0))
        {
            throw new NanoQLException("first fixing date is in the past");
        }

        for (int i = 1; i < fixingDates.Count; ++i)
        {
            if (fixingDates[i] == fixingDates[i - 1])
            {
                throw new NanoQLException("two fixing dates are the same");
            }
        }

        double accruedAverage = pastFixings != 0 ? runningAccumulator / (pastFixings + futureFixings) : 0.0;

        double strike = payoff.Strike - accruedAverage;
        YieldTermStructure qTS = process.DividendYield;
        if (!(strike >= 0.0))
        {
            // the certain exercise, Turnbull and Wakeman's value, where QuantLib refuses "effective strike should to be
            // positive"
            if (payoff.OptionType == OptionType.Put)
            {
                return 0.0;
            }

            int m = futureFixings + pastFixings;
            double spot = process.StateVariable;
            double sAHat = accruedAverage;
            foreach (Date fd in fixingDates)
            {
                sAHat += spot * qTS.Discount(fd) / rTS.Discount(fd) / m;
            }

            return rTS.Discount(exerciseDate) * (sAHat - payoff.Strike);
        }

        // the future average's own strike, where the smile is read, where QuantLib reads its correlations at K − A/m
        // and its volatilities at K
        double futureStrike = strike * (pastFixings + futureFixings) / futureFixings;

        BlackVolTermStructure volTS = process.BlackVolatility;
        Date volRefDate = volTS.ReferenceDate;
        DayCounter volDc = volTS.DayCounter;

        if (futureFixings > 1)
        {
            var fixingTimes = new double[futureFixings];
            var variances = new double[futureFixings];
            for (int i = 0; i < futureFixings; ++i)
            {
                Date fixingDate = fixingDates[i];
                fixingTimes[i] = volDc.YearFraction(volRefDate, fixingDate);
                variances[i] = volTS.BlackVariance(fixingDate, futureStrike);
            }

            var rho = new Matrix(futureFixings, futureFixings);
            for (int i = 0; i < rho.Rows; ++i)
            {
                for (int j = i; j < rho.Columns; ++j)
                {
                    rho[i, j] = rho[j, i] = variances[Math.Min(i, j)] / Math.Sqrt(variances[i] * variances[j]);
                }
            }

            var zeroTS = new FlatForward(rTS.ReferenceDate, 0.0, rTS.DayCounter);

            ImmutableArray<GeneralizedBlackScholesProcess>.Builder processes =
                ImmutableArray.CreateBuilder<GeneralizedBlackScholesProcess>(futureFixings);
            for (int i = 0; i < futureFixings; ++i)
            {
                Date fixingDate = fixingDates[i];
                double sig = volTS.BlackVol(fixingDate, futureStrike) * Math.Sqrt(fixingTimes[i] / fixingTimes[^1]);

                processes.Add(new GeneralizedBlackScholesProcess(
                    process.X0 * qTS.Discount(fixingDate) / rTS.Discount(fixingDate),
                    zeroTS,
                    zeroTS,
                    new BlackConstantVol(volRefDate, volTS.Calendar, sig, volDc)));
            }

            double[] weights = new double[futureFixings];
            Array.Fill(weights, 1.0 / (futureFixings + pastFixings));
            var basketOption = new BasketOption(
                new AverageBasketPayoff(new PlainVanillaPayoff(payoff.OptionType, strike), weights),
                new EuropeanExercise(fixingDates[^1]));

            double basket = ChoiBasketEngine.Calculate(
                basketOption, context, processes.MoveToImmutable(), rho, lambda, maxNrIntegrationSteps).NPV!.Value;
            return basket * rTS.Discount(exerciseDate);
        }

        return BlackFormulaFunctions.BlackFormula(
            payoff.OptionType,
            strike,
            process.X0 / (pastFixings + futureFixings) * qTS.Discount(fixingDates[^1]) / rTS.Discount(fixingDates[^1]),
            Math.Sqrt(volTS.BlackVariance(fixingDates[^1], futureStrike)),
            rTS.Discount(exerciseDate));
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
