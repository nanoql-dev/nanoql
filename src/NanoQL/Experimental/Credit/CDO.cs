// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/cdo.hpp ql/experimental/credit/cdo.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The collateralized debt obligation: a mezzanine CDO tranche, its loss given default between the attachment D₁ and
/// the detachment D₂ &gt; D₁. For purchased protection the value is the protection value V₁ less the premium value V₂.
/// </summary>
/// <remarks>
/// <para>
/// The protection leg: the probability distribution of the loss given default LGD = (1 − r)·L is built at the premium
/// schedule's dates with intermediate steps tᵢ, the expected protection payoff Eᵢ = E[Pay(LGD)] at each, Pay(L) =
/// min(D₂, LGD) − min(D₁, LGD), and V₁ = Σᵢ (Eᵢ − Eᵢ₋₁)·dᵢ with dᵢ the discount factor at tᵢ. The premium is paid on
/// the protected notional, D₂ − D₁ less the expected protection payments: V₂ = m·Σᵢ (D₂ − D₁ − Eᵢ)·Δᵢ₋₁,ᵢ·dᵢ, with m
/// the premium rate and Δ the day count fraction. The loss distribution is Hull and White's probability bucketing
/// ("Valuation of a CDO and nth to default CDS without Monte Carlo simulation", Journal of Derivatives 12, 2, 2004),
/// which allows varying notionals and default term structures. To be done, as in QuantLib: the cases where
/// Eᵢ₊₁ &lt; Eᵢ, which <see cref="CDOResults.Error"/> counts.
/// </para>
/// <para>
/// QuantLib's older CDO, computed by the instrument itself: <see cref="Calculate"/>, nothing cached. Its "today" is the
/// yield curve's reference date, as QuantLib's. The maximum loss given default <see cref="Lgd"/> is the sum of the
/// names' losses given default, which QuantLib's sums onto an uninitialized value (<c>cdo.cpp:73-77</c>). A default
/// probability before its curve's reference date is 0, where QuantLib's refuses it.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class CDO : Instrument
{
    private readonly ImmutableArray<DefaultProbabilityTermStructure> basket;
    private readonly OneFactorCopula copula;
    private readonly bool protectionSeller;
    private readonly Schedule premiumSchedule;
    private readonly double premiumRate;
    private readonly DayCounter dayCounter;
    private readonly double upfrontPremiumRate;
    private readonly YieldTermStructure yieldTS;
    private readonly int nBuckets; // number of buckets up to detachment point
    private readonly Period integrationStep;
    private readonly ImmutableArray<double> lgds;
    private readonly double xMax; // tranche detachment point (tranche_ * nominal_)
    private readonly double xMin; // tranche attachment point (tranche_ * nominal_)

    /// <summary>Initializes a new instance of the <see cref="CDO"/> class.</summary>
    /// <param name="attachment">The fraction of the LGD where protection starts.</param>
    /// <param name="detachment">The fraction of the LGD where protection ends.</param>
    /// <param name="nominals">The basket's nominal amounts, the last repeated for names beyond them.</param>
    /// <param name="basket">The default basket, its names' default term structures.</param>
    /// <param name="copula">The one-factor copula.</param>
    /// <param name="protectionSeller">Whether protection is sold; purchased otherwise.</param>
    /// <param name="premiumSchedule">The premium payments' schedule.</param>
    /// <param name="premiumRate">The annual premium rate, e.g. 0.05 for 5% p.a.</param>
    /// <param name="dayCounter">The premium rate's day count convention.</param>
    /// <param name="recoveryRate">The recovery rate, a fraction.</param>
    /// <param name="upfrontPremiumRate">The upfront premium, a fraction of the tranche notional.</param>
    /// <param name="yieldTS">The yield term structure.</param>
    /// <param name="nBuckets">The number of the distribution's buckets.</param>
    /// <param name="integrationStep">The time step integrating over one premium period, a single step where it is
    /// longer than the period; 10 years by default.</param>
    /// <exception cref="ArgumentNullException">An argument but <paramref name="integrationStep"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The basket is empty ("basket is empty"); the points aren't
    /// 0 ≤ attachment &lt; detachment ≤ 1 ("illegal attachment/detachment point"); there are more nominals than names
    /// ("nominal vector size too large") or none, whose last QuantLib reads.</exception>
    public CDO(
        double attachment,
        double detachment,
        IReadOnlyList<double> nominals,
        IReadOnlyList<DefaultProbabilityTermStructure> basket,
        OneFactorCopula copula,
        bool protectionSeller,
        Schedule premiumSchedule,
        double premiumRate,
        DayCounter dayCounter,
        double recoveryRate,
        double upfrontPremiumRate,
        YieldTermStructure yieldTS,
        int nBuckets,
        Period? integrationStep = null)
    {
        ArgumentNullException.ThrowIfNull(nominals);
        ArgumentNullException.ThrowIfNull(basket);
        ArgumentNullException.ThrowIfNull(copula);
        ArgumentNullException.ThrowIfNull(premiumSchedule);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(yieldTS);
        if (basket.Count == 0)
        {
            throw new ArgumentException("basket is empty", nameof(basket));
        }

        if (!(attachment >= 0 && attachment < detachment && detachment <= 1))
        {
            throw new ArgumentException("illegal attachment/detachment point", nameof(attachment));
        }

        if (!(nominals.Count <= basket.Count))
        {
            throw new ArgumentException("nominal vector size too large", nameof(nominals));
        }

        if (nominals.Count == 0)
        {
            throw new ArgumentException("no nominals given", nameof(nominals));
        }

        this.basket = [.. basket.Select(ts => ts ?? throw new ArgumentNullException(nameof(basket)))];
        Attachment = attachment;
        Detachment = detachment;
        this.copula = copula;
        this.protectionSeller = protectionSeller;
        this.premiumSchedule = premiumSchedule;
        this.premiumRate = premiumRate;
        this.dayCounter = dayCounter;
        RecoveryRate = recoveryRate;
        this.upfrontPremiumRate = upfrontPremiumRate;
        this.yieldTS = yieldTS;
        this.nBuckets = nBuckets;
        this.integrationStep = integrationStep ?? new Period(10, TimeUnit.Years);

        double[] all = [.. nominals, .. Enumerable.Repeat(nominals[^1], basket.Count - nominals.Count)];
        Nominals = [.. all];

        double nominal = 0;
        double lgd = 0;
        double[] lgdList = new double[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            lgdList[i] = all[i] * (1.0 - RecoveryRate);
            nominal += all[i];
            lgd += lgdList[i];
        }

        lgds = [.. lgdList];
        Nominal = nominal;
        Lgd = lgd;
        xMax = Detachment * Nominal;
        xMin = Attachment * Nominal;
    }

    /// <summary>Gets the total basket volume, the sum of the nominals.</summary>
    public double Nominal { get; }

    /// <summary>Gets the maximum loss given default, the sum of the names' losses given default.</summary>
    public double Lgd { get; }

    /// <summary>Gets the attachment, a fraction of the basket's volume.</summary>
    public double Attachment { get; }

    /// <summary>Gets the detachment, a fraction of the basket's volume.</summary>
    public double Detachment { get; }

    /// <summary>Gets the names' nominals.</summary>
    public ImmutableArray<double> Nominals { get; }

    /// <summary>Gets the number of names.</summary>
    public int Size => basket.Length;

    /// <summary>Gets the recovery rate.</summary>
    public double RecoveryRate { get; }

    /// <inheritdoc/>
    /// <remarks>The CDO expires with its schedule's last date, on the yield curve's reference date.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(premiumSchedule.Dates[^1]).HasOccurred(context, yieldTS.ReferenceDate);
    }

    /// <summary>Prices the CDO: the premium and protection legs and the upfront, each step's expected tranche loss
    /// from the copula's bucketed loss distribution.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The results; an expired CDO's NPV is 0.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A curve refuses a read past its maximum date.</exception>
    public CDOResults Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsExpired(context))
        {
            return (CDOResults)SetupExpired();
        }

        double premiumValue = 0;
        double protectionValue = 0;
        double upfrontPremiumValue;
        int error = 0;

        /* Expectations e1 and e2 are portfolio loss given default,
           i.e.  with recovery already "bult in". Multiplication by
           (1-r) is therefore not necessary, neither in premium nor
           protection value calculation.
        */
        double e1 = 0;
        Date today = yieldTS.ReferenceDate;
        if (premiumSchedule[0] > today)
        {
            e1 = ExpectedTrancheLoss(premiumSchedule[0]);
        }

        var calendar = new NullCalendar();
        for (int i = 1; i < premiumSchedule.Size; i++)
        {
            Date d2 = premiumSchedule[i];
            if (d2 < today)
            {
                continue;
            }

            Date d1 = premiumSchedule[i - 1];

            Date d, d0 = d1;
            do
            {
                d = calendar.Advance(d0 > today ? d0 : today, integrationStep);
                if (d > d2)
                {
                    d = d2;
                }

                double e2 = ExpectedTrancheLoss(d);

                premiumValue += (xMax - xMin - e2)
                    * premiumRate * dayCounter.YearFraction(d0, d)
                    * yieldTS.Discount(d);

                if (e2 < e1)
                {
                    error++;
                }

                protectionValue -= (e2 - e1) * yieldTS.Discount(d);

                d0 = d;
                e1 = e2;
            }
            while (d < d2);
        }

        if (premiumSchedule[0] >= today)
        {
            upfrontPremiumValue = (xMax - xMin) * upfrontPremiumRate * yieldTS.Discount(premiumSchedule[0]);
        }
        else
        {
            upfrontPremiumValue = 0.0;
        }

        if (!protectionSeller)
        {
            premiumValue *= -1;
            upfrontPremiumValue *= -1;
            protectionValue *= -1;
        }

        return new CDOResults
        {
            NPV = premiumValue + protectionValue + upfrontPremiumValue,
            PremiumValue = premiumValue,
            ProtectionValue = protectionValue,
            UpfrontPremiumValue = upfrontPremiumValue,
            FairPremium = -premiumRate * protectionValue / premiumValue,
            Error = error,
        };
    }

    /// <inheritdoc/>
    public override InstrumentResults SetupExpired()
    {
        InstrumentResults expired = base.SetupExpired();
        return new CDOResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
        };
    }

    private double ExpectedTrancheLoss(Date d)
    {
        if (d <= basket[0].ReferenceDate)
        {
            return 0;
        }

        double[] defProb = new double[basket.Length];
        for (int j = 0; j < basket.Length; j++)
        {
            defProb[j] = d < basket[j].ReferenceDate ? 0.0 : basket[j].DefaultProbability(d);
        }

        var op = new LossDistBucketing(nBuckets, xMax);
        Distribution dist = copula.Integral(op, lgds, defProb);

        return dist.TrancheExpectedValue(xMin, xMax);

        // The following causes two errors in test against literature values.
        // FIXME: Investigate accuracy.
        // return dist.cumulativeExcessProbability (xMin_, xMax_);

        //   TranchePayoff func (xMin_, xMax_);
        //   return (dist.expectedValue (func)
        //    + (xMax_ - xMin_) * (1.0 - dist.cumulatedProbability (xMax_)));
    }
}
