// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/homogeneouspooldef.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The default loss distribution of a finite homogeneous pool by convolution: the conditional distributions of a
/// one-factor constant-loss latent model integrated over the factor on a midpoint grid.
/// </summary>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
/// <remarks>
/// <para>
/// The losses are split into buckets from 0 to the tranche's detachment, the atoms at their buckets' left edges, and
/// the expected tranche loss is a left Riemann sum of the excess probability: QuantLib's design, kept, 3% above the
/// recursive model's exact value on a [3%, 6%] tranche at 200 buckets and converging with the bucket count. Bound to a
/// basket with realized defaults, the model prices its live names over the restated tranche.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class HomogeneousPoolLossModel<TCopula, TInitTraits> : DefaultLossModel
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    private readonly int nBuckets;
    private readonly double min;
    private readonly int nSteps;
    private readonly double delta;
    private ConstantLossLatentModel<TCopula, TInitTraits> copula;
    private double attachAmount;
    private double detachAmount;
    private ImmutableArray<double> notionals;

    /// <summary>Initializes a new instance of the <see cref="HomogeneousPoolLossModel{TCopula, TInitTraits}"/>
    /// class.</summary>
    /// <param name="copula">The one-factor constant-loss latent model.</param>
    /// <param name="nBuckets">The number of buckets.</param>
    /// <param name="max">The factor grid's upper end.</param>
    /// <param name="min">The factor grid's lower end.</param>
    /// <param name="nSteps">The number of factor steps.</param>
    /// <exception cref="ArgumentNullException"><paramref name="copula"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The latent model has more than one factor ("Inhomogeneous model not
    /// implemented for multifactor", QuantLib's text).</exception>
    public HomogeneousPoolLossModel(
        ConstantLossLatentModel<TCopula, TInitTraits> copula, int nBuckets, double max = 5.0, double min = -5.0, int nSteps = 50)
    {
        ArgumentNullException.ThrowIfNull(copula);
        if (copula.NumFactors != 1)
        {
            throw new ArgumentException("Inhomogeneous model not implemented for multifactor", nameof(copula));
        }

        this.copula = copula;
        this.nBuckets = nBuckets;
        this.min = min;
        this.nSteps = nSteps;
        delta = (max - min) / nSteps;
    }

    /// <inheritdoc/>
    public override double ExpectedTrancheLoss(Date d) => LossDistrib(d).CumulativeExcessProbability(attachAmount, detachAmount);

    /// <inheritdoc/>
    public override double Percentile(Date d, double percentile)
    {
        double portfLoss = LossDistrib(d).ConfidenceLevel(percentile);
        return Math.Min(Math.Max(portfLoss - attachAmount, 0.0), detachAmount - attachAmount);
    }

    /// <inheritdoc/>
    public override double ExpectedShortfall(Date d, double percentile)
    {
        Distribution dist = LossDistrib(d);
        dist.Tranche(attachAmount, detachAmount);
        return dist.ExpectedShortfall(percentile);
    }

    /// <summary>The pool's loss distribution by a date, from 0 to the tranche's detachment.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The distribution.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected Distribution LossDistrib(Date d)
    {
        Basket basket = BoundBasket;
        var bucktLDistBuff = new LossDistHomogeneous(nBuckets, detachAmount);

        ImmutableArray<double> recoveries = copula.Recoveries;
        double[] lgd = new double[recoveries.Length];
        for (int i = 0; i < lgd.Length; i++)
        {
            lgd[i] = (1.0 - recoveries[i]) * notionals[i];
        }

        double[] prob = [.. basket.RemainingProbabilities(d)];
        for (int iName = 0; iName < prob.Length; iName++)
        {
            prob[iName] = copula.InverseCumulativeY(prob[iName], iName);
        }

        // integrate locally (1 factor).
        // use explicitly a 1D latent model object?
        var dist = new Distribution(nBuckets, 0.0, detachAmount);
        double[] mkft = [min + (delta / 2.0)];
        double[] conditionalProbs = new double[notionals.Length];
        for (int i = 0; i < nSteps; i++)
        {
            for (int iName = 0; iName < notionals.Length; iName++)
            {
                conditionalProbs[iName] = copula.ConditionalDefaultProbabilityInvP(prob[iName], iName, mkft);
            }

            Distribution bld = bucktLDistBuff.Evaluate(lgd, conditionalProbs);
            double densitydm = delta * copula.Density(mkft);

            // also, instead of calling the static method it could be wrapped
            // through an inlined call in the latent model
            for (int j = 0; j < nBuckets; j++)
            {
                dist.AddDensity(j, bld.Density(j) * densitydm);
            }

            mkft[0] += delta;
        }

        return dist;
    }

    /// <inheritdoc/>
    protected override void ResetModel()
    {
        // need to be capped now since the limit amounts might be over the
        //  remaining notional (think amortizing)
        Basket basket = BoundBasket;
        notionals = basket.RemainingNotionals();
        attachAmount = basket.RemainingAttachmentAmount();
        detachAmount = basket.RemainingDetachmentAmount();

        copula = (ConstantLossLatentModel<TCopula, TInitTraits>)copula.ResetBasket(SourceBasket);
    }
}
