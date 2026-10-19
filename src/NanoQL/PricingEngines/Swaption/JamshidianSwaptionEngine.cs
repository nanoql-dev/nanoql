// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/jamshidianswaptionengine.hpp ql/pricingengines/swaption/jamshidianswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The Jamshidian swaption engine: a European swaption decomposed into options on the fixed leg's discount bonds.</summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) on a one-factor affine model, an
/// argument where QuantLib's engine holds it. The rate r* at which the fixed leg's bonds are worth the nominal at the
/// exercise is solved by <see cref="Brent"/> (10,000 evaluations, bounds ±10, accuracy 1e-8, guess 0.05), and each
/// coupon's option is the model's discount-bond option on the bond from the swap's first reset, so a model that
/// accounts for the delay between the exercise and the swap's start (Hull-White's) prices it; another might assume
/// that the exercise date is the swap's start.
/// </para>
/// <para>
/// The term structure is needed only when the model can't provide one: a model that is an
/// <see cref="ITermStructureConsistentModel"/> discounts on its own curve, as QuantLib's engine does, whatever curve is
/// given.
/// </para>
/// </remarks>
public static class JamshidianSwaptionEngine
{
    /// <summary>Prices a European swaption on a one-factor affine model.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The swaption's results: the expired results when the swaption has expired.</returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the model is null.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, it is cash settled at the par yield curve, it isn't European, its swap has
    /// a floating spread or nominals that aren't constant, the model has no curve and none is given, or r* can't be
    /// solved.
    /// </exception>
    public static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        OneFactorAffineModel model,
        YieldTermStructure? termStructure = null)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (swaption.IsExpired(context))
        {
            return swaption.SetupExpired();
        }

        swaption.Validate();
        InstrumentResults results = Body(swaption, context, model, termStructure);
        return swaption.FetchResults(results);
    }

    // QuantLib's calculate(): its checks in order, r*, and the sum of the coupons' bond options. QuantLib's fifth
    // check, of the model, is the argument check above.
    private static InstrumentResults Body(
        Swaption swaption, EvaluationContext context, OneFactorAffineModel model, YieldTermStructure? termStructure)
    {
        FixedVsFloatingSwapArguments arguments = FixedVsFloatingSwapArguments.Of(swaption.Underlying, context);

        if (!(swaption.SettlementMethod != SettlementMethod.ParYieldCurve))
        {
            throw new NanoQLException(
                "cash settled (ParYieldCurve) swaptions not priced with JamshidianSwaptionEngine");
        }

        if (!(swaption.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("cannot use the Jamshidian decomposition on exotic swaptions");
        }

        double spread = swaption.Underlying.Spread;
        if (!(spread == 0.0))
        {
            throw new NanoQLException($"non zero spread ({spread.ToString(CultureInfo.InvariantCulture)}) not allowed");
        }

        if (arguments.Nominal is not double nominal)
        {
            throw new NanoQLException("non-constant nominals are not supported yet");
        }

        Date referenceDate;
        DayCounter dayCounter;

        if (model is ITermStructureConsistentModel tsmodel)
        {
            referenceDate = tsmodel.TermStructure.ReferenceDate;
            dayCounter = tsmodel.TermStructure.DayCounter;
        }
        else
        {
            YieldTermStructure curve = termStructure ?? throw new NanoQLException("empty Handle cannot be dereferenced");
            referenceDate = curve.ReferenceDate;
            dayCounter = curve.DayCounter;
        }

        double[] amounts = [.. arguments.FixedCoupons];
        amounts[^1] += nominal;

        double maturity = dayCounter.YearFraction(referenceDate, swaption.Exercise.Date(0));

        double[] fixedPayTimes = new double[arguments.FixedPayDates.Length];
        double valueTime = dayCounter.YearFraction(referenceDate, arguments.FixedResetDates[0]);
        for (int i = 0; i < fixedPayTimes.Length; i++)
        {
            fixedPayTimes[i] = dayCounter.YearFraction(referenceDate, arguments.FixedPayDates[i]);
        }

        var finder = new RStarFinder(model, nominal, maturity, valueTime, fixedPayTimes, amounts);
        double minStrike = -10.0;
        double maxStrike = 10.0;
        Brent s1d = new Brent().SetMaxEvaluations(10000).SetLowerBound(minStrike).SetUpperBound(maxStrike);
        double rStar = s1d.Solve(finder, 1e-8, 0.05, minStrike, maxStrike);

        OptionType w = arguments.Type == SwapType.Payer ? OptionType.Put : OptionType.Call;
        int size = arguments.FixedCoupons.Length;

        double value = 0.0;
        for (int i = 0; i < size; i++)
        {
            double fixedPayTime = dayCounter.YearFraction(referenceDate, arguments.FixedPayDates[i]);
            double strike = finder.DiscountBondRatio(i, rStar);

            // Looks like the swaption decomposed into individual options adjusted for maturity. Each individual
            // option is valued by Hull-White (or other one-factor model).
            double dboValue = model.DiscountBondOption(w, strike, maturity, valueTime, fixedPayTime);
            value += amounts[i] * dboValue;
        }

        return new InstrumentResults { NPV = value };
    }
}

/// <summary>
/// The function whose zero is r*: the nominal less the fixed leg's amounts discounted from the swap's first reset at a
/// short rate at the exercise. QuantLib's <c>JamshidianSwaptionEngine::rStarFinder</c>.
/// </summary>
/// <remarks>
/// Since for an exercise time tₑ the bond price is P(tₑ, T; r) = A(tₑ, T)·exp(−B(tₑ, T)·r), the ratios
/// Rᵢ(r) = P(tₑ, Tᵢ; r)/P(tₑ, T₀; r) have the form cᵢ·exp(dᵢ·r), with cᵢ and dᵢ independent of r: they are found by
/// plugging r = 0 and r = 1 in the formula, which gives Rᵢ(0) = cᵢ and Rᵢ(1) = cᵢ·exp(dᵢ), and are stored.
/// </remarks>
file readonly struct RStarFinder : ISolver1DFunction
{
    private readonly double strike;
    private readonly double[] discountRatios;
    private readonly double[] rateCoefficients;
    private readonly double[] amounts;

    public RStarFinder(
        OneFactorAffineModel model, double nominal, double maturity, double valueTime, double[] fixedPayTimes, double[] amounts)
    {
        strike = nominal;
        discountRatios = new double[fixedPayTimes.Length];
        rateCoefficients = new double[fixedPayTimes.Length];
        this.amounts = amounts;

        double valueDiscount0 = model.DiscountBond(maturity, valueTime, 0.0);
        double valueDiscount1 = model.DiscountBond(maturity, valueTime, 1.0);

        for (int i = 0; i < fixedPayTimes.Length; ++i)
        {
            double discount0 = model.DiscountBond(maturity, fixedPayTimes[i], 0.0);
            double discount1 = model.DiscountBond(maturity, fixedPayTimes[i], 1.0);
            discountRatios[i] = discount0 / valueDiscount0;
            rateCoefficients[i] = Math.Log(discount1 / valueDiscount1 / discountRatios[i]);
        }
    }

    public double DiscountBondRatio(int i, double x) => discountRatios[i] * Math.Exp(rateCoefficients[i] * x);

    public double Evaluate(double x)
    {
        double value = strike;
        for (int i = 0; i < discountRatios.Length; ++i)
        {
            value -= amounts[i] * DiscountBondRatio(i, x);
        }

        return value;
    }
}
