// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/cmsmarket.hpp ql/termstructures/volatility/swaption/cmsmarket.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A set of CMS quotes: the bid and ask spreads over the Ibor leg of CMS swaps of each length on each swap index, with
/// the spreads, spot and forward CMS leg NPVs the market implies and the model gives.
/// </summary>
/// <remarks>
/// <para>
/// For each swap length and index the market's spot CMS leg NPV is implied from the spot swap's Ibor leg (its NPV and
/// BPS on the discounting curve at its reference date, and the mid spread), and a forward CMS leg is the difference of
/// two spot ones; the model prices each forward-starting CMS swap's CMS leg with the index's pricer and sums them into
/// spot legs, whose equilibrium spread over the Ibor leg is the model's spread.
/// </para>
/// <para>
/// QuantLib's market is a lazy object that its calibration reprices in place, giving its pricers a new volatility and
/// mean reversion. This one computes at construction, under the context it is given, and
/// <see cref="Reprice"/> returns a new market whose pricers are copies with the new volatility and reversion, its
/// legs re-priced through <see cref="CouponPricerFunctions"/>; QuantLib's pricers, shared with other legs, change
/// with its reprice, which an immutable pricer can't. QuantLib's quotes are their values. Immutable.
/// </para>
/// </remarks>
public sealed class CmsMarket
{
    private readonly ImmutableArray<Period> swapLengths;
    private readonly ImmutableArray<SwapIndex> swapIndexes;
    private readonly IborIndex iborIndex;
    private readonly ImmutableArray<ImmutableArray<double>> bidAskSpreads;
    private readonly ImmutableArray<CmsCouponPricer> pricers;
    private readonly YieldTermStructure discTS;
    private readonly int nExercise;
    private readonly int nSwapIndexes;
    private readonly ImmutableArray<Period> swapTenors;
    private readonly Matrix spotFloatLegNPV;
    private readonly Matrix spotFloatLegBPS;
    private readonly Matrix mktBidSpreads;
    private readonly Matrix mktAskSpreads;
    private readonly Matrix mktSpreads;
    private readonly Matrix mdlSpreads;
    private readonly Matrix errSpreads;
    private readonly Matrix mktSpotCmsLegNPV;
    private readonly Matrix mdlSpotCmsLegNPV;
    private readonly Matrix errSpotCmsLegNPV;
    private readonly Matrix mktFwdCmsLegNPV;
    private readonly Matrix mdlFwdCmsLegNPV;
    private readonly Matrix errFwdCmsLegNPV;
    private readonly Swap[,] spotSwaps;
    private readonly Swap[,] fwdSwaps;

