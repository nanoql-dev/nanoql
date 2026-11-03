// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaultlossmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The abstract default loss model: <see cref="IDefaultLossModel"/>'s contract with the binding written once, QuantLib's
/// <c>DefaultLossModel</c>.
/// </summary>
/// <remarks>
/// <para>
/// An extension point. A model is an immutable configuration until it is bound: <see cref="SetBasket"/> copies it,
/// restricts the copy to the basket's live names (<see cref="RestrictTo"/>), binds the copy to the basket and runs its
/// <see cref="ResetModel"/> there, once. The copy is the model's own memberwise copy, so a derived model keeps its
/// state in fields it writes only while it is built, restricted or reset, and never after.
/// </para>
/// <para>
/// QuantLib's model holds a relinkable handle to its basket and resets when another basket links it, so a model set on
/// two baskets in turn can keep the first's precomputation (the recursive model's loss weights, the random models'
/// simulations). Each binding here is a fresh model's, bit for bit.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class DefaultLossModel : IDefaultLossModel
{
    private Basket? basket;
    private Basket? source;

    /// <summary>Gets the basket the model is bound to, or <see langword="null"/> before it is bound.</summary>
    public Basket? Basket => basket;

    /// <summary>Gets the basket the model is bound to, which a statistic reads.</summary>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected Basket BoundBasket => basket ?? throw new NanoQLException("No portfolio basket set.");

    /// <summary>Gets the basket as given to <see cref="SetBasket"/>, of every name, whose
    /// <see cref="Experimental.Basket.PricingBasket"/> the model is bound to: a model binding a latent model to it,
    /// which restricts itself to the live names.</summary>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    private protected Basket SourceBasket => source ?? throw new NanoQLException("No portfolio basket set.");

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="basket"/> is <see langword="null"/>.</exception>
    public IDefaultLossModel SetBasket(Basket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        var copy = (DefaultLossModel)MemberwiseClone();
        copy.RestrictTo(basket.ModelRows, basket.Size);
        copy.basket = basket.PricingBasket;
        copy.source = basket;
        copy.ResetModel();
        return copy;
    }

    /// <inheritdoc/>
    public virtual double ExpectedTrancheLoss(Date d) => throw new NanoQLException("expectedTrancheLoss Not implemented for this model.");

    /// <inheritdoc/>
    public virtual double ProbOverLoss(Date d, double lossFraction) => throw new NanoQLException("probOverLoss Not implemented for this model.");

    /// <inheritdoc/>
    public virtual double Percentile(Date d, double percentile) => throw new NanoQLException("percentile Not implemented for this model.");

    /// <inheritdoc/>
    public virtual double ExpectedShortfall(Date d, double percentile) => throw new NanoQLException("eSF Not implemented for this model.");

    /// <inheritdoc/>
    public virtual ImmutableArray<double> SplitVaRLevel(Date d, double loss) => throw new NanoQLException("splitVaRLevel Not implemented for this model.");

    /// <inheritdoc/>
    public virtual ImmutableArray<double> SplitESFLevel(Date d, double loss) => throw new NanoQLException("splitESFLevel Not implemented for this model.");

    /// <inheritdoc/>
    public virtual ImmutableSortedDictionary<double, double> LossDistribution(Date d) => throw new NanoQLException("lossDistribution Not implemented for this model.");

    /// <inheritdoc/>
    public virtual double DensityTrancheLoss(Date d, double lossFraction) => throw new NanoQLException("densityTrancheLoss Not implemented for this model.");

    /// <inheritdoc/>
    public virtual ImmutableArray<double> ProbsBeingNthEvent(int n, Date d) => throw new NanoQLException("probsBeingNthEvent Not implemented for this model.");

    /// <inheritdoc/>
    public virtual double DefaultCorrelation(Date d, int iName, int jName) => throw new NanoQLException("defaultCorrelation Not implemented for this model.");

    /// <inheritdoc/>
    public virtual double ProbAtLeastNEvents(int n, Date d) => throw new NanoQLException("probAtLeastNEvents Not implemented for this model.");

    /// <inheritdoc/>
    public virtual double ExpectedRecovery(Date d, int iName, DefaultProbKey defaultKey) => throw new NanoQLException("expected recovery Not implemented for this model.");

    /// <summary>The model's per-basket precomputation, run once on the bound copy: QuantLib's <c>resetModel</c>.</summary>
    protected abstract void ResetModel();

    /// <summary>
    /// Restricts the copy being bound to a basket's live names, in pool order, before <see cref="ResetModel"/>: a
    /// model holding a latent model or per-name data restricts it to their rows. By default, nothing.
    /// </summary>
    /// <param name="rows">The live names' positions in the basket's pool.</param>
    /// <param name="poolSize">The basket's number of names; <paramref name="rows"/> holds all of them where no name
    /// has defaulted.</param>
    protected virtual void RestrictTo(ImmutableArray<int> rows, int poolSize)
    {
    }
}
