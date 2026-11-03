// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/gaussian1dnonstandardswaptionengine.hpp ql/pricingengines/swaption/gaussian1dnonstandardswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.PricingEngines;

/// <summary>
/// The one-factor model nonstandard swaption engine: a Bermudan or European exercise into a nonstandard swap, rolled
/// back on the model's state grid.
/// </summary>
/// <remarks>
/// <para>
/// All fixed coupons with a start date greater than or equal to the respective option expiry are considered part of
/// the exercise into right, and so are all floating coupons; a redemption flow takes the start date of the regular
/// coupon period with the same payment date. Cash-settled (par yield curve) swaptions are not supported. A compounded
/// overnight coupon reads the model's compounded rate. The option-adjusted spread, continuously compounded on the model
/// curve's day counter, discounts each flow from the exercise; the probabilities, an additional result, are QuantLib's.
/// </para>
/// <para>
/// Immutable, a <see cref="BasketGeneratingEngine"/>: <see cref="Calculate(NonstandardSwaption, EvaluationContext)"/>
/// prices with the engine's model and parameters, and the static <c>Calculate</c> builds the engine for one call. The
/// model reads a discount curve on another clock by date at every instant QuantLib derives from a date (an exercise's
/// numeraire and the Digital probabilities' discount); flat payoff extrapolation's right tail reads the last node; and
/// the calibration basket's initial guess refuses fixed nominals that are positive but none above 1e-8, over whose
/// count QuantLib's average divides (#2845's class).
/// </para>
/// </remarks>
public sealed class Gaussian1dNonstandardSwaptionEngine : BasketGeneratingEngine
{
    /// <summary>Initializes a new instance of the <see cref="Gaussian1dNonstandardSwaptionEngine"/> class.</summary>
    /// <param name="model">The model.</param>
    /// <param name="integrationPoints">The state grid's points on each side.</param>
    /// <param name="stddevs">The state grid's width, in standard deviations.</param>
    /// <param name="extrapolatePayoff">Whether the payoff is extrapolated past the grid.</param>
    /// <param name="flatPayoffExtrapolation">Whether the extrapolation is flat, else the end segments' cubic.</param>
    /// <param name="oas">The option-adjusted spread, continuously compounded on the model curve's day counter;
    /// <see langword="null"/> for none, QuantLib's empty handle.</param>
    /// <param name="discountCurve">The discount curve; <see langword="null"/> for the model's.</param>
    /// <param name="probabilities">The exercise probabilities to compute.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public Gaussian1dNonstandardSwaptionEngine(
        Gaussian1dModel model,
        int integrationPoints = 64,
        double stddevs = 7.0,
        bool extrapolatePayoff = true,
        bool flatPayoffExtrapolation = false,
        double? oas = null,
        YieldTermStructure? discountCurve = null,
        Gaussian1dNonstandardSwaptionEngineProbabilities probabilities = Gaussian1dNonstandardSwaptionEngineProbabilities.None)
        : base(model, oas, discountCurve)
    {
        IntegrationPoints = integrationPoints;
        Stddevs = stddevs;
        ExtrapolatePayoff = extrapolatePayoff;
        FlatPayoffExtrapolation = flatPayoffExtrapolation;
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

    /// <summary>Gets the exercise probabilities computed.</summary>
    public Gaussian1dNonstandardSwaptionEngineProbabilities Probabilities { get; }

    /// <summary>Prices a nonstandard swaption with a new engine of the given model and parameters.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="integrationPoints">The state grid's points on each side.</param>
    /// <param name="stddevs">The state grid's width, in standard deviations.</param>
    /// <param name="extrapolatePayoff">Whether the payoff is extrapolated past the grid.</param>
    /// <param name="flatPayoffExtrapolation">Whether the extrapolation is flat.</param>
    /// <param name="oas">The option-adjusted spread; <see langword="null"/> for none.</param>
    /// <param name="discountCurve">The discount curve; <see langword="null"/> for the model's.</param>
    /// <param name="probabilities">The exercise probabilities to compute.</param>
    /// <returns>The results: the NPV, and the probabilities when asked for.</returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the model is null.</exception>
    /// <exception cref="NanoQLException">The swaption is cash settled on the par yield curve, or its settlement is
    /// inconsistent.</exception>
    public static InstrumentResults Calculate(
        NonstandardSwaption swaption,
        EvaluationContext context,
        Gaussian1dModel model,
        int integrationPoints = 64,
        double stddevs = 7.0,
        bool extrapolatePayoff = true,
        bool flatPayoffExtrapolation = false,
        double? oas = null,
        YieldTermStructure? discountCurve = null,
        Gaussian1dNonstandardSwaptionEngineProbabilities probabilities = Gaussian1dNonstandardSwaptionEngineProbabilities.None) =>
        new Gaussian1dNonstandardSwaptionEngine(
            model, integrationPoints, stddevs, extrapolatePayoff, flatPayoffExtrapolation, oas, discountCurve, probabilities).Calculate(swaption, context);

    /// <summary>Prices a nonstandard swaption.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The results: the NPV, and the probabilities when asked for.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">The swaption is cash settled on the par yield curve, or its settlement is
    /// inconsistent.</exception>
    public InstrumentResults Calculate(NonstandardSwaption swaption, EvaluationContext context)
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
        instrument is NonstandardSwaption swaption
            ? new BasketUnderlying(this, swaption, NonstandardSwapArguments.Of(swaption.Underlying, context), context, memo)
            : throw new NanoQLException("argument types do not match");

    // The first coupon of a leg whose reset date is on or after a date: std::upper_bound(.., d - 1).
    private static int FirstAlive(ImmutableArray<Date> resetDates, Date d) => Gaussian1dSwaptionEngine.UpperBound(resetDates, d - 1);

    private double SpreadDiscount(Date from, Date to) =>
        Oas is double s ? Math.Exp(-s * Model.TermStructure.DayCounter.YearFraction(from, to)) : 1.0;

    // QuantLib's calculate(): its checks in order, the rollback from the last exercise to today, and the probabilities.
    private InstrumentResults Body(NonstandardSwaption swaption, EvaluationContext context)
    {
        if (!(swaption.SettlementMethod != SettlementMethod.ParYieldCurve))
        {
            throw new NanoQLException("cash settled (ParYieldCurve) swaptions not priced with Gaussian1dNonstandardSwaptionEngine");
        }

        Gaussian1dModel model = Model;
        YieldTermStructure? discountCurve = DiscountCurve;
        Date settlement = model.TermStructure.ReferenceDate;
        ImmutableArray<Date> exerciseDates = swaption.Exercise.Dates;

        if (exerciseDates[^1] <= settlement)
        {
            // swaption is expired, possibly generated swap is not valued
            return new InstrumentResults { NPV = 0.0 };
        }

        var rebatedExercise = swaption.Exercise as RebatedExercise;
        int idx = exerciseDates.Length - 1;
        int minIdxAlive = Gaussian1dSwaptionEngine.UpperBound(exerciseDates, settlement);

        NonstandardSwap swap = swaption.Underlying;
        NonstandardSwapArguments arguments = NonstandardSwapArguments.Of(swap, context);
        ImmutableArray<CashFlow> floatingLeg = swap.FloatingLeg;
        var onCoupons = new OvernightIndexedCoupon?[floatingLeg.Length];
        YieldTermStructure? forwardingCurve = swap.IborIndex.ForwardingTermStructure;
        for (int i = 0; i < floatingLeg.Length; ++i)
        {
            onCoupons[i] = floatingLeg[i] as OvernightIndexedCoupon;
        }

        OptionType type = arguments.Type == SwapType.Payer ? OptionType.Call : OptionType.Put;

        var memo = new Gaussian1dMemo();
        double[] npv0 = new double[(2 * IntegrationPoints) + 1];
        double[] npv1 = new double[(2 * IntegrationPoints) + 1];
        Vector z = model.YGrid(Stddevs, IntegrationPoints, 1.0, 0.0, 0.0, memo);
        double[] p = new double[z.Size];

        // for probability computation
        double[][] npvp0 = [], npvp1 = [];
        if (Probabilities != Gaussian1dNonstandardSwaptionEngineProbabilities.None)
        {
            npvp0 = new double[idx - minIdxAlive + 2][];
            npvp1 = new double[idx - minIdxAlive + 2][];
            for (int i = 0; i < idx - minIdxAlive + 2; ++i)
            {
                npvp0[i] = new double[(2 * IntegrationPoints) + 1];
                npvp1[i] = new double[(2 * IntegrationPoints) + 1];
            }
        }

        // end probability computation
        double? expiry1Time = null;
        double expiry0Time;
        Date expiry0;

        do
        {
            expiry0 = idx == minIdxAlive - 1 ? settlement : exerciseDates[idx];

            double t0 = model.TermStructure.TimeFromReference(expiry0);
            expiry0Time = t0 < 0.0 ? 0.0 : t0;

            int j1 = FirstAlive(arguments.FixedResetDates, expiry0);
            int k1 = FirstAlive(arguments.FloatingResetDates, expiry0);

            for (int k = 0; k < (expiry0 > settlement ? npv0.Length : 1); k++)
            {
                double price = 0.0;
                if (expiry1Time is double e1)
                {
                    double zSpreadDf = Oas is double s ? Math.Exp(-s * (e1 - expiry0Time)) : 1.0;
                    Vector yg = model.YGrid(Stddevs, IntegrationPoints, e1, expiry0Time, expiry0 > settlement ? z[k] : 0.0, memo);
                    CubicInterpolation payoff0 = Gaussian1dSwaptionEngine.Spline(z, npv1);
                    for (int i = 0; i < yg.Size; i++)
                    {
                        p[i] = payoff0.Evaluate(yg[i], true);
                    }

                    CubicInterpolation payoff1 = Gaussian1dSwaptionEngine.Spline(z, p);
                    price = Gaussian1dSwaptionEngine.Rollback(z, p, payoff1, ExtrapolatePayoff, FlatPayoffExtrapolation, type, zSpreadDf);
                }

                npv0[k] = price;

                // for probability computation
                if (Probabilities != Gaussian1dNonstandardSwaptionEngineProbabilities.None)
                {
                    for (int m = 0; m < npvp0.Length; m++)
                    {
                        double priceM = 0.0;
                        if (expiry1Time is double e1m)
                        {
                            double zSpreadDf = Oas is double s ? Math.Exp(-s * (e1m - expiry0Time)) : 1.0;
                            Vector yg = model.YGrid(Stddevs, IntegrationPoints, e1m, expiry0Time, expiry0 > settlement ? z[k] : 0.0, memo);
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
                if (expiry0 > settlement)
                {
                    double floatingLegNpv = 0.0;
                    for (int l = k1; l < arguments.FloatingCoupons.Length; l++)
                    {
                        double zSpreadDf = SpreadDiscount(expiry0, arguments.FloatingPayDates[l]);
                        double amount;
                        if (arguments.FloatingIsRedemptionFlow[l])
                        {
                            amount = arguments.FloatingCoupons[l]!.Value;
                        }
                        else
                        {
                            double floatingRate = onCoupons[l] is OvernightIndexedCoupon overnight
                                ? model.CompoundedRate(overnight, expiry0, z[k], forwardingCurve, memo)
                                : model.ForwardRate(arguments.FloatingFixingDates[l], context, expiry0, z[k], swap.IborIndex, memo);
                            amount = arguments.FloatingNominal[l] * arguments.FloatingAccrualTimes[l]
                                * ((arguments.FloatingGearings[l] * floatingRate) + arguments.FloatingSpreads[l]);
                        }

                        floatingLegNpv += amount * model.Zerobond(arguments.FloatingPayDates[l], expiry0, z[k], discountCurve, memo) * zSpreadDf;
                    }

                    double fixedLegNpv = 0.0;
                    for (int l = j1; l < arguments.FixedCoupons.Length; l++)
                    {
                        double zSpreadDf = SpreadDiscount(expiry0, arguments.FixedPayDates[l]);
                        fixedLegNpv += arguments.FixedCoupons[l] * model.Zerobond(arguments.FixedPayDates[l], expiry0, z[k], discountCurve, memo) * zSpreadDf;
                    }

                    double rebate = 0.0;
                    double rebateSpreadDf = 1.0;
                    Date rebateDate = expiry0;
                    if (rebatedExercise is not null)
                    {
                        rebate = rebatedExercise.Rebate(idx);
                        rebateDate = rebatedExercise.RebatePaymentDate(idx);
                        rebateSpreadDf = SpreadDiscount(expiry0, rebateDate);
                    }

                    // the numeraire read at the exercise's date
                    double exerciseValue = (((type == OptionType.Call ? 1.0 : -1.0) * (floatingLegNpv - fixedLegNpv))
                            + (rebate * model.Zerobond(rebateDate, expiry0, z[k], discountCurve, memo) * rebateSpreadDf))
                        / model.NumeraireCore(expiry0Time, expiry0, z[k], discountCurve, memo);

                    // for probability computation
                    if (Probabilities != Gaussian1dNonstandardSwaptionEngineProbabilities.None)
                    {
                        if (idx == exerciseDates.Length - 1)
                        {
                            // if true we are at the latest date, so we init the no call probability
                            npvp0[^1][k] = Probabilities == Gaussian1dNonstandardSwaptionEngineProbabilities.Naive
                                ? 1.0
                                : 1.0 / (model.ZerobondCore(expiry0Time, expiry0, 0.0, null, 0.0, discountCurve, memo) * model.Numeraire(expiry0, z[k], discountCurve, memo));
                        }

                        if (exerciseValue >= npv0[k])
                        {
                            npvp0[idx - minIdxAlive][k] = Probabilities == Gaussian1dNonstandardSwaptionEngineProbabilities.Naive
                                ? 1.0
                                : 1.0 / (model.ZerobondCore(expiry0Time, expiry0, 0.0, null, 0.0, discountCurve, memo)
                                    * model.NumeraireCore(expiry0Time, expiry0, z[k], discountCurve, memo));
                            for (int ii = idx - minIdxAlive + 1; ii < npvp0.Length; ii++)
                            {
                                npvp0[ii][k] = 0.0;
                            }
                        }
                    }

                    // end probability computation
                    npv0[k] = npv0[k] < exerciseValue ? exerciseValue : npv0[k];
                }
            }

            (npv1, npv0) = (npv0, npv1);

            // for probability computation
            if (Probabilities != Gaussian1dNonstandardSwaptionEngineProbabilities.None)
            {
                for (int i = 0; i < npvp0.Length; i++)
                {
                    (npvp1[i], npvp0[i]) = (npvp0[i], npvp1[i]);
                }
            }

            // end probability computation
            expiry1Time = expiry0Time;
        }
        while (--idx >= minIdxAlive - 1);

        double value = npv1[0] * model.NumeraireCore(0.0, null, 0.0, discountCurve, memo);

        // for probability computation
        if (Probabilities != Gaussian1dNonstandardSwaptionEngineProbabilities.None)
        {
            var prob = new double[npvp0.Length];
            for (int i = 0; i < npvp0.Length; i++)
            {
                prob[i] = npvp1[i][0] * (Probabilities == Gaussian1dNonstandardSwaptionEngineProbabilities.Naive ? 1.0 : model.NumeraireCore(0.0, null, 0.0, discountCurve, memo));
            }

            return new InstrumentResults
            {
                NPV = value,
                AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("probabilities", ImmutableArray.Create(prob)),
            };
        }

        // end probability computation
        return new InstrumentResults { NPV = value };
    }

    // QuantLib's underlyingNpv, underlyingType, underlyingLastDate and initialGuess on a swaption's arguments.
    private sealed class BasketUnderlying(
        Gaussian1dNonstandardSwaptionEngine engine,
        NonstandardSwaption swaption,
        NonstandardSwapArguments arguments,
        EvaluationContext context,
        Gaussian1dMemo memo) : IBasketGeneratingUnderlying
    {
        public SwapType Type => swaption.Underlying.Type;

        public Date LastDate => arguments.FixedPayDates[^1];

        public double Npv(Date expiry, double y)
        {
            Gaussian1dModel model = engine.Model;
            ImmutableArray<CashFlow> floatingLeg = swaption.Underlying.FloatingLeg;
            YieldTermStructure? forwardingCurve = swaption.Underlying.IborIndex.ForwardingTermStructure;

            // determine the indices on both legs representing the cashflows that
            // are part of the exercise into right
            int fixedIdx = FirstAlive(arguments.FixedResetDates, expiry);
            int floatingIdx = FirstAlive(arguments.FloatingResetDates, expiry);

            // calculate the npv of these cashflows conditional on y at expiry
            double type = (double)arguments.Type;
            double npv = 0.0;
            for (int i = fixedIdx; i < arguments.FixedResetDates.Length; i++)
            {
                npv -= arguments.FixedCoupons[i] * model.Zerobond(arguments.FixedPayDates[i], expiry, y, engine.DiscountCurve, memo)
                    * engine.SpreadDiscount(expiry, arguments.FixedPayDates[i]);
            }

            for (int i = floatingIdx; i < arguments.FloatingResetDates.Length; i++)
            {
                double amount;
                if (!arguments.FloatingIsRedemptionFlow[i])
                {
                    double floatingRate = floatingLeg[i] is OvernightIndexedCoupon overnight
                        ? model.CompoundedRate(overnight, expiry, y, forwardingCurve, memo)
                        : model.ForwardRate(arguments.FloatingFixingDates[i], context, expiry, y, swaption.Underlying.IborIndex, memo);
                    amount = ((arguments.FloatingGearings[i] * floatingRate) + arguments.FloatingSpreads[i])
                        * arguments.FloatingAccrualTimes[i] * arguments.FloatingNominal[i];
                }
                else
                {
                    amount = arguments.FloatingCoupons[i]!.Value;
                }

                npv += amount * model.Zerobond(arguments.FloatingPayDates[i], expiry, y, engine.DiscountCurve, memo)
                    * engine.SpreadDiscount(expiry, arguments.FloatingPayDates[i]);
            }

            return type * npv;
        }

        public Vector InitialGuess(Date expiry)
        {
            int fixedIdx = FirstAlive(arguments.FixedResetDates, expiry);

            var initial = new Vector(3);
            double nominalSum = 0.0, weightedRate = 0.0, ind = 0.0;
            for (int i = fixedIdx; i < arguments.FixedResetDates.Length; i++)
            {
                nominalSum += arguments.FixedNominal[i];
                double rate = arguments.FixedRate[i];
                if (ComparisonFunctions.Close(rate, 0.0))
                {
                    rate = 0.03; // this value is at least better than zero
                }

                weightedRate += arguments.FixedNominal[i] * rate;
                if (arguments.FixedNominal[i] > 1E-8)
                {
                    // exclude zero nominal periods
                    ind += 1.0;
                }
            }

            if (!(nominalSum > 0.0))
            {
                throw new NanoQLException($"sum of nominals on fixed leg must be positive ({DataFormattersFunctions.General(nominalSum)})");
            }

            // QuantLib divides by the count unguarded (#2845's class)
            if (!(ind > 0.0))
            {
                throw new NanoQLException($"no fixed leg nominals above 1e-8 remain at calibration expiry {expiry}");
            }

            double nominalAvg = nominalSum / ind;
            weightedRate /= nominalSum;
            initial[0] = nominalAvg;
            initial[1] = engine.Model.TermStructure.TimeFromReference(LastDate) - engine.Model.TermStructure.TimeFromReference(expiry);
            initial[2] = weightedRate;
            return initial;
        }
    }
}