    /// <summary>Initializes a new instance of the <see cref="CmsMarket"/> class, and computes it.</summary>
    /// <param name="swapLengths">The CMS swaps' lengths, increasing.</param>
    /// <param name="swapIndexes">The swap indexes.</param>
    /// <param name="iborIndex">The Ibor legs' index.</param>
    /// <param name="bidAskSpreads">A row per swap length, with a bid and an ask spread per swap index, in that
    /// order.</param>
    /// <param name="pricers">A CMS coupon pricer per swap index.</param>
    /// <param name="discountingTS">The discounting curve.</param>
    /// <param name="context">The evaluation context the swaps are built and priced under.</param>
    /// <exception cref="ArgumentNullException">An argument or an element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A row doesn't hold a bid and an ask per swap index, there isn't a row per
    /// swap length, or there isn't a pricer per swap index.</exception>
    /// <exception cref="NanoQLException">A swap or a leg can't be priced.</exception>
    public CmsMarket(
        IEnumerable<Period> swapLengths,
        IEnumerable<SwapIndex> swapIndexes,
        IborIndex iborIndex,
        IEnumerable<IEnumerable<double>> bidAskSpreads,
        IEnumerable<CmsCouponPricer> pricers,
        YieldTermStructure discountingTS,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(swapLengths);
        ArgumentNullException.ThrowIfNull(swapIndexes);
        ArgumentNullException.ThrowIfNull(iborIndex);
        ArgumentNullException.ThrowIfNull(bidAskSpreads);
        ArgumentNullException.ThrowIfNull(pricers);
        ArgumentNullException.ThrowIfNull(discountingTS);
        ArgumentNullException.ThrowIfNull(context);
        this.swapLengths = [.. swapLengths];
        this.swapIndexes = [.. swapIndexes];
        this.iborIndex = iborIndex;
        this.bidAskSpreads =
            [.. bidAskSpreads.Select(row => (row ?? throw new ArgumentNullException(nameof(bidAskSpreads))).ToImmutableArray())];
        this.pricers = [.. pricers];
        discTS = discountingTS;
        foreach (SwapIndex index in this.swapIndexes)
        {
            ArgumentNullException.ThrowIfNull(index, nameof(swapIndexes));
        }

        foreach (CmsCouponPricer pricer in this.pricers)
        {
            ArgumentNullException.ThrowIfNull(pricer, nameof(pricers));
        }

        nExercise = this.swapLengths.Length;
        nSwapIndexes = this.swapIndexes.Length;

        // QuantLib reads the first row's size unchecked, and every row's two columns per index: refused, as their
        // sizes differ, before the first read (R8).
        int columns = this.bidAskSpreads.IsEmpty ? 0 : this.bidAskSpreads[0].Length;
        if (!(2 * nSwapIndexes == columns))
        {
            throw new ArgumentException(
                $"2*nSwapIndexes_ ({2 * nSwapIndexes}) != bidAskSpreads columns() ({columns})", nameof(bidAskSpreads));
        }

        if (!(nExercise == this.bidAskSpreads.Length))
        {
            throw new ArgumentException(
                $"nExercise_ ({nExercise}) != bidAskSpreads rows() ({this.bidAskSpreads.Length})", nameof(bidAskSpreads));
        }

        for (int i = 1; i < nExercise; i++)
        {
            if (!(this.bidAskSpreads[i].Length == columns))
            {
                throw new ArgumentException(
                    $"2*nSwapIndexes_ ({2 * nSwapIndexes}) != bidAskSpreads columns() ({this.bidAskSpreads[i].Length}) "
                    + $"in row {i}",
                    nameof(bidAskSpreads));
            }
        }

        if (!(nSwapIndexes == this.pricers.Length))
        {
            throw new ArgumentException(
                $"nSwapIndexes_ ({nSwapIndexes}) != pricers ({this.pricers.Length})", nameof(pricers));
        }

        swapTenors = [.. this.swapIndexes.Select(index => index.Tenor)];

        spotFloatLegNPV = new Matrix(nExercise, nSwapIndexes);
        spotFloatLegBPS = new Matrix(nExercise, nSwapIndexes);
        mktBidSpreads = new Matrix(nExercise, nSwapIndexes);
        mktAskSpreads = new Matrix(nExercise, nSwapIndexes);
        mktSpreads = new Matrix(nExercise, nSwapIndexes);
        mdlSpreads = new Matrix(nExercise, nSwapIndexes);
        errSpreads = new Matrix(nExercise, nSwapIndexes);
        mktSpotCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        mdlSpotCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        errSpotCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        mktFwdCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        mdlFwdCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        errFwdCmsLegNPV = new Matrix(nExercise, nSwapIndexes);

        // QuantLib's constructor builds the swaps, and its performCalculations prices them.
        var start = new Period(0, TimeUnit.Years);
        spotSwaps = new Swap[nExercise, nSwapIndexes];
        fwdSwaps = new Swap[nExercise, nSwapIndexes];
        for (int i = 0; i < nExercise; ++i)
        {
            if (i > 0)
            {
                start = this.swapLengths[i - 1];
            }

            for (int j = 0; j < nSwapIndexes; ++j)
            {
                // never evaluate the spot swap, only its ibor floating leg
                spotSwaps[i, j] = new MakeCms(this.swapLengths[i], this.swapIndexes[j], iborIndex).ToSwap(context);
                fwdSwaps[i, j] = new MakeCms(this.swapLengths[i] - start, this.swapIndexes[j], iborIndex)
                    .WithForwardStart(start)
                    .WithCmsCouponPricer(this.pricers[j])
                    .WithDiscountingTermStructure(discTS)
                    .ToSwap(context);
            }
        }

        PerformCalculations(context);
    }

