// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/tcopulapolicy.hpp ql/experimental/math/tcopulapolicy.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The Student t latent model's copula policy: independent t factors, each normalized to unit variance by
/// √((ν − 2)/ν), so that a latent variable is a combination of t's (<see cref="CumulativeBehrensFisher"/>).
/// </summary>
/// <remarks>
/// <para>
/// A <c>readonly struct</c> built by <see cref="Create"/>, over immutable state; its zero value holds no distribution,
/// and every member of <c>default(TCopulaPolicy)</c> throws <see cref="InvalidOperationException"/>. The orders must
/// exceed 2 (finite variance) and, for a model with variables, be odd, as the convolution requires.
/// </para>
/// <para>
/// The distributions are Boost.Math's <c>students_t</c>, ported: its <c>cdf</c>, <c>pdf</c> and <c>quantile</c>. At 4
/// and 6 degrees of freedom Boost's quantile takes Shaw's forms, which cancel near ½ (<see
/// cref="InverseCumulativeStudent"/>'s remarks); there, within 0.05 of ½, the policy takes one Newton step on Boost's
/// cumulative function, as <see cref="InverseCumulativeStudent"/> does. Those orders are even, which the convolution
/// refuses, so only a policy built without variables reads them.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public readonly struct TCopulaPolicy : ICopulaPolicy<TCopulaPolicy, TCopulaPolicyInitTraits>
{
    private readonly ImmutableArray<int> distributions;
    private readonly ImmutableArray<double> varianceFactors;
    private readonly ImmutableArray<CumulativeBehrensFisher> latentVarsCumul;
    private readonly ImmutableArray<InverseCumulativeBehrensFisher> latentVarsInverters;

    private TCopulaPolicy(
        ImmutableArray<int> distributions,
        ImmutableArray<double> varianceFactors,
        ImmutableArray<CumulativeBehrensFisher> latentVarsCumul,
        ImmutableArray<InverseCumulativeBehrensFisher> latentVarsInverters)
    {
        this.distributions = distributions;
        this.varianceFactors = varianceFactors;
        this.latentVarsCumul = latentVarsCumul;
        this.latentVarsInverters = latentVarsInverters;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public int NumFactors => Checked().latentVarsInverters.Length + varianceFactors.Length - 1;

    /// <summary>Gets the inverses of the t's variances' square roots, √((ν − 2)/ν), which normalize the
    /// factors.</summary>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public ImmutableArray<double> VarianceFactors => Checked().varianceFactors;

    /// <summary>The policy of a latent model's factor weights.</summary>
    /// <param name="factorWeights">The weights, <c>factorWeights[iVariable][iFactor]</c>.</param>
    /// <param name="initTraits">The t orders: one per systemic factor, then the idiosyncratic factors'.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="ArgumentException">There's no order; an order is 2 or less ("Non finite variance T in latent
    /// model."); a variable's weights don't number the orders less one ("Incompatible number of T functions and number of
    /// factors.") or have a squared norm of 1 or more ("Non normal random factor combination."); or an order is even
    /// where there are variables.</exception>
    public static TCopulaPolicy Create(ImmutableArray<ImmutableArray<double>> factorWeights, TCopulaPolicyInitTraits initTraits)
    {
        ImmutableArray<int> tOrders = initTraits.TOrders.IsDefault ? [] : initTraits.TOrders;
        if (tOrders.IsEmpty)
        {
            throw new ArgumentException("a T copula policy needs at least one T order", nameof(initTraits));
        }

        var vf = ImmutableArray.CreateBuilder<double>(tOrders.Length);
        foreach (int tOrder in tOrders)
        {
            // require no T is of order 2 (finite variance)
            if (!(tOrder > 2))
            {
                throw new ArgumentException("Non finite variance T in latent model.", nameof(initTraits));
            }

            // inverses T variaces used in normalization of the random factors
            // For low values of the T order this number is very close to zero
            // and it enters the expressions dividing them, which introduces
            // numerical errors.
            vf.Add(Math.Sqrt((tOrder - 2.0) / tOrder));
        }

        ImmutableArray<double> varianceFactors = vf.MoveToImmutable();
        ImmutableArray<ImmutableArray<double>> weights = factorWeights.IsDefault ? [] : factorWeights;
        var cumuls = ImmutableArray.CreateBuilder<CumulativeBehrensFisher>(weights.Length);
        var inverters = ImmutableArray.CreateBuilder<InverseCumulativeBehrensFisher>(weights.Length);
        foreach (ImmutableArray<double> factorWeight in weights)
        {
            // This ensures the latent model is 'canonical'
            if (tOrders.Length != factorWeight.Length + 1)
            {
                // num factors plus one
                throw new ArgumentException("Incompatible number of T functions and number of factors.", nameof(factorWeights));
            }

            double factorsNorm = 0.0;
            foreach (double w in factorWeight)
            {
                factorsNorm += w * w;
            }

            if (!(factorsNorm < 1.0))
            {
                throw new ArgumentException("Non normal random factor combination.", nameof(factorWeights));
            }

            double idiosyncFctr = Math.Sqrt(1.0 - factorsNorm);

            // linear comb factors ajusted for the variance renormalization:
            var normFactorWeights = new double[factorWeight.Length + 1];
            for (int iFactor = 0; iFactor < factorWeight.Length; iFactor++)
            {
                normFactorWeights[iFactor] = factorWeight[iFactor] * varianceFactors[iFactor];
            }

            // idiosincratic term, all Z factors are assumed identical.
            normFactorWeights[^1] = idiosyncFctr * varianceFactors[^1];
            cumuls.Add(new CumulativeBehrensFisher(tOrders, normFactorWeights));
            inverters.Add(new InverseCumulativeBehrensFisher(tOrders, normFactorWeights));
        }

        return new TCopulaPolicy(tOrders, varianceFactors, cumuls.MoveToImmutable(), inverters.MoveToImmutable());
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public TCopulaPolicyInitTraits GetInitTraits() => new(Checked().distributions);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="iVariable"/> isn't a variable's index, where QuantLib reads past its end.
    /// </exception>
    public double CumulativeY(double val, int iVariable)
    {
        Checked();
        ArgumentOutOfRangeException.ThrowIfNegative(iVariable);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(iVariable, latentVarsCumul.Length);
        return latentVarsCumul[iVariable].Value(val);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public double CumulativeZ(double z) =>
        StudentsTFunctions.Cdf(Checked().distributions[^1], z / varianceFactors[^1]);

    /// <summary>The joint density of a realization of the systemic factors: the product of each normalized t's
    /// density.</summary>
    /// <param name="m">The systemic factors' values.</param>
    /// <returns>The density.</returns>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="m"/> holds as many values as the distributions or more: QuantLib's extra safety check refuses
    /// them, where its default build reads the idiosyncratic factor's or past the end.
    /// </exception>
    public double Density(ReadOnlySpan<double> m)
    {
        Checked();
        if (!(m.Length < distributions.Length))
        {
            throw new ArgumentException($"{m.Length} factor values given for {distributions.Length - 1} systemic factors", nameof(m));
        }

        double prodDensities = 1.0;
        for (int i = 0; i < m.Length; i++)
        {
            prodDensities *= StudentsTFunctions.Pdf(distributions[i], m[i] / varianceFactors[i]) / varianceFactors[i];
        }

        return prodDensities;
    }

    /// <summary>The inverse of a latent variable's cumulative distribution, the convolution of the factors'.</summary>
    /// <param name="p">The probability.</param>
    /// <param name="iVariable">The variable's index.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    /// <exception cref="NanoQLException">The inversion fails.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="iVariable"/> isn't a variable's index, where QuantLib reads past its end.
    /// </exception>
    public double InverseCumulativeY(double p, int iVariable)
    {
        Checked();
        ArgumentOutOfRangeException.ThrowIfNegative(iVariable);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(iVariable, latentVarsInverters.Length);
        return latentVarsInverters[iVariable].Value(p);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    public double InverseCumulativeZ(double p) => Quantile(Checked().distributions[^1], p) * varianceFactors[^1];

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="iFactor"/> isn't a factor's index, where QuantLib reads past its end.
    /// </exception>
    public double InverseCumulativeDensity(double p, int iFactor)
    {
        Checked();
        ArgumentOutOfRangeException.ThrowIfNegative(iFactor);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(iFactor, distributions.Length);
        return Quantile(distributions[iFactor], p) * varianceFactors[iFactor];
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The policy is the zero value.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="probs"/> holds fewer probabilities than the systemic factors, where QuantLib reads past its end.
    /// </exception>
    public double[] AllFactorCumulInverter(ReadOnlySpan<double> probs)
    {
        Checked();
        int nSystemic = varianceFactors.Length - 1;
        if (!(probs.Length >= nSystemic))
        {
            throw new ArgumentException(
                $"{probs.Length} probabilities given for {nSystemic} systemic factors", nameof(probs));
        }

        var result = new double[probs.Length];
        for (int i = 0; i < nSystemic; i++)
        {
            result[i] = InverseCumulativeDensity(probs[i], i);
        }

        for (int i = nSystemic; i < probs.Length; i++)
        {
            result[i] = InverseCumulativeZ(probs[i]);
        }

        return result;
    }

    // Boost's quantile, with a Newton step on Boost's cumulative function where Shaw's forms at 4 and 6 degrees of
    // freedom cancel near ½.
    private static double Quantile(int df, double p)
    {
        double q = StudentsTFunctions.Quantile(df, p);
        if ((df == 4 || df == 6) && Math.Abs(p - 0.5) < 0.05)
        {
            q -= (StudentsTFunctions.Cdf(df, q) - p) / StudentsTFunctions.Pdf(df, q);
        }

        return q;
    }

    private TCopulaPolicy Checked() =>
        varianceFactors.IsDefault
            ? throw new InvalidOperationException("the zero value of TCopulaPolicy isn't a usable policy")
            : this;
}
