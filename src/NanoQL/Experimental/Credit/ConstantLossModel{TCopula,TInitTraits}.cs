// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/constantlosslatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The constant-loss latent model as a default loss model: it gives no loss distribution, having no integration of
/// one, but prices digital products such as n-th-to-default swaps through its event probabilities, default correlations
/// and recoveries.
/// </summary>
/// <remarks>
/// Unsealed and immutable. QuantLib's derives the latent model and the loss model both; here it derives the latent
/// model and implements <see cref="IDefaultLossModel"/> over it (the S-xcr plan's XR2 (a)): its correlations, event
/// counts and recoveries are the latent model's, its other statistics refuse with QuantLib's texts.
/// <see cref="SetBasket"/> is QuantLib's <c>resetModel</c>, the latent model bound to the basket's live names.
/// </remarks>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
[Experimental("NQL0001")]
public class ConstantLossModel<TCopula, TInitTraits> : ConstantLossLatentModel<TCopula, TInitTraits>, IDefaultLossModel
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    /// <summary>Initializes a new instance of the <see cref="ConstantLossModel{TCopula, TInitTraits}"/> class.</summary>
    /// <param name="factorWeights">The weights of each variable on the independent factors.</param>
    /// <param name="recoveries">Each name's recovery.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recoveries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">As <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    public ConstantLossModel(
        IReadOnlyList<IReadOnlyList<double>> factorWeights,
        IReadOnlyList<double> recoveries,
        LatentModelIntegrationType integralType,
        TInitTraits initTraits = default)
        : base(factorWeights, recoveries, integralType, initTraits)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConstantLossModel{TCopula, TInitTraits}"/> class on one
    /// factor of a market correlation: QuantLib's quote constructor.</summary>
    /// <param name="correlation">The correlation, whose square root is every variable's weight.</param>
    /// <param name="recoveries">Each name's recovery.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="nVariables">The number of variables.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recoveries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">As <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    public ConstantLossModel(
        double correlation,
        IReadOnlyList<double> recoveries,
        LatentModelIntegrationType integralType,
        int nVariables,
        TInitTraits initTraits = default)
        : base(correlation, recoveries, integralType, nVariables, initTraits)
    {
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="basket"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The basket's size isn't the model's, or every name in it has
    /// defaulted.</exception>
    public IDefaultLossModel SetBasket(Basket basket) =>

        // update the default latent model we derive from
        (IDefaultLossModel)ResetBasket(basket);
}
