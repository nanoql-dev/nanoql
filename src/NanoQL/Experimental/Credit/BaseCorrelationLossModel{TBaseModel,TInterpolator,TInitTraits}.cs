// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/basecorrelationlossmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The base correlation loss model, its interpolation by the portfolio's (live) amount percentage: the tranche's
/// expected loss is the difference of two equity tranches' to its attachment and detachment, each priced by a base
/// model at the correlation the surface gives at its level.
/// </summary>
/// <typeparam name="TBaseModel">The base model, built per correlation.</typeparam>
/// <typeparam name="TInterpolator">The surface's 2-D interpolator.</typeparam>
/// <typeparam name="TInitTraits">The base model's copula initialization parameters.</typeparam>
/// <remarks>
/// <para>
/// The literature is immense; beyond the introductory level (precrisis) see chapters 19, 20 and 21 of Dominic O'Kane,
/// "Modelling single name and multi-name credit derivatives", Wiley Finance, 2008. Freely available: "Credit
/// Correlation: A Guide", JP Morgan Credit Derivatives Strategy, 12 March 2004; "Introducing Base Correlations", JP
/// Morgan, 22 March 2004; "A Relative Value Framework for Credit Correlation", JP Morgan, 27 April 2004; "Valuing and
/// Hedging Synthetic CDO Tranches Using Base Correlations", Bear Stearns, 17 May 2004; "Correlation Primer", Nomura Fixed
/// Income Research, 6 August 2004; "Base Correlation Explained", Lehman Brothers Fixed Income Quantitative Credit
/// Research, 15 November 2004; "Pricing CDOs with a smile", Societe Generale Credit Research, February 2005; and for
/// bespoke base correlation "Base Correlation Mapping", Lehman Brothers' Quantitative Credit Research Quarterly, Volume
/// 2007-Q1. Typical postcrisis data is in JP Morgan's Global Correlation Daily Analytics.
/// </para>
/// <para>
/// The crisis problems, pricing stressed portfolios or tranches over the maximum loss, are the base models'
/// responsibility: users select a base model accordingly, a copula or a random loss given default one. This is not a
/// bespoke base correlation model (bespoke as to the basket's composition, not just its attachment levels), whose loss
/// interpolation is on the expected loss to match the two baskets: the surface should refer to the basket priced. To
/// be done, as in QuantLib: bespoke portfolio models. Most statistics are not implemented, not impossible, but the
/// model is intended for pricing rather than portfolio risk management.
/// </para>
/// <para>
/// The base models are QuantLib's four <c>setupModels</c> specialisations, each a type implementing
/// <see cref="IBaseCorrelationModel{TInitTraits}"/>, and immutable: each statistic builds the two at its correlations,
/// where QuantLib's sets the correlation quotes they observe. Bound to a basket with realized defaults, the equity
/// baskets hold the live names, their notionals and recoveries, over the restated tranche: QuantLib's take the live
/// notionals on the whole pool, which its basket refuses ("unmatched data entry sizes in basket";
/// <c>Divergences/BaseCorrelationLossModelTests.cs</c>). A correlation the surface gives outside [0, 1) is refused,
/// naming the date and level, where QuantLib's base model returns NaN below 0 and refuses above 1. An equity tranche's
/// attachment leg is 0, its basket [0, 0] not priced: QuantLib's prices it, so its LHP base refuses where the surface
/// extrapolates below 0 at level 0 and its inhomogeneous pool base throws on every equity tranche ("a out of range",
/// its own <c>\todo</c>: "is failing for equity tranches at least").
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class BaseCorrelationLossModel<TBaseModel, TInterpolator, TInitTraits> : DefaultLossModel
    where TBaseModel : IBaseCorrelationModel<TInitTraits>
    where TInterpolator : struct, IInterpolator2D
    where TInitTraits : struct
{
    private readonly BaseCorrelationTermStructure<TInterpolator> correlTS;

    // Initialization parameters for models copula
    private readonly TInitTraits copulaTraits;
    private ImmutableArray<double> recoveries;
    private double attachRatio;
    private double detachRatio;

    // the equity baskets
    private Basket? basketAttach;
    private Basket? basketDetach;

    /// <summary>Initializes a new instance of the
    /// <see cref="BaseCorrelationLossModel{TBaseModel, TInterpolator, TInitTraits}"/> class.</summary>
    /// <param name="correlTS">The base correlation surface.</param>
    /// <param name="recoveries">The names' recoveries.</param>
    /// <param name="traits">The base models' copula initialization parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="correlTS"/> or <paramref name="recoveries"/> is
    /// <see langword="null"/>.</exception>
    public BaseCorrelationLossModel(
        BaseCorrelationTermStructure<TInterpolator> correlTS, IReadOnlyList<double> recoveries, TInitTraits traits = default)
    {
        ArgumentNullException.ThrowIfNull(correlTS);
        ArgumentNullException.ThrowIfNull(recoveries);
        this.correlTS = correlTS;
        this.recoveries = [.. recoveries];
        copulaTraits = traits;
    }

    /// <summary>The expected tranche loss by a date: the detachment's equity tranche's less the attachment's, on the
    /// live part of the basket.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The loss, an amount.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket; the surface gives a correlation outside
    /// [0, 1) at the date and either level; or a base model refuses.</exception>
    public override double ExpectedTrancheLoss(Date d)
    {
        Basket attach = basketAttach ?? throw new NanoQLException("No portfolio basket set.");
        Basket detach = basketDetach!;
        double correlK2 = Correlation(d, detachRatio);

        /* reset correl and call base models which have the different baskets
        associated.*/
        // an equity tranche's attachment leg is 0, no basket priced and no correlation read
        double expLossK1 = attachRatio == 0.0
            ? 0.0
            : attach.SetLossModel(TBaseModel.ForCorrelation(Correlation(d, attachRatio), recoveries, copulaTraits)).ExpectedTrancheLoss(d);
        double expLossK2 = detach.SetLossModel(TBaseModel.ForCorrelation(correlK2, recoveries, copulaTraits)).ExpectedTrancheLoss(d);
        return expLossK2 - expLossK1;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The recoveries are fewer than the basket's names.</exception>
    protected override void RestrictTo(ImmutableArray<int> rows, int poolSize)
    {
        if (recoveries.Length < poolSize)
        {
            throw new NanoQLException("Incompatible basket and model sizes.");
        }

        if (rows.Length != poolSize)
        {
            recoveries = [.. rows.Select(i => recoveries[i])];
        }
    }

    /// <inheritdoc/>
    protected override void ResetModel()
    {
        // Update model caches after basket assignement.
        Basket basket = BoundBasket;
        double remainingNotional = basket.RemainingNotional();
        attachRatio = basket.RemainingAttachmentAmount() / remainingNotional;
        detachRatio = basket.RemainingDetachmentAmount() / remainingNotional;

        basketAttach = new Basket(
            basket.RefDate, basket.RemainingNames(), basket.RemainingNotionals(), basket.Pool, basket.Context, 0.0, attachRatio, basket.Claim);
        basketDetach = new Basket(
            basket.RefDate, basket.RemainingNames(), basket.RemainingNotionals(), basket.Pool, basket.Context, 0.0, detachRatio, basket.Claim);
    }

    private double Correlation(Date d, double lossLevel)
    {
        double correl = correlTS.Correlation(d, lossLevel);
        if (!(correl >= 0.0 && correl < 1.0))
        {
            throw new NanoQLException(string.Create(
                CultureInfo.InvariantCulture,
                $"base correlation {correl} at {d} and loss level {lossLevel} outside [0, 1): the base model can't price it"));
        }

        return correl;
    }
}
