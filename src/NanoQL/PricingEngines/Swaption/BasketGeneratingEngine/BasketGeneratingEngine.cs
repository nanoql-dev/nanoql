// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/basketgeneratingengine.hpp ql/pricingengines/swaption/basketgeneratingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The base of the pricing engines that generate a calibration basket: per live exercise date, a standard swaption
/// matching the underlying's value, delta and gamma in the model's state, or the at-the-money coterminal one.
/// </summary>
/// <remarks>
/// <para>
/// The generated calibrating swaptions have a strike floored at 0.1bp (minus the lognormal shift, if applicable); this
/// is not true for at-the-money swaptions, whose strike the swaption helper generates. The standard swap base index
/// should have forwarding and discounting curves, which the helpers' market prices read, so the model's prices in a
/// calibration must read the same curves; the volatility structure should be consistent with them, and the engine's
/// discount curve should be the index's discounting curve.
/// </para>
/// <para>
/// Immutable; the derived engines (<see cref="Gaussian1dNonstandardSwaptionEngine"/> and the float-float one) bind
/// the underlying an instrument gives, where QuantLib's read the arguments they hold. A flipped match, a negative
/// nominal read as the other swap type, gives its helper the nominal's size, where QuantLib floors the signed nominal
/// at 1e-6 first and so gives 1e-6 (<c>basketgeneratingengine.cpp:213-214</c>, <c>:230</c>; the S-g1d plan's G1R6
/// (xxiv)).
/// </para>
/// </remarks>
public abstract class BasketGeneratingEngine
{
    private readonly Gaussian1dModel onefactormodel;
    private readonly double? oas;
    private readonly YieldTermStructure? discountCurve;

    private protected BasketGeneratingEngine(Gaussian1dModel model, double? oas, YieldTermStructure? discountCurve)
    {
        ArgumentNullException.ThrowIfNull(model);
        onefactormodel = model;
        this.oas = oas;
        this.discountCurve = discountCurve;
    }

    /// <summary>Gets the model.</summary>
    public Gaussian1dModel Model => onefactormodel;

    /// <summary>Gets the option-adjusted spread, continuously compounded on the model curve's day counter;
    /// <see langword="null"/> for none, QuantLib's empty handle.</summary>
    public double? Oas => oas;

    /// <summary>Gets the discount curve; <see langword="null"/> for the model's.</summary>
    public YieldTermStructure? DiscountCurve => discountCurve;

    // The underlying of an instrument the derived engine prices, or QuantLib's refusal of another's arguments.
    private protected abstract IBasketGeneratingUnderlying Underlying(Instrument instrument, EvaluationContext context, Gaussian1dMemo memo);

