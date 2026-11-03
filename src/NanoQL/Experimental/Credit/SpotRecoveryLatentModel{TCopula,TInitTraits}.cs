// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/spotlosslatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: expectedLoss, conditionalExpLossRR and conditionalExpLossRRInv, never ported: they call conditionalRecoveryInvPinvRR, which QuantLib declares nowhere, so QuantLib never compiles them

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The random spot recovery rate latent variable portfolio model: N. Bennani and J. Maetz, "A Spot Stochastic Recovery
/// Extension of the Gaussian Copula", MPRA July 2009; H. Li, "Extension of Spot Recovery model for Gaussian Copula",
/// MPRA October 2009; adapted to many factors and any copula. Its variables are the n names' defaults, then their n
/// recoveries.
/// </summary>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
/// <remarks>
/// <para>
/// Immutable: <see cref="ResetBasket"/> returns a copy bound to a basket's live names, default row i with recovery row
/// i + n kept, the live count the new n. A model parameter A of 0 is refused: the conditional recovery divides by it
/// (<c>spotlosslatentmodel.hpp:271-276</c>). A default probability before a curve's reference date reads 0.
/// </para>
/// <para>
/// The conditional recovery is QuantLib's formula, whose documentation claims the single-name expected loss
/// pdef·(1 − R) and which reads E[R | default] 0.361 for R = 0.40 at A = 0.8, at equal betas too: kept, recorded for
/// revisiting.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class SpotRecoveryLatentModel<TCopula, TInitTraits> : LatentModel<TCopula, TInitTraits>
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    private readonly double modelA;
    private readonly LMIntegration integration;
    private ImmutableArray<double> recoveries;

    // products of default and recoveries factors, see refs ('covariances')
    private ImmutableArray<double> crossIdiosyncFctrs;
    private int numNames;
    private Basket? basket;

    /// <summary>Initializes a new instance of the <see cref="SpotRecoveryLatentModel{TCopula, TInitTraits}"/>
    /// class.</summary>
    /// <param name="factorWeights">The weights of the n default variables, then of the n recovery variables.</param>
    /// <param name="recoveries">The names' recoveries.</param>
    /// <param name="modelA">The model's parameter A, not 0.</param>
    /// <param name="integralType">The integration's type.</param>
    /// <param name="initTraits">The policy's initialization parameters.</param>
    /// <exception cref="ArgumentNullException">A list is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The weights' rows are odd in number, or the recoveries don't match the
    /// names (QuantLib's texts); A is 0.</exception>
    public SpotRecoveryLatentModel(
        IReadOnlyList<IReadOnlyList<double>> factorWeights,
        IReadOnlyList<double> recoveries,
        double modelA,
        LatentModelIntegrationType integralType,
        TInitTraits initTraits = default)
        : base(factorWeights, initTraits)
    {
        ArgumentNullException.ThrowIfNull(recoveries);
        EnumArguments.Check(integralType, nameof(integralType));
        numNames = factorWeights.Count / 2;
        if (factorWeights.Count % 2 != 0)
        {
            throw new ArgumentException("Number of RR variables must be equal to number of default variables", nameof(factorWeights));
        }

        if (recoveries.Count != numNames)
        {
            throw new ArgumentException("Number of recoveries does not match number of defaultable entities.", nameof(recoveries));
        }

        if (modelA == 0.0)
        {
            throw new ArgumentException("the spot recovery model's parameter A must not be 0: its conditional recovery divides by it", nameof(modelA));
        }

        this.recoveries = [.. recoveries];
        this.modelA = modelA;
        integration = LMIntegration.Create(factorWeights[0].Count, integralType);

        // reminder: first betas are default, last ones are recovery
        var cross = ImmutableArray.CreateBuilder<double>(numNames);
        for (int iName = 0; iName < numNames; iName++)
        {
            /* Corresponds to: (k denotes factor, i denotes modelled
                variable -default and recoveries))
                \sum_k a^2_{i,k} a^2_{N+i,k}
            */
            double cumul = 0.0;
            for (int iB = 0; iB < factorWeights[iName].Count; iB++)
            {
                // actually this size is unique
                cumul += factorWeights[iName][iB] * factorWeights[iName][iB]
                    * factorWeights[iName + numNames][iB] * factorWeights[iName + numNames][iB];
            }

            cross.Add(cumul);
        }

        crossIdiosyncFctrs = cross.MoveToImmutable();
    }

    /// <summary>Gets the basket the model is bound to, or <see langword="null"/> before it is bound.</summary>
    public Basket? Basket => basket;

    /// <summary>Gets the names' recoveries.</summary>
    public ImmutableArray<double> Recoveries => recoveries;

    /// <inheritdoc/>
    protected override LMIntegration Integration => integration;

    private Basket BoundBasket => basket ?? throw new NanoQLException("No portfolio basket set.");

    /// <summary>A copy of the model bound to a basket of its names: to the basket's live names, over its restated
    /// tranche where names have defaulted, the copy keeping their default and recovery rows.</summary>
    /// <param name="basket">The basket, of the model's number of names.</param>
    /// <returns>The bound copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="basket"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The basket's size isn't the model's number of names ("Incompatible new basket
    /// and model sizes."), or every name in it has defaulted.</exception>
    public SpotRecoveryLatentModel<TCopula, TInitTraits> ResetBasket(Basket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);

        // in the future change 'size' to 'liveSize'
        if (basket.Size != numNames)
        {
            throw new NanoQLException("Incompatible new basket and model sizes.");
        }

        Basket pricing = basket.PricingBasket
            ?? throw new NanoQLException("every name in the basket has defaulted: no loss model can be bound");
        var copy = (SpotRecoveryLatentModel<TCopula, TInitTraits>)MemberwiseClone();
        if (basket.ModelRows.Length != numNames)
        {
            ImmutableArray<int> rows = basket.ModelRows;
            copy.RestrictRows([.. rows, .. rows.Select(i => i + numNames)]);
            copy.recoveries = [.. rows.Select(i => recoveries[i])];
            copy.crossIdiosyncFctrs = [.. rows.Select(i => crossIdiosyncFctrs[i])];
            copy.numNames = rows.Length;
        }

        copy.basket = pricing;
        return copy;
    }

    /// <summary>A name's default probability by a date conditional on the factors' values.</summary>
    /// <param name="date">The date.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public double ConditionalDefaultProbability(Date date, int iName, ReadOnlySpan<double> mktFactors) =>
        ConditionalDefaultProbability(Curve(iName).AccruedDefaultProbability(date), iName, mktFactors);

    /// <summary>A name's default probability conditional on the factors' values, from its unconditional probability;
    /// below 1e-10, 0, sparing the inversion.</summary>
    /// <param name="prob">The unconditional probability.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The probability.</returns>
    public double ConditionalDefaultProbability(double prob, int iName, ReadOnlySpan<double> mktFactors)
    {
        /*Avoid redundant call to minimum value inversion (might be \infty),
        and this independently of the copula function.
        */
        if (prob < 1.0e-10)
        {
            return 0.0; // use library macro...
        }

        return ConditionalDefaultProbabilityInvP(InverseCumulativeY(prob, iName), iName, mktFactors);
    }

    /// <summary>A name's default probability conditional on the factors' values, from its unconditional probability's
    /// inverse.</summary>
    /// <param name="invCumYProb">The inverse of the unconditional probability.</param>
    /// <param name="iName">The name.</param>
    /// <param name="m">The factors' values.</param>
    /// <returns>The probability.</returns>
    public double ConditionalDefaultProbabilityInvP(double invCumYProb, int iName, ReadOnlySpan<double> m)
    {
        ImmutableArray<double> w = FactorWeights[iName];
        double sumMs = 0.0;
        for (int k = 0; k < w.Length; k++)
        {
            sumMs += w[k] * m[k];
        }

        return CumulativeZ((invCumYProb - sumMs) / IdiosyncFctrs[iName]);
    }

    /// <summary>
    /// A name's expected spot recovery by a date conditional on the factors' values and on its default: the
    /// multifactor generalization of H. Li's eq. 44 (p. 15), the integrated attainable recovery values.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The recovery.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public double ExpCondRecovery(Date d, int iName, ReadOnlySpan<double> mktFactors) =>
        ExpCondRecoveryP(Curve(iName).AccruedDefaultProbability(d), iName, mktFactors);

    /// <summary>A name's expected spot recovery conditional on the factors' values and on its default, from its
    /// unconditional default probability.</summary>
    /// <param name="uncondDefP">The unconditional default probability.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The recovery.</returns>
    public double ExpCondRecoveryP(double uncondDefP, int iName, ReadOnlySpan<double> mktFactors) =>
        ExpCondRecoveryInvPinvRR(
            InverseCumulativeY(uncondDefP, iName), InverseCumulativeY(recoveries[iName], iName + numNames), iName, mktFactors);

    /// <summary>A name's expected spot recovery conditional on the factors' values and on its default, from the
    /// inverses of its unconditional default probability and recovery.</summary>
    /// <param name="invUncondDefP">The default probability's inverse.</param>
    /// <param name="invUncondRR">The recovery's inverse.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The recovery.</returns>
    public double ExpCondRecoveryInvPinvRR(double invUncondDefP, double invUncondRR, int iName, ReadOnlySpan<double> mktFactors)
    {
        ImmutableArray<ImmutableArray<double>> fctrs = FactorWeights;
        double sumMs = 0.0;
        for (int k = 0; k < fctrs[iName].Length; k++)
        {
            sumMs += fctrs[iName][k] * mktFactors[k];
        }

        double sumBetaLoss = 0.0;
        for (int k = 0; k < fctrs[iName + numNames].Length; k++)
        {
            sumBetaLoss += fctrs[iName + numNames][k] * fctrs[iName + numNames][k];
        }

        return CumulativeZ(
            (sumMs + (Math.Sqrt(1.0 - crossIdiosyncFctrs[iName]) * Math.Sqrt(1.0 + (modelA * modelA)) * invUncondRR)
                - (Math.Sqrt(crossIdiosyncFctrs[iName]) * invUncondDefP))
            / Math.Sqrt(1.0 - sumBetaLoss + (modelA * modelA * (1.0 - crossIdiosyncFctrs[iName]))));
    }

    /// <summary>
    /// A name's spot recovery on a latent sample leading to its default (H. Li's equation 42, p. 14, second): the
    /// sample isn't checked to lead to a default; for use within a simulation. Below a default probability of 1e-10,
    /// 0.
    /// </summary>
    /// <param name="latentVarSample">The name's recovery variable's sample.</param>
    /// <param name="iName">The name.</param>
    /// <param name="d">The default's date.</param>
    /// <returns>The recovery.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public double ConditionalRecovery(double latentVarSample, int iName, Date d)
    {
        // retrieve the default probability for this name
        DefaultProbabilityTermStructure dfts = Curve(iName);
        double pdef = d < dfts.ReferenceDate ? 0.0 : dfts.DefaultProbability(d, true);

        // before asking for -\infty
        if (pdef < 1.0e-10)
        {
            return 0.0;
        }

        int iRecovery = iName + numNames; // should be live pool
        return CumulativeY(
            ((latentVarSample - (Math.Sqrt(crossIdiosyncFctrs[iName]) * InverseCumulativeY(pdef, iName)))
                / (modelA * Math.Sqrt(1.0 - crossIdiosyncFctrs[iName])))

            // cache the sqrts
            // cache this factor.
            + (Math.Sqrt(1.0 + (1.0 / (modelA * modelA))) * InverseCumulativeY(recoveries[iName], iRecovery)),
            iRecovery);
    }

    /// <summary>A name's recovery variable's value on a sample of every factor, systemic and idiosyncratic (default
    /// and recovery); meaningful only on a sample leading to the name's default.</summary>
    /// <param name="allFactors">The sample.</param>
    /// <param name="iName">The name.</param>
    /// <returns>The value.</returns>
    public double LatentRRVarValue(ReadOnlySpan<double> allFactors, int iName)
    {
        int iRecovery = iName + numNames; // should be live pool
        return LatentVarValue(allFactors, iRecovery);
    }

    /// <summary>The name's default curve in the bound basket.</summary>
    /// <param name="iName">The name.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    internal DefaultProbabilityTermStructure Curve(int iName)
    {
        Basket b = BoundBasket;
        return b.Pool.Get(b.Names[iName]).DefaultProbability(b.DefaultKeys[iName]);
    }
}
