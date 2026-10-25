// SPDX-FileCopyrightText: Copyright (C) 2014 Fabien Le Floc'h
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/hestonexpansionengine.hpp ql/pricingengines/vanilla/hestonexpansionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The Heston expansion engine: Black's formula at an expansion's implied volatility.</summary>
/// <remarks>
/// <para>
/// The model's time runs from the earliest curve date and the forward and discount are read by date (the S-hes plan's
/// HR7 (xi), S-eq's <see cref="AnalyticHestonEngine"/>'s reads); at the model's time 0, Black's formula at a zero
/// standard deviation, the forward's discounted intrinsic value, before any coefficient (HR7 (xxxi)).
/// </para>
/// <para>
/// References: M. Forde, A. Jacquier, R. Lee, The small-time smile and term structure of implied volatility under the
/// Heston model, SIAM Journal on Financial Mathematics, 2012; M. Lorig, S. Pagliarani, A. Pascucci, Explicit implied
/// vols for multifactor local-stochastic vol models, arXiv:1306.5447v3, 2014.
/// </para>
/// </remarks>
public static class HestonExpansionEngine
{
    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="formula">The expansion.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="formula"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, the payoff isn't plain vanilla or the underlying
    /// isn't positive.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option, EvaluationContext context, HestonModel model, HestonExpansionEngineHestonExpansionFormula formula)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        EnumArguments.Check(formula, nameof(formula));
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        // this is a european option pricer
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        // plain vanilla
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non plain vanilla payoff given");
        }

        HestonProcess process = model.Process;
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

        double forward = spotPrice * dividendDiscount / riskFreeDiscount;
        if (term == 0.0)
        {
            // no variance has accrued: Black at a zero standard deviation, before any coefficient (HR7 (xxxi))
            return option.FetchResults(new OneAssetOptionResults
            {
                NPV = BlackFormulaFunctions.BlackFormula(payoff, forward, 0.0, riskFreeDiscount, 0),
            });
        }

        HestonExpansion expansion = formula switch
        {
            HestonExpansionEngineHestonExpansionFormula.LPP2 =>
                new LPP2HestonExpansion(model.Kappa, model.Theta, model.Sigma, model.V0, model.Rho, term),
            HestonExpansionEngineHestonExpansionFormula.LPP3 =>
                new LPP3HestonExpansion(model.Kappa, model.Theta, model.Sigma, model.V0, model.Rho, term),
            _ => new FordeHestonExpansion(model.Kappa, model.Theta, model.Sigma, model.V0, model.Rho, term),
        };
        double vol = expansion.ImpliedVolatility(strikePrice, forward);
        double price = BlackFormulaFunctions.BlackFormula(payoff, forward, vol * Math.Sqrt(term), riskFreeDiscount, 0);
        return option.FetchResults(new OneAssetOptionResults { NPV = price });
    }
}
