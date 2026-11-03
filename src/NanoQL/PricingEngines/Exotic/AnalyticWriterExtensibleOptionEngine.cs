// SPDX-FileCopyrightText: Copyright (C) 2011 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/exotic/analyticwriterextensibleoptionengine.hpp ql/pricingengines/exotic/analyticwriterextensibleoptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a writer-extensible option: Longstaff's formula, the vanilla to the first exercise and a
/// bivariate term for the extension, on West's bivariate normal.
/// </summary>
/// <remarks>
/// <para>
/// Stateless. QuantLib evaluates the formula on one zero rate, dividend yield and volatility, read at the first
/// exercise, for both periods, so it is exact only on flat markets; here each term reads the variances to each exercise
/// date at the first strike, the correlation from their ratio and the discounts by date, which is exact under
/// deterministic rates, dividends and volatility (<c>Divergences/WriterExtensibleTermStructureTests.cs</c>, against the
/// exact integral at the first exercise) and QuantLib's algebra on a flat market, its value there within its last bits.
/// </para>
/// <para>
/// Every structure is read by date, accruing nothing before its own date, where QuantLib times both periods on the
/// risk-free curve (<c>Divergences/ExoticMismatchedTimesTests.cs</c>); a date before the process's clock keeps
/// QuantLib's "negative time" refusal.
/// </para>
/// </remarks>
public static class AnalyticWriterExtensibleOptionEngine
{
    /// <summary>Prices a writer-extensible option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process.</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The option's terms are invalid, or an exercise is before the clock's date.</exception>
    public static OneAssetOptionResults Calculate(
        WriterExtensibleOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = Value(option, process) });
    }

    // QuantLib's calculate(), on discounts and variances by date.
    private static double Value(WriterExtensibleOption option, GeneralizedBlackScholesProcess process)
    {
        // We take all the arguments:
        var payoff1 = (PlainVanillaPayoff)option.Payoff!;
        PlainVanillaPayoff payoff2 = option.Payoff2;
        OptionType type = payoff1.OptionType;

        // S = spot
        double spot = process.X0;

        // the discounts and variances to each exercise, by date
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        Date date1 = option.Exercise.LastDate();
        Date date2 = option.Exercise2.LastDate();
        double dividendDiscount1 = process.DividendYield.AccruedDiscountOn(clock, date1);
        double riskFreeDiscount1 = process.RiskFreeRate.AccruedDiscountOn(clock, date1);
        double dividendDiscount2 = process.DividendYield.AccruedDiscountOn(clock, date2);
        double riskFreeDiscount2 = process.RiskFreeRate.AccruedDiscountOn(clock, date2);
        double variance1 = process.BlackVolatility.AccruedVariance(date1, payoff1.Strike);
        double variance2 = process.BlackVolatility.AccruedVariance(date2, payoff1.Strike);
        double stdDev1 = Math.Sqrt(variance1);
        double stdDev2 = Math.Sqrt(variance2);

        double forwardPrice = spot * dividendDiscount1 / riskFreeDiscount1;

        // Call the B&S method:
        double black = BlackFormula(type, payoff1.Strike, forwardPrice, stdDev1, riskFreeDiscount1);

        // Standard bivariate normal distribution:
        double ro = Math.Sqrt(variance1 / variance2);
        double z1 = (Math.Log(spot / payoff2.Strike) + Math.Log(dividendDiscount2 / riskFreeDiscount2) + (variance2 / 2))
            / stdDev2;
        double z2 = (Math.Log(spot / payoff1.Strike) + Math.Log(dividendDiscount1 / riskFreeDiscount1) + (variance1 / 2))
            / stdDev1;

        // Call the bivariate method:
        var biv = new BivariateCumulativeNormalDistributionWe04DP(-ro);

        // Final computing:
        double bivariate1, bivariate2;
        if (type == OptionType.Call)
        {
            // Call case:
            bivariate1 = biv.Evaluate(z1, -z2);
            bivariate2 = biv.Evaluate(z1 - stdDev2, -z2 + stdDev1);
            return black + (spot * dividendDiscount2 * bivariate1) - (payoff2.Strike * riskFreeDiscount2 * bivariate2);
        }

        // Put case:
        bivariate1 = biv.Evaluate(-z1, z2);
        bivariate2 = biv.Evaluate(-z1 + stdDev2, z2 - stdDev1);
        return black - (spot * dividendDiscount2 * bivariate1) + (payoff2.Strike * riskFreeDiscount2 * bivariate2);
    }
}
