// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/gaussian1djamshidianswaptionengine.hpp ql/pricingengines/swaption/gaussian1djamshidianswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Jamshidian swaption engine on a Gaussian1d model: a European swaption decomposed into options on the coupon
/// bond's zero bonds, struck at the state y* where the bond is worth the nominal.
/// </summary>
/// <remarks>
/// <para>
/// A static class (master plan §2's engine row): <c>Calculate</c> runs QuantLib's four steps (playbook §2.1). Only the
/// fixed coupons whose start is on or after the exercise date are part of the bond, which pays the nominal at the
/// floating leg's accrual end. A floating coupon paid after its accrual end (an overnight swap with a payment lag)
/// has the difference its delay makes frozen at time zero and folded into the nominal at the value date, as QuantLib's
/// does. One call shares one memo of the model's terms (the S-g1d plan's G1R1 (iii)).
/// </para>
/// <para>
/// Two documented divergences (the plan's G1R6 (xviii); <c>Divergences/JamshidianBracketTests.cs</c> and
/// <c>Divergences/JamshidianNoFixedCouponTests.cs</c>). QuantLib solves for y* on the fixed bracket [−8, 8] and refuses
/// short-dated swaptions away from the money; the bracket here is QuantLib's first and is doubled only where it
/// doesn't bracket the root, so QuantLib's solver path and bits stand wherever it prices. And with no fixed coupon
/// starting on or after the exercise, QuantLib reads the fixed reset dates past their end as the bond's value date;
/// here the value date is the first floating reset on or after the exercise, the start of the coupons QuantLib's
/// Gaussian1d swaption engine exercises into, and with none the swaption is worth 0, as that engine values it.
/// </para>
/// </remarks>
public static class Gaussian1dJamshidianSwaptionEngine
{
    /// <summary>Prices a European swaption on a Gaussian1d model by Jamshidian's decomposition.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <returns>The swaption's results: the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the model is null.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, it is cash settled at the par yield curve, it isn't European, its swap
    /// has a floating spread or nominals that aren't constant, or y* can't be bracketed.
    /// </exception>
    public static InstrumentResults Calculate(Swaption swaption, EvaluationContext context, Gaussian1dModel model)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (swaption.IsExpired(context))
        {
            return swaption.SetupExpired();
        }

        swaption.Validate();
        InstrumentResults results = Body(swaption, context, model);
        return swaption.FetchResults(results);
    }

    // QuantLib's calculate(): its checks in order, the payment lag's frozen part, y*, and the sum of the bond options.
    private static InstrumentResults Body(Swaption swaption, EvaluationContext context, Gaussian1dModel model)
    {
        FixedVsFloatingSwapArguments arguments = FixedVsFloatingSwapArguments.Of(swaption.Underlying, context);

        if (!(swaption.SettlementMethod != SettlementMethod.ParYieldCurve))
        {
            throw new NanoQLException("cash settled (ParYieldCurve) swaptions not priced with Gaussian1dJamshidianSwaptionEngine");
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

        Date exercise = swaption.Exercise.Date(0);

        // only consider coupons with start date >= exercise dates
        int startIndex = Gaussian1dSwaptionEngine.UpperBound(arguments.FixedResetDates, exercise - 1);

        // the bond's value date: the first fixed reset on or after the exercise, as QuantLib's; with none, the first
        // floating reset on or after it, where QuantLib reads past the fixed reset dates (G1R6 (xviii))
        Date valueDate;
        if (startIndex < arguments.FixedResetDates.Length)
        {
            valueDate = arguments.FixedResetDates[startIndex];
        }
        else
        {
            int floatingStart = Gaussian1dSwaptionEngine.UpperBound(arguments.FloatingResetDates, exercise - 1);
            if (floatingStart == arguments.FloatingResetDates.Length)
            {
                return new InstrumentResults { NPV = 0.0 };
            }

            valueDate = arguments.FloatingResetDates[floatingStart];
        }

        // handle SOFR with a payment lag
        double[] amounts = [.. arguments.FixedCoupons, nominal];
        Date floatingAccrualEnd = swaption.Underlying.FloatingSchedule.Dates[^1];
        Date[] payDates = [.. arguments.FixedPayDates, floatingAccrualEnd];

        double parNominal = nominal;
        {
            YieldTermStructure curve = model.TermStructure;
            ImmutableArray<Date> floatDates = swaption.Underlying.FloatingSchedule.Dates;
            double dfValue = curve.Discount(valueDate);
            for (int j = 0; j + 1 < floatDates.Length && j < arguments.FloatingPayDates.Length; ++j)
            {
                if (floatDates[j] < valueDate)
                {
                    continue;
                }

                Date accrualEnd = floatDates[j + 1];
                if (arguments.FloatingPayDates[j] > accrualEnd)
                {
                    double dfStart = curve.Discount(floatDates[j]);
                    double dfEnd = curve.Discount(accrualEnd);
                    double dfPay = curve.Discount(arguments.FloatingPayDates[j]);

                    // Without a lag, the NPV is N * (dfStart/dfEnd - 1) * dfEnd = N * (dfStart - dfEnd). With a lag,
                    // it is N * (dfStart/dfEnd - 1) * dfPay = N * (dfStart - dfEnd) * (dfPay/dfEnd). Jamshidian cannot
                    // discount coupons to separate dates, so the difference between the two, i.e.
                    // N * (dfStart - dfEnd) * (1 - dfPay/dfEnd), is frozen at time zero - no payment-delay convexity -
                    // and folded into the notional at the value date.
                    parNominal -= nominal * (dfStart - dfEnd) * (1.0 - (dfPay / dfEnd)) / dfValue;
                }
            }
        }

        var memo = new Gaussian1dMemo();
        var finder = new RStarFinder(model, memo, parNominal, exercise, valueDate, payDates, amounts, startIndex);
        double rStar = finder.SolveRStar();

        OptionType w = arguments.Type == SwapType.Payer ? OptionType.Put : OptionType.Call;
        int size = amounts.Length;

        double value = 0.0;
        for (int i = startIndex; i < size; i++)
        {
            double strike = model.Zerobond(payDates[i], exercise, rStar, null, memo) / model.Zerobond(valueDate, exercise, rStar, null, memo);
            double dboValue = model.ZerobondOption(w, exercise, valueDate, payDates[i], strike, null, 0.0, null, 7.0, 64, true, false, memo);
            value += amounts[i] * dboValue;
        }

        return new InstrumentResults { NPV = value };
    }
}

