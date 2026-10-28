// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/recursivelossmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: expectedConditionalLoss, never ported: private and unused, its only call commented out

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The recursive STCDO default loss model of a heterogeneous pool: names heterogeneous in their default probabilities,
/// notionals and recoveries, their correlations given by the latent model. The recursive pricing algorithm is Andersen,
/// Sidenius and Basu's, "All your hedges in one basket", Risk, November 2003, pages 67-72.
/// </summary>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
/// <remarks>
/// <para>
/// With copulas other than the Gaussian it is only an approximation (see the remark on p. 68). The expected tranche
/// loss inverts the sum and the integral (the first, unnumbered, equation on p. 70): EL(t) = ∫ q(ω) dω Σ_k l_k
/// P(l_k; t | ω), the conditional expected loss integrated over the factor. The losses are counted in a loss unit, the
/// smallest nonzero loss given default over the number of buckets, each name's loss given default rounded to a whole
/// number of units, so the distribution's attainable losses are the sums of the names' weights. To be done, as in
/// QuantLib: a loss unit from some small fraction of the portfolio's loss weights (fine for pricing, not for risk
/// metrics; O'Kane 18.3.2), and integrands reading the inverted probabilities throughout.
/// </para>
/// <para>
/// The loss distribution labels each point by its attainable loss, and the percentile at 0 and 1 is the tranche's loss
/// at the first and last, as its interior's are tranche losses (the S-xcr code review's C3 O1-1): QuantLib's labels
/// the i-th attainable loss i times the loss unit, wrong where the weights aren't 0, 1, 2, …, and its percentile
/// returns the distribution's probability at both ends (1.0000000003 and 0.717; the S-xcr plan's XR8 (vii);
/// <c>Divergences/RecursiveLossModelTests.cs</c>). Bound to a basket, a copy
/// computes the weights afresh, where QuantLib's appends them to the previous basket's (XR8 (viii)), and prices the
/// live names over the restated tranche (XR8 (ii)). Refused, where QuantLib's reads before or past its vectors (XR8
/// (xxix)): a basket whose losses given default are all 0, and a percentile or shortfall whose search leaves the
/// distribution. A shortfall the distribution never reaches is refused, as the binomial model's is, where QuantLib's
/// reads 0 with its comment "well, we are in error.... fix: FAIL" (<c>recursivelossmodel.hpp:298</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class RecursiveLossModel<TCopula, TInitTraits> : DefaultLossModel
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    // loss model descriptor members
    private readonly int nBuckets;
    private ConstantLossLatentModel<TCopula, TInitTraits> copula;
    private double[] wk = [];
    private double[] attainable = [];
    private double lossUnit;

    // cached remaining basket magnitudes:
    private double attachAmount;
    private double detachAmount;
    private int remainingBsktSize;

    /// <summary>Initializes a new instance of the <see cref="RecursiveLossModel{TCopula, TInitTraits}"/>
    /// class.</summary>
    /// <param name="m">The constant-loss latent model.</param>
    /// <param name="nbuckets">The number of loss units in the smallest loss given default (nope! use the max common
    /// divisor, see O'Kane; or give both options at least).</param>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is <see langword="null"/>.</exception>
    public RecursiveLossModel(ConstantLossLatentModel<TCopula, TInitTraits> m, int nbuckets = 1)
    {
        ArgumentNullException.ThrowIfNull(m);
        copula = m;
        nBuckets = nbuckets;
    }

    /// <summary>The expected tranche loss by a date, the conditional expected loss integrated over the factor.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The loss, an amount.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public override double ExpectedTrancheLoss(Date date)
    {
        // calculate inverted unconditional Ps first so we save the inversion:
        ImmutableArray<double> uncDefProb = BoundBasket.RemainingProbabilities(date);
        double[] invProb = new double[uncDefProb.Length];
        for (int i = 0; i < uncDefProb.Length; ++i)
        {
            invProb[i] = copula.InverseCumulativeY(uncDefProb[i], i);
        }

        return copula.IntegratedExpectedValue(v1 => ExpectedConditionalLossInvP(invProb, v1));
    }

    /// <summary>The probabilities of the attainable losses by a date, in increasing order of the loss.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The probabilities.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public double[] LossProbability(Date date)
    {
        ImmutableArray<double> uncDefProb = BoundBasket.RemainingProbabilities(date);
        return copula.IntegratedExpectedValueV(v1 => ConditionalLossProb(uncDefProb, v1));
    }

    /// <inheritdoc/>
    public override ImmutableSortedDictionary<double, double> LossDistribution(Date d)
    {
        var distrib = ImmutableSortedDictionary.CreateBuilder<double, double>();
        double[] values = LossProbability(d);
        double sum = 0.0;
        for (int i = 0; i < values.Length; ++i)
        {
            distrib.TryAdd(attainable[i] * lossUnit, sum + values[i]);
            sum += values[i];
        }

        return distrib.ToImmutable();
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The search ends before the distribution's start or past its end, where
    /// QuantLib reads there.</exception>
    public override double Percentile(Date d, double percentile)
    {
        // Integrate then search rather than search and then integrate?
        // Here I am not using a search because the point might be not attainable
        //   (loss distrib is not continuous)
        ImmutableSortedDictionary<double, double> dist = LossDistribution(d);
        double[] keys = [.. dist.Keys];
        double[] probs = [.. dist.Values];

        if (probs[0] >= 1.0)
        {
            return keys[0];
        }

        // deterministic case (e.g. date requested is todays date)
        if (keys.Length == 1)
        {
            return keys[0];
        }

        if (percentile == 1.0)
        {
            // the tranche's loss at the largest attainable loss
            return Math.Min(Math.Max(keys[^1] - attachAmount, 0.0), detachAmount - attachAmount);
        }

        if (percentile == 0.0)
        {
            return keys[0];
        }

        int itdist = 0;
        while (probs[itdist] <= percentile)
        {
            if (++itdist == keys.Length)
            {
                throw new NanoQLException("Recursive model fails to calculate the percentile: past the loss distribution's end");
            }
        }

        if (itdist == 0)
        {
            throw new NanoQLException("Recursive model fails to calculate the percentile: before the loss distribution's start");
        }

        double valPlus = probs[itdist];
        double xPlus = keys[itdist];
        --itdist; // we're never 1st or last, because of tests above
        double valMin = probs[itdist];
        double xMin = keys[itdist];

        double portfLoss = xPlus - ((xPlus - xMin) * (valPlus - percentile) / (valPlus - valMin));
        return Math.Min(Math.Max(portfLoss - attachAmount, 0.0), detachAmount - attachAmount);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The distribution reaches the percentile at its first point or at its last,
    /// where QuantLib reads before its start or past its end, or never ("Recursive model fails to calculate
    /// ESF.").</exception>
    public override double ExpectedShortfall(Date d, double percentile)
    {
        if (d == BoundBasket.Context.EvaluationDate)
        {
            return 0.0;
        }

        // refused where the distribution never reaches the percentile, where QuantLib's reads 0: "well, we are in
        // error.... fix: FAIL"
        return BinomialLossModel<TCopula, TInitTraits>.TrancheShortfall(
            LossDistribution(d), percentile, attachAmount, detachAmount, "Recursive", failsAtEnd: true);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Every live name's loss given default is 0, where QuantLib's loss unit reads
    /// the minimum of an empty list.</exception>
    protected override void ResetModel()
    {
        // basket update:
        Basket basket = BoundBasket;
        ImmutableArray<double> notionals = basket.RemainingNotionals();
        attachAmount = basket.RemainingAttachmentAmount();
        detachAmount = basket.RemainingDetachmentAmount();

        // model parameters:
        remainingBsktSize = notionals.Length;

        copula = (ConstantLossLatentModel<TCopula, TInitTraits>)copula.ResetBasket(SourceBasket);

        double[] lgdsTmp = new double[remainingBsktSize];
        for (int i = 0; i < remainingBsktSize; ++i)
        {
            lgdsTmp[i] = notionals[i] * (1.0 - copula.Recoveries[i]);
        }

        double[] lgds = [.. lgdsTmp.Where(l => l != 0.0)];
        if (lgds.Length == 0)
        {
            throw new NanoQLException("every live name's loss given default is 0: the recursive model has no loss unit");
        }

        lossUnit = lgds.Min() / nBuckets;
        wk = new double[remainingBsktSize];
        for (int i = 0; i < remainingBsktSize; ++i)
        {
            wk[i] = Math.Floor((lgdsTmp[i] / lossUnit) + .5);
        }

        // the attainable losses, in units: the keys of every conditional distribution
        var keys = new SortedSet<double> { 0.0 };
        foreach (double w in wk)
        {
            keys.UnionWith([.. keys.Select(k => k + w)]);
        }

        attainable = [.. keys];
    }

    // The conditional distribution of the attainable losses, in units (eq. 10, p. 68): the recursive algorithm.
    private SortedDictionary<double, double> ConditionalLossDistrib(ImmutableArray<double> pDefDate, ReadOnlySpan<double> mktFactor)
    {
        var pIndepDistrib = new SortedDictionary<double, double> { [0.0] = 1.0 };
        for (int iName = 0; iName < remainingBsktSize; ++iName)
        {
            double pDef = copula.ConditionalDefaultProbability(pDefDate[iName], iName, mktFactor);
            pIndepDistrib = Recurse(pIndepDistrib, pDef, wk[iName]);
        }

        /* Apply tranche limits now .... mind you this could be done outside*/
        return pIndepDistrib;
    }

    // twice?! rewrite one in terms of the other, this is a duplicate!
    private SortedDictionary<double, double> ConditionalLossDistribInvP(double[] invpDefDate, ReadOnlySpan<double> mktFactor)
    {
        // eq. 10 p.68
        // attainable losses distribution, recursive algorithm
        var pIndepDistrib = new SortedDictionary<double, double> { [0.0] = 1.0 };
        for (int iName = 0; iName < remainingBsktSize; ++iName)
        {
            double pDef = copula.ConditionalDefaultProbabilityInvP(invpDefDate[iName], iName, mktFactor);
            pIndepDistrib = Recurse(pIndepDistrib, pDef, wk[iName]);
        }

        /* Apply tranche limits now .... mind you this could be done outside*/
        return pIndepDistrib;
    }

    // One name added to the distribution: each attainable loss is kept if the name survives and moved by its weight if
    // it defaults, in the order of the losses.
    private static SortedDictionary<double, double> Recurse(SortedDictionary<double, double> pIndepDistrib, double pDef, double weight)
    {
        // iterate on all possible losses in the distribution:
        var pDistTemp = new SortedDictionary<double, double>();
        foreach (KeyValuePair<double, double> distIt in pIndepDistrib)
        {
            // update prob if this name does not default
            if (pDistTemp.TryGetValue(distIt.Key, out double match))
            {
                pDistTemp[distIt.Key] = match + (distIt.Value * (1.0 - pDef));
            }
            else
            {
                pDistTemp.Add(distIt.Key, distIt.Value * (1.0 - pDef));
            }

            // and if it does
            double loss = distIt.Key + weight;
            if (pDistTemp.TryGetValue(loss, out match))
            {
                pDistTemp[loss] = match + (distIt.Value * pDef);
            }
            else
            {
                pDistTemp.Add(loss, distIt.Value * pDef);
            }
        }

        // copy back
        return pDistTemp;
    }

    // again, I am duplicating code.
    private double ExpectedConditionalLossInvP(double[] invPDefDate, ReadOnlySpan<double> mktFactor)
    {
        SortedDictionary<double, double> pIndepDistrib = ConditionalLossDistribInvP(invPDefDate, mktFactor);

        // get the expected value subject to the value of the market
        //   factor.
        double expLoss = 0.0;
        foreach (KeyValuePair<double, double> distIt in pIndepDistrib)
        {
            double loss = distIt.Key * lossUnit;
            loss = Math.Min(Math.Max(loss - attachAmount, 0.0), detachAmount - attachAmount);

            // MIN MAX BUGS ....???
            expLoss += loss * distIt.Value;
        }

        return expLoss;
    }

    private double[] ConditionalLossProb(ImmutableArray<double> pDefDate, ReadOnlySpan<double> mktFactor) =>
        [.. ConditionalLossDistrib(pDefDate, mktFactor).Values];
}