    // QuantLib's calibrationBasket (basketgeneratingengine.cpp:31-243).
    internal ImmutableArray<SwaptionHelper> CalibrationBasket(
        Instrument instrument,
        Exercise exercise,
        SwapIndex standardSwapBase,
        SwaptionVolatilityStructure swaptionVolatility,
        BasketGeneratingEngineCalibrationBasketType basketType,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(standardSwapBase);
        ArgumentNullException.ThrowIfNull(swaptionVolatility);
        if (basketType is not (BasketGeneratingEngineCalibrationBasketType.Naive or BasketGeneratingEngineCalibrationBasketType.MaturityStrikeByDeltaGamma))
        {
            throw new ArgumentOutOfRangeException(nameof(basketType), basketType, $"Calibration basket type not known ({(int)basketType})");
        }

        if (standardSwapBase.ForwardingTermStructure is null)
        {
            throw new ArgumentException("standard swap base forwarding term structure must not be empty.", nameof(standardSwapBase));
        }

        if (!(!standardSwapBase.ExogenousDiscount || standardSwapBase.DiscountingTermStructure is not null))
        {
            throw new ArgumentException("standard swap base discounting term structure must not be empty.", nameof(standardSwapBase));
        }

        var memo = new Gaussian1dMemo();
        IBasketGeneratingUnderlying underlying = Underlying(instrument, context, memo);
        var result = ImmutableArray.CreateBuilder<SwaptionHelper>();

        Date today = context.EvaluationDate;
        ImmutableArray<Date> dates = exercise.Dates;
        int minIdxAlive = Gaussian1dSwaptionEngine.UpperBound(dates, today);

        var rebEx = exercise as RebatedExercise;

        YieldTermStructure helperCurve = (standardSwapBase.ExogenousDiscount
            ? standardSwapBase.DiscountingTermStructure
            : standardSwapBase.ForwardingTermStructure)!;

        for (int i = minIdxAlive; i < dates.Length; i++)
        {
            Date expiry = exercise.Date(i);
            double rebate = 0.0;
            Date rebateDate = expiry;
            if (rebEx is not null)
            {
                rebate = rebEx.Rebate(i);
                rebateDate = rebEx.RebatePaymentDate(i);
            }

            SwaptionHelper helper;
            if (basketType == BasketGeneratingEngineCalibrationBasketType.Naive)
            {
                double swapLength = swaptionVolatility.DayCounter.YearFraction(standardSwapBase.ValueDate(expiry), underlying.LastDate);
                SmileSection sec = swaptionVolatility.SmileSection(
                    expiry, new Period((int)Math.Round(swapLength * 12.0, MidpointRounding.AwayFromZero), TimeUnit.Months), true);
                double? atmStrike = sec.AtmLevel();
                double atmVol = atmStrike is double atm ? sec.Volatility(atm) : sec.Volatility(0.03);
                double shift = sec.Shift;
                helper = new SwaptionHelper(
                    expiry,
                    underlying.LastDate,
                    atmVol,
                    standardSwapBase.IborIndex,
                    standardSwapBase.FixedLegTenor,
                    standardSwapBase.DayCounter,
                    standardSwapBase.IborIndex.DayCounter,
                    helperCurve,
                    context,
                    BlackCalibrationHelperCalibrationErrorType.RelativePriceError,
                    null,
                    1.0,
                    swaptionVolatility.VolatilityType,
                    shift);
            }
            else
            {
                // determine the npv, first and second order derivatives at
                // $y=0$ of the underlying swap
                const double h = 0.0001; // finite difference step in $y$, make
                                         // this a parameter of the engine ?
                double zSpreadDsc = oas is double s
                    ? Math.Exp(-s * onefactormodel.TermStructure.DayCounter.YearFraction(expiry, rebateDate))
                    : 1.0;

                double npvm = underlying.Npv(expiry, -h)
                    + (rebate * onefactormodel.Zerobond(rebateDate, expiry, -h, discountCurve, memo) * zSpreadDsc);
                double npv = underlying.Npv(expiry, 0.0)
                    + (rebate * onefactormodel.Zerobond(rebateDate, expiry, 0, discountCurve, memo) * zSpreadDsc);
                double npvp = underlying.Npv(expiry, h)
                    + (rebate * onefactormodel.Zerobond(rebateDate, expiry, h, discountCurve, memo) * zSpreadDsc);
                double delta = (npvp - npvm) / (2.0 * h);
                double gamma = (npvp - (2.0 * npv) + npvm) / (h * h);

                if (!((npv * npv) + (delta * delta) + (gamma * gamma) > 0.0))
                {
                    throw new NanoQLException("(npv,delta,gamma) must have a positive norm");
                }

                // play safe, we restrict the maximum maturity so to easily fit
                // in the date class restriction
                double maxMaturity = swaptionVolatility.DayCounter.YearFraction(expiry, Date.MaxDate - 365);

                var matchHelper = new MatchHelper(
                    underlying.Type, npv, delta, gamma, onefactormodel, standardSwapBase, expiry, maxMaturity, h, context, memo);

                // Optimize
                Vector initial = underlying.InitialGuess(expiry);
                if (!(initial.Size == 3))
                {
                    throw new NanoQLException($"initial guess must have size 3 (but is {initial.Size})");
                }

                var ec = new EndCriteria(1000, 200, 1E-8, 1E-8, 1E-8); // make these
                                                                        // criteria and the
                                                                        // optimizer itself
                                                                        // parameters of
                                                                        // the method ?
                Constraint constraint = new NoConstraint();
                var p = new ProblemState(new Problem(matchHelper, constraint, initial.AsSpan()));
                var lm = new LevenbergMarquardt();
                EndCriteriaType ret = lm.Minimize(p, ec);

                if (!(ret != EndCriteriaType.None && ret != EndCriteriaType.Unknown && ret != EndCriteriaType.MaxIterations))
                {
                    throw new NanoQLException($"optimizer returns error ({ret})");
                }

                Vector solution = p.CurrentValue.Clone();

                double maturity = Math.Abs(solution[1]);
                int years = (int)Math.Floor(maturity);
                maturity -= years;
                maturity *= 12.0;
                int months = (int)Math.Floor(maturity + 0.5);
                if (years == 0 && months == 0)
                {
                    months = 1; // ensure a maturity of at least one months
                }

                Period matPeriod = new Period(years, TimeUnit.Years) + new Period(months, TimeUnit.Months);

                SmileSection sec = swaptionVolatility.SmileSection(expiry, matPeriod, true);
                double shift = sec.Shift;

                // we have to floor the strike of the calibration instrument,
                // see warning in the header
                solution[2] = Math.Max(solution[2], 0.00001 - shift); // floor at 0.1bp - shift

                // also the calibrated nominal may be zero, so we floor it, too; its size, as the match read it (G1R6
                // (xxiv))
                solution[0] = Math.Max(Math.Abs(solution[0]), 0.000001); // float at 0.01bp

                double vol = sec.Volatility(solution[2]);
                helper = new SwaptionHelper(
                    expiry,
                    matPeriod,
                    vol,
                    standardSwapBase.IborIndex,
                    standardSwapBase.FixedLegTenor,
                    standardSwapBase.DayCounter,
                    standardSwapBase.IborIndex.DayCounter,
                    helperCurve,
                    context,
                    BlackCalibrationHelperCalibrationErrorType.RelativePriceError,
                    solution[2],
                    Math.Abs(solution[0]),
                    swaptionVolatility.VolatilityType,
                    shift);
            }

            result.Add(helper);
        }

        return result.ToImmutable();
    }

