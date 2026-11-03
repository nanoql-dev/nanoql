// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/nthtodefault.hpp ql/experimental/credit/nthtodefault.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// An n-th-to-default swap: protection against the n-th default in a basket of credits, for premium payments on the
/// protected notional. Priced as a credit default swap whose default probability is the basket's probability of n
/// defaults or more (Hull and White, "Valuation of a CDO and nth to default CDS without Monte Carlo simulation",
/// Journal of Derivatives 12, 2, 2004).
/// </summary>
/// <remarks>
/// <para>
/// Immutable: it holds the basket with its bound loss model, and an engine's static <c>Calculate</c> prices it
/// (<see cref="IntegralNtdEngine"/>). The product is digital: the basket's tranche doesn't enter its price. Every name
/// has the same weight in the protected notional.
/// </para>
/// <para>
/// An order of 0 is refused: QuantLib checks only n ≤ size (<c>nthtodefault.cpp:48</c>), and at n = 0 its fair premium
/// is noise, 6.0e-5, its NPV 3.5e-12 (<c>Conventions/CreditPortfolioConventionTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class NthToDefault : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="NthToDefault"/> class.</summary>
    /// <param name="basket">The basket, with its loss model.</param>
    /// <param name="n">The default's order, from 1 to the basket's size.</param>
    /// <param name="side">The protection's side.</param>
    /// <param name="premiumSchedule">The premium leg's schedule.</param>
    /// <param name="upfrontRate">The upfront, a rate on the nominal.</param>
    /// <param name="premiumRate">The running premium.</param>
    /// <param name="dayCounter">The premium leg's day counter.</param>
    /// <param name="nominal">The protected notional.</param>
    /// <param name="settlePremiumAccrual">Whether the premium accrued at default is paid.</param>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is below 1.</exception>
    /// <exception cref="ArgumentException"><paramref name="n"/> exceeds the basket's size ("NTD order provided is
    /// larger than the basket size."), or the basket's inception follows the schedule's start ("Basket did not exist
    /// before contract start.").</exception>
    public NthToDefault(
        Basket basket,
        int n,
        ProtectionSide side,
        Schedule premiumSchedule,
        double upfrontRate,
        double premiumRate,
        DayCounter dayCounter,
        double nominal,
        bool settlePremiumAccrual)
    {
        ArgumentNullException.ThrowIfNull(basket);
        ArgumentNullException.ThrowIfNull(premiumSchedule);
        ArgumentNullException.ThrowIfNull(dayCounter);
        if (n < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "the NTD order must be at least 1");
        }

        if (n > basket.Size)
        {
            throw new ArgumentException("NTD order provided is larger than the basket size.", nameof(n));
        }

        // Basket inception must lie before contract protection start.
        if (!(basket.RefDate <= premiumSchedule.StartDate()))
        {
            // using the start date of the schedule might be wrong, think of the CDS rule
            throw new ArgumentException("Basket did not exist before contract start.", nameof(basket));
        }

        Basket = basket;
        Rank = n;
        Side = side;
        Nominal = nominal;
        PremiumSchedule = premiumSchedule;
        Premium = premiumRate;
        UpfrontRate = upfrontRate;
        DayCounter = dayCounter;
        SettlePremiumAccrual = settlePremiumAccrual;
        PremiumLeg = new FixedRateLeg(premiumSchedule)
            .WithNotionals(nominal)
            .WithCouponRates(premiumRate, dayCounter)
            .WithPaymentAdjustment(BusinessDayConvention.Unadjusted)
            .ToLeg();
    }

    /// <summary>Gets the basket, with its loss model.</summary>
    public Basket Basket { get; }

    /// <summary>Gets the default's order.</summary>
    public int Rank { get; }

    /// <summary>Gets the protection's side.</summary>
    public ProtectionSide Side { get; }

    /// <summary>Gets the protected notional.</summary>
    public double Nominal { get; }

    /// <summary>Gets the premium leg's schedule.</summary>
    public Schedule PremiumSchedule { get; }

    /// <summary>Gets the running premium.</summary>
    public double Premium { get; }

    /// <summary>Gets the upfront, a rate on the nominal.</summary>
    public double UpfrontRate { get; }

    /// <summary>Gets the premium leg's day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Gets a value indicating whether the premium accrued at default is paid.</summary>
    public bool SettlePremiumAccrual { get; }

    /// <summary>Gets the premium leg.</summary>
    public ImmutableArray<CashFlow> PremiumLeg { get; }

    /// <summary>Gets the basket's number of names.</summary>
    public int BasketSize => Basket.Size;

    /// <summary>Gets the maturity: the premium schedule's end.</summary>
    public Date Maturity => PremiumSchedule.EndDate();

    /// <inheritdoc/>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(PremiumLeg[^1].Date).HasOccurred(context);
    }

    /// <inheritdoc/>
    public override InstrumentResults SetupExpired() => new NthToDefaultResults
    {
        NPV = 0.0,
        ErrorEstimate = 0.0,
        ValuationDate = null,
        PremiumValue = 0.0,
        ProtectionValue = 0.0,
        UpfrontPremiumValue = 0.0,
        FairPremium = 0.0,
    };
}
