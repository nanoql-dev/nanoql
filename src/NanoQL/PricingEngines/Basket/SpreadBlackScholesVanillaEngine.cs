// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/spreadblackscholesvanillaengine.hpp ql/pricingengines/basket/spreadblackscholesvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The base of the spread engines on two Black-Scholes processes: a European spread option priced from the two
/// forwards, their variances at the forwards and the first process's discount, by the derived engine's formula.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as QuantLib's class: an engine derives from it and implements
/// <see cref="Calculate(double, double, double, OptionType, double, double, double)"/>. Immutable. Each forward reads
/// its own process's curves and the payoff is discounted on the first process's risk-free curve, QuantLib's reading.
/// </para>
/// <para>
/// Each structure is read by date and accrues nothing before its reference date (the playbook's row for structures on
/// another clock), where QuantLib's read throws for an option expiring before a later structure's date; on structures
/// dated alike the reads are QuantLib's (<c>Divergences/TwoAssetMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
public abstract class SpreadBlackScholesVanillaEngine
{
    /// <summary>Initializes a new instance of the <see cref="SpreadBlackScholesVanillaEngine"/> class.</summary>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <exception cref="ArgumentNullException">A process is <see langword="null"/>.</exception>
    protected SpreadBlackScholesVanillaEngine(
        GeneralizedBlackScholesProcess process1, GeneralizedBlackScholesProcess process2, double correlation)
    {
        ArgumentNullException.ThrowIfNull(process1);
        ArgumentNullException.ThrowIfNull(process2);
        Process1 = process1;
        Process2 = process2;
        Rho = correlation;
    }

    /// <summary>Gets the first asset's process.</summary>
    protected GeneralizedBlackScholesProcess Process1 { get; }

    /// <summary>Gets the second asset's process.</summary>
    protected GeneralizedBlackScholesProcess Process2 { get; }

    /// <summary>Gets the correlation of the two assets.</summary>
    protected double Rho { get; }

    /// <summary>Prices a European spread option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option, on a <see cref="SpreadBasketPayoff"/> over a plain vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't a spread payoff over a plain vanilla payoff, or the formula
    /// refuses the market.
    /// </exception>
    public MultiAssetOptionResults Calculate(BasketOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        if (option.Exercise is not EuropeanExercise exercise)
        {
            throw new NanoQLException("not an European exercise");
        }

        // QuantLib's text, its leading space kept
        if (option.Payoff is not SpreadBasketPayoff spreadPayoff)
        {
            throw new NanoQLException(" spread payoff expected");
        }

        if (spreadPayoff.BasePayoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        double strike = payoff.Strike;
        OptionType optionType = payoff.OptionType;
        Date maturityDate = exercise.LastDate();

        double f1 = Process1.X0
            / Process1.RiskFreeRate.AccruedDiscount(maturityDate)
            * Process1.DividendYield.AccruedDiscount(maturityDate);
        double f2 = Process2.X0
            / Process2.RiskFreeRate.AccruedDiscount(maturityDate)
            * Process2.DividendYield.AccruedDiscount(maturityDate);

        double variance1 = Process1.BlackVolatility.AccruedVariance(maturityDate, f1);
        double variance2 = Process2.BlackVolatility.AccruedVariance(maturityDate, f2);

        double df = Process1.RiskFreeRate.AccruedDiscount(exercise.LastDate());

        return option.FetchResults(new MultiAssetOptionResults
        {
            NPV = Calculate(f1, f2, strike, optionType, variance1, variance2, df),
        });
    }

    /// <summary>The value of a spread option on two forwards.</summary>
    /// <param name="f1">The first forward.</param>
    /// <param name="f2">The second forward.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="optionType">The option's type.</param>
    /// <param name="variance1">The first forward's Black variance to expiry.</param>
    /// <param name="variance2">The second forward's Black variance to expiry.</param>
    /// <param name="df">The discount factor to expiry.</param>
    /// <returns>The value.</returns>
    protected abstract double Calculate(
        double f1, double f2, double strike, OptionType optionType, double variance1, double variance2, double df);
}