    // A repriced market: the source's terms, swaps and spot legs, the forward swaps' CMS legs given the new pricers
    // through the visitor, as QuantLib's pricers change in place under the legs that hold them.
    private CmsMarket(CmsMarket source, ImmutableArray<CmsCouponPricer> pricers, EvaluationContext context)
    {
        swapLengths = source.swapLengths;
        swapIndexes = source.swapIndexes;
        iborIndex = source.iborIndex;
        bidAskSpreads = source.bidAskSpreads;
        this.pricers = pricers;
        discTS = source.discTS;
        nExercise = source.nExercise;
        nSwapIndexes = source.nSwapIndexes;
        swapTenors = source.swapTenors;
        spotFloatLegNPV = new Matrix(nExercise, nSwapIndexes);
        spotFloatLegBPS = new Matrix(nExercise, nSwapIndexes);
        mktBidSpreads = new Matrix(nExercise, nSwapIndexes);
        mktAskSpreads = new Matrix(nExercise, nSwapIndexes);
        mktSpreads = new Matrix(nExercise, nSwapIndexes);
        mdlSpreads = new Matrix(nExercise, nSwapIndexes);
        errSpreads = new Matrix(nExercise, nSwapIndexes);
        mktSpotCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        mdlSpotCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        errSpotCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        mktFwdCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        mdlFwdCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        errFwdCmsLegNPV = new Matrix(nExercise, nSwapIndexes);
        spotSwaps = source.spotSwaps;
        fwdSwaps = new Swap[nExercise, nSwapIndexes];
        for (int i = 0; i < nExercise; ++i)
        {
            for (int j = 0; j < nSwapIndexes; ++j)
            {
                fwdSwaps[i, j] = CouponPricerFunctions.SetCouponPricer(source.fwdSwaps[i, j], 0, pricers[j]);
            }
        }

        PerformCalculations(context);
    }

    /// <summary>Gets the swap indexes' tenors.</summary>
    public ImmutableArray<Period> SwapTenors => swapTenors;

    /// <summary>Gets the CMS swaps' lengths.</summary>
    public ImmutableArray<Period> SwapLengths => swapLengths;

    /// <summary>The model's spreads over the Ibor leg.</summary>
    /// <returns>A copy: a row per swap length and a column per swap index.</returns>
    public Matrix ImpliedCmsSpreads() => mdlSpreads.Clone();

    /// <summary>The model's spreads less the market's mid spreads.</summary>
    /// <returns>A copy: a row per swap length and a column per swap index.</returns>
    public Matrix SpreadErrors() => errSpreads.Clone();

    /// <summary>
    /// A market repriced on another volatility and mean reversion: QuantLib's <c>reprice</c>, each pricer given the
    /// volatility and, unless it is <see langword="null"/>, the mean reversion.
    /// </summary>
    /// <param name="volStructure">The swaption volatility.</param>
    /// <param name="meanReversion">The mean reversion, or <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>,
    /// to keep each pricer's.</param>
    /// <param name="context">The evaluation context the new market is computed under.</param>
    /// <returns>The new market; this one is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A mean reversion is given and a pricer doesn't revert to the mean.</exception>
    public CmsMarket Reprice(SwaptionVolatilityStructure? volStructure, double? meanReversion, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var repriced = new CmsCouponPricer[nSwapIndexes];
        for (int j = 0; j < nSwapIndexes; ++j)
        {
            // set new volatility structure and new mean reversion
            CmsCouponPricer p = pricers[j].WithSwaptionVolatility(volStructure);
            if (meanReversion is double m)
            {
                p = p is IMeanRevertingPricer reverting
                    ? (CmsCouponPricer)reverting.WithMeanReversion(m)
                    : throw new NanoQLException($"mean reverting pricer required at index {j}");
            }

            repriced[j] = p;
        }

        return new CmsMarket(this, [.. repriced], context);
    }

