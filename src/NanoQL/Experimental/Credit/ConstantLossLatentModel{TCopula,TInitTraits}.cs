// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/constantlosslatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The constant-loss default latent model: each name's loss given default a deterministic constant, its
/// recovery; integrable. QuantLib's <c>ConstantLossLatentmodel</c>.</summary>
/// <remarks>Unsealed and immutable; restricted to a basket's live names, it keeps their recoveries (the S-xcr plan's
/// XR2 (f)).</remarks>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
[Experimental("NQL0001")]
public class ConstantLossLatentModel<TCopula, TInitTraits> : DefaultLatentModel<TCopula, TInitTraits>
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    private ImmutableArray<double> recoveries;

    /// <summary>Initializes a new instance of the <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/>
    /// class.</summary>
    /// <param name="factorWeights">The weights of each variable on the independent factors.</param>
    /// <param name="recoveries">Each name's recovery.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recoveries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">As <see cref="DefaultLatentModel{TCopula, TInitTraits}"/>'s constructor, or
    /// the recoveries don't number the variables ("Incompatible factors and recovery sizes.").</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="DefaultLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    public ConstantLossLatentModel(
        IReadOnlyList<IReadOnlyList<double>> factorWeights,
        IReadOnlyList<double> recoveries,
        LatentModelIntegrationType integralType,
        TInitTraits initTraits = default)
        : base(factorWeights, integralType, initTraits)
    {
        ArgumentNullException.ThrowIfNull(recoveries);
        if (recoveries.Count != factorWeights.Count)
        {
            throw new ArgumentException("Incompatible factors and recovery sizes.", nameof(recoveries));
        }

        this.recoveries = [.. recoveries];
    }

    /// <summary>Initializes a new instance of the <see cref="ConstantLossLatentModel{TCopula, TInitTraits}"/> class on
    /// one factor of a market correlation: QuantLib's quote constructor.</summary>
    /// <param name="correlation">The correlation, whose square root is every variable's weight.</param>
    /// <param name="recoveries">Each name's recovery.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="nVariables">The number of variables.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recoveries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">As <see cref="DefaultLatentModel{TCopula, TInitTraits}"/>'s constructor, or
    /// the recoveries don't number the variables ("Incompatible model and recovery sizes.").</exception>
    /// <exception cref="ArgumentOutOfRangeException">As <see cref="DefaultLatentModel{TCopula, TInitTraits}"/>'s
    /// constructor.</exception>
    public ConstantLossLatentModel(
        double correlation,
        IReadOnlyList<double> recoveries,
        LatentModelIntegrationType integralType,
        int nVariables,
        TInitTraits initTraits = default)
        : base(correlation, nVariables, integralType, initTraits)
    {
        ArgumentNullException.ThrowIfNull(recoveries);

        // actually one could define the other and get rid of the variable
        // here and in other similar models
        if (recoveries.Count != nVariables)
        {
            throw new ArgumentException("Incompatible model and recovery sizes.", nameof(recoveries));
        }

        this.recoveries = [.. recoveries];
    }

    /// <summary>Gets each name's recovery.</summary>
    public ImmutableArray<double> Recoveries => recoveries;

    /// <summary>A name's recovery conditional on the factors at a date: the constant.</summary>
    /// <param name="d">The date.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The recovery.</returns>
    public double ConditionalRecovery(Date d, int iName, ReadOnlySpan<double> mktFactors) => recoveries[iName];

    /// <summary>A name's recovery conditional on the factors, given its unconditional default probability: the
    /// constant.</summary>
    /// <param name="uncondDefP">The unconditional default probability.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The recovery.</returns>
    public double ConditionalRecovery(double uncondDefP, int iName, ReadOnlySpan<double> mktFactors) => recoveries[iName];

    /// <summary>A name's recovery conditional on the factors, given the inverse cumulative of its unconditional default
    /// probability: the constant.</summary>
    /// <param name="invUncondDefP">The inverse cumulative of the unconditional default probability.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The recovery.</returns>
    public double ConditionalRecoveryInvP(double invUncondDefP, int iName, ReadOnlySpan<double> mktFactors) => recoveries[iName];

    /// <summary>A name's recovery given a sample of its latent variable at a date: the constant.</summary>
    /// <param name="latentVarSample">The latent variable's value.</param>
    /// <param name="iName">The name.</param>
    /// <param name="d">The date.</param>
    /// <returns>The recovery.</returns>
    public double ConditionalRecovery(double latentVarSample, int iName, Date d) => recoveries[iName];

    /// <summary>A name's expected recovery given its default by a date: the constant.</summary>
    /// <param name="d">The date.</param>
    /// <param name="iName">The name.</param>
    /// <param name="defaultKey">The name's default key, unused.</param>
    /// <returns>The recovery.</returns>
    public double ExpectedRecovery(Date d, int iName, DefaultProbKey defaultKey) => recoveries[iName];

    /// <inheritdoc/>
    private protected override void RestrictRows(ImmutableArray<int> rows)
    {
        base.RestrictRows(rows);
        recoveries = [.. rows.Select(i => recoveries[i])];
    }
}
