// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/gaussian1dswaptionengine.hpp ql/pricingengines/swaption/gaussian1dswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The one-factor model swaption engine: a European or Bermudan swaption rolled back over a grid of a Gaussian1d
/// model's state, each step integrating the next exercise's values' cubic spline against the state's Gaussian density.
/// </summary>
/// <remarks>
/// <para>
/// A static class (master plan §2's engine row): <c>Calculate</c> runs QuantLib's four steps (playbook §2.1), the model
/// and QuantLib's settings arguments. Every fixed coupon whose start is on or after an exercise date is part of the
/// swap exercised into there. An overnight-indexed underlying is supported: a compounded coupon's daily compounding
/// telescopes to a zero bonds' ratio over its rate period, over the shifted value dates under an observation shift,
/// normalised by the coupon's own rate accrual, which is exact for plain compounding and a lookback with the shift; a
/// lookback without the shift and a lockout are priced as if they weren't there. A payment delay is handled
/// statically. Cash-settled swaptions and arithmetically averaged overnight coupons aren't supported.
/// </para>
/// <para>
/// One call shares one memo of the model's terms, built inside it (the S-g1d plan's G1R1 (iii)). QuantLib's OpenMP
/// branch, which warms the model's caches before a parallel loop, isn't ported: the loop runs in sequence, as
/// QuantLib's default build does.
/// </para>
/// <para>
/// Two documented divergences. A discount or forwarding curve on another clock than the model's is read at the
/// instants QuantLib's arguments denote: the exercise's date in the numeraire's read and the Digital probabilities'
/// discount, the coupons' dates where QuantLib passes them; QuantLib's reads where the curves share a clock (the plan's
/// G1R6 (xvi); <c>Divergences/Gaussian1dMismatchedTimesTests.cs</c>). With <c>flatPayoffExtrapolation</c>, the right
/// tail integrates the payoff's last node, where QuantLib's integrates its second-last (G1R6 (xxi);
/// <c>Divergences/FlatPayoffExtrapolationTests.cs</c>).
/// </para>
/// </remarks>
public static class Gaussian1dSwaptionEngine
{
    /// <summary>Prices a swaption on a Gaussian1d model.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="integrationPoints">The grid's points on each side of the state's mean; 64 by default.</param>
    /// <param name="stddevs">The grid's width, in standard deviations; 7 by default.</param>
    /// <param name="extrapolatePayoff">Whether to integrate the payoff past the grid; <see langword="true"/> by
    /// default.</param>
    /// <param name="flatPayoffExtrapolation">Whether past the grid the payoff is flat; <see langword="false"/> by
    /// default.</param>
    /// <param name="discountCurve">The discount curve; <see langword="null"/>, QuantLib's empty handle, for the
    /// model's.</param>
    /// <param name="probabilities">The exercise probabilities to compute, an additional result; none by
    /// default.</param>
    /// <returns>
    /// The swaption's results: the expired results when it has expired; with <paramref name="probabilities"/>, the
    /// additional result <c>probabilities</c>, one per exercise alive and the last the probability of no exercise.
    /// </returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the model is null.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, it is cash settled at the par yield curve, or its swap's nominals aren't
    /// constant.
    /// </exception>
    public static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        Gaussian1dModel model,
        int integrationPoints = 64,
        double stddevs = 7.0,
        bool extrapolatePayoff = true,
        bool flatPayoffExtrapolation = false,
        YieldTermStructure? discountCurve = null,
        Gaussian1dSwaptionEngineProbabilities probabilities = Gaussian1dSwaptionEngineProbabilities.None)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (swaption.IsExpired(context))
        {
            return swaption.SetupExpired();
        }

        swaption.Validate();
        InstrumentResults results = Body(
            swaption, context, model, integrationPoints, stddevs, extrapolatePayoff, flatPayoffExtrapolation, discountCurve, probabilities);
        return swaption.FetchResults(results);
    }

    // The integral over the grid of the spline of the values at the next exercise: QuantLib's inner loop, which it
    // writes out for the price and for each probability; the nonstandard engine's multiplies each term by its spread's
    // discount.
    internal static double Rollback(
        Vector z,
        double[] p,
        CubicInterpolation payoff1,
        bool extrapolatePayoff,
        bool flatPayoffExtrapolation,
        OptionType type,
        double zSpreadDf = 1.0)
    {
        double price = 0.0;
        for (int i = 0; i < z.Size - 1; i++)
        {
            price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(
                0.0, payoff1.CCoefficients()[i], payoff1.BCoefficients()[i], payoff1.ACoefficients()[i], p[i], z[i], z[i], z[i + 1])
                * zSpreadDf;
        }

        if (extrapolatePayoff)
        {
            if (flatPayoffExtrapolation)
            {
                // the right tail at the last node's value, where QuantLib's takes the second-last's (G1R6 (xxi))
                price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(0.0, 0.0, 0.0, 0.0, p[z.Size - 1], z[z.Size - 2], z[z.Size - 1], 100.0)
                    * zSpreadDf;
                price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(0.0, 0.0, 0.0, 0.0, p[0], z[0], -100.0, z[0]) * zSpreadDf;
            }
            else
            {
                if (type == OptionType.Call)
                {
                    price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(
                        0.0,
                        payoff1.CCoefficients()[z.Size - 2],
                        payoff1.BCoefficients()[z.Size - 2],
                        payoff1.ACoefficients()[z.Size - 2],
                        p[z.Size - 2],
                        z[z.Size - 2],
                        z[z.Size - 1],
                        100.0) * zSpreadDf;
                }

                if (type == OptionType.Put)
                {
                    price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(
                        0.0, payoff1.CCoefficients()[0], payoff1.BCoefficients()[0], payoff1.ACoefficients()[0], p[0], z[0], -100.0, z[0])
                        * zSpreadDf;
                }
            }
        }

        return price;
    }

    // QuantLib's cubic spline of a grid's values: monotonic, with Lagrange ends.
    internal static CubicInterpolation Spline(Vector z, double[] values) =>
        new(z.AsSpan(), values, CubicInterpolationDerivativeApprox.Spline, true, CubicInterpolationBoundaryCondition.Lagrange, 0.0, CubicInterpolationBoundaryCondition.Lagrange, 0.0);

    // std::upper_bound over sorted dates: the first index whose date is after d.
    internal static int UpperBound(ImmutableArray<Date> dates, Date d)
    {
        int first = 0;
        int count = dates.Length;
        while (count > 0)
        {
            int step = count / 2;
            int it = first + step;
            if (!(d < dates[it]))
            {
                first = it + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }

    // QuantLib's calculate(): its checks in order, the rollback from the last exercise to today, and the probabilities.
    private static InstrumentResults Body(
        Swaption swaption,
        EvaluationContext context,
        Gaussian1dModel model,
        int integrationPoints,
        double stddevs,
        bool extrapolatePayoff,
        bool flatPayoffExtrapolation,
        YieldTermStructure? discountCurve,
        Gaussian1dSwaptionEngineProbabilities probabilities)
    {
        FixedVsFloatingSwapArguments arguments = FixedVsFloatingSwapArguments.Of(swaption.Underlying, context);

        if (!(swaption.SettlementMethod != SettlementMethod.ParYieldCurve))
        {
            throw new NanoQLException("cash settled (ParYieldCurve) swaptions not priced with Gaussian1dSwaptionEngine");
        }

        if (arguments.Nominal is not double nominal)
        {
            throw new NanoQLException("non-constant nominals are not supported yet");
        }

        Date settlement = model.TermStructure.ReferenceDate;
        ImmutableArray<Date> exerciseDates = swaption.Exercise.Dates;

        if (exerciseDates[^1] <= settlement)
        {
            // swaption is expired, possibly generated swap is not valued
            return new InstrumentResults { NPV = 0.0 };
        }

        int idx = exerciseDates.Length - 1;
        int minIdxAlive = UpperBound(exerciseDates, settlement);

        FixedVsFloatingSwap swap = swaption.Underlying;
        OptionType type = arguments.Type == SwapType.Payer ? OptionType.Call : OptionType.Put;
        Schedule fixedSchedule = swap.FixedSchedule;
        Schedule floatSchedule = swap.FloatingSchedule;

        YieldTermStructure? forwardingCurve = swap.IborIndex.ForwardingTermStructure;

        ImmutableArray<CashFlow> floatingLeg = swap.FloatingLeg;
        var overnightCoupons = new OvernightIndexedCoupon?[floatingLeg.Length];
        for (int l = 0; l < floatingLeg.Length; ++l)
        {
            overnightCoupons[l] = floatingLeg[l] as OvernightIndexedCoupon;
        }

        var memo = new Gaussian1dMemo();
        double[] npv0 = new double[(2 * integrationPoints) + 1];
        double[] npv1 = new double[(2 * integrationPoints) + 1];
        Vector z = model.YGrid(stddevs, integrationPoints, 1.0, 0.0, 0.0, memo);
        double[] p = new double[z.Size];

        // for probability computation
        double[][] npvp0 = [], npvp1 = [];
        if (probabilities != Gaussian1dSwaptionEngineProbabilities.None)
        {
            npvp0 = new double[idx - minIdxAlive + 2][];
            npvp1 = new double[idx - minIdxAlive + 2][];
            for (int i = 0; i < idx - minIdxAlive + 2; ++i)
            {
                npvp0[i] = new double[(2 * integrationPoints) + 1];
                npvp1[i] = new double[(2 * integrationPoints) + 1];
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

            int j1 = UpperBound(fixedSchedule.Dates, expiry0 - 1);
            int k1 = UpperBound(floatSchedule.Dates, expiry0 - 1);

            for (int k = 0; k < (expiry0 > settlement ? npv0.Length : 1); k++)
            {
                double price = 0.0;
                if (expiry1Time is double e1)
                {
                    Vector yg = model.YGrid(stddevs, integrationPoints, e1, expiry0Time, expiry0 > settlement ? z[k] : 0.0, memo);
                    CubicInterpolation payoff0 = Spline(z, npv1);
                    for (int i = 0; i < yg.Size; i++)
                    {
                        p[i] = payoff0.Evaluate(yg[i], true);
                    }

                    CubicInterpolation payoff1 = Spline(z, p);
                    price = Rollback(z, p, payoff1, extrapolatePayoff, flatPayoffExtrapolation, type);
                }

                npv0[k] = price;

                // for probability computation
                if (probabilities != Gaussian1dSwaptionEngineProbabilities.None)
                {
                    for (int m = 0; m < npvp0.Length; m++)
                    {
                        double priceM = 0.0;
                        if (expiry1Time is double e1m)
                        {
                            Vector yg = model.YGrid(stddevs, integrationPoints, e1m, expiry0Time, expiry0 > settlement ? z[k] : 0.0, memo);
                            CubicInterpolation payoff0 = Spline(z, npvp1[m]);
                            for (int i = 0; i < yg.Size; i++)
                            {
                                p[i] = payoff0.Evaluate(yg[i], true);
                            }

                            CubicInterpolation payoff1 = Spline(z, p);
                            priceM = Rollback(z, p, payoff1, extrapolatePayoff, flatPayoffExtrapolation, type);
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
                        double floatingRate = overnightCoupons[l] is OvernightIndexedCoupon overnight
                            ? model.CompoundedRate(overnight, expiry0, z[k], forwardingCurve, memo)
                            : model.ForwardRate(arguments.FloatingFixingDates[l], context, expiry0, z[k], swap.IborIndex, memo);
                        floatingLegNpv += nominal * arguments.FloatingAccrualTimes[l] * (arguments.FloatingSpreads[l] + floatingRate)
                            * model.Zerobond(arguments.FloatingPayDates[l], expiry0, z[k], discountCurve, memo);
                    }

                    double fixedLegNpv = 0.0;
                    for (int l = j1; l < arguments.FixedCoupons.Length; l++)
                    {
                        fixedLegNpv += arguments.FixedCoupons[l] * model.Zerobond(arguments.FixedPayDates[l], expiry0, z[k], discountCurve, memo);
                    }

                    // the numeraire read at the exercise's date (G1R6 (xvi))
                    double exerciseValue = (type == OptionType.Call ? 1.0 : -1.0) * (floatingLegNpv - fixedLegNpv)
                        / model.NumeraireCore(expiry0Time, expiry0, z[k], discountCurve, memo);

                    // for probability computation
                    if (probabilities != Gaussian1dSwaptionEngineProbabilities.None)
                    {
                        if (idx == exerciseDates.Length - 1)
                        {
                            // if true we are at the latest date, so we init the no call probability
                            npvp0[^1][k] = probabilities == Gaussian1dSwaptionEngineProbabilities.Naive
                                ? 1.0
                                : 1.0 / (model.ZerobondCore(expiry0Time, expiry0, 0.0, null, 0.0, discountCurve, memo) * model.Numeraire(expiry0, z[k], discountCurve, memo));
                        }

                        if (exerciseValue >= npv0[k])
                        {
                            npvp0[idx - minIdxAlive][k] = probabilities == Gaussian1dSwaptionEngineProbabilities.Naive
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
            if (probabilities != Gaussian1dSwaptionEngineProbabilities.None)
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
        if (probabilities != Gaussian1dSwaptionEngineProbabilities.None)
        {
            var prob = new double[npvp0.Length];
            for (int i = 0; i < npvp0.Length; i++)
            {
                prob[i] = npvp1[i][0] * (probabilities == Gaussian1dSwaptionEngineProbabilities.Naive ? 1.0 : model.NumeraireCore(0.0, null, 0.0, discountCurve, memo));
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
}
