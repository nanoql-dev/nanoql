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
/// A default loss model: the basket's contract with the algorithms that give its joint loss statistics (latent models,
/// top-down models). QuantLib's <c>DefaultLossModel</c>, whose abstract class form is <see cref="DefaultLossModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// A model is an immutable configuration until it is bound: <see cref="SetBasket"/> returns a copy bound to a basket,
/// its per-basket precomputation run once there (QuantLib's <c>resetModel</c>), and a basket's
/// <see cref="Experimental.Basket.SetLossModel"/> binds it (the S-xcr plan's XR1). Every statistic is a member with a
/// body that refuses, with QuantLib's text, unless the model implements it; QuantLib's protected statistics are public
/// here, the basket reading them through this interface (XR2 (a)).
/// </para>
/// <para>
/// An interface, beside the abstract class, so that a model with another base implements the contract over it, as
/// QuantLib's constant-loss and Gaussian LHP models derive their latent model beside the loss model.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public interface IDefaultLossModel
{
    /// <summary>Gets the basket the model is bound to, or <see langword="null"/> before it is bound.</summary>
    Basket? Basket { get; }

    /// <summary>A copy of the model bound to a basket: to its live names, over its restated tranche where names have
    /// defaulted.</summary>
    /// <param name="basket">The basket.</param>
    /// <returns>The bound model.</returns>
    IDefaultLossModel SetBasket(Basket basket);

    /// <summary>The expected tranche loss by a date, an amount.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The loss.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    double ExpectedTrancheLoss(Date d) => throw new NanoQLException("expectedTrancheLoss Not implemented for this model.");

    /// <summary>The probability of the tranche losing a fraction of its notional or more.</summary>
    /// <param name="d">The date.</param>
    /// <param name="lossFraction">The fraction of the tranche's notional.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    double ProbOverLoss(Date d, double lossFraction) => throw new NanoQLException("probOverLoss Not implemented for this model.");

    /// <summary>The value at risk at a percentile of the default loss.</summary>
    /// <param name="d">The date.</param>
    /// <param name="percentile">The percentile.</param>
    /// <returns>The loss.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    double Percentile(Date d, double percentile) => throw new NanoQLException("percentile Not implemented for this model.");

    /// <summary>The expected shortfall at a percentile of the default loss.</summary>
    /// <param name="d">The date.</param>
    /// <param name="percentile">The percentile.</param>
    /// <returns>The shortfall.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    double ExpectedShortfall(Date d, double percentile) => throw new NanoQLException("eSF Not implemented for this model.");

    /// <summary>Each name's share of a value-at-risk loss level.</summary>
    /// <param name="d">The date.</param>
    /// <param name="loss">The loss level.</param>
    /// <returns>The shares, by live name.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    ImmutableArray<double> SplitVaRLevel(Date d, double loss) => throw new NanoQLException("splitVaRLevel Not implemented for this model.");

    /// <summary>Each name's share of an expected-shortfall loss level.</summary>
    /// <param name="d">The date.</param>
    /// <param name="loss">The loss level.</param>
    /// <returns>The shares, by live name.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    ImmutableArray<double> SplitESFLevel(Date d, double loss) => throw new NanoQLException("splitESFLevel Not implemented for this model.");

    /// <summary>The full loss distribution: each loss level's probability.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The distribution.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    ImmutableSortedDictionary<double, double> LossDistribution(Date d) => throw new NanoQLException("lossDistribution Not implemented for this model.");

    /// <summary>The probability density of a loss fraction of the basket's notional.</summary>
    /// <param name="d">The date.</param>
    /// <param name="lossFraction">The fraction.</param>
    /// <returns>The density.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    double DensityTrancheLoss(Date d, double lossFraction) => throw new NanoQLException("densityTrancheLoss Not implemented for this model.");

    /// <summary>Each live name's probability of having defaulted by a date as the n-th default of the basket, in pool
    /// order.</summary>
    /// <param name="n">The default's order.</param>
    /// <param name="d">The date.</param>
    /// <returns>The probabilities.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    ImmutableArray<double> ProbsBeingNthEvent(int n, Date d) => throw new NanoQLException("probsBeingNthEvent Not implemented for this model.");

    /// <summary>Pearson's correlation of two names' default indicators by a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="iName">The first name.</param>
    /// <param name="jName">The second name.</param>
    /// <returns>The correlation.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    double DefaultCorrelation(Date d, int iName, int jName) => throw new NanoQLException("defaultCorrelation Not implemented for this model.");

    /// <summary>The probability of a number of defaults or more in the basket by a date.</summary>
    /// <param name="n">The number.</param>
    /// <param name="d">The date.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    double ProbAtLeastNEvents(int n, Date d) => throw new NanoQLException("probAtLeastNEvents Not implemented for this model.");

    /// <summary>A name's expected recovery rate conditional on its default by a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="iName">The name.</param>
    /// <param name="defaultKey">The name's default key.</param>
    /// <returns>The recovery.</returns>
    /// <exception cref="NanoQLException">The model doesn't implement it.</exception>
    double ExpectedRecovery(Date d, int iName, DefaultProbKey defaultKey) => throw new NanoQLException("expected recovery Not implemented for this model.");
}