    // QuantLib's performCalculations, into the matrices.
    private void PerformCalculations(EvaluationContext context)
    {
        for (int j = 0; j < nSwapIndexes; ++j)
        {
            double mktPrevPart = 0.0, mdlPrevPart = 0.0;
            for (int i = 0; i < nExercise; ++i)
            {
                // **** market
                mktBidSpreads[i, j] = this.bidAskSpreads[i][j * 2];
                mktAskSpreads[i, j] = this.bidAskSpreads[i][(j * 2) + 1];
                mktSpreads[i, j] = (mktBidSpreads[i, j] + mktAskSpreads[i, j]) / 2;

                ImmutableArray<CashFlow> spotFloatLeg = spotSwaps[i, j].Leg(1);
                spotFloatLegNPV[i, j] = CashFlows.Npv(spotFloatLeg, discTS, context, false, discTS.ReferenceDate);
                spotFloatLegBPS[i, j] = CashFlows.Bps(spotFloatLeg, discTS, context, false, discTS.ReferenceDate);

                // imply the spot CMS leg NPV from the spot ibor floating leg NPV
                mktSpotCmsLegNPV[i, j] = -(spotFloatLegNPV[i, j] + (spotFloatLegBPS[i, j] * mktSpreads[i, j] / 1e-4));

                // fwd CMS legs can be computed as differences between spot legs
                mktFwdCmsLegNPV[i, j] = mktSpotCmsLegNPV[i, j] - mktPrevPart;
                mktPrevPart = mktSpotCmsLegNPV[i, j];

                // **** model

                // calculate the forward swap (the time consuming part)
                SwapResults results = DiscountingSwapEngine.Calculate(fwdSwaps[i, j], context, discTS);
                mdlFwdCmsLegNPV[i, j] = results.LegNPV[0] ?? throw new NanoQLException("result not available");
                errFwdCmsLegNPV[i, j] = mdlFwdCmsLegNPV[i, j] - mktFwdCmsLegNPV[i, j];

                // spot CMS legs can be computed as incremental sum of forward legs
                mdlSpotCmsLegNPV[i, j] = mdlPrevPart + mdlFwdCmsLegNPV[i, j];
                mdlPrevPart = mdlSpotCmsLegNPV[i, j];
                errSpotCmsLegNPV[i, j] = mdlSpotCmsLegNPV[i, j] - mktSpotCmsLegNPV[i, j];

                // equilibriums spread over ibor leg
                double npv = spotFloatLegNPV[i, j] + mdlSpotCmsLegNPV[i, j];
                mdlSpreads[i, j] = -npv / spotFloatLegBPS[i, j] * 1e-4;
                errSpreads[i, j] = mdlSpreads[i, j] - mktSpreads[i, j];
            }
        }
    }

    /// <summary>The weighted root-mean-square of the forward CMS legs' NPV errors.</summary>
    /// <param name="w">The weights: a row per swap length and a column per swap index.</param>
    /// <returns>The error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="w"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The weights aren't a row per swap length and a column per swap
    /// index.</exception>
    public double WeightedFwdNpvError(Matrix w) => WeightedMean(errFwdCmsLegNPV, w);

    /// <summary>The weighted root-mean-square of the spot CMS legs' NPV errors.</summary>
    /// <param name="w">The weights: a row per swap length and a column per swap index.</param>
    /// <returns>The error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="w"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The weights aren't a row per swap length and a column per swap
    /// index.</exception>
    public double WeightedSpotNpvError(Matrix w) => WeightedMean(errSpotCmsLegNPV, w);

    /// <summary>The weighted root-mean-square of the spread errors.</summary>
    /// <param name="w">The weights: a row per swap length and a column per swap index.</param>
    /// <returns>The error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="w"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The weights aren't a row per swap length and a column per swap
    /// index.</exception>
    public double WeightedSpreadError(Matrix w) => WeightedMean(errSpreads, w);

    /// <summary>The forward CMS legs' NPV errors, each times the square root of its weight, for a least-squares
    /// fit.</summary>
    /// <param name="w">The weights: a row per swap length and a column per swap index.</param>
    /// <returns>The errors, swap length by swap length and index by index within each.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="w"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The weights aren't a row per swap length and a column per swap
    /// index.</exception>
    public Vector WeightedFwdNpvErrors(Matrix w) => WeightedMeans(errFwdCmsLegNPV, w);

    /// <summary>The spot CMS legs' NPV errors, each times the square root of its weight, for a least-squares
    /// fit.</summary>
    /// <param name="w">The weights: a row per swap length and a column per swap index.</param>
    /// <returns>The errors, swap length by swap length and index by index within each.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="w"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The weights aren't a row per swap length and a column per swap
    /// index.</exception>
    public Vector WeightedSpotNpvErrors(Matrix w) => WeightedMeans(errSpotCmsLegNPV, w);

