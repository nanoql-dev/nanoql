// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/gaussianlhplossmodel.hpp ql/experimental/credit/gaussianlhplossmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The portfolio loss model of a large homogeneous pool on a Gaussian one-factor copula, its expected tranche loss
/// analytic: Anna Kalemanova, Bernd Schmid, Ralf Werner, "The Normal Inverse Gaussian Distribution for Synthetic CDO
/// pricing.", Journal of Derivatives, Vol. 14, No. 3, (Spring 2007), pp. 80-93.
/// </summary>
/// <remarks>
/// <para>
/// A latent model implementing <see cref="IDefaultLossModel"/> over itself: its correlation is a
/// <see langword="double"/>, so QuantLib's quote and value constructors merge. Bound to a basket, a copy keeps the live
/// names' rows and recoveries and prices the restated tranche.
/// </para>
/// <para>
/// The pool's average default probability and recovery are weighted by the notionals of the names alive at the
/// evaluation date. QuantLib's reads the notionals alive at the statistic's date beside the probabilities and the
/// first <c>remainingSize()</c> recoveries of the evaluation date (<c>gaussianlhplossmodel.hpp:153-190</c>), so a
/// default programmed after the evaluation date reads past its vectors, undefined (2.24789, 2.24363, 1.77310 and a
/// throw in four runs); here the model reads QuantLib's model without the event (5.50809;
/// <c>Divergences/LhpProgrammedDefaultTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class GaussianLHPLossModel
    : LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>, IDefaultLossModel, IBaseCorrelationModel<GaussianCopulaPolicyInitTraits>
{
    private static readonly CumulativeNormalDistribution Phi = new();
    private readonly double sqrt1minuscorrel;
    private readonly double beta;
    private readonly BivariateCumulativeNormalDistributionWe04DP biphi;
    private ImmutableArray<double> rrQuotes;
    private Basket? basket;

    /// <summary>Initializes a new instance of the <see cref="GaussianLHPLossModel"/> class on recovery
    /// quotes.</summary>
    /// <param name="correlation">The correlation, the factor weight's square.</param>
    /// <param name="quotes">The names' recovery quotes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="quotes"/> or a quote is
    /// <see langword="null"/>.</exception>
    public GaussianLHPLossModel(double correlation, IReadOnlyList<RecoveryRateQuote> quotes)
        : base(Math.Sqrt(correlation), (quotes ?? throw new ArgumentNullException(nameof(quotes))).Count)
    {
        sqrt1minuscorrel = Math.Sqrt(1.0 - correlation);
        beta = Math.Sqrt(correlation);
        biphi = new BivariateCumulativeNormalDistributionWe04DP(-Math.Sqrt(correlation));
        rrQuotes = [.. quotes.Select(q => (q ?? throw new ArgumentNullException(nameof(quotes))).Value)];
    }

    /// <summary>Initializes a new instance of the <see cref="GaussianLHPLossModel"/> class on recovery
    /// rates.</summary>
    /// <param name="correlation">The correlation, the factor weight's square.</param>
    /// <param name="recoveries">The names' recovery rates, each in [0, 1].</param>
    /// <exception cref="ArgumentNullException"><paramref name="recoveries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A recovery lies outside [0, 1] (its quote's refusal).</exception>
    public GaussianLHPLossModel(double correlation, IReadOnlyList<double> recoveries)
        : this(correlation, [.. (recoveries ?? throw new ArgumentNullException(nameof(recoveries))).Select(r => new RecoveryRateQuote(r))])
    {
    }

    /// <inheritdoc/>
    public Basket? Basket => basket;

    /// <summary>The model at a correlation over the given recoveries, the copula's parameters unread: QuantLib's
    /// base-correlation sub-model.</summary>
    /// <param name="correlation">The correlation.</param>
    /// <param name="recoveries">The names' recoveries.</param>
    /// <param name="traits">The Gaussian copula's parameters, none.</param>
    /// <returns>The model, unbound.</returns>
    /// <exception cref="ArgumentException">A recovery lies outside [0, 1].</exception>
    public static IDefaultLossModel ForCorrelation(double correlation, ImmutableArray<double> recoveries, GaussianCopulaPolicyInitTraits traits) =>
        new GaussianLHPLossModel(correlation, recoveries);

    /// <summary>A copy of the model bound to a basket of its names, or fewer: to the basket's live names over its
    /// restated tranche, the copy keeping their rows and recoveries.</summary>
    /// <param name="basket">The basket.</param>
    /// <returns>The bound copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="basket"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The model has fewer names than the basket, whose recoveries QuantLib's reads
    /// past its quotes ("Incompatible basket and model sizes."), or every name in it has defaulted.</exception>
    public IDefaultLossModel SetBasket(Basket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        if (Size < basket.Size)
        {
            throw new NanoQLException("Incompatible basket and model sizes.");
        }

        Basket pricing = basket.PricingBasket
            ?? throw new NanoQLException("every name in the basket has defaulted: no loss model can be bound");
        var copy = (GaussianLHPLossModel)MemberwiseClone();
        if (basket.ModelRows.Length != Size)
        {
            ImmutableArray<int> rows = basket.ModelRows;
            copy.RestrictRows(rows);
            copy.rrQuotes = [.. rows.Select(i => rrQuotes[i])];
        }

        copy.basket = pricing;
        return copy;
    }

    /// <inheritdoc/>
    public double ExpectedTrancheLoss(Date d)
    {
        Basket b = BoundBasket;

        // can calls to Basket::remainingNotional(d) be cached?
        double remainingfullNot = b.RemainingNotional();
        double averageRR = AverageRecovery(d);
        double prob = AverageProb(d);
        double attach = b.RemainingAttachmentAmount() / remainingfullNot;
        double detach = b.RemainingDetachmentAmount() / remainingfullNot;

        return ExpectedTrancheLossImpl(remainingfullNot, prob, averageRR, attach, detach);
    }

    /// <summary>The probability of a tranche loss at or above a level, given as a fraction of the live
    /// tranche.</summary>
    /// <param name="d">The date.</param>
    /// <param name="remainingLossFraction">The level, in [0, 1].</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level lies outside [0, 1] ("Incorrect loss
    /// fraction.").</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public double ProbOverLoss(Date d, double remainingLossFraction)
    {
        // these test goes into basket
        if (!(remainingLossFraction >= 0.0 && remainingLossFraction <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(remainingLossFraction), remainingLossFraction, "Incorrect loss fraction.");
        }

        Basket b = BoundBasket;

        // live unerlying portfolio loss fraction (remaining portf fraction)
        double remainingBasktNot = b.RemainingNotional();
        double attach = Math.Min(b.RemainingAttachmentAmount() / remainingBasktNot, 1.0);
        double detach = Math.Min(b.RemainingDetachmentAmount() / remainingBasktNot, 1.0);

        double portfFract = attach + (remainingLossFraction * (detach - attach));

        double averageRR = AverageRecovery(d);
        double maxAttLossFract = 1.0 - averageRR;
        if (portfFract > maxAttLossFract)
        {
            return 0.0;
        }

        // for non-equity losses add the probability jump at zero tranche
        //   losses (since this method returns prob of losing more or
        //   equal to)
        if (portfFract <= NumericLimits.QL_EPSILON)
        {
            return 1.0;
        }

        double prob = AverageProb(d);

        double ip = InverseCumulativeNormal.StandardValue(prob);
        double invFlightK = (ip - (sqrt1minuscorrel * InverseCumulativeNormal.StandardValue(portfFract / (1.0 - averageRR)))) / beta;

        return Phi.Evaluate(invFlightK); // probOver
    }

    /// <summary>The expected shortfall above a percentile, as an amount: a transformation from the expected tranche
    /// loss.</summary>
    /// <param name="d">The date.</param>
    /// <param name="percentile">The percentile, in [0, 1].</param>
    /// <returns>The shortfall.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The percentile lies outside [0, 1].</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public double ExpectedShortfall(Date d, double percentile)
    {
        // loss as a fraction of the live portfolio
        double ptflLossPerc = PercentilePortfolioLossFraction(d, percentile);
        Basket b = BoundBasket;
        double remainingNot = b.RemainingNotional();
        double attach = Math.Min(b.RemainingAttachmentAmount() / remainingNot, 1.0);
        double detach = Math.Min(b.RemainingDetachmentAmount() / remainingNot, 1.0);

        if (ptflLossPerc >= detach - NumericLimits.QL_EPSILON)
        {
            return remainingNot * (detach - attach); // equivalent
        }

        double maxLossLevel = Math.Max(attach, ptflLossPerc);
        double prob = AverageProb(d);
        double averageRR = AverageRecovery(d);

        double valA = ExpectedTrancheLossImpl(remainingNot, prob, averageRR, maxLossLevel, detach);

        // from fraction of basket notional to fraction of tranche notional
        double valB = ProbOverLoss(d, Math.Min(Math.Max((maxLossLevel - attach) / (detach - attach), 0.0), 1.0));
        return (valA + ((maxLossLevel - attach) * remainingNot * valB)) / (1.0 - percentile);
    }

    /// <summary>The tranche's loss at a percentile, as an amount.</summary>
    /// <param name="d">The date.</param>
    /// <param name="percentile">The percentile, in [0, 1].</param>
    /// <returns>The loss.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The percentile lies outside [0, 1].</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public double Percentile(Date d, double percentile)
    {
        Basket b = BoundBasket;
        double remainingNot = b.RemainingNotional();
        double attach = Math.Min(b.RemainingAttachmentAmount() / remainingNot, 1.0);
        double detach = Math.Min(b.RemainingDetachmentAmount() / remainingNot, 1.0);
        return remainingNot * Math.Min(Math.Max(PercentilePortfolioLossFraction(d, percentile) - attach, 0.0), detach - attach);
    }

    /// <summary>A name's recovery, its quote's.</summary>
    /// <param name="d">The date, unread.</param>
    /// <param name="iName">The name's position in the bound basket.</param>
    /// <param name="defaultKey">The key, unread.</param>
    /// <returns>The recovery.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="iName"/> isn't one of the names, where QuantLib reads past its end.
    /// </exception>
    public double ExpectedRecovery(Date d, int iName, DefaultProbKey defaultKey)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(iName);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(iName, rrQuotes.Length);
        return rrQuotes[iName];
    }

    /// <summary>The pool's default probability by a date, weighted by the live notionals.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public double AverageProb(Date d)
    {
        // weighted average by programmed exposure.
        Basket b = BoundBasket;
        ImmutableArray<double> probs = b.RemainingProbabilities(d); // use remaining basket
        ImmutableArray<double> remainingNots = b.RemainingNotionals();
        double sum = 0.0;
        for (int i = 0; i < probs.Length; i++)
        {
            sum += probs[i] * remainingNots[i];
        }

        return sum / b.RemainingNotional();
    }

    /// <summary>
    /// The pool's recovery by a date, weighted by the live notionals times the default probabilities, so that the
    /// aggregated pool's expected loss is the pool's; 0 where every weight is 0.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <returns>The recovery.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    public double AverageRecovery(Date d)
    {
        Basket b = BoundBasket;
        ImmutableArray<double> probs = b.RemainingProbabilities(d);
        int n = b.RemainingSize();
        ImmutableArray<double> notionals = b.RemainingNotionals();
        double denominator = 0.0;
        for (int i = 0; i < notionals.Length; i++)
        {
            denominator += notionals[i] * probs[i];
        }

        if (denominator == 0.0)
        {
            return 0.0;
        }

        double sum = 0.0;
        for (int i = 0; i < n; i++)
        {
            sum += rrQuotes[i] * (notionals[i] * probs[i]);
        }

        return sum / denominator;
    }

    /// <summary>The untranched pool's loss at a percentile, as a fraction of the live pool.</summary>
    /// <param name="d">The date.</param>
    /// <param name="perctl">The percentile, in [0, 1].</param>
    /// <returns>The loss fraction.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The percentile lies outside [0, 1] ("Percentile argument out of
    /// bounds.").</exception>
    /// <exception cref="NanoQLException">The model isn't bound to a basket.</exception>
    protected double PercentilePortfolioLossFraction(Date d, double perctl)
    {
        // this test goes into basket
        if (!(perctl >= 0.0 && perctl <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(perctl), perctl, "Percentile argument out of bounds.");
        }

        if (perctl == 0.0)
        {
            return 0.0; // portfl == attach
        }

        if (perctl == 1.0)
        {
            perctl = 1.0 - NumericLimits.QL_EPSILON; // portfl == detach
        }

        return (1.0 - AverageRecovery(d))
            * Phi.Evaluate((InverseCumulativeNormal.StandardValue(AverageProb(d)) + (beta * InverseCumulativeNormal.StandardValue(perctl)))
                / sqrt1minuscorrel);
    }

    private Basket BoundBasket => basket ?? throw new NanoQLException("No portfolio basket set.");

    // attachLimit as a fraction of the underlying live portfolio notional
    private double ExpectedTrancheLossImpl(
        double remainingNot, // << at the given date 'd'
        double prob, // << at the given date 'd'
        double averageRR, // << at the given date 'd'
        double attachLimit, // these are percentual values:
        double detachLimit)
    {
        if (attachLimit >= detachLimit)
        {
            return 0.0; // or is it an error?
        }

        // expected remaining notional:
        if (remainingNot == 0.0)
        {
            return 0.0;
        }

        const double one = 1.0 - 1.0e-12; // FIXME DUE TO THE INV CUMUL AT 1
        double k1 = Math.Min(one, attachLimit / (1.0 - averageRR)) + NumericLimits.QL_EPSILON;
        double k2 = Math.Min(one, detachLimit / (1.0 - averageRR)) + NumericLimits.QL_EPSILON;

        if (prob > 0)
        {
            double ip = InverseCumulativeNormal.StandardValue(prob);
            double invFlightK1 = (ip - (sqrt1minuscorrel * InverseCumulativeNormal.StandardValue(k1))) / beta;
            double invFlightK2 = (ip - (sqrt1minuscorrel * InverseCumulativeNormal.StandardValue(k2))) / beta;

            return remainingNot * ((detachLimit * Phi.Evaluate(invFlightK2))
                - (attachLimit * Phi.Evaluate(invFlightK1))
                + ((1.0 - averageRR) * (biphi.Evaluate(ip, -invFlightK2) - biphi.Evaluate(ip, -invFlightK1))));
        }

        return 0.0;
    }
}
