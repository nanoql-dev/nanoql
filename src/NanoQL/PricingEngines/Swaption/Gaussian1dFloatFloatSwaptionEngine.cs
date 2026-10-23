// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/gaussian1dfloatfloatswaptionengine.hpp ql/pricingengines/swaption/gaussian1dfloatfloatswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Gaussian1d float-float swaption engine: the swaption's value rolled back on the model's state grid from its
/// last event, each coupon fixing and exercise date, to today, each leg's coupons estimated at their fixing (an Ibor
/// index's forward rate, a swap index's rate, a swap spread index's two rates geared), capped and floored.
/// </summary>
/// <remarks>
/// <para>
/// All float coupons with a fixing date on or after the respective option expiry are part of the exercise into right;
/// a redemption flow belongs to it when the start date of the regular coupon period paid with it is on or after the
/// exercise. Cash-settled (par yield curve) swaptions are not supported. The additional result
/// <c>underlyingValue</c> is the underlying's value from today, with every fixing after today, or on it too with
/// <c>includeTodaysExercise</c>; the probabilities, another, are QuantLib's.
/// </para>
/// <para>
/// Immutable, a <see cref="BasketGeneratingEngine"/> (the S-g1d plan's G1D5): <see cref="Calculate(FloatFloatSwaption,
/// EvaluationContext)"/> prices with the engine's model and parameters, and the static <c>Calculate</c> builds the
/// engine for one call. Four of QuantLib's defects are fixed. A rebate is valued in the state, on the discount curve,
/// as the nonstandard engine values it, where QuantLib's reads the model curve's bond at the mean state (G1R6 (v)); the
/// model reads a discount curve on another clock by date at every instant QuantLib derives from a date (an event's
/// numeraire, the Digital probabilities' discount; (xvi)); flat payoff extrapolation's right tail reads the last node
/// ((xxi)); and the calibration basket's initial guess refuses an expiry with no leg 1 coupon left or with leg 1's
/// remaining nominals summing to zero, as QuantLib's master does (#2845, G1R5 (iv)).
/// </para>
/// </remarks>
public sealed class Gaussian1dFloatFloatSwaptionEngine : BasketGeneratingEngine
{
    /// <summary>Initializes a new instance of the <see cref="Gaussian1dFloatFloatSwaptionEngine"/> class.</summary>
    /// <param name="model">The model.</param>
    /// <param name="integrationPoints">The state grid's points on each side.</param>
    /// <param name="stddevs">The state grid's width, in standard deviations.</param>
    /// <param name="extrapolatePayoff">Whether the payoff is extrapolated past the grid.</param>
    /// <param name="flatPayoffExtrapolation">Whether the extrapolation is flat, else the end segments' cubic.</param>
    /// <param name="oas">The option-adjusted spread, continuously compounded on the model curve's day counter;
    /// <see langword="null"/> for none, QuantLib's empty handle.</param>
    /// <param name="discountCurve">The discount curve; <see langword="null"/> for the model's.</param>
    /// <param name="includeTodaysExercise">Whether an exercise and the fixings on today's date count.</param>
    /// <param name="probabilities">The exercise probabilities to compute.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public Gaussian1dFloatFloatSwaptionEngine(
        Gaussian1dModel model,
        int integrationPoints = 64,
        double stddevs = 7.0,
        bool extrapolatePayoff = true,
        bool flatPayoffExtrapolation = false,
        double? oas = null,
        YieldTermStructure? discountCurve = null,
        bool includeTodaysExercise = false,
        Gaussian1dFloatFloatSwaptionEngineProbabilities probabilities = Gaussian1dFloatFloatSwaptionEngineProbabilities.None)
        : base(model, oas, discountCurve)
    {
        IntegrationPoints = integrationPoints;
        Stddevs = stddevs;
        ExtrapolatePayoff = extrapolatePayoff;
        FlatPayoffExtrapolation = flatPayoffExtrapolation;
        IncludeTodaysExercise = includeTodaysExercise;
        Probabilities = probabilities;
    }

    /// <summary>Gets the state grid's points on each side.</summary>
    public int IntegrationPoints { get; }

    /// <summary>Gets the state grid's width, in standard deviations.</summary>
    public double Stddevs { get; }

    /// <summary>Gets a value indicating whether the payoff is extrapolated past the grid.</summary>
    public bool ExtrapolatePayoff { get; }

    /// <summary>Gets a value indicating whether the extrapolation is flat.</summary>
    public bool FlatPayoffExtrapolation { get; }

    /// <summary>Gets a value indicating whether an exercise and the fixings on today's date count.</summary>
    public bool IncludeTodaysExercise { get; }

    /// <summary>Gets the exercise probabilities computed.</summary>
    public Gaussian1dFloatFloatSwaptionEngineProbabilities Probabilities { get; }

    /// <summary>Gets the curve the engine discounts on: the discount curve, or the model's.</summary>
    public YieldTermStructure DiscountingCurve => DiscountCurve ?? Model.TermStructure;

    /// <summary>Prices a float-float swaption with a new engine of the given model and parameters.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="integrationPoints">The state grid's points on each side.</param>
    /// <param name="stddevs">The state grid's width, in standard deviations.</param>
    /// <param name="extrapolatePayoff">Whether the payoff is extrapolated past the grid.</param>
    /// <param name="flatPayoffExtrapolation">Whether the extrapolation is flat.</param>
    /// <param name="oas">The option-adjusted spread; <see langword="null"/> for none.</param>
    /// <param name="discountCurve">The discount curve; <see langword="null"/> for the model's.</param>
    /// <param name="includeTodaysExercise">Whether an exercise and the fixings on today's date count.</param>
    /// <param name="probabilities">The exercise probabilities to compute.</param>
    /// <returns>The results: the NPV, the underlying's value, and the probabilities when asked for.</returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the model is null.</exception>
    /// <exception cref="NanoQLException">The swaption is cash settled on the par yield curve, its settlement is
    /// inconsistent, or an index is neither an Ibor, a swap nor a swap spread index.</exception>
    public static InstrumentResults Calculate(
        FloatFloatSwaption swaption,
        EvaluationContext context,
        Gaussian1dModel model,
        int integrationPoints = 64,
        double stddevs = 7.0,
        bool extrapolatePayoff = true,
        bool flatPayoffExtrapolation = false,
        double? oas = null,
        YieldTermStructure? discountCurve = null,
        bool includeTodaysExercise = false,
        Gaussian1dFloatFloatSwaptionEngineProbabilities probabilities = Gaussian1dFloatFloatSwaptionEngineProbabilities.None) =>
        new Gaussian1dFloatFloatSwaptionEngine(
            model, integrationPoints, stddevs, extrapolatePayoff, flatPayoffExtrapolation, oas, discountCurve, includeTodaysExercise, probabilities)
            .Calculate(swaption, context);

    /// <summary>Prices a float-float swaption.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The results: the NPV, the underlying's value, and the probabilities when asked for.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">The swaption is cash settled on the par yield curve, its settlement is
    /// inconsistent, or an index is neither an Ibor, a swap nor a swap spread index.</exception>
    public InstrumentResults Calculate(FloatFloatSwaption swaption, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);
        if (swaption.IsExpired(context))
        {
            return swaption.SetupExpired();
        }

        swaption.Validate();
        InstrumentResults results = Body(swaption, context);
        return swaption.FetchResults(results);
    }

    private protected override IBasketGeneratingUnderlying Underlying(Instrument instrument, EvaluationContext context, Gaussian1dMemo memo) =>
        instrument is FloatFloatSwaption swaption
            ? new BasketUnderlying(this, swaption, FloatFloatSwapArguments.Of(swaption.Underlying, context), context, memo)
            : throw new NanoQLException("argument types do not match");

    private double SpreadDiscount(Date from, Date to) =>
        Oas is double s ? Math.Exp(-s * Model.TermStructure.DayCounter.YearFraction(from, to)) : 1.0;

    // QuantLib's calculate().
    private InstrumentResults Body(FloatFloatSwaption swaption, EvaluationContext context)
    {
        if (!(swaption.SettlementMethod != SettlementMethod.ParYieldCurve))
        {
            throw new NanoQLException("cash settled (ParYieldCurve) swaptions not priced with Gaussian1dFloatFloatSwaptionEngine");
        }

        Date settlement = Model.TermStructure.ReferenceDate;

        if (swaption.Exercise.Dates[^1] <= settlement)
        {
            // swaption is expired, possibly generated swap is not valued
            return new InstrumentResults { NPV = 0.0 };
        }

        var memo = new Gaussian1dMemo();
        FloatFloatSwapArguments arguments = FloatFloatSwapArguments.Of(swaption.Underlying, context);
        (double value, double underlying, ImmutableArray<double>? probabilities) =
            Npvs(arguments, swaption.Exercise, settlement, 0.0, IncludeTodaysExercise, true, context, memo);

        var additionalResults = ImmutableSortedDictionary.CreateBuilder<string, object>();
        additionalResults["underlyingValue"] = underlying;
        if (probabilities is ImmutableArray<double> prob)
        {
            additionalResults["probabilities"] = prob;
        }

        return new InstrumentResults { NPV = value, AdditionalResults = additionalResults.ToImmutable() };
    }

    // calculate npv and underlying npv as of expiry date, and the probabilities when considered
    private (double Npv, double Underlying, ImmutableArray<double>? Probabilities) Npvs(
        FloatFloatSwapArguments arguments,
        Exercise exercise,
        Date expiry,
        double y,
        bool includeExerciseOnExpiry,
        bool considerProbabilities,
        EvaluationContext context,
        Gaussian1dMemo memo)
    {
        Gaussian1dModel model = Model;
        YieldTermStructure? discountCurve = DiscountCurve;
        ImmutableArray<Date> exerciseDates = exercise.Dates;
        FloatFloatSwapArguments.Leg leg1 = arguments.Leg1, leg2 = arguments.Leg2;
        bool probabilitiesOn = considerProbabilities && Probabilities != Gaussian1dFloatFloatSwaptionEngineProbabilities.None;
        var rebatedExercise = exercise as RebatedExercise;

        // pricing

        // event dates are coupon fixing dates and exercise dates
        // we explicitly estimate cms and also libor coupons (although
        // the latter could be calculated analytically) to make the code
        // simpler
        var events = new SortedSet<Date>([.. exerciseDates, .. leg1.FixingDates, .. leg2.FixingDates]).ToList();

        // only events on or after expiry are of interest by definition of the
        // deal part that is exericsed into.
        Date first = expiry - (includeExerciseOnExpiry ? 1 : 0);
        events.RemoveRange(0, events.FindIndex(e => e > first) is int f and >= 0 ? f : events.Count);

        int idx = events.Count - 1;

        OptionType type = arguments.Type == SwapType.Payer ? OptionType.Call : OptionType.Put;

        double[] npv0 = new double[(2 * IntegrationPoints) + 1], npv1 = new double[(2 * IntegrationPoints) + 1]; // the option's
        double[] npv0a = new double[(2 * IntegrationPoints) + 1], npv1a = new double[(2 * IntegrationPoints) + 1]; // the underlying's
        Vector z = model.YGrid(Stddevs, IntegrationPoints, 1.0, 0.0, 0.0, memo);
        double[] p = new double[z.Size], pa = new double[z.Size];

        // for probability computation
        // how many active exercise dates are there ?
        int noEx = exerciseDates.Length - Gaussian1dSwaptionEngine.UpperBound(exerciseDates, first);
        int exIdx = noEx; // current exercise index
        double[][] npvp0 = [], npvp1 = [];
        if (probabilitiesOn)
        {
            npvp0 = new double[noEx + 1][];
            npvp1 = new double[noEx + 1][];
            for (int i = 0; i < noEx + 1; ++i)
            {
                npvp0[i] = new double[(2 * IntegrationPoints) + 1];
                npvp1[i] = new double[(2 * IntegrationPoints) + 1];
            }
        }

        // end probability computation
        double? event1Time = null;
        double event0Time;
        Date event0;

        var ibor1 = arguments.Index1 as IborIndex;
        var cms1 = arguments.Index1 as SwapIndex;
        var cmsspread1 = arguments.Index1 as SwapSpreadIndex;
        var ibor2 = arguments.Index2 as IborIndex;
        var cms2 = arguments.Index2 as SwapIndex;
        var cmsspread2 = arguments.Index2 as SwapSpreadIndex;

        if (!(ibor1 is not null || cms1 is not null || cmsspread1 is not null))
        {
            throw new NanoQLException("index1 must be ibor or swap or swap spread index");
        }

        if (!(ibor2 is not null || cms2 is not null || cmsspread2 is not null))
        {
            throw new NanoQLException("index2 must be ibor or swap or swap spread index");
        }

        do
        {
            // we are at event0 date, which can be a structured coupon fixing
            // date or an exercise date or both.
            bool isEventDate = true;
            if (idx == -1)
            {
                event0 = expiry;
                isEventDate = false;
            }
            else
            {
                event0 = events[idx];
                if (event0 == expiry)
                {
                    idx = -1; // avoid double roll back if expiry equal to earliest event date
                }
            }

            bool isExercise = exerciseDates.Contains(event0);
            bool isLeg1Fixing = leg1.FixingDates.Contains(event0);
            bool isLeg2Fixing = leg2.FixingDates.Contains(event0);

            double t0 = model.TermStructure.TimeFromReference(event0);
            event0Time = t0 < 0.0 ? 0.0 : t0;

            for (int k = 0; k < (event0 > expiry ? npv0.Length : 1); k++)
            {
                // roll back
                double price = 0.0, pricea = 0.0;
                if (event1Time is double e1)
                {
                    double zSpreadDf = Oas is double s ? Math.Exp(-s * (e1 - event0Time)) : 1.0;
                    Vector yg = model.YGrid(Stddevs, IntegrationPoints, e1, event0Time, event0 > expiry ? z[k] : y, memo);
                    CubicInterpolation payoff0 = Gaussian1dSwaptionEngine.Spline(z, npv1);
                    CubicInterpolation payoff0a = Gaussian1dSwaptionEngine.Spline(z, npv1a);
                    for (int i = 0; i < yg.Size; i++)
                    {
                        p[i] = payoff0.Evaluate(yg[i], true);
                        pa[i] = payoff0a.Evaluate(yg[i], true);
                    }

                    CubicInterpolation payoff1 = Gaussian1dSwaptionEngine.Spline(z, p);
                    CubicInterpolation payoff1a = Gaussian1dSwaptionEngine.Spline(z, pa);

                    // the right tail at the last node's value (G1R6 (xxi))
                    price = Gaussian1dSwaptionEngine.Rollback(z, p, payoff1, ExtrapolatePayoff, FlatPayoffExtrapolation, type, zSpreadDf);
                    pricea = Gaussian1dSwaptionEngine.Rollback(z, pa, payoff1a, ExtrapolatePayoff, FlatPayoffExtrapolation, type, zSpreadDf);
                }

                npv0[k] = price;
                npv0a[k] = pricea;

                // for probability computation
                if (probabilitiesOn)
                {
                    for (int m = 0; m < npvp0.Length; m++)
                    {
                        double priceM = 0.0;
                        if (event1Time is double e1m)
                        {
                            double zSpreadDf = Oas is double s ? Math.Exp(-s * (e1m - event0Time)) : 1.0;
                            Vector yg = model.YGrid(Stddevs, IntegrationPoints, e1m, event0Time, event0 > expiry ? z[k] : 0.0, memo);
                            CubicInterpolation payoff0 = Gaussian1dSwaptionEngine.Spline(z, npvp1[m]);
                            for (int i = 0; i < yg.Size; i++)
                            {
                                p[i] = payoff0.Evaluate(yg[i], true);
                            }

                            CubicInterpolation payoff1 = Gaussian1dSwaptionEngine.Spline(z, p);
                            priceM = Gaussian1dSwaptionEngine.Rollback(z, p, payoff1, ExtrapolatePayoff, FlatPayoffExtrapolation, type, zSpreadDf);
                        }

                        npvp0[m][k] = priceM;
                    }
                }

                // end probability computation

                // event date calculations
                if (isEventDate)
                {
                    double zk = event0 > expiry ? z[k] : y;

                    // the numeraire read at the event's date (G1R6 (xvi))
                    if (isLeg1Fixing)
                    {
                        // if event is a fixing date and exercise date,
                        // the coupon is part of the exercise into right (by
                        // definition)
                        npv0a[k] = LegNpv(npv0a[k], -1.0, leg1, arguments.Nominal1, ibor1, cms1, cmsspread1, event0, event0Time, zk, context, memo);
                    }

                    if (isLeg2Fixing)
                    {
                        npv0a[k] = LegNpv(npv0a[k], 1.0, leg2, arguments.Nominal2, ibor2, cms2, cmsspread2, event0, event0Time, zk, context, memo);
                    }

                    if (isExercise)
                    {
                        int j = exerciseDates.IndexOf(event0);
                        double rebate = 0.0;
                        double zSpreadDf = 1.0;
                        Date rebateDate = event0;
                        if (rebatedExercise is not null)
                        {
                            rebate = rebatedExercise.Rebate(j);
                            rebateDate = rebatedExercise.RebatePaymentDate(j);
                            zSpreadDf = SpreadDiscount(event0, rebateDate);
                        }

                        // the rebate in the state, on the discount curve, where QuantLib's reads the model curve's
                        // bond at y = 0 (G1R6 (v))
                        double exerciseValue = ((type == OptionType.Call ? 1.0 : -1.0) * npv0a[k])
                            + (rebate * model.Zerobond(rebateDate, event0, zk, discountCurve, memo) * zSpreadDf
                                / model.NumeraireCore(event0Time, event0, zk, discountCurve, memo));

                        if (probabilitiesOn)
                        {
                            if (exIdx == noEx)
                            {
                                // if true we are at the latest date,
                                // so we init
                                // the no call probability
                                npvp0[^1][k] = Probabilities == Gaussian1dFloatFloatSwaptionEngineProbabilities.Naive
                                    ? 1.0
                                    : 1.0 / (model.ZerobondCore(event0Time, event0, 0.0, null, 0.0, discountCurve, memo)
                                        * model.Numeraire(event0, z[k], discountCurve, memo));
                            }

                            if (exerciseValue >= npv0[k])
                            {
                                npvp0[exIdx - 1][k] = Probabilities == Gaussian1dFloatFloatSwaptionEngineProbabilities.Naive
                                    ? 1.0
                                    : 1.0 / (model.ZerobondCore(event0Time, event0, 0.0, null, 0.0, discountCurve, memo)
                                        * model.NumeraireCore(event0Time, event0, z[k], discountCurve, memo));
                                for (int ii = exIdx; ii < noEx + 1; ++ii)
                                {
                                    npvp0[ii][k] = 0.0;
                                }
                            }
                        }

                        // end probability computation
                        npv0[k] = npv0[k] < exerciseValue ? exerciseValue : npv0[k];
                    }
                }
            }

            if (isExercise)
            {
                --exIdx;
            }

            (npv1, npv0) = (npv0, npv1);
            (npv1a, npv0a) = (npv0a, npv1a);

            // for probability computation
            if (probabilitiesOn)
            {
                for (int i = 0; i < npvp0.Length; ++i)
                {
                    (npvp1[i], npvp0[i]) = (npvp0[i], npvp1[i]);
                }
            }

            // end probability computation
            event1Time = event0Time;
        }
        while (--idx >= -1);

        // event1 is the expiry: its numeraire by its date
        double numeraire = model.NumeraireCore(event1Time.Value, expiry, y, discountCurve, memo);
        double npv = npv1[0] * numeraire;
        double underlying = npv1a[0] * numeraire * (type == OptionType.Call ? 1.0 : -1.0);

        // for probability computation
        if (probabilitiesOn)
        {
            var prob = new double[noEx + 1];
            for (int i = 0; i < noEx + 1; i++)
            {
                prob[i] = npvp1[i][0] * (Probabilities == Gaussian1dFloatFloatSwaptionEngineProbabilities.Naive
                    ? 1.0
                    : model.NumeraireCore(0.0, null, 0.0, discountCurve, memo));
            }

            return (npv, underlying, ImmutableArray.Create(prob));
        }

        // end probability computation
        return (npv, underlying, null);
    }

    // The underlying's value with a leg's coupons fixing on an event date added (sign 1) or taken off (sign -1), one at
    // a time, as QuantLib's: each estimated, capped and floored, times its nominal and accrual, or a redemption's
    // amount, by the bond to its payment over the numeraire at the event, discounted by the spread to the first one's
    // payment.
    private double LegNpv(
        double npv,
        double sign,
        FloatFloatSwapArguments.Leg leg,
        ImmutableArray<double> nominal,
        IborIndex? ibor,
        SwapIndex? cms,
        SwapSpreadIndex? cmsspread,
        Date event0,
        double event0Time,
        double zk,
        EvaluationContext context,
        Gaussian1dMemo memo)
    {
        Gaussian1dModel model = Model;
        YieldTermStructure? discountCurve = DiscountCurve;
        int j = leg.FixingDates.IndexOf(event0);
        double zSpreadDf = SpreadDiscount(event0, leg.PayDates[j]);
        bool done;
        do
        {
            double amount;
            if (leg.IsRedemptionFlow[j])
            {
                amount = leg.Coupons[j]!.Value;
            }
            else
            {
                double estFixing = 0.0;
                if (ibor is not null)
                {
                    estFixing = model.ForwardRate(leg.FixingDates[j], context, event0, zk, ibor, memo);
                }

                if (cms is not null)
                {
                    estFixing = model.SwapRate(leg.FixingDates[j], cms.Tenor, context, event0, zk, cms, memo);
                }

                if (cmsspread is not null)
                {
                    estFixing = (cmsspread.Gearing1
                            * model.SwapRate(leg.FixingDates[j], cmsspread.SwapIndex1.Tenor, context, event0, zk, cmsspread.SwapIndex1, memo))
                        + (cmsspread.Gearing2
                            * model.SwapRate(leg.FixingDates[j], cmsspread.SwapIndex2.Tenor, context, event0, zk, cmsspread.SwapIndex2, memo));
                }

                double rate = leg.Spreads[j] + (leg.Gearings[j] * estFixing);
                if (leg.CappedRates[j] is double capped)
                {
                    rate = Math.Min(capped, rate);
                }

                if (leg.FlooredRates[j] is double floored)
                {
                    rate = Math.Max(floored, rate);
                }

                amount = rate * nominal[j] * leg.AccrualTimes[j];
            }

            double term = amount * model.Zerobond(leg.PayDates[j], event0, zk, discountCurve, memo)
                / model.NumeraireCore(event0Time, event0, zk, discountCurve, memo) * zSpreadDf;
            npv = sign > 0.0 ? npv + term : npv - term;

            if (j < leg.FixingDates.Length - 1)
            {
                j++;
                done = event0 != leg.FixingDates[j];
            }
            else
            {
                done = true;
            }
        }
        while (!done);

        return npv;
    }

    // QuantLib's underlyingNpv, underlyingType, underlyingLastDate and initialGuess on a swaption's arguments.
    private sealed class BasketUnderlying(
        Gaussian1dFloatFloatSwaptionEngine engine,
        FloatFloatSwaption swaption,
        FloatFloatSwapArguments arguments,
        EvaluationContext context,
        Gaussian1dMemo memo) : IBasketGeneratingUnderlying
    {
        public SwapType Type => swaption.Underlying.Type;

        public Date LastDate
        {
            get
            {
                Date l1 = arguments.Leg1.PayDates[^1];
                Date l2 = arguments.Leg2.PayDates[^1];
                return l2 >= l1 ? l2 : l1;
            }
        }

        public double Npv(Date expiry, double y) => engine.Npvs(arguments, swaption.Exercise, expiry, y, true, false, context, memo).Underlying;

        public Vector InitialGuess(Date expiry)
        {
            ImmutableArray<Date> resetDates = arguments.Leg1.ResetDates;
            int idx1 = Gaussian1dSwaptionEngine.UpperBound(resetDates, expiry - 1);

            // QuantLib's master's refusals (#2845)
            int remainingResetCount = resetDates.Length - idx1;
            if (!(remainingResetCount > 0))
            {
                throw new NanoQLException($"no leg 1 reset dates remain at calibration expiry {expiry}");
            }

            // very simple initial guess
            // check guess for nominal and weighted maturity !
            double nominalSum1 = 0.0;
            for (int i = idx1; i < resetDates.Length; i++)
            {
                nominalSum1 += arguments.Nominal1[i];
            }

            if (!(nominalSum1 != 0.0))
            {
                throw new NanoQLException($"remaining leg 1 notionals sum to zero at calibration expiry {expiry}");
            }

            double nominalAvg1 = nominalSum1 / remainingResetCount;
            double weightedMaturity1 = 0.0;
            for (int i = idx1; i < resetDates.Length; i++)
            {
                weightedMaturity1 += arguments.Leg1.AccrualTimes[i] * arguments.Nominal1[i];
            }

            weightedMaturity1 /= nominalAvg1;

            return new Vector([nominalAvg1, weightedMaturity1, 0.03]); // ???
        }
    }
}