    /// <summary>The spread errors, each times the square root of its weight, for a least-squares fit.</summary>
    /// <param name="w">The weights: a row per swap length and a column per swap index.</param>
    /// <returns>The errors, swap length by swap length and index by index within each.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="w"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The weights aren't a row per swap length and a column per swap
    /// index.</exception>
    public Vector WeightedSpreadErrors(Matrix w) => WeightedMeans(errSpreads, w);

    /// <summary>The market's table: QuantLib's <c>browse</c>.</summary>
    /// <returns>
    /// A row per swap index and swap length, index by index and length by length within each, of 14 columns: the
    /// index's tenor length, the swap length, the bid, ask and mid spreads, the model's spread, its error and its
    /// distance outside the bid-ask range, in basis points; and the market's, the model's and their difference's spot
    /// and then forward CMS leg NPVs.
    /// </returns>
    public Matrix Browse()
    {
        var result = new Matrix(nExercise * nSwapIndexes, 14);
        for (int j = 0; j < nSwapIndexes; ++j)
        {
            for (int i = 0; i < nExercise; ++i)
            {
                int row = (j * nExercise) + i;
                result[row, 0] = swapTenors[j].Length;
                result[row, 1] = swapLengths[i].Length;

                // Spreads
                result[row, 2] = mktBidSpreads[i, j] * 10000;
                result[row, 3] = mktAskSpreads[i, j] * 10000;
                result[row, 4] = mktSpreads[i, j] * 10000;
                result[row, 5] = mdlSpreads[i, j] * 10000;
                result[row, 6] = errSpreads[i, j] * 10000;
                if (mdlSpreads[i, j] > mktAskSpreads[i, j])
                {
                    result[row, 7] = (mdlSpreads[i, j] - mktAskSpreads[i, j]) * 10000;
                }
                else if (mdlSpreads[i, j] < mktBidSpreads[i, j])
                {
                    result[row, 7] = (mktBidSpreads[i, j] - mdlSpreads[i, j]) * 10000;
                }
                else
                {
                    result[row, 7] = 0.0;
                }

                // spot CMS Leg NPVs
                result[row, 8] = mktSpotCmsLegNPV[i, j];
                result[row, 9] = mdlSpotCmsLegNPV[i, j];
                result[row, 10] = errSpotCmsLegNPV[i, j];

                // forward CMS Leg NPVs
                result[row, 11] = mktFwdCmsLegNPV[i, j];
                result[row, 12] = mdlFwdCmsLegNPV[i, j];
                result[row, 13] = errFwdCmsLegNPV[i, j];
            }
        }

        return result;
    }

    private double WeightedMean(Matrix var, Matrix w)
    {
        CheckWeights(w);
        double mean = 0.0;
        for (int i = 0; i < nExercise; ++i)
        {
            for (int j = 0; j < nSwapIndexes; ++j)
            {
                mean += w[i, j] * var[i, j] * var[i, j];
            }
        }

        mean = Math.Sqrt(mean / (nExercise * nSwapIndexes));
        return mean;
    }

    private Vector WeightedMeans(Matrix var, Matrix w)
    {
        CheckWeights(w);
        var weightedVars = new Vector(nExercise * nSwapIndexes);
        for (int i = 0; i < nExercise; ++i)
        {
            for (int j = 0; j < nSwapIndexes; ++j)
            {
                weightedVars[(i * nSwapIndexes) + j] = Math.Sqrt(w[i, j]) * var[i, j];
            }
        }

        return weightedVars;
    }

    // QuantLib reads the weights unchecked; its calibration checks their size when it is built, and the market's own
    // members refuse them here (R8).
    private void CheckWeights(Matrix w)
    {
        ArgumentNullException.ThrowIfNull(w);
        if (!(w.Rows == nExercise && w.Columns == nSwapIndexes))
        {
            throw new ArgumentException(
                $"weights ({w.Rows}x{w.Columns}) must be a row per swap length ({nExercise}) and a column per swap "
                + $"index ({nSwapIndexes})",
                nameof(w));
        }
    }
}
