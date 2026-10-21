// SPDX-FileCopyrightText: Copyright (C) 2026 Chirag Desai
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/fxforward.hpp ql/instruments/fxforward.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A foreign-exchange forward: an agreement to exchange an amount of one currency for an amount of another at a future
/// date, at a rate fixed today.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. The settlement conventions are QuantLib's: overnight (0 settlement days), tom-next (1) and spot (2, the
/// default); the settlement date is the evaluation date advanced by the settlement days on the payment calendar.
/// <see cref="PricingEngines.DiscountingFxForwardEngine"/> prices it, its results adding the fair forward rate and the
/// NPV in each currency (<see cref="FxForwardResults"/>).
/// </para>
/// </remarks>
public sealed class FxForward : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="FxForward"/> class from the two nominal amounts.</summary>
    /// <param name="sourceNominal">The amount in the source (domestic) currency.</param>
    /// <param name="sourceCurrency">The source currency.</param>
    /// <param name="targetNominal">The amount in the target (foreign) currency.</param>
    /// <param name="targetCurrency">The target currency.</param>
    /// <param name="maturityDate">The date the amounts are exchanged.</param>
    /// <param name="paySourceCurrency">Whether the source currency is paid and the target received; otherwise the
    /// reverse.</param>
    /// <param name="settlementDays">The business days from the evaluation date to settlement: 0 overnight, 1 tom-next,
    /// 2 spot.</param>
    /// <param name="paymentCalendar">The calendar the settlement date is computed on; <see langword="null"/>,
    /// QuantLib's empty calendar, is <see cref="NullCalendar"/>.</param>
    /// <exception cref="ArgumentNullException">A currency is <see langword="null"/>, QuantLib's empty one.</exception>
    /// <exception cref="ArgumentException">The currencies are the same, or a nominal isn't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public FxForward(
        double sourceNominal,
        Currency sourceCurrency,
        double targetNominal,
        Currency targetCurrency,
        Date maturityDate,
        bool paySourceCurrency,
        int settlementDays = 2,
        Calendar? paymentCalendar = null)
    {
        Date.ThrowIfDefault(maturityDate, nameof(maturityDate));
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        CheckCurrencies(sourceCurrency, targetCurrency);
        if (!(sourceNominal > 0.0))
        {
            throw new ArgumentException("source nominal must be positive", nameof(sourceNominal));
        }

        if (!(targetNominal > 0.0))
        {
            throw new ArgumentException("target nominal must be positive", nameof(targetNominal));
        }

        SourceNominal = sourceNominal;
        SourceCurrency = sourceCurrency;
        TargetNominal = targetNominal;
        TargetCurrency = targetCurrency;
        MaturityDate = maturityDate;
        PaySourceCurrency = paySourceCurrency;
        SettlementDays = settlementDays;
        SettlementCalendar = paymentCalendar ?? new NullCalendar();
    }

    /// <summary>Initializes a new instance of the <see cref="FxForward"/> class from a forward rate.</summary>
    /// <param name="sourceNominal">The amount in the source currency.</param>
    /// <param name="sourceCurrency">The source currency.</param>
    /// <param name="targetCurrency">The currency it is exchanged into.</param>
    /// <param name="forwardRate">The forward rate, units of the target per unit of the source; the target amount is
    /// the source amount times it.</param>
    /// <param name="maturityDate">The date the amounts are exchanged.</param>
    /// <param name="paySourceCurrency">Whether the source currency is paid and the target received; otherwise the
    /// reverse.</param>
    /// <param name="settlementDays">The business days from the evaluation date to settlement.</param>
    /// <param name="paymentCalendar">The calendar the settlement date is computed on; <see langword="null"/> is
    /// <see cref="NullCalendar"/>.</param>
    /// <exception cref="ArgumentNullException">A currency is <see langword="null"/>, QuantLib's empty one.</exception>
    /// <exception cref="ArgumentException">The currencies are the same, or the nominal or the rate isn't
    /// positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public FxForward(
        double sourceNominal,
        Currency sourceCurrency,
        Currency targetCurrency,
        double forwardRate,
        Date maturityDate,
        bool paySourceCurrency,
        int settlementDays = 2,
        Calendar? paymentCalendar = null)
    {
        Date.ThrowIfDefault(maturityDate, nameof(maturityDate));
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        CheckCurrencies(sourceCurrency, targetCurrency);
        if (!(sourceNominal > 0.0))
        {
            throw new ArgumentException("source nominal must be positive", nameof(sourceNominal));
        }

        if (!(forwardRate > 0.0))
        {
            throw new ArgumentException("forward rate must be positive", nameof(forwardRate));
        }

        SourceNominal = sourceNominal;
        SourceCurrency = sourceCurrency;
        TargetNominal = sourceNominal * forwardRate;
        TargetCurrency = targetCurrency;
        MaturityDate = maturityDate;
        PaySourceCurrency = paySourceCurrency;
        SettlementDays = settlementDays;
        SettlementCalendar = paymentCalendar ?? new NullCalendar();
    }

    /// <summary>Gets the amount in the source currency.</summary>
    public double SourceNominal { get; }

    /// <summary>Gets the source currency.</summary>
    public Currency SourceCurrency { get; }

    /// <summary>Gets the amount in the target currency.</summary>
    public double TargetNominal { get; }

    /// <summary>Gets the target currency.</summary>
    public Currency TargetCurrency { get; }

    /// <summary>Gets the date the amounts are exchanged.</summary>
    public Date MaturityDate { get; }

    /// <summary>Gets a value indicating whether the source currency is paid and the target received.</summary>
    public bool PaySourceCurrency { get; }

    /// <summary>Gets the contracted forward rate: units of the target currency per unit of the source.</summary>
    public double ForwardRate => TargetNominal / SourceNominal;

    /// <summary>Gets the business days from the evaluation date to settlement.</summary>
    public int SettlementDays { get; }

    /// <summary>Gets the calendar the settlement date is computed on.</summary>
    public Calendar SettlementCalendar { get; }

    /// <summary>The settlement date: the evaluation date advanced by the settlement days on the payment
    /// calendar.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The settlement date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public Date SettlementDate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return SettlementCalendar.Advance(context.EvaluationDate, SettlementDays, TimeUnit.Days);
    }

    /// <inheritdoc/>
    /// <remarks>Expired once its maturity date is before the evaluation date, as QuantLib's: the reference date's
    /// events settings don't count.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return MaturityDate < context.EvaluationDate;
    }

    /// <inheritdoc/>
    /// <remarks>An expired forward's fair rate and NPVs in each currency are <see langword="null"/>, as
    /// QuantLib's.</remarks>
    public override FxForwardResults SetupExpired()
    {
        InstrumentResults expired = base.SetupExpired();
        return new FxForwardResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The engine's results aren't an FX forward's.</exception>
    public override FxForwardResults FetchResults(InstrumentResults results)
    {
        InstrumentResults fetched = base.FetchResults(results);
        if (fetched is not FxForwardResults r)
        {
            throw new ArgumentException("wrong result type", nameof(results));
        }

        return r;
    }

    private static void CheckCurrencies(Currency sourceCurrency, Currency targetCurrency)
    {
        if (sourceCurrency is null)
        {
            throw new ArgumentNullException(nameof(sourceCurrency), "source currency must not be empty");
        }

        if (targetCurrency is null)
        {
            throw new ArgumentNullException(nameof(targetCurrency), "target currency must not be empty");
        }

        if (sourceCurrency == targetCurrency)
        {
            throw new ArgumentException("source and target currencies must be different", nameof(targetCurrency));
        }
    }
}
