// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/basket.hpp ql/experimental/credit/basket.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: densityTrancheLoss, never ported: basket.hpp declares it and nothing defines it, so QuantLib never compiles a call to it

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

// The realized defaults' form is the Open Source Risk Engine's (ORE).
/// <summary>
/// A credit basket: credit names, each with a notional, drawn from a pool of issuers, and a tranche of the pool's loss,
/// with the default loss model that gives its loss statistics.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, of its terms and its evaluation context: it computes QuantLib's <c>computeBasket()</c> at the context's
/// evaluation date at construction, its live names, notionals and keys, its settled loss and its remaining tranche, and
/// keeps the context, since QuantLib's statistics take no date of evaluation and its cached state is that date's.
/// QuantLib's constructor computes that state before it sets the tranche's attachment and detachment amounts, so its
/// remaining amounts read 0 until the first statistic (<c>basket.cpp:57-68</c>); here they are computed after them
/// (<c>Divergences/BasketConstructionOrderTests.cs</c>).
/// </para>
/// <para>
/// <see cref="SetLossModel"/> returns a basket of the same terms and context whose <see cref="LossModel"/> is the model
/// bound to it, its precomputation run once, there: QuantLib's flows set a model on a basket and change it through
/// observers, which become rebuilds. A basket with no model refuses its statistics with QuantLib's "Basket has no
/// default loss model assigned."
/// </para>
/// <para>
/// The live reads take the live names' positions: QuantLib's <c>remainingNotionals(d)</c> and
/// <c>remainingProbabilities(d)</c> index the live list's positions, not its names (<c>basket.cpp:196-220</c>), so after
/// the first of three names defaults they read the first two names' values (<c>Divergences/BasketLiveReadsTests.cs</c>).
/// Every curve read before a curve's reference date is a default probability of 0, where QuantLib's refuses it
/// ("negative time").
/// </para>
/// <para>
/// <b>Realized defaults</b>. QuantLib's basket misprices once a name has defaulted: its expected tranche loss adds the
/// whole settled loss, its models tranche the live names' loss against min(D, A + max(0, L − A)) where its own header
/// means max(0, A − L), its n-th-to-default probabilities ignore the defaults, and an unsettled default's recovery is a
/// null; its header warns "Baskets with realized defaults not tested/WIP." Here the names defaulted by the evaluation
/// date leave the pricing basket: the lost notional L writes the tranche down from its attachment, the recovered
/// notional amortizes it from its detachment's side (min(D, N − recovered)), and the model is bound to a basket of the
/// live names over that restated tranche, each latent model restricted to their rows (<see cref="PricingBasket"/>). Its
/// statistics add the realized tranche loss: the expected tranche loss, the percentile and the expected shortfall add
/// it to the live model's; <see cref="ProbOverLoss"/> maps its inception fraction onto the restated tranche, 1 at or
/// below the realized loss; the loss distribution is the live one shifted by the realized loss; the event counts take
/// the order less the realized defaults, 1 (and QuantLib's zeros) once they reach it; the vectors run over the live
/// names; the name-indexed reads take pool positions, a defaulted name's recovery its settled one and its default
/// correlation refused. A default not settled for the name's key's seniority is refused, QuantLib's own <c>\todo</c>
/// (<c>basket.cpp:128-131</c>). An event dated after the evaluation date isn't realized there, so it stays out of the
/// restatement, and the reads with a date argument keep QuantLib's use of such "programmed" events for jump-to-default
/// analysis (<c>basket.hpp:138-141</c>). On a basket with no realized default, QuantLib's values, bit for bit.
/// <c>Divergences/BasketRealizedDefaultsTests.cs</c>.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class Basket
{
    private readonly Pool pool;
    private readonly ImmutableArray<double> notionals;
    private readonly ImmutableArray<int> liveList;
    private readonly ImmutableArray<string> liveNames;
    private readonly ImmutableArray<double> liveNotionals;
    private readonly ImmutableArray<DefaultProbKey> liveKeys;
    private readonly double settledLoss;
    private readonly double remainingNotional;
    private readonly double attachAmount;
    private readonly double detachAmount;
    private readonly double realizedTrancheLoss;
    private readonly Basket? pricingBasket;

    /// <summary>Initializes a new instance of the <see cref="Basket"/> class: constant notional positions subject to
    /// default risk only, with no loss model.</summary>
    /// <param name="refDate">The basket's inception date, from which default events count.</param>
    /// <param name="names">The names, which QuantLib ignores for the pool's, and so does nanoql.</param>
    /// <param name="notionals">The notionals, one per pool name.</param>
    /// <param name="pool">The pool.</param>
    /// <param name="context">The evaluation context, at whose date the basket's state is computed.</param>
    /// <param name="attachmentRatio">The tranche's attachment, a fraction of the inception notional.</param>
    /// <param name="detachmentRatio">The tranche's detachment, a fraction of the inception notional.</param>
    /// <param name="claim">The claim, the same for every name; a <see cref="FaceValueClaim"/> when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">An argument but <paramref name="claim"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="refDate"/> is <c>default(Date)</c>; the notionals are empty
    /// ("notionals empty"); the ratios aren't 0 ≤ attachment ≤ detachment ≤ 1 ("invalid attachment/detachment ratio");
    /// or the notionals don't number the pool's names ("unmatched data entry sizes in basket").</exception>
    /// <exception cref="NanoQLException">The evaluation date precedes the inception ("Target date lies before basket
    /// inception"), or a name has defaulted by it without a settlement for its key's seniority.</exception>
    public Basket(
        Date refDate,
        IEnumerable<string> names,
        IEnumerable<double> notionals,
        Pool pool,
        EvaluationContext context,
        double attachmentRatio = 0.0,
        double detachmentRatio = 1.0,
        Claim? claim = null)
    {
        Date.ThrowIfDefault(refDate, nameof(refDate));
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(notionals);
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(context);
        this.notionals = [.. notionals];
        if (this.notionals.IsEmpty)
        {
            throw new ArgumentException("notionals empty", nameof(notionals));
        }

        if (!(attachmentRatio >= 0 && attachmentRatio <= detachmentRatio && detachmentRatio <= 1))
        {
            throw new ArgumentException("invalid attachment/detachment ratio", nameof(attachmentRatio));
        }

        if (this.notionals.Length != pool.Size)
        {
            throw new ArgumentException("unmatched data entry sizes in basket", nameof(notionals));
        }

        RefDate = refDate;
        this.pool = pool;
        Context = context;
        Claim = claim ?? new FaceValueClaim();
        AttachmentRatio = attachmentRatio;
        DetachmentRatio = detachmentRatio;
        DefaultKeys = pool.DefaultKeys;

        // At this point Issuers in the pool might or might not have
        //   probability term structures for the defultKeys(eventType+
        //   currency+seniority) entering in this basket. This is not
        //   necessarily a problem.
        double basketNotional = 0.0, attachmentAmount = 0.0, detachmentAmount = 0.0;
        foreach (double notional in this.notionals)
        {
            basketNotional += notional;
            attachmentAmount += notional * attachmentRatio;
            detachmentAmount += notional * detachmentRatio;
        }

        BasketNotional = basketNotional;
        AttachmentAmount = attachmentAmount;
        DetachmentAmount = detachmentAmount;
        TrancheNotional = detachmentAmount - attachmentAmount;

        // QuantLib's computeBasket() at the evaluation date, after the tranche's amounts
        Date today = context.EvaluationDate;
        CheckDate(today);
        liveList = LiveList(today);
        liveKeys = [.. liveList.Select(i => DefaultKeys[i])];
        (double loss, double recovered) = Realized(today, unsettledRefused: true);
        settledLoss = loss;
        remainingNotional = RemainingNotional(today);
        liveNotionals = [.. liveList.Select(i => Exposure(pool.Names[i], today))];
        liveNames = [.. liveList.Select(i => pool.Names[i])];
        (attachAmount, detachAmount) = Restated(loss, recovered, remainingNotional);
        double effectiveDetachment = Math.Min(DetachmentAmount, BasketNotional - recovered);
        realizedTrancheLoss = Math.Clamp(loss - AttachmentAmount, 0.0, Math.Max(0.0, effectiveDetachment - AttachmentAmount));
        pricingBasket = liveList.Length == pool.Size
            ? this
            : remainingNotional > 0.0 ? Restate() : null;
    }

    // The basket of a source's terms, context and state, bound to a model.
    private Basket(Basket source, IDefaultLossModel? lossModel)
    {
        pool = source.pool;
        notionals = source.notionals;
        RefDate = source.RefDate;
        Context = source.Context;
        Claim = source.Claim;
        AttachmentRatio = source.AttachmentRatio;
        DetachmentRatio = source.DetachmentRatio;
        DefaultKeys = source.DefaultKeys;
        BasketNotional = source.BasketNotional;
        AttachmentAmount = source.AttachmentAmount;
        DetachmentAmount = source.DetachmentAmount;
        TrancheNotional = source.TrancheNotional;
        liveList = source.liveList;
        liveKeys = source.liveKeys;
        settledLoss = source.settledLoss;
        remainingNotional = source.remainingNotional;
        liveNotionals = source.liveNotionals;
        liveNames = source.liveNames;
        attachAmount = source.attachAmount;
        detachAmount = source.detachAmount;
        realizedTrancheLoss = source.realizedTrancheLoss;
        pricingBasket = ReferenceEquals(source.pricingBasket, source) ? this : source.pricingBasket;
        if (lossModel is not null)
        {
            if (pricingBasket is null)
            {
                throw new NanoQLException("every name in the basket has defaulted: no loss model can be bound");
            }

            LossModel = lossModel.SetBasket(this);
        }
    }

    /// <summary>Gets the basket's inception date.</summary>
    public Date RefDate { get; }

    /// <summary>Gets the evaluation context, at whose date the basket's state is computed.</summary>
    public EvaluationContext Context { get; }

    /// <summary>Gets the claim, the same for every name.</summary>
    public Claim Claim { get; }

    /// <summary>Gets the default loss model bound to the basket, or <see langword="null"/> where none is.</summary>
    public IDefaultLossModel? LossModel { get; }

    /// <summary>Gets the number of names at inception.</summary>
    public int Size => pool.Size;

    /// <summary>Gets the names at inception, the pool's.</summary>
    public ImmutableArray<string> Names => pool.Names;

    /// <summary>Gets the notionals at inception.</summary>
    public ImmutableArray<double> Notionals => notionals;

    /// <summary>Gets the pool.</summary>
    public Pool Pool => pool;

    /// <summary>Gets the keys each name enters the basket with, in pool order.</summary>
    public ImmutableArray<DefaultProbKey> DefaultKeys { get; }

    /// <summary>Gets the tranche's attachment, a fraction of the inception notional.</summary>
    public double AttachmentRatio { get; }

    /// <summary>Gets the tranche's detachment, a fraction of the inception notional.</summary>
    public double DetachmentRatio { get; }

    /// <summary>Gets the inception notional, losses ignored.</summary>
    public double BasketNotional { get; }

    /// <summary>Gets the tranche's inception notional, losses ignored.</summary>
    public double TrancheNotional { get; }

    /// <summary>Gets the attachment amount: the attachment ratio times each notional, summed.</summary>
    public double AttachmentAmount { get; }

    /// <summary>Gets the detachment amount: the detachment ratio times each notional, summed.</summary>
    public double DetachmentAmount { get; }

    /// <summary>Gets the inception notional, the notionals summed.</summary>
    public double Notional => notionals.Sum();

    /// <summary>Gets the losses settled by the evaluation date.</summary>
    public double SettledLoss() => settledLoss;

    /// <summary>Gets the losses settled by the evaluation date: QuantLib's <c>cumulatedLoss()</c>, the same as
    /// <see cref="SettledLoss()"/>.</summary>
    public double CumulatedLoss() => settledLoss;

    /// <summary>Gets the notional of the names alive at the evaluation date.</summary>
    public double RemainingNotional() => remainingNotional;

    /// <summary>Gets the notionals of the names alive at the evaluation date.</summary>
    public ImmutableArray<double> RemainingNotionals() => liveNotionals;

    /// <summary>Gets the names alive at the evaluation date.</summary>
    public ImmutableArray<string> RemainingNames() => liveNames;

    /// <summary>Gets the keys of the names alive at the evaluation date.</summary>
    public ImmutableArray<DefaultProbKey> RemainingDefaultKeys() => liveKeys;

    /// <summary>Gets the number of names alive at the evaluation date.</summary>
    public int RemainingSize() => liveList.Length;

    /// <summary>Gets the pool positions of the names alive at the evaluation date.</summary>
    public ImmutableArray<int> LiveList() => liveList;

    /// <summary>Gets the restated tranche's attachment amount on the live names at the evaluation date:
    /// max(0, A − L), the lost notional writing the tranche down.</summary>
    public double RemainingAttachmentAmount() => attachAmount;

    /// <summary>Gets the restated tranche's detachment amount on the live names at the evaluation date:
    /// min(D, N − recovered) − L, the recovered notional amortizing the tranche.</summary>
    public double RemainingDetachmentAmount() => detachAmount;

    /// <summary>Gets the tranche's loss realized by the evaluation date.</summary>
    public double RealizedTrancheLoss => realizedTrancheLoss;

    /// <summary>
    /// Gets the basket the loss model prices on: the basket itself where no name has defaulted by the evaluation date,
    /// otherwise the live names over the restated tranche; <see langword="null"/> where every name has defaulted.
    /// </summary>
    public Basket? PricingBasket => pricingBasket;

    // The rows a model of the pool's names keeps when bound to the basket: the live names'.
    internal ImmutableArray<int> ModelRows => liveList;

    /// <summary>The tranche's outstanding notional at the evaluation date: the restated detachment less the restated
    /// attachment.</summary>
    /// <returns>The notional.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, which QuantLib's read requires.</exception>
    public double RemainingTrancheNotional()
    {
        Model();
        return detachAmount - attachAmount;
    }

    /// <summary>The tranche's outstanding notional at a date, its events to that date counted.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The notional.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, which QuantLib's read requires; or the date
    /// precedes the inception.</exception>
    public double RemainingTrancheNotional(Date endDate)
    {
        Model();
        return RemainingDetachmentAmount(endDate) - RemainingAttachmentAmount(endDate);
    }

    /// <summary>A basket of the same terms and context with a loss model bound to it.</summary>
    /// <param name="lossModel">The model, or <see langword="null"/> for none.</param>
    /// <returns>The basket.</returns>
    /// <exception cref="NanoQLException">Every name has defaulted, or the model can't be bound.</exception>
    public Basket SetLossModel(IDefaultLossModel? lossModel) => new(this, lossModel);

    /// <summary>A name's exposure: the notionals of every position on it.</summary>
    /// <param name="name">The name.</param>
    /// <param name="d">The date, unused: positions don't amortize.</param>
    /// <returns>The exposure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The name isn't in the basket ("Name not in basket.").</exception>
    public double Exposure(string name, Date? d = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        _ = d;
        double totalNotional = 0.0;
        bool found = false;
        for (int i = 0; i < pool.Names.Length; i++)
        {
            if (pool.Names[i] == name)
            {
                totalNotional += notionals[i];
                found = true;
            }
        }

        return found ? totalNotional : throw new ArgumentException("Name not in basket.", nameof(name));
    }

    /// <summary>Each name's default probability by a date, 0 before its curve's reference date.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The probabilities, in pool order.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public ImmutableArray<double> Probabilities(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        var prob = ImmutableArray.CreateBuilder<double>(Size);
        for (int j = 0; j < Size; j++)
        {
            prob.Add(pool.Get(pool.Names[j]).DefaultProbability(DefaultKeys[j]).AccruedDefaultProbability(d));
        }

        return prob.MoveToImmutable();
    }

    /// <summary>The losses settled between the inception and a date, at the events' realized recoveries; an event
    /// not yet settled is left out. A date after the evaluation date counts the "programmed" events stored to
    /// it.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The losses.</returns>
    /// <exception cref="NanoQLException">The date precedes the inception, or an event settled other seniorities than
    /// its name's key's.</exception>
    public double SettledLoss(Date endDate) => Realized(endDate, unsettledRefused: false).Loss;

    /// <summary>The losses settled between the inception and a date: QuantLib's <c>cumulatedLoss(date)</c>, the same as
    /// <see cref="SettledLoss(Date)"/>.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The losses.</returns>
    /// <exception cref="NanoQLException">As <see cref="SettledLoss(Date)"/>.</exception>
    public double CumulatedLoss(Date endDate) => SettledLoss(endDate);

    /// <summary>The notionals of the names alive at a date, recovery ignored.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The notionals, in pool order.</returns>
    /// <exception cref="NanoQLException">The date precedes the inception ("Target date lies before basket
    /// inception").</exception>
    public ImmutableArray<double> RemainingNotionals(Date endDate)
    {
        CheckDate(endDate);
        return [.. LiveList(endDate).Select(i => Exposure(pool.Names[i], endDate))];
    }

    /// <summary>The names alive at a date.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The names.</returns>
    /// <exception cref="NanoQLException">The date precedes the inception.</exception>
    public ImmutableArray<string> RemainingNames(Date endDate)
    {
        CheckDate(endDate);
        return [.. LiveList(endDate).Select(i => pool.Names[i])];
    }

    /// <summary>The keys of the names alive at a date.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The keys.</returns>
    /// <exception cref="NanoQLException">The date precedes the inception.</exception>
    public ImmutableArray<DefaultProbKey> RemainingDefaultKeys(Date endDate)
    {
        CheckDate(endDate);
        return [.. LiveList(endDate).Select(i => DefaultKeys[i])];
    }

    /// <summary>The number of names alive at a date.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The number.</returns>
    /// <exception cref="NanoQLException">The date precedes the inception.</exception>
    public int RemainingSize(Date endDate) => RemainingDefaultKeys(endDate).Length;

    /// <summary>The default probabilities by a date of the names alive at the evaluation date, extrapolated, 0 before
    /// a curve's reference date.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The probabilities, in pool order.</returns>
    /// <exception cref="NanoQLException">The date precedes the inception.</exception>
    public ImmutableArray<double> RemainingProbabilities(Date d)
    {
        CheckDate(d);
        var prob = ImmutableArray.CreateBuilder<double>(liveList.Length);
        foreach (int i in liveList)
        {
            DefaultProbabilityTermStructure curve = pool.Get(pool.Names[i]).DefaultProbability(DefaultKeys[i]);
            prob.Add(d < curve.ReferenceDate ? 0.0 : curve.DefaultProbability(d, true));
        }

        return prob.MoveToImmutable();
    }

    /// <summary>The restated tranche's attachment amount on the names alive at a date, its settled events to that date
    /// counted.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The amount.</returns>
    /// <exception cref="NanoQLException">The date precedes the inception, or an event settled other seniorities than
    /// its name's key's.</exception>
    public double RemainingAttachmentAmount(Date endDate)
    {
        (double loss, double recovered) = Realized(endDate, unsettledRefused: false);
        return Restated(loss, recovered, RemainingNotional(endDate)).Attach;
    }

    /// <summary>The restated tranche's detachment amount on the names alive at a date, its settled events to that date
    /// counted.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The amount.</returns>
    /// <exception cref="NanoQLException">As <see cref="RemainingAttachmentAmount(Date)"/>.</exception>
    public double RemainingDetachmentAmount(Date endDate)
    {
        (double loss, double recovered) = Realized(endDate, unsettledRefused: false);
        return Restated(loss, recovered, RemainingNotional(endDate)).Detach;
    }

    /// <summary>The expected tranche loss by a date: the realized tranche loss and the live model's.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The loss, an amount.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, or the model refuses.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public double ExpectedTrancheLoss(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return realizedTrancheLoss + Model().ExpectedTrancheLoss(d);
    }

    /// <summary>The probability of the tranche losing a fraction of its inception notional or more: 1 at or below the
    /// realized tranche loss, the live model's on the restated tranche above it.</summary>
    /// <param name="d">The date.</param>
    /// <param name="lossFraction">The fraction of the tranche's inception notional (attachment 0, detachment 1).</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, or the model refuses.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public double ProbOverLoss(Date d, double lossFraction)
    {
        Date.ThrowIfDefault(d, nameof(d));
        // convert initial basket fraction to remaining basket fraction
        IDefaultLossModel model = Model();

        // if eaten up all the tranche the prob of losing any amount is 1
        //  (we have already lost it): a basket of zero notionals, which binds its model
        if (remainingNotional == 0.0)
        {
            return 1.0;
        }

        // Turn to live (remaining) tranche units to feed into the model request
        double xPtfl = AttachmentAmount + ((DetachmentAmount - AttachmentAmount) * lossFraction);
        double xPrim = (xPtfl - settledLoss - attachAmount) / (detachAmount - attachAmount);

        // in live tranche fractional units
        // if the level falls within realized losses the prob is 1.
        if (realizedTrancheLoss > 0.0 && xPrim <= 0.0)
        {
            return 1.0;
        }

        return model.ProbOverLoss(d, xPrim);
    }

    /// <summary>The tranche loss at a percentile: the realized tranche loss and the live model's.</summary>
    /// <param name="d">The date.</param>
    /// <param name="prob">The percentile.</param>
    /// <returns>The loss.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, or the model refuses.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public double Percentile(Date d, double prob)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return realizedTrancheLoss + Model().Percentile(d, prob);
    }

    /// <summary>The expected shortfall at a percentile: the realized tranche loss and the live model's.</summary>
    /// <param name="d">The date.</param>
    /// <param name="prob">The percentile.</param>
    /// <returns>The shortfall.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, or the model refuses.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public double ExpectedShortfall(Date d, double prob)
    {
        Date.ThrowIfDefault(d, nameof(d));
        return realizedTrancheLoss + Model().ExpectedShortfall(d, prob);
    }

    /// <summary>Each live name's share of a loss level, typically a percentile's.</summary>
    /// <param name="date">The date.</param>
    /// <param name="loss">The loss level.</param>
    /// <returns>The shares, by live name.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, or the model refuses.</exception>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>, not a date.</exception>
    public ImmutableArray<double> SplitVaRLevel(Date date, double loss)
    {
        Date.ThrowIfDefault(date, nameof(date));
        return Model().SplitVaRLevel(date, loss);
    }

    /// <summary>The full loss distribution: the live model's, shifted by the realized loss, the settled loss where the
    /// model's distribution is the pool's and the realized tranche loss where it is the tranche's (the random
    /// models').</summary>
    /// <param name="d">The date.</param>
    /// <returns>Each loss level's probability.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, or the model refuses.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public ImmutableSortedDictionary<double, double> LossDistribution(Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        IDefaultLossModel model = Model();
        ImmutableSortedDictionary<double, double> live = model.LossDistribution(d);
        double shift = model is ITrancheLossDistribution ? realizedTrancheLoss : settledLoss;
        if (shift == 0.0)
        {
            return live;
        }

        var shifted = ImmutableSortedDictionary.CreateBuilder<double, double>();
        foreach (KeyValuePair<double, double> kv in live)
        {
            shifted[kv.Key + shift] = kv.Value;
        }

        return shifted.ToImmutable();
    }

    /// <summary>Pearson's correlation of two names' default indicators by a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="iName">The first name's pool position.</param>
    /// <param name="jName">The second name's pool position.</param>
    /// <returns>The correlation.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, the model refuses, or a name has defaulted, its
    /// correlation 0/0.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public double DefaultCorrelation(Date d, int iName, int jName)
    {
        Date.ThrowIfDefault(d, nameof(d));
        IDefaultLossModel model = Model();
        return model.DefaultCorrelation(d, LiveIndex(iName, "a defaulted name's default correlation is undefined"), LiveIndex(jName, "a defaulted name's default correlation is undefined"));
    }

    /// <summary>
    /// Each live name's probability of being the n-th default of the basket by a date: zeros where the realized
    /// defaults reach n, the live model's for the order less them otherwise.
    /// </summary>
    /// <param name="n">The default's order.</param>
    /// <param name="d">The date.</param>
    /// <returns>The probabilities, by live name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="NanoQLException">The basket has no loss model, or the model refuses.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public ImmutableArray<double> ProbsBeingNthEvent(int n, Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        int alreadyDefaulted = pool.Size - liveNames.Length;
        if (alreadyDefaulted >= n)
        {
            return [.. Enumerable.Repeat(0.0, liveNames.Length)];
        }

        return Model().ProbsBeingNthEvent(n - alreadyDefaulted, d);
    }

    /// <summary>The probability of n or more defaults in the basket by a date: 1 where the realized defaults reach n,
    /// the live model's for the order less them otherwise.</summary>
    /// <param name="n">The number.</param>
    /// <param name="d">The date.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="NanoQLException">The basket has no loss model, or the model refuses.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public double ProbAtLeastNEvents(int n, Date d)
    {
        Date.ThrowIfDefault(d, nameof(d));
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        IDefaultLossModel model = Model();
        int alreadyDefaulted = pool.Size - liveNames.Length;
        if (alreadyDefaulted > 0 && alreadyDefaulted >= n)
        {
            return 1.0;
        }

        return model.ProbAtLeastNEvents(n - alreadyDefaulted, d);
    }

    /// <summary>A name's expected recovery rate given its default on a date: a defaulted name's settled recovery, a
    /// live name's from the model.</summary>
    /// <param name="d">The date.</param>
    /// <param name="iName">The name's pool position.</param>
    /// <returns>The recovery.</returns>
    /// <exception cref="NanoQLException">The basket has no loss model, or the model refuses.</exception>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>, not a date.</exception>
    public double RecoveryRate(Date d, int iName)
    {
        Date.ThrowIfDefault(d, nameof(d));
        IDefaultLossModel model = Model();
        if (iName < 0 || iName >= pool.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(iName), iName, "not a name of the basket");
        }

        int live = liveList.IndexOf(iName);
        if (live < 0)
        {
            DefaultEvent e = pool.Get(pool.Names[iName]).DefaultedBetween(RefDate, Context.EvaluationDate, DefaultKeys[iName], Context)!;
            return e.Settlement!.RecoveryRate(DefaultKeys[iName].Seniority)!.Value;
        }

        return model.ExpectedRecovery(d, live, DefaultKeys[iName]);
    }

    // The restated tranche on a pool that lost L and recovered R, of live notional N': max(0, A − L), and
    // min(D, N − R) − L, within the live notional.
    private (double Attach, double Detach) Restated(double loss, double recovered, double live)
    {
        double attach = Math.Min(Math.Max(0.0, AttachmentAmount - loss), live);
        double effectiveDetachment = Math.Min(DetachmentAmount, BasketNotional - recovered);
        double detach = Math.Min(Math.Max(attach, effectiveDetachment - loss), live);
        return (attach, detach);
    }

    // The live names over the restated tranche, unbound.
    private Basket Restate()
    {
        double detach = Math.Min(1.0, detachAmount / remainingNotional);
        double attach = Math.Min(detach, attachAmount / remainingNotional);
        return new Basket(RefDate, liveNames, liveNotionals, pool.Subset(liveList), Context, attach, detach, Claim);
    }

    // The settled losses and recovered notionals of the events to a date; an unsettled event refused, or skipped.
    private (double Loss, double Recovered) Realized(Date endDate, bool unsettledRefused)
    {
        CheckDate(endDate);
        double loss = 0.0, recovered = 0.0;
        for (int i = 0; i < Size; i++)
        {
            DefaultEvent? credEvent = pool.Get(pool.Names[i]).DefaultedBetween(RefDate, endDate, DefaultKeys[i], Context);
            if (credEvent is null)
            {
                continue;
            }

            Seniority seniority = DefaultKeys[i].Seniority;
            double? recovery = credEvent.Settlement?.RecoveryRate(seniority);
            if (recovery is null)
            {
                if (credEvent.HasSettled || unsettledRefused)
                {
                    throw new NanoQLException(
                        $"{pool.Names[i]} has defaulted with no settlement for its seniority ({seniority}): its loss is unknown");
                }

                continue;
            }

            double exposure = Exposure(pool.Names[i], credEvent.Date);
            double amount = Claim.Amount(credEvent.Date, exposure, recovery.Value, Context);
            loss += amount;
            recovered += exposure - amount;
        }

        return (loss, recovered);
    }

    /// <summary>The pool positions of the names alive at a date.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The positions.</returns>
    public ImmutableArray<int> LiveList(Date endDate)
    {
        var alive = ImmutableArray.CreateBuilder<int>();
        for (int i = 0; i < Size; i++)
        {
            if (pool.Get(pool.Names[i]).DefaultedBetween(RefDate, endDate, DefaultKeys[i], Context) is null)
            {
                alive.Add(i);
            }
        }

        return alive.ToImmutable();
    }

    /// <summary>The notional of the names alive at a date, recovery ignored.</summary>
    /// <param name="endDate">The date.</param>
    /// <returns>The notional.</returns>
    public double RemainingNotional(Date endDate)
    {
        double notional = 0.0;
        for (int i = 0; i < Size; i++)
        {
            if (pool.Get(pool.Names[i]).DefaultedBetween(RefDate, endDate, DefaultKeys[i], Context) is null)
            {
                notional += notionals[i];
            }
        }

        return notional;
    }

    private int LiveIndex(int iName, string defaulted)
    {
        if (iName < 0 || iName >= pool.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(iName), iName, "not a name of the basket");
        }

        int live = liveList.IndexOf(iName);
        return live >= 0 ? live : throw new NanoQLException(defaulted);
    }

    private void CheckDate(Date endDate)
    {
        if (endDate < RefDate)
        {
            throw new NanoQLException("Target date lies before basket inception");
        }
    }

    private IDefaultLossModel Model() => LossModel ?? throw new NanoQLException("Basket has no default loss model assigned.");
}