    // The match's cost: a standard swap's value, delta and gamma in the model's state at the expiry, against the
    // underlying's, relative to its delta and gamma; the maturity interpolated between whole months.
    private sealed class MatchHelper(
        SwapType type,
        double npv,
        double delta,
        double gamma,
        Gaussian1dModel model,
        SwapIndex indexBase,
        Date expiry,
        double maxMaturity,
        double h,
        EvaluationContext context,
        Gaussian1dMemo memo) : CostFunction
    {
        public override double Value(Vector x)
        {
            Vector vals = Values(x);
            double res = 0.0;
            for (int i = 0; i < vals.Size; i++)
            {
                res += vals[i] * vals[i];
            }

            return Math.Sqrt(res / vals.Size);
        }

        public override Vector Values(Vector x)
        {
            // transformations
            int sign = (int)type; // start with same type as non standard
                                  // underlying (1 means payer, -1 receiver)
            double nominal = Math.Abs(x[0]);
            if (x[0] < 0.0)
            {
                sign *= -1;
            }

            double maturity = Math.Min(Math.Abs(x[1]), maxMaturity);

            double fixedRate = x[2]; // allow for negative rates explicitly
                                     // (though it might not be reasonable for calibration depending
                                     // on the model to calibrate and the market instrument quotation)
            int years = (int)Math.Floor(maturity);
            maturity -= years;
            maturity *= 12.0;
            int months = (int)Math.Floor(maturity);
            double alpha = 1.0 - (maturity - months);
            if (years == 0 && months == 0)
            {
                months = 1;  // ensure a maturity of at least one month ...
                alpha = 1.0; // ... but in this case only look at the lower
                             // maturity swap
            }

            // generate swap
            Period lowerPeriod = new Period(years, TimeUnit.Years) + new Period(months, TimeUnit.Months);
            Period upperPeriod = lowerPeriod + new Period(1, TimeUnit.Months);

            VanillaSwap swapLower = memo.UnderlyingSwap(indexBase, expiry, lowerPeriod, context);
            VanillaSwap swapUpper = memo.UnderlyingSwap(indexBase, expiry, upperPeriod, context);

            // compute npv, delta, gamma
            double npvm = (alpha * Npv(swapLower, fixedRate, nominal, -h, sign)) + ((1.0 - alpha) * Npv(swapUpper, fixedRate, nominal, -h, sign));
            double npv0 = (alpha * Npv(swapLower, fixedRate, nominal, 0.0, sign)) + ((1.0 - alpha) * Npv(swapUpper, fixedRate, nominal, 0.0, sign));
            double npvu = (alpha * Npv(swapLower, fixedRate, nominal, h, sign)) + ((1.0 - alpha) * Npv(swapUpper, fixedRate, nominal, h, sign));
            double delta0 = (npvu - npvm) / (2.0 * h);
            double gamma0 = (npvu - (2.0 * npv0) + npvm) / (h * h);

            // return target function values
            return new Vector([(npv0 - npv) / delta, (delta0 - delta) / delta, (gamma0 - gamma) / gamma]);
        }

        private double Npv(VanillaSwap swap, double fixedRate, double nominal, double y, int sign)
        {
            double value = 0.0;
            YieldTermStructure? discounting = indexBase.DiscountingTermStructure;
            foreach (CashFlow i in swap.FixedLeg)
            {
                var c = (FixedRateCoupon)i;
                value -= fixedRate * c.AccrualPeriod() * nominal * model.Zerobond(c.Date, expiry, y, discounting, memo);
            }

            foreach (CashFlow i in swap.FloatingLeg)
            {
                var c = (IborCoupon)i;
                value += model.ForwardRate(c.FixingDate(), context, expiry, y, c.IborIndex, memo) * c.AccrualPeriod() * nominal
                    * model.Zerobond(c.Date, expiry, y, discounting, memo);
            }

            return sign * value;
        }
    }
}
