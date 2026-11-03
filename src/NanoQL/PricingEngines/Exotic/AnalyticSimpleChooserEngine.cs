// SPDX-FileCopyrightText: Copyright (C) 2010 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/exotic/analyticsimplechooserengine.hpp ql/pricingengines/exotic/analyticsimplechooserengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a simple chooser option: Rubinstein's decomposition at the choosing date, the call to
/// maturity and the put to the choosing date at the strike carried there.
/// </summary>
/// <remarks>
/// <para>
/// Stateless. QuantLib evaluates Rubinstein's formula on the zero rates and the volatility at maturity, its times from
/// the evaluation date, so it is exact only on flat markets; here the option is C(K, T) + D_q(t, T)·P(K·D_r(t, T) /
/// D_q(t, T), t), put-call parity at the choosing date t, each priced by <see cref="AnalyticEuropeanEngine"/> on the
/// process: exact under deterministic rates, dividends and volatility
/// (<c>Divergences/SimpleChooserTermStructureTests.cs</c>), QuantLib's formula's value on a flat market within its last
/// bits. Each structure is read on its own times, so QuantLib's refusals of day counters that differ, which its
/// formula's single time needed, don't apply.
/// </para>
/// <para>QuantLib's refusals of a spot, a strike or a volatility at maturity that isn't positive, and of a choosing date
/// on or before the evaluation date, are kept.</para>
/// </remarks>
public static class AnalyticSimpleChooserEngine
{
    /// <summary>Prices a simple chooser option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process.</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid; the spot, the strike or the volatility at maturity isn't positive; or the choosing
    /// date isn't after the evaluation date.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        SimpleChooserOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = Value(option, context, process) });
    }

    // QuantLib's calculate(), on discounts and variances by date.
    private static double Value(
        SimpleChooserOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        double spot = process.X0;
        var payoff = (StrikedTypePayoff)option.Payoff!;
        double strike = payoff.Strike;
        Date maturity = option.Exercise.LastDate();
        double volatility = process.BlackVolatility.BlackVol(maturity, strike);
        double timeToChoosing = process.RiskFreeRate.DayCounter.YearFraction(context.EvaluationDate, option.ChoosingDate);

        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null spot value");
        }

        if (!(strike > 0.0))
        {
            throw new NanoQLException("negative or null strike value");
        }

        if (!(volatility > 0.0))
        {
            throw new NanoQLException("negative or null volatility");
        }

        if (!(timeToChoosing > 0.0))
        {
            throw new NanoQLException("choosing date earlier than or equal to evaluation date");
        }

        // put-call parity at the choosing date: max(C, P) = C + D_q(t, T)·max(K·D_r(t, T)/D_q(t, T) − S, 0)
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        double dividendDiscount = process.DividendYield.AccruedDiscountOn(clock, maturity)
            / process.DividendYield.AccruedDiscountOn(clock, option.ChoosingDate);
        double riskFreeDiscount = process.RiskFreeRate.AccruedDiscountOn(clock, maturity)
            / process.RiskFreeRate.AccruedDiscountOn(clock, option.ChoosingDate);
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturity));
        var put = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Put, strike * riskFreeDiscount / dividendDiscount),
            new EuropeanExercise(option.ChoosingDate));
        return AnalyticEuropeanEngine.Calculate(call, context, process).NPV!.Value
            + (dividendDiscount * AnalyticEuropeanEngine.Calculate(put, context, process).NPV!.Value);
    }
}