/// <summary>
/// The function whose zero is y*: the nominal less the coupon bond's amounts discounted to the value date at a
/// standardized state at the exercise. QuantLib's <c>Gaussian1dJamshidianSwaptionEngine::rStarFinder</c>.
/// </summary>
file readonly struct RStarFinder : ISolver1DFunction
{
    // The widest bracket the doubling tries, past which QuantLib's refusal stands.
    private const double MaxBracket = 8.0 * 1048576.0;

    private readonly Gaussian1dModel model;
    private readonly Gaussian1dMemo memo;
    private readonly double strike;
    private readonly Date maturityDate;
    private readonly Date valueDate;
    private readonly Date[] times;
    private readonly double[] amounts;
    private readonly int startIndex;

    public RStarFinder(Gaussian1dModel model, Gaussian1dMemo memo, double nominal, Date maturityDate, Date valueDate, Date[] fixedPayDates, double[] amounts, int startIndex)
    {
        this.model = model;
        this.memo = memo;
        strike = nominal;
        this.maturityDate = maturityDate;
        this.valueDate = valueDate;
        times = fixedPayDates;
        this.amounts = amounts;
        this.startIndex = startIndex;
    }

    public double Evaluate(double x)
    {
        double value = strike;
        int size = times.Length;
        for (int i = startIndex; i < size; i++)
        {
            double dbValue = model.Zerobond(times[i], maturityDate, x, null, memo) / model.Zerobond(valueDate, maturityDate, x, null, memo);
            value -= amounts[i] * dbValue;
        }

        return value;
    }

    // QuantLib's Brent on [−8, 8], accuracy 1e-8, guess 0; the bracket doubled only while it doesn't bracket the root,
    // as the solver's own check reads it, so where QuantLib's brackets, its call is QuantLib's (G1R6 (xviii)).
    public double SolveRStar()
    {
        double minStrike = -8.0;
        double maxStrike = 8.0;
        while (maxStrike < MaxBracket && !Brackets(minStrike, maxStrike))
        {
            minStrike *= 2.0;
            maxStrike *= 2.0;
        }

        Brent s1d = new Brent().SetMaxEvaluations(10000).SetLowerBound(minStrike).SetUpperBound(maxStrike);

        // this is actually yStar
        return s1d.Solve(this, 1e-8, 0.00, minStrike, maxStrike);
    }

    // The solver's bracket check: an end at the root, or ends of opposite signs.
    private bool Brackets(double xMin, double xMax)
    {
        double fxMin = Evaluate(xMin);
        if (ComparisonFunctions.Close(fxMin, 0.0))
        {
            return true;
        }

        double fxMax = Evaluate(xMax);
        return ComparisonFunctions.Close(fxMax, 0.0) || fxMin * fxMax < 0.0;
    }
}
