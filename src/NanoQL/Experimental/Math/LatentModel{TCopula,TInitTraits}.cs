// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The generic multifactor latent variable model: latent variables Yᵢ = Σₖ aᵢₖMₖ + √(1 − Σₖ aᵢₖ²) Zᵢ over independent
/// zero-mean, unit-variance systemic factors Mₖ and idiosyncratic factors Zᵢ, the Zᵢ all of one law, their
/// distributions given by a copula policy.
/// </summary>
/// <remarks>
/// <para>
/// An unsealed, immutable class: its derived models give the latent variables a meaning (a default time, a recovery),
/// and an integrable one its integration. The correlation of Yᵢ and Yⱼ is Σₖ aᵢₖaⱼₖ. Its factor samplers are working
/// types of their own (<see cref="LatentModelFactorSampler{TCopula, TInitTraits, TUsng}"/>,
/// <see cref="BoxMullerFactorSampler{TUrng}"/>, <see cref="PolarStudentTFactorSampler{TUrng}"/>).
/// </para>
/// <para>
/// QuantLib's four constructors are three and a factory: its quote constructor takes a correlation and uses its square
/// root as the factor (<c>latentmodel.hpp:696-710</c>), where its <c>(correlSqr, nVariables)</c> constructor takes the
/// factor itself, so the two can't merge once a quote is a <see cref="double"/>; the correlation form is
/// <see cref="FromCorrelation"/>, and a new correlation is a new model, where QuantLib's observes its quote. A model of
/// no variables or no factors is refused, where QuantLib's reads the first row of an empty matrix
/// (<c>latentmodel.hpp:653</c>). The constructor on one factor's weights builds the policy from the weights, where
/// QuantLib's default-constructs it first, which the Gaussian policy can't do: QuantLib crashes
/// (<c>latentmodel.hpp:667-680</c>; <c>Divergences/LatentModelVectorConstructorTests.cs</c>).
/// </para>
/// </remarks>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
[Experimental("NQL0001")]
public class LatentModel<TCopula, TInitTraits>
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    // Ordering is: factorWeights[iVariable][iFactor]
    private ImmutableArray<ImmutableArray<double>> factorWeights;

    // \sqrt{1-\sum_k \beta_{i,k}^2} the addition being along the factors.
    private ImmutableArray<double> idiosyncFctrs;
    private TCopula copula;
    private TInitTraits initTraits;

    /// <summary>Initializes a new instance of the <see cref="LatentModel{TCopula, TInitTraits}"/> class on any number
    /// of variables and factors.</summary>
    /// <param name="factorWeights">The weights, <c>factorWeights[iVariable][iFactor]</c>.</param>
    /// <param name="initTraits">The policy's initialization parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factorWeights"/> or a row is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There's no variable or no factor; the policy refuses the weights; or a row
    /// has another number of factors than the first ("Name i provides a different number of factors").</exception>
    public LatentModel(IReadOnlyList<IReadOnlyList<double>> factorWeights, TInitTraits initTraits = default)
    {
        ArgumentNullException.ThrowIfNull(factorWeights);
        var rows = ImmutableArray.CreateBuilder<ImmutableArray<double>>(factorWeights.Count);
        foreach (IReadOnlyList<double> row in factorWeights)
        {
            ArgumentNullException.ThrowIfNull(row, nameof(factorWeights));
            rows.Add([.. row]);
        }

        ImmutableArray<ImmutableArray<double>> fw = rows.MoveToImmutable();
        CheckNotEmpty(fw.Length, fw.IsEmpty ? 0 : fw[0].Length, nameof(factorWeights));
        copula = TCopula.Create(fw, initTraits);
        int nFactors = fw[0].Length;
        var idiosync = ImmutableArray.CreateBuilder<double>(fw.Length);
        for (int i = 0; i < fw.Length; i++)
        {
            double norm = 0.0;
            foreach (double w in fw[i])
            {
                norm += w * w;
            }

            idiosync.Add(Math.Sqrt(1.0 - norm));

            // while at it, check sizes are coherent:
            if (fw[i].Length != nFactors)
            {
                throw new ArgumentException($"Name {i} provides a different number of factors", nameof(factorWeights));
            }
        }

        this.factorWeights = fw;
        idiosyncFctrs = idiosync.MoveToImmutable();
        this.initTraits = initTraits;
    }

    /// <summary>Initializes a new instance of the <see cref="LatentModel{TCopula, TInitTraits}"/> class on one factor,
    /// each variable with its own weight.</summary>
    /// <param name="factorsWeight">Each variable's weight on the factor.</param>
    /// <param name="initTraits">The policy's initialization parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factorsWeight"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There's no variable, or the policy refuses the weights.</exception>
    public LatentModel(IReadOnlyList<double> factorsWeight, TInitTraits initTraits = default)
    {
        ArgumentNullException.ThrowIfNull(factorsWeight);
        CheckNotEmpty(factorsWeight.Count, 1, nameof(factorsWeight));
        factorWeights = [.. factorsWeight.Select(w => ImmutableArray.Create(w))];
        idiosyncFctrs = [.. factorsWeight.Select(w => Math.Sqrt(1.0 - (w * w)))];

        // convert row to column vector....
        copula = TCopula.Create(factorWeights, initTraits);
        this.initTraits = initTraits;
    }

    /// <summary>Initializes a new instance of the <see cref="LatentModel{TCopula, TInitTraits}"/> class on one factor,
    /// every variable with the same weight.</summary>
    /// <param name="correlSqr">The weight, the same for every variable: the square root of the variables'
    /// correlation.</param>
    /// <param name="nVariables">The number of variables.</param>
    /// <param name="initTraits">The policy's initialization parameters.</param>
    /// <exception cref="ArgumentException">There's no variable, or the policy refuses the weights.</exception>
    public LatentModel(double correlSqr, int nVariables, TInitTraits initTraits = default)
        : this(nVariables, correlSqr, Math.Sqrt(1.0 - (correlSqr * correlSqr)), initTraits)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LatentModel{TCopula, TInitTraits}"/> class on one factor of
    /// a correlation, QuantLib's quote constructor, for a derived model's: every variable's weight the correlation's
    /// square root, its idiosyncratic factor √(1 − correlation), as QuantLib computes them.</summary>
    /// <param name="nVariables">The number of variables.</param>
    /// <param name="correlation">The correlation.</param>
    /// <param name="initTraits">The policy's initialization parameters.</param>
    /// <exception cref="ArgumentException">There's no variable, or the policy refuses the weights.</exception>
    protected LatentModel(int nVariables, double correlation, TInitTraits initTraits)
        : this(nVariables, Math.Sqrt(correlation), Math.Sqrt(1.0 - correlation), initTraits)
    {
    }

    private LatentModel(int nVariables, double weight, double idiosync, TInitTraits initTraits)
    {
        CheckNotEmpty(nVariables, 1, nameof(nVariables));
        factorWeights = [.. Enumerable.Repeat(ImmutableArray.Create(weight), nVariables)];
        idiosyncFctrs = [.. Enumerable.Repeat(idiosync, nVariables)];
        copula = TCopula.Create(factorWeights, initTraits);
        this.initTraits = initTraits;
    }

    /// <summary>Gets the number of latent variables.</summary>
    public int Size => factorWeights.Length;

    /// <summary>Gets the number of systemic factors.</summary>
    public int NumFactors => factorWeights[0].Length;

    /// <summary>Gets the number of independent random factors, systemic and idiosyncratic.</summary>
    public int NumTotalFactors => Size + NumFactors;

    /// <summary>Gets the factor weights, <c>FactorWeights[iVariable][iFactor]</c>.</summary>
    public ImmutableArray<ImmutableArray<double>> FactorWeights => factorWeights;

    /// <summary>Gets the idiosyncratic factors' weights, √(1 − Σₖ aᵢₖ²).</summary>
    public ImmutableArray<double> IdiosyncFctrs => idiosyncFctrs;

    /// <summary>Gets the copula policy.</summary>
    public TCopula Copula => copula;

    /// <summary>Gets the policy's integration, which an integrable model provides.</summary>
    /// <exception cref="NanoQLException">The model provides none ("Integration non implemented in Latent
    /// model.").</exception>
    protected virtual LMIntegration Integration => throw new NanoQLException("Integration non implemented in Latent model.");

    /// <summary>The one-factor model of a correlation, every variable with the same: QuantLib's quote constructor.</summary>
    /// <param name="correlation">The correlation, whose square root is the factor's weight.</param>
    /// <param name="nVariables">The number of variables.</param>
    /// <param name="initTraits">The policy's initialization parameters.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentException">There's no variable, or the policy refuses the weights.</exception>
    public static LatentModel<TCopula, TInitTraits> FromCorrelation(double correlation, int nVariables, TInitTraits initTraits = default) =>
        new(nVariables, correlation, initTraits);

    /// <summary>The cumulative probability of a latent variable taking a value.</summary>
    /// <param name="val">The value.</param>
    /// <param name="iVariable">The variable.</param>
    /// <returns>The probability.</returns>
    public double CumulativeY(double val, int iVariable) => copula.CumulativeY(val, iVariable);

    /// <summary>The cumulative distribution of the idiosyncratic factors.</summary>
    /// <param name="z">The value.</param>
    /// <returns>The probability.</returns>
    public double CumulativeZ(double z) => copula.CumulativeZ(z);

    /// <summary>The density of the systemic factors.</summary>
    /// <param name="m">The factors' values.</param>
    /// <returns>The density.</returns>
    public double Density(ReadOnlySpan<double> m) => copula.Density(m);

    /// <summary>The inverse cumulative distribution of a systemic factor.</summary>
    /// <param name="p">The probability.</param>
    /// <param name="iFactor">The factor.</param>
    /// <returns>The value.</returns>
    public double InverseCumulativeDensity(double p, int iFactor) => copula.InverseCumulativeDensity(p, iFactor);

    /// <summary>The inverse cumulative distribution of a latent variable.</summary>
    /// <param name="p">The probability.</param>
    /// <param name="iVariable">The variable.</param>
    /// <returns>The value.</returns>
    public double InverseCumulativeY(double p, int iVariable) => copula.InverseCumulativeY(p, iVariable);

    /// <summary>The inverse cumulative distribution of the idiosyncratic factors.</summary>
    /// <param name="p">The probability.</param>
    /// <returns>The value.</returns>
    public double InverseCumulativeZ(double p) => copula.InverseCumulativeZ(p);

    /// <summary>The inverse cumulative distributions of all the factors, the systemic ones and then one idiosyncratic
    /// factor per variable, as integrations and samplers use them.</summary>
    /// <param name="probs">The probabilities.</param>
    /// <returns>The factors' values.</returns>
    public double[] AllFactorCumulInverter(ReadOnlySpan<double> probs) => copula.AllFactorCumulInverter(probs);

    /// <summary>A latent variable's value given every factor's: the systemic factors' and then the idiosyncratic
    /// ones', of which only the variable's own is read.</summary>
    /// <param name="allFactors">The factors' values, a full sample.</param>
    /// <param name="iVar">The variable.</param>
    /// <returns>The variable's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="iVar"/> isn't one of the model's variables, where QuantLib reads past its weights.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="allFactors"/> doesn't reach the variable's idiosyncratic factor, where QuantLib reads past its
    /// end.
    /// </exception>
    public double LatentVarValue(ReadOnlySpan<double> allFactors, int iVar)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(iVar);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(iVar, factorWeights.Length);
        if (!(allFactors.Length > NumFactors + iVar))
        {
            throw new ArgumentException($"{allFactors.Length} factor values don't reach variable {iVar}'s", nameof(allFactors));
        }

        ImmutableArray<double> fw = factorWeights[iVar];

        // idiosyncratic term:
        double result = allFactors[NumFactors + iVar] * idiosyncFctrs[iVar];

        // systemic term:
        for (int k = 0; k < fw.Length; k++)
        {
            result += fw[k] * allFactors[k];
        }

        return result;
    }

    /// <summary>The correlation of two latent variables.</summary>
    /// <param name="iVar1">The first variable.</param>
    /// <param name="iVar2">The second variable.</param>
    /// <returns>The correlation.</returns>
    public double LatentVariableCorrel(int iVar1, int iVar2)
    {
        // true for any normalized combination
        double result = iVar1 == iVar2 ? idiosyncFctrs[iVar1] * idiosyncFctrs[iVar1] : 0.0;
        ImmutableArray<double> f1 = factorWeights[iVar1], f2 = factorWeights[iVar2];
        for (int k = 0; k < f1.Length; k++)
        {
            result += f1[k] * f2[k];
        }

        return result;
    }

    /// <summary>The expected value of a scalar function of the systemic factors, integrated over their density.</summary>
    /// <param name="f">The function.</param>
    /// <returns>The expected value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The model has no integration.</exception>
    public double IntegratedExpectedValue(MultidimIntegrand f)
    {
        ArgumentNullException.ThrowIfNull(f);
        TCopula c = copula;

        // function composition: composes the integrand with the density
        //   through a product.
        return Integration.Integrate(x => c.Density(x) * f(x));
    }

    /// <summary>The expected value of a vector function of the systemic factors, integrated over their density.</summary>
    /// <param name="f">The function.</param>
    /// <returns>The expected value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The model has no integration, or its integration no vector form.</exception>
    public double[] IntegratedExpectedValueV(MultidimVectorIntegrand f)
    {
        ArgumentNullException.ThrowIfNull(f);
        TCopula c = copula;
        return Integration.IntegrateV(x =>
        {
            double d = c.Density(x);
            double[] v = f(x);
            var r = new double[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                r[i] = v[i] * d;
            }

            return r;
        });
    }

    /// <summary>Restricts this copy to a subset of its variables, in their order: their weights and the policy built
    /// on them. A derived model restricts its own per-variable data and calls this.</summary>
    /// <param name="rows">The variables kept.</param>
    private protected virtual void RestrictRows(ImmutableArray<int> rows)
    {
        factorWeights = [.. rows.Select(i => factorWeights[i])];
        idiosyncFctrs = [.. rows.Select(i => idiosyncFctrs[i])];
        copula = TCopula.Create(factorWeights, initTraits);
    }

    private static void CheckNotEmpty(int nVariables, int nFactors, string paramName)
    {
        if (nVariables < 1 || nFactors < 1)
        {
            throw new ArgumentException("a latent model needs at least one variable and one factor", paramName);
        }
    }
}
