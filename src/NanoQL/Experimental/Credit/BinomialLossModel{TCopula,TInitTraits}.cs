// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/binomiallossmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The binomial defaultable basket loss model: the portfolio loss distribution approximated by an adjusted binomial,
/// which fits the loss distribution's two moments and so allows the portfolio's inhomogeneity at little cost over the
/// LHP. See Dominic O'Kane, "Approximating Independent Loss Distributions with an Adjusted Binomial Distribution",
/// EDHEC Risk and Asset Management Research Centre, 2007, and "Modelling single name and multi-name credit
/// derivatives", chapter 18.5.2, Wiley Finance, 2008.
/// </summary>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
/// <remarks>
/// <para>
/// The version presented here is adapted to the multifactor case by computing a conditional binomial approximation;
/// the binomial is stable, so the model serves risk management as well as pricing. The copula is arbitrary, though the
/// model was designed for a Gaussian setup. QuantLib's template parameter is a loss latent model; the constant-loss
/// model is the only one with the conditional recovery it reads, so it is the model's. To be done, as in QuantLib: the
/// random recovery models, and the probability inversions the cumulative functions already computed.
/// </para>
/// <para>
/// The binomial's weight α mixes it with a two-point mass at the floor and ceiling of the expected count m, both of
/// mean m, so the conditional variance matches the pool's: α = (V − V₂)/(V_B − V₂) with V the pool's count variance,
/// V_B the binomial's and V₂ = (c − m)(m − f) the two-point mass's, all in QuantLib's units of a count over n². QuantLib
/// takes V₂'s place with −(c − m)² − ((f − m)² − c²)(c − m), not a variance, so its conditional distribution misses the
/// variance it exists to match, 10,656 for 11,718 on three names (the S-xcr plan's XR8 (xviii);
/// <c>Divergences/BinomialLossModelTests.cs</c>); on a homogeneous pool V = V_B and α = 1, QuantLib's, taken where V
/// equals V_B to 1e-12 in these units, since V_B − V₂ vanishes like the squared distance of the probability from 0
/// or 1, where the two variances' rounding differs most, and would move α (by 2e-7 at a distance of 1e-10); below
/// that difference the variance a weight of 1 misses is negligible.
/// </para>
/// <para>
/// Bound to a basket with realized defaults, the model prices its live names over the restated tranche (XR8 (ii)). The
/// statistics read the notionals of the names alive at the evaluation date: QuantLib's read those alive at the
/// statistic's date beside the evaluation date's recoveries, so a default programmed after the evaluation date reads
/// past a vector, undefined; here it reads QuantLib's model without the event. A percentile or shortfall whose search
/// leaves the loss distribution, which QuantLib reads before its start or past its end, is refused (XR8 (xxix)). The
/// percentile at 1 is the tranche's loss at the largest attainable loss, as its interior's are tranche losses, where
/// QuantLib's returns the pool's largest loss (<c>binomiallossmodel.hpp:355</c>; the S-xcr code review's C3 O1-1).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class BinomialLossModel<TCopula, TInitTraits> : DefaultLossModel
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    private ConstantLossLatentModel<TCopula, TInitTraits> copula;

    // cached arguments:
    // remaining basket magnitudes:
    private double attachAmount;
    private double detachAmount;

    /// <summary>Initializes a new instance of the <see cref="BinomialLossModel{TCopula, TInitTraits}"/>
    /// class.</summary>
    /// <param name="copula">The loss latent model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="copula"/> is <see langword="null"/>.</exception>
    public BinomialLossModel(ConstantLossLatentModel<TCopula, TInitTraits> copula)
    {
        ArgumentNullException.ThrowIfNull(copula);
        this.copula = copula;
    }

    /// <inheritdoc/>
    public override ImmutableSortedDictionary<double, double> LossDistribution(Date d)
    {
        var distrib = ImmutableSortedDictionary.CreateBuilder<double, double>();
        double[] lossPts = LossPoints(d);
        double[] values = ExpectedDistribution(d);
        double sum = 0.0;
        for (int i = 0; i < lossPts.Length; i++)
        {
            // capped, some situations giving a very small probability over 1
            distrib.TryAdd(lossPts[i], Math.Min(sum + values[i], 1.0));
            sum += values[i];
        }

        return distrib.ToImmutable();
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The search passes the distribution's last point, which QuantLib reads past its
    /// end.</exception>
    public override double Percentile(Date d, double percentile)
    {
        ImmutableSortedDictionary<double, double> dist = LossDistribution(d);
        double[] keys = [.. dist.Keys];
        double[] probs = [.. dist.Values];

        // \todo: Use some of the library interpolators instead
        if (probs[0] >= percentile)
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
            // the tranche's loss at the largest attainable loss, where QuantLib's returns the pool's
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
                throw new NanoQLException("Binomial model fails to calculate the percentile: past the loss distribution's end");
            }
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
    /// <exception cref="NanoQLException">The distribution reaches the percentile at its first point or past its
    /// second-to-last, where QuantLib reads before its start or past its end, or never ("Binomial model fails to
    /// calculate ESF.").</exception>
    public override double ExpectedShortfall(Date d, double percentile)
    {
        // taken from recursive since we have the distribution in both cases.
        if (d == BoundBasket.Context.EvaluationDate)
        {
            return 0.0;
        }

        return TrancheShortfall(LossDistribution(d), percentile, attachAmount, detachAmount, "Binomial", failsAtEnd: true);
    }

    /// <inheritdoc/>
    public override double ExpectedTrancheLoss(Date d)
    {
        double[] lossVals = LossPoints(d);
        ImmutableArray<double> notionals = BoundBasket.RemainingNotionals();
        double[] invProbs = InverseProbabilities(d);

        return copula.IntegratedExpectedValue(v1 => CondTrancheLoss(d, lossVals, notionals, invProbs, v1));
    }

    // The tranched shortfall of a cumulative loss distribution beyond a percentile, QuantLib's trapezoids on the
    // binomial and recursive models: a distribution that never reaches the percentile fails the binomial model and
    // reads 0 on the recursive one, and a search ending at either end of the distribution, where QuantLib reads before
    // its start or past its end, is refused.
    internal static double TrancheShortfall(
        ImmutableSortedDictionary<double, double> distrib,
        double perctl,
        double attachAmount,
        double detachAmount,
        string model,
        bool failsAtEnd)
    {
        double[] keys = [.. distrib.Keys];
        double[] probs = [.. distrib.Values];
        int itDist = 0;
        for (; itDist < keys.Length; ++itDist)
        {
            if (probs[itDist] >= perctl)
            {
                break;
            }
        }

        int itNxt = itDist;
        if (itNxt == keys.Length)
        {
            return failsAtEnd ? throw new NanoQLException($"{model} model fails to calculate ESF.") : 0.0;
        }

        if (itDist == 0)
        {
            throw new NanoQLException(
                $"{model} model fails to calculate ESF: the percentile is reached at the loss distribution's start");
        }

        if (itNxt + 1 == keys.Length)
        {
            throw new NanoQLException(
                $"{model} model fails to calculate ESF: the percentile is reached at the loss distribution's end");
        }

        --itDist;

        // \todo: I could linearly triangulate the exact point and get
        //    extra precission on the first(broken) period.
        double lossNxt = Math.Min(Math.Max(keys[itNxt] - attachAmount, 0.0), detachAmount - attachAmount);
        double lossHere = Math.Min(Math.Max(keys[itDist] - attachAmount, 0.0), detachAmount - attachAmount);

        double val = lossNxt - ((probs[itNxt] - perctl) * (lossNxt - lossHere) / (probs[itNxt] - probs[itDist]));
        double suma = (probs[itNxt] - perctl) * (lossNxt + val) * .5;
        ++itDist;
        ++itNxt;
        do
        {
            lossNxt = Math.Min(Math.Max(keys[itNxt] - attachAmount, 0.0), detachAmount - attachAmount);
            lossHere = Math.Min(Math.Max(keys[itDist] - attachAmount, 0.0), detachAmount - attachAmount);
            suma += .5 * (lossHere + lossNxt) * (probs[itNxt] - probs[itDist]);
            ++itDist;
            ++itNxt;
        }
        while (itNxt != keys.Length);
        return suma / (1.0 - perctl);
    }

    /// <summary>The probabilities of the loss values <see cref="LossPoints"/> gives, integrated over the
    /// factors.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The probabilities.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double[] ExpectedDistribution(Date date)
    {
        // precal date conditional magnitudes:
        ImmutableArray<double> notionals = BoundBasket.RemainingNotionals();
        double[] invProbs = InverseProbabilities(date);
        return copula.IntegratedExpectedValueV(v1 => LossProbability(date, notionals, invProbs, v1));
    }

    /// <summary>The attainable loss points this model provides: the counts 0 to n times the average loss.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The loss points, amounts.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double[] LossPoints(Date d)
    {
        Basket basket = BoundBasket;
        ImmutableArray<double> notionals = basket.RemainingNotionals();

        double aveLossFrct = copula.IntegratedExpectedValue(v1 => AverageLoss(d, notionals, v1));

        int dataSize = basket.RemainingSize() + 1;
        double[] data = new double[dataSize];

        // use std::algorithm
        double outsNot = basket.RemainingNotional();
        for (int i = 0; i < dataSize; i++)
        {
            data[i] = i * aveLossFrct * outsNot;
        }

        return data;
    }

    /// <summary>The average loss per credit conditional on the factors, a fraction of the live notional.</summary>
    /// <param name="d">The date.</param>
    /// <param name="reminingNots">The live names' notionals.</param>
    /// <param name="mktFctrs">The factors' values.</param>
    /// <returns>The average loss fraction.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double AverageLoss(Date d, ImmutableArray<double> reminingNots, ReadOnlySpan<double> mktFctrs)
    {
        int bsktSize = BoundBasket.RemainingSize();
        /* The conditional loss per unit notional of each name at time 'date'
            The spot recovery model is returning for all i's:
            \frac{\int_0^t  [1-rr_i(\tau; \xi)] P_{def-i}(0, \tau; \xi) d\tau}
                 {P_{def-i}(0,t;\xi)}
            and the constant recovery model is simply returning:
            1-RR_i
        */
        double[] fractionalEL = ExpConditionalLgd(d, mktFctrs);
        double notBskt = 0.0;
        foreach (double notional in reminingNots)
        {
            notBskt += notional;
        }

        double lgdsLeft = 0.0;
        for (int i = 0; i < fractionalEL.Length; i++)
        {
            lgdsLeft += fractionalEL[i] * reminingNots[i];
        }

        return lgdsLeft / (bsktSize * notBskt);
    }

    /// <summary>The expected tranche loss conditional on the factors.</summary>
    /// <param name="d">The date.</param>
    /// <param name="lossVals">The loss points.</param>
    /// <param name="bsktNots">The live names' notionals.</param>
    /// <param name="uncondDefProbsInv">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="mkf">The factors' values.</param>
    /// <returns>The conditional expected tranche loss.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double CondTrancheLoss(
        Date d, double[] lossVals, ImmutableArray<double> bsktNots, double[] uncondDefProbsInv, ReadOnlySpan<double> mkf)
    {
        double[] condLProb = LossProbability(d, bsktNots, uncondDefProbsInv, mkf);

        // \to do: move to a do-while over attach to detach
        double suma = 0.0;
        for (int i = 0; i < lossVals.Length; i++)
        {
            suma += condLProb[i] * Math.Min(Math.Max(lossVals[i] - attachAmount, 0.0), detachAmount - attachAmount);
        }

        return suma;
    }

    /// <summary>The live names' fractional losses given default conditional on the factors (expected as in time
    /// value, not average, see the literature).</summary>
    /// <param name="d">The date.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The conditional losses given default.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double[] ExpConditionalLgd(Date d, ReadOnlySpan<double> mktFactors)
    {
        ImmutableArray<int> evalDateLives = BoundBasket.LiveList();
        double[] condLgds = new double[evalDateLives.Length];
        for (int i = 0; i < condLgds.Length; i++)
        {
            condLgds[i] = 1.0 - copula.ConditionalRecovery(d, evalDateLives[i], mktFactors);
        }

        return condLgds;
    }

    /// <summary>The loss count's probability density conditional on the factors: the adjusted binomial, where the
    /// burden of the algorithm's setup lies.</summary>
    /// <param name="date">The date.</param>
    /// <param name="bsktNots">The expected exposures at the date; with no wrong way risk the exposure doesn't depend on
    /// the factors.</param>
    /// <param name="uncondDefProbInv">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The probabilities of 0 to n defaults.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double[] LossProbability(
        Date date, ImmutableArray<double> bsktNots, double[] uncondDefProbInv, ReadOnlySpan<double> mktFactors)
    {
        // the model as it is does not model the exposures conditional to the
        //   mkt factr, otherwise this needs revision
        // model does not take the unconditional rr
        int bsktSize = BoundBasket.RemainingSize();
        /* The conditional loss per unit notional of each name at time 'date'
            The spot recovery model is returning for all i's:
            \frac{\int_0^t  [1-rr_i(\tau; \xi)] P_{def-i}(0, \tau; \xi) d\tau}
                 {P_{def-i}(0,t;\xi)}
            and the constant recovery model is simply returning:
            1-RR_i
        */
        // conditional fractional LGD expected as given by the recovery model
        //   for the ramaining(live) names at the current eval date.
        double[] fractionalEL = ExpConditionalLgd(date, mktFactors);
        double[] lgdsLeft = new double[fractionalEL.Length];
        double sumLgds = 0.0;
        for (int i = 0; i < lgdsLeft.Length; i++)
        {
            lgdsLeft[i] = fractionalEL[i] * bsktNots[i];
            sumLgds += lgdsLeft[i];
        }

        double avgLgd = sumLgds / bsktSize;

        double[] condDefProb = new double[bsktSize];
        for (int j = 0; j < bsktSize; j++)
        {
            // transform
            condDefProb[j] = copula.ConditionalDefaultProbabilityInvP(uncondDefProbInv[j], j, mktFactors);
        }

        // of full portfolio:
        double avgProb;
        if (avgLgd <= NumericLimits.QL_EPSILON)
        {
            // only if all are 0
            avgProb = 0.0;
        }
        else
        {
            double inner = 0.0;
            for (int j = 0; j < bsktSize; j++)
            {
                inner += condDefProb[j] * lgdsLeft[j];
            }

            avgProb = inner / (avgLgd * bsktSize);
        }

        // model parameters:
        double m = avgProb * bsktSize;
        double floorAveProb = Math.Min(bsktSize - 1, Math.Floor(m));
        double ceilAveProb = floorAveProb + 1.0;

        // nu_A
        double varianceBinom = avgProb * (1.0 - avgProb) / bsktSize;

        // nu_E
        double variance = 0.0;
        for (int j = 0; j < bsktSize; j++)
        {
            // breaks condDefProb and lgdsLeft to spare memory
            condDefProb[j] *= 1.0 - condDefProb[j];
            lgdsLeft[j] *= lgdsLeft[j];
            variance += condDefProb[j] * lgdsLeft[j];
        }

        variance = avgLgd <= NumericLimits.QL_EPSILON ? 0.0 : variance / (bsktSize * bsktSize * avgLgd * avgLgd);

        // the two-point mass's variance, (c - m)(m - f) counts squared, in the units of a count over n squared
        // times n; where the pool's variance is the binomial's to 1e-12 in these units (a homogeneous pool, to
        // rounding), the weight is 1, QuantLib's: the denominator vanishes like the squared distance of the
        // probability from 0 or 1, and the variances' rounding differs most there
        double sumAves = -((ceilAveProb - m) * (m - floorAveProb)) / bsktSize;
        double alpha = Math.Abs((variance - varianceBinom) * bsktSize) <= 1e-12
            ? 1.0
            : ((variance * bsktSize) + sumAves) / ((varianceBinom * bsktSize) + sumAves);

        // Full distribution:
        // ....DO SOMETHING CHEAPER at least go up to the loss tranche limit.
        double[] lossProbDensity = new double[bsktSize + 1];
        if (avgProb >= 1.0 - NumericLimits.QL_EPSILON)
        {
            lossProbDensity[bsktSize] = 1.0;
        }
        else if (avgProb <= NumericLimits.QL_EPSILON)
        {
            lossProbDensity[0] = 1.0;
        }
        else
        {
            /* FIX ME: With high default probabilities one only gets tiny values
            at the end and the sum of probabilities in the
            conditional distribution does not add up to one. It might be due to
            the fact that recursion should be done in the other direction as
            pointed out in the book. This is numerical.
            */
            double probsRatio = avgProb / (1.0 - avgProb);
            lossProbDensity[0] = Math.Pow(1.0 - avgProb, bsktSize);
            for (int i = 1; i < bsktSize + 1; i++)
            {
                // recursive to avoid factorial
                lossProbDensity[i] = lossProbDensity[i - 1] * probsRatio * (bsktSize - i + 1.0) / i;
            }

            // redistribute probability:
            for (int i = 0; i < bsktSize + 1; i++)
            {
                lossProbDensity[i] *= alpha;
            }

            // adjust average
            double epsilon = (1.0 - alpha) * (ceilAveProb - m);
            double epsilonPlus = 1.0 - alpha - epsilon;
            lossProbDensity[(int)floorAveProb] += epsilon;
            lossProbDensity[(int)ceilAveProb] += epsilonPlus;
        }

        return lossProbDensity;
    }

    /// <inheritdoc/>
    protected override void ResetModel()
    {
        /* say there are defaults and these havent settled... and this is
        the engine to compute them.... is this the wrong place?:*/
        Basket basket = BoundBasket;
        attachAmount = basket.RemainingAttachmentAmount();
        detachAmount = basket.RemainingDetachmentAmount();

        copula = (ConstantLossLatentModel<TCopula, TInitTraits>)copula.ResetBasket(SourceBasket); // forces interface
    }

    private double[] InverseProbabilities(Date d)
    {
        double[] invProbs = [.. BoundBasket.RemainingProbabilities(d)];
        for (int iName = 0; iName < invProbs.Length; iName++)
        {
            invProbs[iName] = copula.InverseCumulativeY(invProbs[iName], iName);
        }

        return invProbs;
    }
}
