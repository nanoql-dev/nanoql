// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaultprobabilitylatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The default-event latent model: a latent model of a portfolio's joint default events, a name defaulting by a date
/// when its latent variable falls below the inverse of its default probability. It models the events alone, no
/// severity or exposure; the basket's names and the model's variables correspond by index.
/// </summary>
/// <remarks>
/// <para>
/// Unsealed and immutable. <see cref="ResetBasket"/> returns a copy bound to a basket, where QuantLib's model holds a
/// mutable pointer to one; bound to a basket with realized defaults, the copy keeps the live names' rows (the S-xcr
/// plan's XR2 (f)). A curve read before its reference date is a default probability of 0, where QuantLib's refuses it
/// ("negative time"; XR8 (x)).
/// </para>
/// <para>
/// <see cref="ProbAtLeastNEvents"/> sums over every default configuration, 2ⁿ per integration node, counting them in
/// QuantLib's <c>BigNatural</c>, 32 bits under MSVC: from 32 names its count is 0 and its probability of one or more
/// defaults 0, and an order above 30 shifts a 32-bit 1 past its sign (<c>defaultprobabilitylatentmodel.hpp:295-309</c>),
/// both refused (XR8 (xxxi)). <see cref="DefaultCorrelation"/> divides by the product of the probabilities and their
/// complements, 0/0 at a probability of 0 or 1, refused (XR8 (xlvi)).
/// </para>
/// </remarks>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
[Experimental("NQL0001")]
public class DefaultLatentModel<TCopula, TInitTraits> : LatentModel<TCopula, TInitTraits>
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    private readonly LMIntegration integration;
    private Basket? basket;

    /// <summary>Initializes a new instance of the <see cref="DefaultLatentModel{TCopula, TInitTraits}"/> class.</summary>
    /// <param name="factorWeights">The weights of each variable on the independent factors.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentException">As <see cref="LatentModel{TCopula, TInitTraits}"/>'s constructor.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="integralType"/> isn't a member of its type, or
    /// there are more than 15 factors.</exception>
    public DefaultLatentModel(
        IReadOnlyList<IReadOnlyList<double>> factorWeights,
        LatentModelIntegrationType integralType,
        TInitTraits initTraits = default)
        : base(factorWeights, initTraits)
    {
        integration = LMIntegration.Create(NumFactors, integralType);
    }

    /// <summary>Initializes a new instance of the <see cref="DefaultLatentModel{TCopula, TInitTraits}"/> class on one
    /// factor of a market correlation: QuantLib's quote constructor.</summary>
    /// <param name="correlation">The correlation, whose square root is every variable's weight.</param>
    /// <param name="nVariables">The number of variables.</param>
    /// <param name="integralType">The integration.</param>
    /// <param name="initTraits">The copula's initialization parameters.</param>
    /// <exception cref="ArgumentException">As <see cref="LatentModel{TCopula, TInitTraits}.FromCorrelation"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="integralType"/> isn't a member of its
    /// type.</exception>
    public DefaultLatentModel(
        double correlation,
        int nVariables,
        LatentModelIntegrationType integralType,
        TInitTraits initTraits = default)
        : base(nVariables, correlation, initTraits)
    {
        integration = LMIntegration.Create(1, integralType);
    }

    /// <summary>Gets the basket the model is bound to, or <see langword="null"/> before it is bound.</summary>
    public Basket? Basket => basket;

    /// <summary>Gets the model's integration.</summary>
    protected override LMIntegration Integration => integration;

    /// <summary>Gets the basket the model is bound to, which a read requires.</summary>
    /// <exception cref="NanoQLException">The model isn't bound ("No portfolio basket set.").</exception>
    private protected Basket BoundBasket => basket ?? throw new NanoQLException("No portfolio basket set.");

    /// <summary>
    /// A copy of the model bound to a basket of its names: to the basket's live names, over its restated tranche where
    /// names have defaulted (<see cref="Experimental.Basket.PricingBasket"/>), the copy keeping their rows.
    /// </summary>
    /// <param name="basket">The basket, of the model's number of names.</param>
    /// <returns>The bound copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="basket"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The basket's size isn't the model's ("Incompatible new basket and model
    /// sizes."), or every name in it has defaulted.</exception>
    public DefaultLatentModel<TCopula, TInitTraits> ResetBasket(Basket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);

        // in the future change 'size' to 'liveSize'
        if (basket.Size != Size)
        {
            throw new NanoQLException("Incompatible new basket and model sizes.");
        }

        Basket pricing = basket.PricingBasket
            ?? throw new NanoQLException("every name in the basket has defaulted: no loss model can be bound");
        var copy = (DefaultLatentModel<TCopula, TInitTraits>)MemberwiseClone();
        if (basket.ModelRows.Length != Size)
        {
            copy.RestrictRows(basket.ModelRows);
        }

        copy.basket = pricing;
        return copy;
    }

    /// <summary>
    /// A name's default probability conditional on the factors' values, from its unconditional probability; the date
    /// is implicit in the probability. Below 1e-10, 0, sparing the inversion.
    /// </summary>
    /// <param name="prob">The unconditional probability.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The conditional probability.</returns>
    public double ConditionalDefaultProbability(double prob, int iName, ReadOnlySpan<double> mktFactors)
    {
        /*Avoid redundant call to minimum value inversion (might be \infty),
        and this independently of the copula function.
        */
        if (prob < 1.0e-10)
        {
            return 0.0;
        }

        return ConditionalDefaultProbabilityInvP(InverseCumulativeY(prob, iName), iName, mktFactors);
    }

    /// <summary>
    /// A name's default probability conditional on the factors' values, from the inverse cumulative of its
    /// unconditional probability, which an integration over the factors computes once.
    /// </summary>
    /// <param name="invCumYProb">The inverse cumulative of the unconditional probability, of the copula's law.</param>
    /// <param name="iName">The name.</param>
    /// <param name="m">The factors' values.</param>
    /// <returns>The conditional probability.</returns>
    public double ConditionalDefaultProbabilityInvP(double invCumYProb, int iName, ReadOnlySpan<double> m)
    {
        ImmutableArray<double> fw = FactorWeights[iName];
        double sumMs = 0.0;
        for (int k = 0; k < fw.Length; k++)
        {
            sumMs += fw[k] * m[k];
        }

        return CumulativeZ((invCumYProb - sumMs) / IdiosyncFctrs[iName]);
    }

    /// <summary>A name's unconditional default probability by a date, integrated over the factors: a trivial method
    /// for testing.</summary>
    /// <param name="iName">The name.</param>
    /// <param name="d">The date.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The model isn't bound ("No portfolio basket set.").</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public double ProbOfDefault(int iName, Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        double pUncond = UnconditionalProbability(iName, d);
        if (pUncond < 1.0e-10)
        {
            return 0.0;
        }

        // avoid repeating this in the integration:
        double inv = InverseCumulativeY(pUncond, iName);
        return IntegratedExpectedValue(v1 => ConditionalDefaultProbabilityInvP(inv, iName, v1));
    }

    /// <summary>Pearson's correlation of two names' default indicators by a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="iNamei">The first name.</param>
    /// <param name="iNamej">The second name.</param>
    /// <returns>The correlation.</returns>
    /// <exception cref="NanoQLException">The model isn't bound, or a name's default probability is 0 or 1, where the
    /// correlation is 0/0.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public double DefaultCorrelation(Date d, int iNamei, int iNamej)
    {
        Date.ThrowIfDefault(d, nameof(d));
        // unconditionals:
        double pi = UnconditionalProbability(iNamei, d);
        double pj = UnconditionalProbability(iNamej, d);
        if (pi == 0.0 || pi == 1.0 || pj == 0.0 || pj == 1.0)
        {
            throw new NanoQLException("the default correlation is undefined at a default probability of 0 or 1");
        }

        double pipj = pi * pj;
        double invPi = InverseCumulativeY(pi, iNamei);
        double invPj = InverseCumulativeY(pj, iNamej);

        // avoid repetitive calls when i=j?
        // joint default covariance term
        double e1i1j = iNamei != iNamej
            ? IntegratedExpectedValue(v1 => CondProbProduct(invPi, invPj, iNamei, iNamej, v1))
            : pi;
        return (e1i1j - pipj) / Math.Sqrt(pipj * (1.0 - pi) * (1.0 - pj));
    }

    /// <summary>The probability of n or more defaults in the basket by a date.</summary>
    /// <param name="n">The number, at most 30.</param>
    /// <param name="date">The date.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative or above 30.</exception>
    /// <exception cref="NanoQLException">The model isn't bound, or its basket has 32 names or more.</exception>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>, not a date.</exception>
    public double ProbAtLeastNEvents(int n, Date date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        if (n is < 0 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "the number of events must be from 0 to 30");
        }

        int poolSize = BoundBasket.Size;
        if (poolSize >= 32)
        {
            throw new NanoQLException("a basket of 32 names or more has too many default configurations to count");
        }

        // Precalc the unconditional probabilities and their inverses, read at every node
        var p = new double[poolSize];
        var inv = new double[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            p[i] = UnconditionalProbability(i, date);
            inv[i] = p[i] < 1.0e-10 ? 0.0 : InverseCumulativeY(p[i], i);
        }

        return IntegratedExpectedValue(v1 => ConditionalProbAtLeastNEvents(n, p, inv, v1));
    }

    /// <summary>A name's default probability by a date conditional on the factors' values.</summary>
    /// <param name="date">The date.</param>
    /// <param name="iName">The name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The conditional probability.</returns>
    /// <exception cref="NanoQLException">The model isn't bound.</exception>
    protected double ConditionalDefaultProbability(Date date, int iName, ReadOnlySpan<double> mktFactors) =>
        ConditionalDefaultProbability(UnconditionalProbability(iName, date), iName, mktFactors);

    /// <summary>The product of two names' conditional default probabilities, a step of the correlation.</summary>
    /// <param name="invCumYProb1">The first name's inverse cumulative unconditional probability.</param>
    /// <param name="invCumYProb2">The second name's.</param>
    /// <param name="iName1">The first name.</param>
    /// <param name="iName2">The second name.</param>
    /// <param name="mktFactors">The factors' values.</param>
    /// <returns>The product.</returns>
    protected double CondProbProduct(double invCumYProb1, double invCumYProb2, int iName1, int iName2, ReadOnlySpan<double> mktFactors) =>
        ConditionalDefaultProbabilityInvP(invCumYProb1, iName1, mktFactors)
        * ConditionalDefaultProbabilityInvP(invCumYProb2, iName2, mktFactors);

    /// <summary>A name's unconditional default probability by a date, read on its curve: 0 before the curve's
    /// reference date.</summary>
    /// <param name="iName">The name.</param>
    /// <param name="date">The date.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The model isn't bound ("No portfolio basket set.").</exception>
    protected double UnconditionalProbability(int iName, Date date)
    {
        Basket b = BoundBasket;
        return b.Pool.Get(b.Names[iName]).DefaultProbability(b.DefaultKeys[iName]).AccruedDefaultProbability(date);
    }

    // The conditional probability of n defaults or more: every configuration of the pool's names, from the lowest
    // with n defaults, as QuantLib's mask counts them.
    private double ConditionalProbAtLeastNEvents(int n, double[] p, double[] inv, ReadOnlySpan<double> mktFactors)
    {
        int poolSize = p.Length;
        var pDefCond = new double[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            pDefCond[i] = p[i] < 1.0e-10 ? 0.0 : ConditionalDefaultProbabilityInvP(inv[i], i, mktFactors);
        }

        double probNEventsOrMore = 0.0;
        uint limit = poolSize == 0 ? 1u : 1u << poolSize;

        // cheap permutations
        for (uint mask = (1u << n) - 1u; mask < limit; mask++)
        {
            if (BitOperations.PopCount(mask) >= n)
            {
                double pConfig = 1.0;
                for (int i = 0; i < poolSize; i++)
                {
                    pConfig *= ((mask >> i) & 1u) != 0 ? pDefCond[i] : 1.0 - pDefCond[i];
                }

                probNEventsOrMore += pConfig;
            }
        }

        return probNEventsOrMore;
    }
}
