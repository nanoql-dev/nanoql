// SPDX-FileCopyrightText: Copyright (C) 2011, 2012 Andre Miemiec
// SPDX-FileCopyrightText: Copyright (C) 2012 Samuel Tebege
// SPDX-FileCopyrightText: Copyright (C) 2011, 2012, 2013, 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/swaptions/haganirregularswaptionengine.hpp ql/experimental/swaptions/haganirregularswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: HaganIrregularSwaptionEngine::LGMPrice and rStarFinder, never ported: haganirregularswaptionengine.hpp declares them and nothing defines them

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The engine for European irregular swaptions by super-replication with a basket of vanilla swaptions: QuantLib's
/// <c>HaganIrregularSwaptionEngine</c>.
/// </summary>
/// <remarks>
/// <para>
/// References: P.S. Hagan, "Methodology for Callable Swaps and Bermudan 'Exercise into Swaptions'"; P.J. Hunt and J.E.
/// Kennedy, "Implied interest rate pricing models", Finance Stochast. 2, 275-293 (1998).
/// </para>
/// <para>
/// A static class in the engines' form. The floating leg's spread moves to the fixed leg first, the fixed rates lowered
/// by the rate whose fixed-leg value is the spread's; the swap then splits into vanilla swaps ending at each fixed
/// payment date, whose weights solve a linear system (by <see cref="SVD"/>) and whose rates are shifted by a λ that
/// <see cref="Bisection"/> finds in [−0.5, 0.5] to 1e-8 so that the basket's value matches the swap's; the price is the
/// sum of the weighted vanilla swaptions, each by <see cref="BachelierSwaptionEngine"/> on a normal volatility. The
/// basket is a working object per call. As QuantLib warns, a spread isn't handled exactly, and the swaptions assume
/// that the exercise date is the swap's start. On a regular swap the replication is close to the Bachelier engine's
/// price, not equal to it: within 5.1e-7 relative on a five-year swap a year forward at a flat normal 1%, QuantLib's
/// values to 12 digits, and 7.5e-5 out of the money on a four-year swap at 0.8%.
/// </para>
/// <para>
/// QuantLib's engine takes the discount curve as an optional handle and dereferences it, so an empty one throws; here it
/// is required.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class HaganIrregularSwaptionEngine
{
    /// <summary>Prices a European irregular swaption.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="volatilityStructure">The swaption volatility, normal.</param>
    /// <param name="termStructure">The discount curve.</param>
    /// <returns>The swaption's value: the expired results when the swaption has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">
    /// A fixed cash flow isn't a fixed-rate coupon or a floating one an Ibor coupon, the swaption isn't European, the
    /// volatility isn't normal, λ can't be bracketed, or a leg can't be priced.
    /// </exception>
    public static InstrumentResults Calculate(
        IrregularSwaption swaption,
        EvaluationContext context,
        SwaptionVolatilityStructure volatilityStructure,
        YieldTermStructure termStructure)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(volatilityStructure);
        ArgumentNullException.ThrowIfNull(termStructure);
        if (swaption.IsExpired(context))
        {
            return swaption.SetupExpired();
        }

        // QuantLib's setupArguments reads every fixed cash flow as a fixed-rate coupon and every floating one as an
        // Ibor coupon, dereferencing the failed casts; the engine's own texts here
        CheckCoupons(swaption.Underlying);
        swaption.Validate();

        // check exercise type
        Exercise exercise = swaption.Exercise;
        if (!(exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("swaption must be european");
        }

        // extract the underlying irregular swap
        IrregularSwap swap = swaption.Underlying;

        // Reshuffle spread from float to fixed (, i.e. remove spread from float side by finding the
        // adjusted fixed coupon such that the NPV of the swap stays constant).
        ImmutableArray<CashFlow> fixedLeg = swap.FixedLeg;
        double fxdLgBPS = CashFlows.Bps(fixedLeg, termStructure, context, true);
        ImmutableArray<CashFlow> floatLeg = swap.FloatingLeg;
        double fltLgNPV = CashFlows.Npv(floatLeg, termStructure, context, true);
        double fltLgBPS = CashFlows.Bps(floatLeg, termStructure, context, true);

        var floatBuilder = ImmutableArray.CreateBuilder<CashFlow>(floatLeg.Length);
        foreach (CashFlow j in floatLeg)
        {
            // retrieve ibor coupon from floating leg
            var coupon = (IborCoupon)j;
            floatBuilder.Add(new IborCoupon(
                coupon.Date,
                coupon.Nominal(context),
                coupon.AccrualStartDate,
                coupon.AccrualEndDate,
                coupon.FixingDays,
                coupon.IborIndex,
                coupon.Gearing,
                0.0,
                coupon.ReferencePeriodStart,
                coupon.ReferencePeriodEnd,
                coupon.DayCounter,
                coupon.IsInArrears,
                pricer: coupon.IsInArrears ? null : new BlackIborCouponPricer(context)));
        }

        ImmutableArray<CashFlow> floatCFS = floatBuilder.MoveToImmutable();
        double sprdLgNPV = fltLgNPV - CashFlows.Npv(floatCFS, termStructure, context, true);
        double avgSpread = sprdLgNPV / fltLgBPS / 10000;
        double cpnAdjustment = avgSpread * fltLgBPS / fxdLgBPS;

        var fixedCFS = ImmutableArray.CreateBuilder<CashFlow>(fixedLeg.Length);
        foreach (CashFlow i in fixedLeg)
        {
            // retrieve fixed rate coupon from fixed leg
            var coupon = (FixedRateCoupon)i;
            fixedCFS.Add(new FixedRateCoupon(
                coupon.Date,
                coupon.Nominal(context),
                coupon.InterestRate.Rate - cpnAdjustment,
                coupon.DayCounter,
                coupon.AccrualStartDate,
                coupon.AccrualEndDate,
                coupon.ReferencePeriodStart,
                coupon.ReferencePeriodEnd));
        }

        // this is the irregular swap with spread removed
        var stripped = new IrregularSwap(swap.Type, fixedCFS.MoveToImmutable(), floatCFS);

        // Sets up the basket by implementing the methodology described in
        // P.S.Hagan "Callable Swaps and Bermudan 'Exercise into Swaptions'"
        var basket = new Basket(stripped, termStructure, context);

        // find lambda
        double minLambda = -0.5;
        double maxLambda = 0.5;
        Bisection s1d = new Bisection().SetMaxEvaluations(10000).SetLowerBound(minLambda).SetUpperBound(maxLambda);
        s1d.Solve(new BasketDefect(basket), 1.0e-8, 0.01, minLambda, maxLambda);

        // compute the price of the irreg swaption as the sum of the prices of the regular swaptions
        return swaption.FetchResults(
            new InstrumentResults { NPV = HKPrice(basket, exercise, context, volatilityStructure, termStructure) });
    }

    // The irregular swaption's price after Hunt and Kennedy: the basket's weighted swaptions, each by the Bachelier
    // engine, which assumes that the swaption's exercise date is the swap's start.
    private static double HKPrice(
        Basket basket,
        Exercise exercise,
        EvaluationContext context,
        SwaptionVolatilityStructure volatilityStructure,
        YieldTermStructure termStructure)
    {
        // Swaption Engine: assumes that the swaptions exercise date equals the swap start date
        if (!(volatilityStructure.VolatilityType == VolatilityType.Normal))
        {
            throw new NanoQLException("swaptionEngine: only normal volatility implemented.");
        }

        // retrieve weights of underlying swaps
        Vector weights = basket.Weights();
        double npv = 0.0;
        for (int i = 0; i < weights.Size; ++i)
        {
            VanillaSwap pvSwap = basket.Component(i);
            var swaption = new Swaption(pvSwap, exercise);
            InstrumentResults results =
                BachelierSwaptionEngine.Calculate(swaption, context, termStructure, volatilityStructure);
            npv += weights[i] * results.NPV!.Value;
        }

        return npv;
    }

    private static void CheckCoupons(IrregularSwap swap)
    {
        foreach (CashFlow c in swap.FixedLeg)
        {
            if (c is not FixedRateCoupon)
            {
                throw new NanoQLException("dynamic cast of fixed leg coupon failed.");
            }
        }

        foreach (CashFlow c in swap.FloatingLeg)
        {
            if (c is not IborCoupon)
            {
                throw new NanoQLException("dynamic cast of float leg coupon failed.");
            }
        }
    }

    // The bisection's function: the basket's defect, each evaluation setting the basket's lambda.
    private readonly struct BasketDefect(Basket basket) : ISolver1DFunction
    {
        public double Evaluate(double x) => basket.Evaluate(x);
    }

    // QuantLib's Basket: the swap's replication by vanilla swaps ending at each fixed payment date. Mutable for its call
    // only: the bisection's evaluations set its λ, the last one at the root, which its weights then read.
    private sealed class Basket
    {
        private readonly IrregularSwap swap;
        private readonly YieldTermStructure termStructure;
        private readonly EvaluationContext context;
        private readonly double targetNPV;
        private readonly double[] fairRates;
        private readonly double[] annuities;
        private readonly Date[] expiries;
        private double lambda;

        public Basket(IrregularSwap swap, YieldTermStructure termStructure, EvaluationContext context)
        {
            this.swap = swap;
            this.termStructure = termStructure;
            this.context = context;

            // store swap npv
            targetNPV = DiscountingSwapEngine.Calculate(swap, context, termStructure).NPV!.Value;

            // build standard swaps
            ImmutableArray<CashFlow> fixedLeg = swap.FixedLeg;
            ImmutableArray<CashFlow> floatLeg = swap.FloatingLeg;
            int n = fixedLeg.Length;
            fairRates = new double[n];
            annuities = new double[n];
            expiries = new Date[n];
            var fixedCFS = ImmutableArray.CreateBuilder<CashFlow>(n);
            for (int i = 0; i < n; ++i)
            {
                // retrieve fixed rate coupon from fixed leg
                if (fixedLeg[i] is not FixedRateCoupon coupon)
                {
                    throw new NanoQLException("dynamic cast of fixed leg coupon failed.");
                }

                expiries[i] = coupon.Date;
                fixedCFS.Add(new FixedRateCoupon(
                    coupon.Date,
                    1.0,
                    coupon.InterestRate.Rate,
                    coupon.DayCounter,
                    coupon.AccrualStartDate,
                    coupon.AccrualEndDate,
                    coupon.ReferencePeriodStart,
                    coupon.ReferencePeriodEnd));
                annuities[i] = 10000 * CashFlows.Bps(fixedCFS.ToImmutable(), termStructure, context, true);

                var floatCFS = ImmutableArray.CreateBuilder<CashFlow>();
                foreach (CashFlow j in floatLeg)
                {
                    // retrieve ibor coupon from floating leg
                    if (j is not IborCoupon floating)
                    {
                        throw new NanoQLException("dynamic cast of float leg coupon failed.");
                    }

                    if (floating.Date <= expiries[i])
                    {
                        floatCFS.Add(new IborCoupon(
                            floating.Date,
                            1.0,
                            floating.AccrualStartDate,
                            floating.AccrualEndDate,
                            floating.FixingDays,
                            floating.IborIndex,
                            1.0,
                            floating.Spread,
                            floating.ReferencePeriodStart,
                            floating.ReferencePeriodEnd,
                            floating.DayCounter,
                            floating.IsInArrears,
                            pricer: floating.IsInArrears ? null : new BlackIborCouponPricer(context)));
                    }
                }

                double floatLegNPV = CashFlows.Npv(floatCFS.ToImmutable(), termStructure, context, true);
                fairRates[i] = floatLegNPV / annuities[i];
            }
        }

        // The weights at the λ the last evaluation set.
        public Vector Weights() => Compute(lambda);

        // The basket's defect at λ: the swap's value less the shifted basket's.
        public double Evaluate(double x)
        {
            Vector weights = Compute(x);
            double defect = -targetNPV;
            for (int i = 0; i < weights.Size; ++i)
            {
                defect -= (int)swap.Type * x * weights[i] * annuities[i];
            }

            return defect;
        }

        // creates a standard swap by deducing its conventions from market data objects
        public VanillaSwap Component(int i)
        {
            if (swap.FloatingLeg.IsEmpty || swap.FloatingLeg[0] is not IborCoupon iborCpn)
            {
                throw new NanoQLException("dynamic cast of float leg coupon failed. Can't find index.");
            }

            IborIndex iborIndex = iborCpn.IborIndex;
            var dummySwapLength = new Period(1, TimeUnit.Years);
            VanillaSwap memberSwap = new MakeVanillaSwap(dummySwapLength, iborIndex)
                .WithType(swap.Type)
                .WithEffectiveDate(swap.StartDate())
                .WithTerminationDate(expiries[i])
                .WithRule(DateGenerationRule.Backward)
                .WithDiscountingTermStructure(termStructure)
                .ToVanillaSwap(context);
            double stdAnnuity = 10000 * CashFlows.Bps(memberSwap.FixedLeg, termStructure, context, true);

            // compute annuity transformed rate
            double transformedRate = (fairRates[i] + lambda) * annuities[i] / stdAnnuity;
            return new MakeVanillaSwap(dummySwapLength, iborIndex)
                .WithFixedRate(transformedRate)
                .WithType(swap.Type)
                .WithEffectiveDate(swap.StartDate())
                .WithTerminationDate(expiries[i])
                .WithRule(DateGenerationRule.Backward)
                .WithDiscountingTermStructure(termStructure)
                .ToVanillaSwap(context);
        }

        // computes a replication of the swap in terms of a basket of vanilla swaps by solving a linear system of
        // equation
        private Vector Compute(double lambda)
        {
            // update members
            this.lambda = lambda;
            ImmutableArray<CashFlow> fixedLeg = swap.FixedLeg;
            int n = fixedLeg.Length;

            // build linear system of equations
            var arr = new Matrix(n, n, 0.0);
            var rhs = new Vector(n);

            // fill the matrix describing the linear system of equations by looping over rows
            for (int r = 0; r < n; ++r)
            {
                if (fixedLeg[r] is not FixedRateCoupon cpnR)
                {
                    throw new NanoQLException("Cast to fixed rate coupon failed.");
                }

                // looping over columns
                for (int c = r; c < n; ++c)
                {
                    // set homogenous part of lse
                    arr[r, c] = (fairRates[c] + lambda) * cpnR.AccrualPeriod();
                }

                // add nominal repayment for i-th swap
                arr[r, r] += 1;
            }

            for (int r = 0; r < n; ++r)
            {
                var cpnR = (FixedRateCoupon)fixedLeg[r];

                // set inhomogenity of lse
                double nR = cpnR.Nominal(context);
                if (r < n - 1)
                {
                    var cpnRp1 = (FixedRateCoupon)fixedLeg[r + 1];
                    double nRp1 = cpnRp1.Nominal(context);
                    rhs[r] = (nR * cpnR.InterestRate.Rate * cpnR.AccrualPeriod()) + (nR - nRp1);
                }
                else
                {
                    rhs[r] = (nR * cpnR.InterestRate.Rate * cpnR.AccrualPeriod()) + nR;
                }
            }

            var svd = new SVD(arr);
            return svd.SolveFor(rhs);
        }
    }
}
