// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/saddlepointlossmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The saddle-point portfolio credit default loss model: the saddle-point expansion's integrations of several default
/// risk metrics, the dependence dealt with through a latent model that makes the integrals conditional on its factor,
/// the latent variables integrated indirectly.
/// </summary>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
/// <remarks>
/// <para>
/// See R. Martin, K. Thompson and C. Browne, "Taking to the saddle", Risk, June 2001, p. 91; R. Martin, "The saddlepoint
/// method and portfolio optionalities", Risk, December 2006; R. Martin, K. Thompson and C. Browne, "VAR: who contributes
/// and how much?", Risk, August 2001; R. J. Martin, "Shortfall: Who contributes and how much?", Credit Suisse, 3
/// January 2007; J. Annaert, C. Garcia Joao Batista, J. Lamoot and G. Lanine, "Don't Fall from the Saddle: the
/// Importance of Higher Moments of Credit Loss Distributions", Gent University, February 2006; A. Antonov, S. Mechkov
/// and T. Misirpashaev, "Analytical techniques for synthetic CDOs and credit default risk measures", NumeriX, 23 May
/// 2005; X. Huang, C. W. Oosterlee and M. Mesters, "Computation of VaR and VaR contribution in the Vasicek portfolio
/// credit loss model: a comparative study", Journal of Credit Risk 3(3), Fall 2007, and "Higher-order saddlepoint
/// approximations in the Vasicek portfolio credit loss model", Journal of Computational Finance 11(1), Fall 2007. A
/// high order expansion is used, more expensive; see Antonov et al. for the terms retained. A. Owen, A. McLeod and K.
/// Thompson's hybrid saddlepoint method fixes the error at low loss levels (Risk, August 2009), not implemented here;
/// for the general theory, R. W. Butler, "Saddlepoint approximations with applications", Cambridge, 2007. To be done, as
/// in QuantLib: some portfolios' instabilities in the high order terms; calls avoiding the copula inversions; the saddle
/// point search's convergence in extreme cases (very high factors, factors not ordered from high to low). Random
/// recoveries would have to be passed to the conditional methods as the probabilities are.
/// </para>
/// <para>
/// <b>Kept</b>, QuantLib's method (the S-xcr plan's XR8 (xxxvi), the user's choice): the expected tranche loss is the
/// tranche of the conditional expected loss, a large-pool reading, not a saddle-point expansion; at correlation 0 on
/// 100 names its four tranches read 292.78, 0, 0, 0 where the exact recursive model reads 245.40, 46.48, 0.89, 0.0003.
/// </para>
/// <para>
/// <b>The tail's degenerate nodes and domain</b> (XR8 (xlvii); <c>Divergences/SaddlePointLossModelTests.cs</c>).
/// QuantLib's saddle search brackets on the name of the largest loss, taking the logarithm of a ratio of its
/// conditional probability p and 1 − p, so a node where that name is at 0 or 1 throws ("invalid range"): at the
/// evaluation date every probability is 0, and on ten names every correlation from 0.9 throws. Here the bracket's name
/// is the largest among those strictly between 0 and 1, the certain ones carried by the cumulants (a name at 1 a linear
/// term, one at 0 nothing), and a node where every name is at 0 or 1 reads its exact conditional loss: its tail 1 or
/// 0, its density 0, its split and shortfall exact. Where QuantLib's throws (100 names, hazard 0.03, recovery 0.4), the
/// tail at 10% of the pool reads the exact (recursive) model's within 1.2% through correlation 0.9 and within 5% at
/// 0.95, below it (5.0% at three months), the expansion's own accuracy on near-comonotonic pools, where QuantLib's runs
/// reading it within 1% at 0.7 and 0.75 (the S-xcr run's stop 2, ruled on 2026-10-28). Refused, naming the saddle-point
/// expansion: a tail outside [0, 1] (beyond the quadrature's rounding, 1e-12), and a largest relative loss at or below
/// the bracket's 1e-5, whose logarithm QuantLib takes of a negative number. The factor nodes' tails are QuantLib's, inside [0, 1] or not: at low
/// losses single nodes read up to 6.31 or slightly below 0 where the integrated tail and the percentile are right (on
/// 100 names, hazard 0.03, the percentile at 50%, 90% and 99% within the exact distribution's loss unit at correlations
/// 0.1 to 0.5 and 30 days to ten years; the S-xcr run's stop 1, ruled on 2026-10-28). <b>Kept</b>, the method's domain:
/// on small pools the expansion misreads the tail inside [0, 1], on ten names at correlation 0.3 P(L ≥ 1%) 0.983 for an
/// exact 0.0223 at a month and 0.754 for 0.603 at five years, where 100 names read 0.4389 for 0.4387 at a year; outside
/// it (26.78 a day out at correlation 0.548, 2.61 at a week on ten names) the refusal.
/// </para>
/// <para>
/// <b>The saddle search</b> (the S-xcr run's stop 3, ruled on 2026-10-28; the S-xcr code review's C3 O1-3). QuantLib's
/// brackets the point on the name of the largest loss and, below or above the bracket's losses, returns the bracket's
/// end instead of solving, so on pools whose largest loss is a name unlikely to default it reads up to 40 times the
/// exact tail (0.978 for 0.198 at 1% of 100 names a year out at correlation 0.7, 0.2008 for 0.0049 at 10% at 0.9);
/// here the bracket widens until it holds the level and the point is solved, QuantLib's search wherever its bracket
/// holds it. The cumulants keep QuantLib's arithmetic but for names at 0, out of the sums, names at 1, their linear
/// terms, and where QuantLib's form overflows the logistic form, so a widened point reads no 0·∞; the split's
/// contributions take the same treatment, where QuantLib's form reads ∞/∞ (the S-xcr code review's C3 O2-2). Above
/// the largest loss a factor node reaches, its names' losses given default but those certain not to default, no point
/// exists: the tail reads 0 there by QuantLib's own return above 1 less the average recovery, and the density and the
/// conditional shortfall read 0 too, where at the widened end QuantLib's expressions read 0/0, so the shortfall of a
/// tranche detaching above the pool's largest loss was NaN (QuantLib's TO DO list: "Failing when the tranche upper
/// loss limit goes over the max attainable loss"; O2-1).
/// <c>Divergences/SaddlePointLossModelTests.cs</c>.
/// </para>
/// <para>
/// <see cref="ProbOverLoss"/> reads 0 above the whole tranche, where QuantLib's compares the fraction with the
/// detachment amount; <see cref="ExpectedShortfall"/> is the tranche loss's, at most the tranche's remaining notional,
/// where QuantLib's integrates the whole portfolio's tail above the tranche's percentile and caps it at the tranche
/// notional times the remaining ratio (9 for a 300 tranche; "ESF &lt;- tranched????!!" in its TO DO list) (XR8 (xxiii);
/// the S-xcr code review's C3 O1-2). Bound to a basket with realized defaults, the model prices its live names over
/// the restated tranche (XR8 (ii)).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class SaddlePointLossModel<TCopula, TInitTraits> : DefaultLossModel
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    private static readonly CumulativeNormalDistribution Phi = new();
    private static readonly NormalDistribution Density = new();
    private ConstantLossLatentModel<TCopula, TInitTraits> copula;

    // cached todays arguments values
    private ImmutableArray<double> remainingNotionals;
    private double remainingNotional;

    // remaining basket levels:
    private double attachRatio;
    private double detachRatio;

    /// <summary>Initializes a new instance of the <see cref="SaddlePointLossModel{TCopula, TInitTraits}"/>
    /// class.</summary>
    /// <param name="m">The constant-loss latent model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is <see langword="null"/>.</exception>
    public SaddlePointLossModel(ConstantLossLatentModel<TCopula, TInitTraits> m)
    {
        ArgumentNullException.ThrowIfNull(m);
        copula = m;
    }

    /// <summary>The loss amount by a date at which the probability of losing that amount or less is the percentile:
    /// a tranche loss.</summary>
    /// <param name="d">The date.</param>
    /// <param name="percentile">The percentile, in [0, 1].</param>
    /// <returns>The loss, an amount; 0 on or before the evaluation date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The percentile lies outside [0, 1] ("Incorrect percentile
    /// value.").</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or the expansion refuses (a tail
    /// outside [0, 1], an unresolvable loss).</exception>
    public override double Percentile(Date d, double percentile)
    {
        // this test should be in the calling basket...?
        if (!(percentile >= 0.0 && percentile <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Incorrect percentile value.");
        }

        Basket basket = BoundBasket;

        // still this does not tackle the situation where we have cumulated
        //   losses from previous defaults:
        if (d <= basket.Context.EvaluationDate)
        {
            return 0.0;
        }

        // Trivial cases when the percentile is outside the prob range
        //   associated to the tranche limits:
        if (percentile <= 1.0 - ProbOverLoss(d, 0.0))
        {
            return 0.0;
        }

        if (percentile >= 1.0 - ProbOverLoss(d, 1.0))
        {
            return basket.RemainingTrancheNotional();
        }

        // The passed x is the _tranche_ loss fraction
        double targetValue = 1.0 - percentile;
        double minVal = NumericLimits.QL_EPSILON;
        double maxVal = 1.0 - NumericLimits.QL_EPSILON;
        double guess = 0.5;
        double solut = new Brent().SetMaxEvaluations(100).Solve(x => ProbOverLoss(d, x) - targetValue, 1.0e-4, guess, minVal, maxVal);
        return basket.RemainingTrancheNotional() * solut;
    }

    /// <summary>The probability of losing a fraction of the live tranche or more by a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="trancheLossFract">The fraction of the live tranche.</param>
    /// <returns>The probability; 0 above the whole tranche.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or the expansion refuses (a tail
    /// outside [0, 1], an unresolvable loss).</exception>
    public override double ProbOverLoss(Date d, double trancheLossFract)
    {
        // avoid computation: no tranche loss exceeds the tranche
        if (trancheLossFract > 1.0)
        {
            return 0.0;
        }

        double[] invUncondProbs = InverseProbabilities(d);
        return CheckedTail(copula.IntegratedExpectedValue(v1 => ProbOverLossCond(invUncondProbs, trancheLossFract, v1)));
    }

    /// <inheritdoc/>
    public override ImmutableSortedDictionary<double, double> LossDistribution(Date d)
    {
        var distrib = ImmutableSortedDictionary.CreateBuilder<double, double>();
        const double numPts = 500.0;
        for (double lossFraction = 1.0 / numPts; lossFraction < 0.45; lossFraction += 1.0 / numPts)
        {
            distrib.TryAdd(lossFraction * remainingNotional, 1.0 - ProbOverPortfLoss(d, lossFraction * remainingNotional));
        }

        return distrib.ToImmutable();
    }

    /// <summary>The probability of losing an amount of the whole (untranched) live portfolio or more by a
    /// date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="loss">The loss, an amount.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or the expansion refuses (a tail
    /// outside [0, 1], an unresolvable loss).</exception>
    public double ProbOverPortfLoss(Date d, double loss)
    {
        double[] invUncondProbs = InverseProbabilities(d);
        return CheckedTail(copula.IntegratedExpectedValue(v1 => ProbOverLossPortfCond(invUncondProbs, loss, v1)));
    }

    /// <inheritdoc/>
    public override double ExpectedTrancheLoss(Date d)
    {
        double[] invUncondProbs = InverseProbabilities(d);
        return copula.IntegratedExpectedValue(v1 => ConditionalExpectedTrancheLoss(invUncondProbs, v1));
    }

    /// <summary>The probability density of the whole (untranched) live portfolio's loss at an amount by a
    /// date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="loss">The loss, an amount.</param>
    /// <returns>The density.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or the expansion refuses (a tail
    /// outside [0, 1], an unresolvable loss).</exception>
    public double ProbDensity(Date d, double loss)
    {
        double[] invUncondProbs = InverseProbabilities(d);
        return copula.IntegratedExpectedValue(v1 => ProbDensityCond(invUncondProbs, loss, v1));
    }

    /// <summary>
    /// The names' sensitivities to a loss level of the whole (untranched) portfolio: ratios to the structure's
    /// notional, which add up to the loss. Equation 8 of R. Martin, K. Thompson and C. Browne, "VAR: who contributes and
    /// how much?", Risk, August 2001. To be done, as in QuantLib: the tranched basket.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="loss">The loss level, an amount, equivalent to a percentile.</param>
    /// <returns>The contributions, by live name.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or the expansion refuses (a tail
    /// outside [0, 1], an unresolvable loss).</exception>
    public override ImmutableArray<double> SplitVaRLevel(Date date, double loss)
    {
        double[] invUncondProbs = InverseProbabilities(date);
        return [.. copula.IntegratedExpectedValueV(v1 => SplitLossCond(invUncondProbs, loss, v1))];
    }

    /// <summary>The expected loss beyond a percentile's loss by a date, of the whole portfolio above it, at most the
    /// tranche's remaining notional.</summary>
    /// <param name="d">The date.</param>
    /// <param name="percentile">The percentile, in [0, 1].</param>
    /// <returns>The expected shortfall, an amount.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The percentile lies outside [0, 1].</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or the expansion refuses (a tail
    /// outside [0, 1], an unresolvable loss).</exception>
    public override double ExpectedShortfall(Date d, double percentile)
    {
        // assuming I have the tranched one.
        double lossPerc = Percentile(d, percentile);

        // check the trivial case when the loss is over the detachment limit
        //   to avoid computation:
        double trancheAmount = BoundBasket.RemainingTrancheNotional();

        // assumed the amount includes the realized loses
        if (lossPerc >= trancheAmount)
        {
            return trancheAmount;
        }

        // SHOULD CHECK NOW THE OPPOSITE LIMIT ("zero" losses)....
        double[] invUncondProbs = InverseProbabilities(d);

        // The tranche loss T = min(L, D) - A above its percentile x is L >= A + x:
        // E[T; T >= x] = [ESF(A + x) - A P(L >= A + x)] - [ESF(D) - D P(L >= D)], each the portfolio's
        // E[L; L >= y] and tail integrated, the second nothing on the whole pool
        double attach = attachRatio * remainingNotional;
        double detach = detachRatio * remainingNotional;
        double above = attach + lossPerc;
        double tranche = copula.IntegratedExpectedValue(v1 =>
        {
            double t = ExpectedShortfallFullPortfolioCond(invUncondProbs, above, v1)
                - (attach * ProbOverLossPortfCond(invUncondProbs, above, v1));
            return detachRatio < 1.0
                ? t - (ExpectedShortfallFullPortfolioCond(invUncondProbs, detach, v1) - (detach * ProbOverLossPortfCond(invUncondProbs, detach, v1)))
                : t;
        });
        return tranche / (1.0 - percentile);
    }

    /// <summary>The cumulant generating function by a date, the conditional one weighted by the factor's density;
    /// unused by the statistics, which integrate indirectly.</summary>
    /// <param name="date">The date.</param>
    /// <param name="s">The saddle point.</param>
    /// <returns>The cumulant generating function.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double CumulantGenerating(Date date, double s)
    {
        double[] invUncondProbs = InverseProbabilities(date);
        return copula.IntegratedExpectedValue(v1 => CumulantGeneratingCond(invUncondProbs, s, v1));
    }

    /// <summary>The cumulant generating function's first derivative by a date, unconditional.</summary>
    /// <param name="date">The date.</param>
    /// <param name="s">The saddle point.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double CumGen1stDerivative(Date date, double s)
    {
        double[] invUncondProbs = InverseProbabilities(date);
        return copula.IntegratedExpectedValue(v1 => CumGen1stDerivativeCond(invUncondProbs, s, v1));
    }

    /// <summary>The cumulant generating function's second derivative by a date, unconditional.</summary>
    /// <param name="date">The date.</param>
    /// <param name="s">The saddle point.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double CumGen2ndDerivative(Date date, double s)
    {
        double[] invUncondProbs = InverseProbabilities(date);
        return copula.IntegratedExpectedValue(v1 => CumGen2ndDerivativeCond(invUncondProbs, s, v1));
    }

    /// <summary>The cumulant generating function's third derivative by a date, unconditional.</summary>
    /// <param name="date">The date.</param>
    /// <param name="s">The saddle point.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double CumGen3rdDerivative(Date date, double s)
    {
        double[] invUncondProbs = InverseProbabilities(date);
        return copula.IntegratedExpectedValue(v1 => CumGen3rdDerivativeCond(invUncondProbs, s, v1));
    }

    /// <summary>The cumulant generating function's fourth derivative by a date, unconditional.</summary>
    /// <param name="date">The date.</param>
    /// <param name="s">The saddle point.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double CumGen4thDerivative(Date date, double s)
    {
        double[] invUncondProbs = InverseProbabilities(date);
        return copula.IntegratedExpectedValue(v1 => CumGen4thDerivativeCond(invUncondProbs, s, v1));
    }

    /// <summary>The cumulant generating function conditional on the factor (the zero-th order expansion term):
    /// K = Σⱼ ln(1 − pⱼ + pⱼ e^(Nⱼ·LGDⱼ·s)).</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="lossFraction">The saddle point.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The cumulant generating function.</returns>
    protected double CumulantGeneratingCond(double[] invUncondProbs, double lossFraction, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double sum = 0.0;

        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);
            double quantLibs = Math.Log(1.0 - pBuffer + (pBuffer * Math.Exp(remainingNotionals[iName]
                * (1.0 - copula.ConditionalRecoveryInvP(invUncondProbs[iName], iName, mktFactor)) * lossFraction / remainingNotional)));
            sum += Term(0, pBuffer, LossInDefault(invUncondProbs, iName, mktFactor), lossFraction, quantLibs);
        }

        return sum;
    }

    /// <summary>The cumulant generating function's first derivative conditional on the factor: at 0 the portfolio's
    /// expected loss, at infinity its maximum loss, fractions of the live notional.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="saddle">The saddle point.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The derivative.</returns>
    protected double CumGen1stDerivativeCond(double[] invUncondProbs, double saddle, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double sum = 0.0;

        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);

            // loss in fractional units
            double lossInDef = LossInDefault(invUncondProbs, iName, mktFactor);
            double midFactor = pBuffer * Math.Exp(lossInDef * saddle);
            sum += Term(1, pBuffer, lossInDef, saddle, lossInDef * midFactor / (1.0 - pBuffer + midFactor));
        }

        return sum;
    }

    /// <summary>The cumulant generating function's second derivative conditional on the factor.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="saddle">The saddle point.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The derivative.</returns>
    protected double CumGen2ndDerivativeCond(double[] invUncondProbs, double saddle, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double sum = 0.0;

        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);

            // loss in fractional units
            double lossInDef = LossInDefault(invUncondProbs, iName, mktFactor);
            double midFactor = pBuffer * Math.Exp(lossInDef * saddle);
            double denominator = 1.0 - pBuffer + midFactor;
            sum += Term(2, pBuffer, lossInDef, saddle, (lossInDef * lossInDef * midFactor / denominator) - Math.Pow(lossInDef * midFactor / denominator, 2.0));
        }

        return sum;
    }

    /// <summary>The cumulant generating function's third derivative conditional on the factor.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="saddle">The saddle point.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The derivative.</returns>
    protected double CumGen3rdDerivativeCond(double[] invUncondProbs, double saddle, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double sum = 0.0;

        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);
            double lossInDef = LossInDefault(invUncondProbs, iName, mktFactor);

            double midFactor = pBuffer * Math.Exp(lossInDef * saddle);
            double denominator = 1.0 - pBuffer + midFactor;

            double suma0 = denominator;
            double suma1 = lossInDef * midFactor;
            double suma2 = lossInDef * suma1;
            double suma3 = lossInDef * suma2;

            sum += Term(3, pBuffer, lossInDef, saddle, (suma3 + (((2.0 * Math.Pow(suma1, 3.0) / suma0) - (3.0 * suma1 * suma2)) / suma0)) / suma0);
        }

        return sum;
    }

    /// <summary>The cumulant generating function's fourth derivative conditional on the factor.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="saddle">The saddle point.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The derivative.</returns>
    protected double CumGen4thDerivativeCond(double[] invUncondProbs, double saddle, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double sum = 0.0;

        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);
            double lossInDef = LossInDefault(invUncondProbs, iName, mktFactor);

            double midFactor = pBuffer * Math.Exp(lossInDef * saddle);
            double denominator = 1.0 - pBuffer + midFactor;

            double suma0 = denominator;
            double suma1 = lossInDef * midFactor;
            double suma2 = lossInDef * suma1;
            double suma3 = lossInDef * suma2;
            double suma4 = lossInDef * suma3;

            sum += Term(4, pBuffer, lossInDef, saddle, Fourth(suma0, suma1, suma2, suma3, suma4));
        }

        return sum;
    }

    /// <summary>The cumulant generating function and its second to fourth derivatives conditional on the factor,
    /// together, as most methods expand on them.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="saddle">The saddle point.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The cumulant generating function and its second, third and fourth derivatives.</returns>
    protected (double Deriv0, double Deriv2, double Deriv3, double Deriv4) CumGen0234DerivCond(
        double[] invUncondProbs, double saddle, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double deriv0 = 0.0, deriv2 = 0.0, deriv3 = 0.0, deriv4 = 0.0;
        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);
            double lossInDef = LossInDefault(invUncondProbs, iName, mktFactor);

            double midFactor = pBuffer * Math.Exp(lossInDef * saddle);
            double denominator = 1.0 - pBuffer + midFactor;

            double suma0 = denominator;
            double suma1 = lossInDef * midFactor;
            double suma2 = lossInDef * suma1;
            double suma3 = lossInDef * suma2;
            double suma4 = lossInDef * suma3;

            // To do: optimize these:
            deriv0 += Term(0, pBuffer, lossInDef, saddle, Math.Log(suma0));
            deriv2 += Term(2, pBuffer, lossInDef, saddle, (suma2 / suma0) - Math.Pow(suma1 / suma0, 2.0));
            deriv3 += Term(3, pBuffer, lossInDef, saddle, (suma3 + (((2.0 * Math.Pow(suma1, 3.0) / suma0) - (3.0 * suma1 * suma2)) / suma0)) / suma0);
            deriv4 += Term(4, pBuffer, lossInDef, saddle, Fourth(suma0, suma1, suma2, suma3, suma4));
        }

        return (deriv0, deriv2, deriv3, deriv4);
    }

    /// <summary>The cumulant generating function and its second derivative conditional on the factor.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="saddle">The saddle point.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The cumulant generating function and its second derivative.</returns>
    protected (double Deriv0, double Deriv2) CumGen02DerivCond(double[] invUncondProbs, double saddle, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double deriv0 = 0.0, deriv2 = 0.0;
        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);
            double lossInDef = LossInDefault(invUncondProbs, iName, mktFactor);

            double midFactor = pBuffer * Math.Exp(lossInDef * saddle);
            double denominator = 1.0 - pBuffer + midFactor;

            double suma0 = denominator;
            double suma1 = lossInDef * midFactor;
            double suma2 = lossInDef * suma1;

            // To do: optimize these:
            deriv0 += Term(0, pBuffer, lossInDef, saddle, Math.Log(suma0));
            deriv2 += Term(2, pBuffer, lossInDef, saddle, (suma2 / suma0) - Math.Pow(suma1 / suma0, 2.0));
        }

        return (deriv0, deriv2);
    }

    /// <summary>
    /// The saddle point conditional on the factor for a loss level, the date implicit in the probabilities (passed
    /// for performance, the same at every node of the integration). To be done, as in QuantLib: the convergence speed
    /// (see the references and The Oxford Handbook of Credit Derivatives, section 2.9), and a requirement that the
    /// level be below the portfolio's maximum loss.
    /// </summary>
    /// <param name="invUncondPs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="lossLevel">The loss level, a fraction of the whole live portfolio.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum evaluations.</param>
    /// <returns>The saddle point; at a level below or above the bracket's losses, the bracket's end.</returns>
    /// <exception cref="NanoQLException">Every name's conditional probability is 0 or 1, so there is no expansion;
    /// the largest relative loss of a name between them is at or below 1e-5; or the solver fails.</exception>
    protected double FindSaddle(
        double[] invUncondPs, double lossLevel, ReadOnlySpan<double> mktFactor, double accuracy = 1.0e-3, int maxEvaluations = 50)
    {
        int nNames = remainingNotionals.Length;
        double[] lgds = new double[nNames];
        for (int iName = 0; iName < nNames; iName++)
        {
            lgds[iName] = remainingNotionals[iName] * (1.0 - copula.ConditionalRecoveryInvP(invUncondPs[iName], iName, mktFactor));
        }

        // computed limits:
        // position of the name with the largest relative exposure loss (i.e.:
        //   largest: N_i LGD_i / N_{total}), among the names neither certain to default nor certain not to
        int iNamMax = -1;
        double pMaxName = 0.0;
        for (int iName = 0; iName < nNames; iName++)
        {
            double p = copula.ConditionalDefaultProbabilityInvP(invUncondPs[iName], iName, mktFactor);
            if (p > 0.0 && p < 1.0 && (iNamMax < 0 || lgds[iName] > lgds[iNamMax]))
            {
                iNamMax = iName;
                pMaxName = p;
            }
        }

        if (iNamMax < 0)
        {
            throw new NanoQLException("the saddle-point expansion has no saddle point where every name's conditional default probability is 0 or 1");
        }

        // gap to be considered zero at the negative side of the logistic
        //   inversion:
        const double deltaMin = 1.0e-5;
        if (!(lgds[iNamMax] / remainingNotional > deltaMin))
        {
            throw new NanoQLException(string.Create(
                CultureInfo.InvariantCulture,
                $"the saddle-point expansion can't resolve a largest relative loss of {lgds[iNamMax] / remainingNotional}, at or below its {deltaMin} gap"));
        }

        // approximates the  saddle pt corresponding to this minimum; finds
        //   it by using only the smallest logistic term and thus this is
        //   smaller than the true value:
        double saddleMin = 1.0 / (lgds[iNamMax] / remainingNotional)
            * Math.Log(deltaMin * (1.0 - pMaxName) / ((pMaxName * lgds[iNamMax] / remainingNotional) - (pMaxName * deltaMin)));

        // and the associated minimum loss is approximately: (this is thence
        //   the minimum loss we can resolve/invert)
        double minLoss = CumGen1stDerivativeCond(invUncondPs, saddleMin, mktFactor);

        double saddleMax = 1.0 / (lgds[iNamMax] / remainingNotional)
            * Math.Log(((lgds[iNamMax] / remainingNotional) - deltaMin) * (1.0 - pMaxName) / (pMaxName * deltaMin));
        double[] probs = invUncondPs;
        double[] factor = mktFactor.ToArray();
        Brent solver = new Brent().SetMaxEvaluations(maxEvaluations);
        double F(double x) => CumGen1stDerivativeCond(probs, x, factor) - lossLevel;

        // Below or above the bracket's losses QuantLib returns its end, "typically the functionals to integrate will
        // have a low dependency on this point", which misreads pools whose largest loss is a name unlikely to default:
        // here the bracket widens, by its width doubling, until it holds the level, and the point is solved; where no
        // width reaches it (below or above every attainable loss), the widened end.
        if (lossLevel < minLoss)
        {
            double width = saddleMax - saddleMin, lower = saddleMin;
            for (int k = 0; k < 64 && F(lower) > 0.0; k++)
            {
                lower -= width;
                width *= 2.0;
            }

            return F(lower) > 0.0 ? lower : solver.Solve(F, accuracy, (lower + saddleMin) / 2.0, lower, saddleMin);
        }

        double maxLoss = CumGen1stDerivativeCond(invUncondPs, saddleMax, mktFactor);
        if (lossLevel > maxLoss)
        {
            double width = saddleMax - saddleMin, upper = saddleMax;
            for (int k = 0; k < 64 && F(upper) < 0.0; k++)
            {
                upper += width;
                width *= 2.0;
            }

            return F(upper) < 0.0 ? upper : solver.Solve(F, accuracy, (saddleMax + upper) / 2.0, saddleMax, upper);
        }

        double guess = (saddleMin + saddleMax) / 2.0;
        return solver.Solve(F, accuracy, guess, saddleMin, saddleMax);
    }

    /// <summary>The probability conditional on the factor of losing a fraction of the live tranche or
    /// more.</summary>
    /// <param name="invUncondPs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="trancheLossFract">The fraction of the tranche's notional, in [0, 1].</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The conditional probability.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or the saddle point refuses the
    /// node.</exception>
    protected double ProbOverLossCond(double[] invUncondPs, double trancheLossFract, ReadOnlySpan<double> mktFactor)
    {
        double portfFract = attachRatio + (trancheLossFract * (detachRatio - attachRatio)); // these are remaining ratios

        // for non-equity losses add the probability jump at zero tranche
        //   losses (since this method returns prob of losing more or
        //   equal to)
        return ProbOverLossPortfCond(
            invUncondPs,

            // below; should substract realized loses. Use remaining amounts??
            portfFract * BoundBasket.BasketNotional,
            mktFactor);
    }

    /// <summary>
    /// The probability conditional on the factor of the whole (untranched) live portfolio losing an amount or more:
    /// the first order expansion's integral with the high order corrections, equation 8 of R. Martin, K. Thompson and
    /// C. Browne, "Taking to the Saddle", Risk, June 2001, p. 91; exact at a node where every name's probability is 0
    /// or 1.
    /// </summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="loss">The loss, an amount.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The conditional probability.</returns>
    /// <exception cref="NanoQLException">The saddle point refuses the node.</exception>
    protected double ProbOverLossPortfCond(double[] invUncondProbs, double loss, ReadOnlySpan<double> mktFactor)
    {
        if (loss <= NumericLimits.QL_EPSILON)
        {
            return 1.0;
        }

        double relativeLoss = loss / remainingNotional;
        if (relativeLoss >= 1.0 - NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        int nNames = remainingNotionals.Length;

        double averageRecovery = 0.0;
        for (int iName = 0; iName < nNames; iName++)
        {
            averageRecovery += copula.ConditionalRecoveryInvP(invUncondProbs[iName], iName, mktFactor);
        }

        averageRecovery /= nNames;

        double maxAttLossFract = 1.0 - averageRecovery;
        if (relativeLoss > maxAttLossFract)
        {
            return 0.0;
        }

        if (IsDegenerate(invUncondProbs, mktFactor, out double certainLoss))
        {
            return certainLoss >= relativeLoss ? 1.0 : 0.0;
        }

        double saddlePt = FindSaddle(invUncondProbs, relativeLoss, mktFactor);

        (double baseVal, double secondVal, double k3Saddle, double k4Saddle) = CumGen0234DerivCond(invUncondProbs, saddlePt, mktFactor);

        double saddleTo2 = saddlePt * saddlePt;
        double saddleTo3 = saddleTo2 * saddlePt;
        double saddleTo4 = saddleTo3 * saddlePt;
        double saddleTo6 = saddleTo4 * saddleTo2;
        double k3SaddleTo2 = k3Saddle * k3Saddle;

        if (saddlePt > 0.0)
        {
            // <-> (loss > condEL)
            double exponent = baseVal - (relativeLoss * saddlePt) + (.5 * saddleTo2 * secondVal);
            if (Math.Abs(exponent) > 700.0)
            {
                return 0.0;
            }

            return Math.Exp(exponent)
                * Phi.Evaluate(-Math.Abs(saddlePt) * Math.Sqrt(/*saddleTo2 **/secondVal))

                // high order corrections:
                * (1.0 - (saddleTo3 * k3Saddle / 6.0) + (saddleTo4 * k4Saddle / 24.0) + (saddleTo6 * k3SaddleTo2 / 72.0));
        }
        else if (saddlePt == 0.0)
        {
            // <-> (loss == condEL)
            return .5;
        }
        else
        {
            // <->(loss < condEL)
            double exponent = baseVal - (relativeLoss * saddlePt) + (.5 * saddleTo2 * secondVal);
            if (Math.Abs(exponent) > 700.0)
            {
                return 0.0;
            }

            return 1.0 - (Math.Exp(exponent)
                * Phi.Evaluate(-Math.Abs(saddlePt) * Math.Sqrt(/*saddleTo2 **/secondVal))

                // high order corrections:
                * (1.0 - (saddleTo3 * k3Saddle / 6.0) + (saddleTo4 * k4Saddle / 24.0) + (saddleTo6 * k3SaddleTo2 / 72.0)));
        }
    }

    /// <summary>The first order expansion of <see cref="ProbOverLossPortfCond"/>, cheaper with fewer terms
    /// retained, though the cost lies in the saddle point; unused by the statistics.</summary>
    /// <param name="invUncondPs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="loss">The loss, an amount.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The conditional probability.</returns>
    /// <exception cref="NanoQLException">The saddle point refuses the node.</exception>
    protected double ProbOverLossPortfCond1stOrder(double[] invUncondPs, double loss, ReadOnlySpan<double> mktFactor)
    {
        if (loss <= NumericLimits.QL_EPSILON)
        {
            return 1.0;
        }

        int nNames = remainingNotionals.Length;

        double relativeLoss = loss / remainingNotional;
        if (relativeLoss >= 1.0 - NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        // only true for constant recovery models......?
        double averageRecovery = 0.0;
        for (int iName = 0; iName < nNames; iName++)
        {
            averageRecovery += copula.ConditionalRecoveryInvP(invUncondPs[iName], iName, mktFactor);
        }

        averageRecovery /= nNames;

        double maxAttLossFract = 1.0 - averageRecovery;
        if (relativeLoss > maxAttLossFract)
        {
            return 0.0;
        }

        if (IsDegenerate(invUncondPs, mktFactor, out double certainLoss))
        {
            return certainLoss >= relativeLoss ? 1.0 : 0.0;
        }

        double saddlePt = FindSaddle(invUncondPs, relativeLoss, mktFactor);

        (double baseVal, double secondVal) = CumGen02DerivCond(invUncondPs, saddlePt, mktFactor);

        double saddleTo2 = saddlePt * saddlePt;

        if (saddlePt > 0.0)
        {
            // <-> (loss > condEL)
            double exponent = baseVal - (relativeLoss * saddlePt) + (.5 * saddleTo2 * secondVal);
            if (Math.Abs(exponent) > 700.0)
            {
                return 0.0;
            }

            // dangerous exponential; fix me
            return Math.Exp(exponent) * Phi.Evaluate(-Math.Abs(saddlePt) * Math.Sqrt(/*saddleTo2 **/secondVal));
        }
        else if (saddlePt == 0.0)
        {
            // <-> (loss == condEL)
            return .5;
        }
        else
        {
            // <->(loss < condEL)
            double exponent = baseVal - (relativeLoss * saddlePt) + (.5 * saddleTo2 * secondVal);
            if (Math.Abs(exponent) > 700.0)
            {
                return 0.0;
            }

            return 1.0 - (Math.Exp(exponent) * Phi.Evaluate(-Math.Abs(saddlePt) * Math.Sqrt(/*saddleTo2 **/secondVal)));
        }
    }

    /// <summary>The probability density conditional on the factor of the whole (untranched) live portfolio's loss at an
    /// amount, from the expected shortfall's integrals (R. Martin, "The saddle point method and portfolio
    /// optionalities", Risk, December 2006, p. 93); 0 at a node where every name's probability is 0 or 1.</summary>
    /// <param name="invUncondPs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="loss">The loss, an amount.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The conditional density, 0 above the node's largest loss.</returns>
    /// <exception cref="NanoQLException">The saddle point refuses the node.</exception>
    protected double ProbDensityCond(double[] invUncondPs, double loss, ReadOnlySpan<double> mktFactor)
    {
        if (loss <= NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        if (IsDegenerate(invUncondPs, mktFactor, out _))
        {
            return 0.0;
        }

        double relativeLoss = loss / remainingNotional;
        if (IsUnattainable(invUncondPs, relativeLoss, mktFactor))
        {
            return 0.0;
        }

        double saddlePt = FindSaddle(invUncondPs, relativeLoss, mktFactor);

        (double k0Saddle, double k2Saddle, double k3Saddle, double k4Saddle) = CumGen0234DerivCond(invUncondPs, saddlePt, mktFactor);

        // \todo the exponentials below are dangerous and agressive, tame them.
        return (1.0 + (k4Saddle / (8.0 * Math.Pow(k2Saddle, 2.0))) - (5.0 * Math.Pow(k3Saddle, 2.0) / (24.0 * Math.Pow(k2Saddle, 3.0))))
            * Math.Exp(k0Saddle - (saddlePt * relativeLoss))
            / Math.Sqrt(2.0 * Math.PI * k2Saddle);
    }

    /// <summary>The live names' contributions conditional on the factor to a loss of the whole (untranched)
    /// portfolio; exact at a node where every name's probability is 0 or 1.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="loss">The loss, an amount.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The contributions, amounts.</returns>
    /// <exception cref="NanoQLException">The saddle point refuses the node.</exception>
    protected double[] SplitLossCond(double[] invUncondProbs, double loss, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double[] condContrib = new double[nNames];
        if (loss <= NumericLimits.QL_EPSILON)
        {
            return condContrib;
        }

        // a name certain to default contributes its loss, whatever the saddle point, one certain not to nothing
        double saddlePt = IsDegenerate(invUncondProbs, mktFactor, out _)
            ? 0.0
            : FindSaddle(invUncondProbs, loss / remainingNotional, mktFactor);

        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);
            double lossInDef = remainingNotionals[iName] * (1.0 - copula.ConditionalRecoveryInvP(invUncondProbs[iName], iName, mktFactor));
            double midFactor = pBuffer * Math.Exp(lossInDef * saddlePt / remainingNotional);
            double denominator = 1.0 - pBuffer + midFactor;

            condContrib[iName] = Term(1, pBuffer, lossInDef, saddlePt / remainingNotional, lossInDef * midFactor / denominator);
        }

        return condContrib;
    }

    /// <summary>The live portfolio's expected loss conditional on the factor.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The conditional expected loss, an amount.</returns>
    protected double ConditionalExpectedLoss(double[] invUncondProbs, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double eloss = 0.0;
        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);
            eloss += pBuffer * remainingNotionals[iName] * (1.0 - copula.ConditionalRecoveryInvP(invUncondProbs[iName], iName, mktFactor));
        }

        return eloss;
    }

    /// <summary>The live tranche's expected loss conditional on the factor: the tranche of the conditional expected
    /// loss, QuantLib's large-pool reading (XR8 (xxxvi)).</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The conditional expected tranche loss, an amount.</returns>
    protected double ConditionalExpectedTrancheLoss(double[] invUncondProbs, ReadOnlySpan<double> mktFactor)
    {
        double eloss = ConditionalExpectedLoss(invUncondProbs, mktFactor);
        return Math.Min(Math.Max(eloss - (attachRatio * remainingNotional), 0.0), (detachRatio - attachRatio) * remainingNotional);
    }

    /// <summary>The live names' expected shortfall contributions conditional on the factor at a loss, a Gaussian
    /// approximation of each; unused by the statistics.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="lossPerc">The loss, an amount.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The contributions.</returns>
    protected double[] ExpectedShortfallSplitCond(double[] invUncondProbs, double lossPerc, ReadOnlySpan<double> mktFactor)
    {
        int nNames = remainingNotionals.Length;
        double[] lgds = new double[nNames];
        for (int iName = 0; iName < nNames; iName++)
        {
            lgds[iName] = remainingNotionals[iName] * (1.0 - copula.ConditionalRecoveryInvP(invUncondProbs[iName], iName, mktFactor));
        }

        double[] vola = new double[nNames], mu = new double[nNames];
        double volaTot = 0.0, muTot = 0.0;
        for (int iName = 0; iName < nNames; iName++)
        {
            double pBuffer = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);
            mu[iName] = lgds[iName] * pBuffer / remainingNotionals[iName];
            muTot += lgds[iName] * pBuffer;
            vola[iName] = lgds[iName] * lgds[iName] * pBuffer * (1.0 - pBuffer) / remainingNotionals[iName];
            volaTot += lgds[iName] * lgds[iName] * pBuffer * (1.0 - pBuffer);
        }

        for (int iName = 0; iName < nNames; iName++)
        {
            vola[iName] = vola[iName] / volaTot;
        }

        double[] esfPartition = new double[nNames];
        for (int iName = 0; iName < nNames; iName++)
        {
            double uEdisp = (lossPerc - muTot) / Math.Sqrt(volaTot);
            esfPartition[iName] = (mu[iName] * Phi.Evaluate(uEdisp)) + (vola[iName] * Density.Evaluate(uEdisp));
        }

        return esfPartition;
    }

    /// <summary>The tranche's expected shortfall conditional on the factor, from the whole portfolio's with a tranche
    /// correction; unused by the statistics. To be done, as in QuantLib: a general expression valid in every
    /// situation.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="lossPerc">The percentile's loss, an amount.</param>
    /// <param name="percentile">The percentile.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The conditional shortfall.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or the saddle point refuses the
    /// node.</exception>
    protected double ExpectedShortfallTrancheCond(double[] invUncondProbs, double lossPerc, double percentile, ReadOnlySpan<double> mktFactor)
    {
        Basket basket = BoundBasket;

        // tranche correction term:
        double correctionTerm = 0.0;
        double probLOver = ProbOverLossPortfCond(invUncondProbs, basket.DetachmentAmount, mktFactor);
        if (basket.AttachmentAmount > NumericLimits.QL_EPSILON)
        {
            if (lossPerc < basket.AttachmentAmount)
            {
                correctionTerm = (((basket.DetachmentAmount - (2.0 * basket.AttachmentAmount))
                    * ProbOverLossPortfCond(invUncondProbs, lossPerc, mktFactor)) + (basket.AttachmentAmount * probLOver)) / (1.0 - percentile);
            }
            else
            {
                correctionTerm = (((percentile - 1) * basket.AttachmentAmount) + (basket.DetachmentAmount * probLOver)) / (1.0 - percentile);
            }
        }

        return ExpectedShortfallFullPortfolioCond(invUncondProbs, Math.Max(lossPerc, basket.AttachmentAmount), mktFactor)
            + ExpectedShortfallFullPortfolioCond(invUncondProbs, basket.DetachmentAmount, mktFactor)
            - correctionTerm;
    }

    /// <summary>The whole portfolio's expected loss above an amount conditional on the factor, Martin's 2006
    /// expression (see also S. A. Broda and M. S. Paolella, "Saddlepoint approximation of expected shortfall for
    /// transformed means", Amsterdam School of Economics); exact at a node where every name's probability is 0 or
    /// 1.</summary>
    /// <param name="invUncondProbs">The inverse cumulatives of the names' default probabilities.</param>
    /// <param name="lossPerc">The loss, an amount.</param>
    /// <param name="mktFactor">The factor's values.</param>
    /// <returns>The conditional expected loss above the amount, 0 above the node's largest loss.</returns>
    /// <exception cref="NanoQLException">The expansion refuses the node.</exception>
    protected double ExpectedShortfallFullPortfolioCond(double[] invUncondProbs, double lossPerc, ReadOnlySpan<double> mktFactor)
    {
        double lossPercRatio = lossPerc / remainingNotional;
        double elCond = ConditionalExpectedLoss(invUncondProbs, mktFactor);
        if (IsDegenerate(invUncondProbs, mktFactor, out _))
        {
            // the certain loss, where it reaches the amount
            return elCond * ProbOverLossPortfCond(invUncondProbs, lossPerc, mktFactor);
        }

        if (IsUnattainable(invUncondProbs, lossPercRatio, mktFactor))
        {
            return 0.0;
        }

        double saddlePt = FindSaddle(invUncondProbs, lossPercRatio, mktFactor);

        // Martin 2006:
        return (elCond * ProbOverLossPortfCond(invUncondProbs, lossPerc, mktFactor))
            + ((lossPerc - elCond) * ProbDensityCond(invUncondProbs, lossPerc, mktFactor) / saddlePt);
    }

    /// <inheritdoc/>
    protected override void ResetModel()
    {
        Basket basket = BoundBasket;
        remainingNotionals = basket.RemainingNotionals();
        remainingNotional = basket.RemainingNotional();
        attachRatio = Math.Min(basket.RemainingAttachmentAmount() / basket.RemainingNotional(), 1.0);
        detachRatio = Math.Min(basket.RemainingDetachmentAmount() / basket.RemainingNotional(), 1.0);
        copula = (ConstantLossLatentModel<TCopula, TInitTraits>)copula.ResetBasket(SourceBasket);
    }

    // A tail integrated over the factor, refused outside [0, 1] beyond the quadrature's rounding: the expansion
    // outside its domain.
    private static double CheckedTail(double value)
    {
        const double rounding = 1e-12;
        if (!(value >= -rounding && value <= 1.0 + rounding))
        {
            throw new NanoQLException(string.Create(
                CultureInfo.InvariantCulture,
                $"the saddle-point expansion reads a tail probability of {value}, outside its domain [0, 1]"));
        }

        return value;
    }

    // A name's term of the cumulant generating function or its derivative of an order at a saddle point: QuantLib's
    // arithmetic, given, wherever it is finite; nothing for a name certain not to default, whose terms are 0 but read
    // 0 times infinity where the exponential overflows; a name certain to default its linear term, its loss times the
    // point and its loss; and where QuantLib's form overflows, the logistic form of the tilted probability q:
    // log(1 - p) + log(1 + e^t), l q, l^2 q(1 - q), l^3 q(1 - q)(1 - 2q), l^4 q(1 - q)(1 - 6q + 6q^2).
    private static double Term(int order, double p, double l, double s, double quantLibs)
    {
        if (p == 0.0)
        {
            return 0.0;
        }

        if (p == 1.0)
        {
            return order == 0 ? l * s : order == 1 ? l : 0.0;
        }

        if (double.IsFinite(quantLibs))
        {
            return quantLibs;
        }

        double t = (l * s) + Math.Log(p) - Math.Log(1.0 - p);
        double q = 1.0 / (1.0 + Math.Exp(-t));
        return order switch
        {
            0 => Math.Log(1.0 - p) + Math.Max(t, 0.0) + Math.Log(1.0 + Math.Exp(-Math.Abs(t))),
            1 => l * q,
            2 => l * l * q * (1.0 - q),
            3 => l * l * l * q * (1.0 - q) * (1.0 - (2.0 * q)),
            _ => l * l * l * l * q * (1.0 - q) * (1.0 - (6.0 * q) + (6.0 * q * q)),
        };
    }

    // The cumulants' fourth derivative term of a name.
    private static double Fourth(double suma0, double suma1, double suma2, double suma3, double suma4) =>
        (suma4 + (((-4.0 * suma1 * suma3) - (3.0 * suma2 * suma2)
            + (((12.0 * suma1 * suma1 * suma2) - (6.0 * Math.Pow(suma1, 4.0) / suma0)) / suma0)) / suma0)) / suma0;

    // A name's loss given default, a fraction of the live notional.
    private double LossInDefault(double[] invUncondProbs, int iName, ReadOnlySpan<double> mktFactor) =>
        remainingNotionals[iName] * (1.0 - copula.ConditionalRecoveryInvP(invUncondProbs[iName], iName, mktFactor)) / remainingNotional;

    // Whether every name's conditional default probability is 0 or 1 at the node, and the certain loss, a fraction of
    // the live notional: the node's loss is then exact.
    private bool IsDegenerate(double[] invUncondProbs, ReadOnlySpan<double> mktFactor, out double certainLoss)
    {
        certainLoss = 0.0;
        bool degenerate = true;
        for (int iName = 0; iName < remainingNotionals.Length; iName++)
        {
            double p = copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor);
            if (p == 1.0)
            {
                certainLoss += LossInDefault(invUncondProbs, iName, mktFactor);
            }
            else if (p != 0.0)
            {
                degenerate = false;
            }
        }

        return degenerate;
    }

    // Whether a loss, a fraction of the live notional, lies at or above the largest the node reaches, its names' losses
    // given default but those certain not to default, K1's limit, beyond the sum's rounding: no saddle point reaches
    // it.
    private bool IsUnattainable(double[] invUncondProbs, double relativeLoss, ReadOnlySpan<double> mktFactor)
    {
        const double rounding = 1e-12;
        double attainable = 0.0;
        for (int iName = 0; iName < remainingNotionals.Length; iName++)
        {
            if (copula.ConditionalDefaultProbabilityInvP(invUncondProbs[iName], iName, mktFactor) != 0.0)
            {
                attainable += LossInDefault(invUncondProbs, iName, mktFactor);
            }
        }

        return relativeLoss >= attainable - rounding;
    }

    private double[] InverseProbabilities(Date d)
    {
        double[] invUncondProbs = [.. BoundBasket.RemainingProbabilities(d)];
        for (int i = 0; i < invUncondProbs.Length; i++)
        {
            invUncondProbs[i] = copula.InverseCumulativeY(invUncondProbs[i], i);
        }

        return invUncondProbs;
    }
}
