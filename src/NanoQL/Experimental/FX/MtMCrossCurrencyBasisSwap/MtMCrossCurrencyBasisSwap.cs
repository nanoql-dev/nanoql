// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/mtmcrosscurrencybasisswap.hpp ql/experimental/fx/mtmcrosscurrencybasisswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A mark-to-market cross-currency basis swap.</summary>
/// <remarks>
/// <para>
/// A float-vs-float cross-currency basis swap in which the notional of one leg (the <em>resettable</em> leg) is reset
/// for each coupon period to track the prevailing FX rate, while the other leg keeps a constant notional that is
/// exchanged at inception and maturity. Each reset has a fixing date, on which the FX rate is observed, and a value
/// date at the start of the accrual period.
/// </para>
/// <para>
/// Following the convention of <see cref="MtMCrossCurrencyBasisSwapRateHelper"/>, the two legs are identified by their
/// role in the FX pair: the <em>base-currency</em> leg (the first) and the <em>quote-currency</em> leg (the second),
/// and
/// <c>isFxBaseCurrencyLegResettable</c> selects which of them carries the resetting notional. The trade's type controls
/// whether the base-currency leg is paid or received.
/// </para>
/// <para>
/// Resetting the notional removes most of the FX exposure of the principal and is the market-standard convention for
/// many currency pairs. The resettable leg is built from FX-linked cash flows: <see cref="FxResetCoupon"/> instances,
/// whose notional is the constant-leg notional converted at the period's FX-reset observation, and
/// <see cref="FxResetNotionalExchange"/> instances paying the netted notional differences (the first notional at
/// inception, the reset difference at each period boundary, and the last notional at maturity).
/// </para>
/// <para>
/// During pricing, <see cref="DiscountingMtMCrossCurrencyBasisSwapEngine"/> gives copies of the FX-linked cash flows an
/// <see cref="FxResetPricer"/> built from its market data, leaving the swap's own cash flows unchanged; querying them
/// independently needs a pricer set through <see cref="FxResetCashFlowsFunctions.SetFxResetPricer{T}"/>. A future
/// reset is projected as the forward FX implied by its pricer's discount curves. If a reset fixing date precedes the
/// evaluation date, the realized exchange rate must be available from the context's
/// <see cref="EvaluationContext.ExchangeRates"/> on that fixing date; direct, inverse and derived exchange rates are
/// supported.
/// </para>
/// <para>
/// The constructor builds the legs, as QuantLib's does: an Ibor leg reads the context's at-par switch, so it takes the
/// context.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class MtMCrossCurrencyBasisSwap : CrossCurrencySwap
{
    private const double BasisPoint = 1.0e-4;

    /// <summary>Initializes a new instance of the <see cref="MtMCrossCurrencyBasisSwap"/> class.</summary>
    /// <param name="type">Whether the FX base-currency leg is paid or received.</param>
    /// <param name="fxBaseNominal">The base-currency leg's notional.</param>
    /// <param name="fxBaseCurrency">The base currency.</param>
    /// <param name="fxBaseSchedule">The base-currency leg's schedule.</param>
    /// <param name="fxBaseIndex">The base-currency leg's index.</param>
    /// <param name="fxBaseSpread">The spread over the base-currency leg's index.</param>
    /// <param name="fxBaseGearing">The gearing of the base-currency leg's index.</param>
    /// <param name="fxQuoteNominal">The quote-currency leg's notional.</param>
    /// <param name="fxQuoteCurrency">The quote currency.</param>
    /// <param name="fxQuoteSchedule">The quote-currency leg's schedule.</param>
    /// <param name="fxQuoteIndex">The quote-currency leg's index.</param>
    /// <param name="fxQuoteSpread">The spread over the quote-currency leg's index.</param>
    /// <param name="fxQuoteGearing">The gearing of the quote-currency leg's index.</param>
    /// <param name="isFxBaseCurrencyLegResettable">Whether the base-currency (first) leg is the resettable leg;
    /// otherwise the quote-currency (second) leg resets. The other leg keeps a constant notional and exchanges it at
    /// inception and maturity.</param>
    /// <param name="context">The evaluation context, whose at-par switch an Ibor leg reads.</param>
    /// <param name="fxResetFixingDays">The fixing days used to derive each FX fixing date from its accrual-start value
    /// date. The default fixes on the accrual start date.</param>
    /// <param name="fxResetFixingCalendar">The calendar used to derive each FX fixing date; for a non-zero fixing lag,
    /// the resettable leg's schedule calendar when <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="fxBasePaymentLag">The base-currency leg's coupon payment lag, in days.</param>
    /// <param name="fxQuotePaymentLag">The quote-currency leg's coupon payment lag, in days.</param>
    /// <param name="fxBasePaymentConvention">The payment convention of the base-currency leg and its notional
    /// exchanges.</param>
    /// <param name="fxQuotePaymentConvention">The payment convention of the quote-currency leg and its notional
    /// exchanges.</param>
    /// <param name="fxBaseCompoundSpread">Whether an overnight base-currency leg compounds its spread daily.</param>
    /// <param name="fxBaseLookbackDays">An overnight base-currency leg's lookback days; the index's fixing days when
    /// <see langword="null"/>, QuantLib's <c>Null&lt;Natural&gt;()</c>.</param>
    /// <param name="fxBaseObservationShift">Whether an overnight base-currency leg applies the observation
    /// shift.</param>
    /// <param name="fxBaseLockoutDays">An overnight base-currency leg's lockout days.</param>
    /// <param name="fxBaseAveragingMethod">An overnight base-currency leg's averaging method.</param>
    /// <param name="fxQuoteCompoundSpread">Whether an overnight quote-currency leg compounds its spread daily.</param>
    /// <param name="fxQuoteLookbackDays">An overnight quote-currency leg's lookback days; the index's fixing days when
    /// <see langword="null"/>.</param>
    /// <param name="fxQuoteObservationShift">Whether an overnight quote-currency leg applies the observation
    /// shift.</param>
    /// <param name="fxQuoteLockoutDays">An overnight quote-currency leg's lockout days.</param>
    /// <param name="fxQuoteAveragingMethod">An overnight quote-currency leg's averaging method.</param>
    /// <param name="telescopicValueDates">Whether overnight legs use telescopic value dates.</param>
    /// <param name="useIndexedCoupons">Whether Ibor legs' coupons are indexed or at par; the context's at-par switch
    /// when <see langword="null"/>.</param>
    /// <param name="fxBaseStubIndexSelection">The index selection for an Ibor base-currency leg's irregular coupons;
    /// none when <see langword="null"/>.</param>
    /// <param name="fxQuoteStubIndexSelection">The index selection for an Ibor quote-currency leg's irregular coupons;
    /// none when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A schedule, an index or the context is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The type, a convention or an averaging method isn't a member of
    /// its enum, or fixing, lookback or lockout days are negative.</exception>
    /// <exception cref="ArgumentException">The constant leg's notional is zero, or a leg can't be built from the
    /// arguments.</exception>
    /// <exception cref="NanoQLException">An FX fixing date can't be derived (a fixing lag with no calendar), or a
    /// notional exchange's date can't be adjusted (no schedule calendar).</exception>
    public MtMCrossCurrencyBasisSwap(
        MtMCrossCurrencyBasisSwapType type,
        double fxBaseNominal,
        Currency? fxBaseCurrency,
        Schedule fxBaseSchedule,
        IborIndex fxBaseIndex,
        double fxBaseSpread,
        double fxBaseGearing,
        double fxQuoteNominal,
        Currency? fxQuoteCurrency,
        Schedule fxQuoteSchedule,
        IborIndex fxQuoteIndex,
        double fxQuoteSpread,
        double fxQuoteGearing,
        bool isFxBaseCurrencyLegResettable,
        EvaluationContext context,
        int fxResetFixingDays = 0,
        Calendar? fxResetFixingCalendar = null,
        int fxBasePaymentLag = 0,
        int fxQuotePaymentLag = 0,
        BusinessDayConvention fxBasePaymentConvention = BusinessDayConvention.Following,
        BusinessDayConvention fxQuotePaymentConvention = BusinessDayConvention.Following,
        bool fxBaseCompoundSpread = false,
        int? fxBaseLookbackDays = null,
        bool fxBaseObservationShift = false,
        int fxBaseLockoutDays = 0,
        RateAveragingType fxBaseAveragingMethod = RateAveragingType.Compound,
        bool fxQuoteCompoundSpread = false,
        int? fxQuoteLookbackDays = null,
        bool fxQuoteObservationShift = false,
        int fxQuoteLockoutDays = 0,
        RateAveragingType fxQuoteAveragingMethod = RateAveragingType.Compound,
        bool telescopicValueDates = false,
        bool? useIndexedCoupons = null,
        StubIndexSelection? fxBaseStubIndexSelection = null,
        StubIndexSelection? fxQuoteStubIndexSelection = null)
        : this(new Terms(
            type,
            fxBaseNominal,
            fxBaseCurrency,
            fxBaseSchedule,
            fxBaseIndex,
            fxBaseSpread,
            fxBaseGearing,
            fxQuoteNominal,
            fxQuoteCurrency,
            fxQuoteSchedule,
            fxQuoteIndex,
            fxQuoteSpread,
            fxQuoteGearing,
            isFxBaseCurrencyLegResettable,
            context,
            fxResetFixingDays,
            fxResetFixingCalendar,
            fxBasePaymentLag,
            fxQuotePaymentLag,
            fxBasePaymentConvention,
            fxQuotePaymentConvention,
            new LegOptions(
                fxBaseCompoundSpread,
                fxBaseLookbackDays,
                fxBaseObservationShift,
                fxBaseLockoutDays,
                fxBaseAveragingMethod),
            new LegOptions(
                fxQuoteCompoundSpread,
                fxQuoteLookbackDays,
                fxQuoteObservationShift,
                fxQuoteLockoutDays,
                fxQuoteAveragingMethod),
            telescopicValueDates,
            useIndexedCoupons,
            fxBaseStubIndexSelection ?? new StubIndexSelection(),
            fxQuoteStubIndexSelection ?? new StubIndexSelection()))
    {
    }

    private MtMCrossCurrencyBasisSwap(Terms terms)
        : base(
            terms.Legs,
            [terms.Type == MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
                terms.Type != MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency],
            [terms.FxBaseCurrency, terms.FxQuoteCurrency])
    {
        Type = terms.Type;
        FxBaseNominal = terms.FxBaseNominal;
        FxBaseCurrency = terms.FxBaseCurrency;
        FxBaseSchedule = terms.FxBaseSchedule;
        FxBaseIndex = terms.FxBaseIndex;
        FxBaseSpread = terms.FxBaseSpread;
        FxBaseGearing = terms.FxBaseGearing;
        FxBaseStubIndexSelection = terms.FxBaseStubIndexSelection;
        FxQuoteNominal = terms.FxQuoteNominal;
        FxQuoteCurrency = terms.FxQuoteCurrency;
        FxQuoteSchedule = terms.FxQuoteSchedule;
        FxQuoteIndex = terms.FxQuoteIndex;
        FxQuoteSpread = terms.FxQuoteSpread;
        FxQuoteGearing = terms.FxQuoteGearing;
        FxQuoteStubIndexSelection = terms.FxQuoteStubIndexSelection;
        IsFxBaseCurrencyLegResettable = terms.IsFxBaseCurrencyLegResettable;
        FxResetConvention = terms.FxResetConvention;
        FxBasePaymentConvention = terms.FxBasePaymentConvention;
        FxQuotePaymentConvention = terms.FxQuotePaymentConvention;
    }

    /// <summary>Gets whether the FX base-currency leg is paid or received.</summary>
    public MtMCrossCurrencyBasisSwapType Type { get; }

    /// <summary>Gets a value indicating whether the swap pays the FX base-currency leg.</summary>
    public bool PaysFxBaseCurrency => Type == MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency;

    /// <summary>Gets the base-currency leg's notional.</summary>
    public double FxBaseNominal { get; }

    /// <summary>Gets the base currency.</summary>
    public Currency? FxBaseCurrency { get; }

    /// <summary>Gets the base-currency leg's schedule.</summary>
    public Schedule FxBaseSchedule { get; }

    /// <summary>Gets the base-currency leg's index.</summary>
    public IborIndex FxBaseIndex { get; }

    /// <summary>Gets the spread over the base-currency leg's index.</summary>
    public double FxBaseSpread { get; }

    /// <summary>Gets the gearing of the base-currency leg's index.</summary>
    public double FxBaseGearing { get; }

    /// <summary>Gets the index selection for an Ibor base-currency leg's irregular coupons.</summary>
    public StubIndexSelection FxBaseStubIndexSelection { get; }

    /// <summary>Gets the quote-currency leg's notional.</summary>
    public double FxQuoteNominal { get; }

    /// <summary>Gets the quote currency.</summary>
    public Currency? FxQuoteCurrency { get; }

    /// <summary>Gets the quote-currency leg's schedule.</summary>
    public Schedule FxQuoteSchedule { get; }

    /// <summary>Gets the quote-currency leg's index.</summary>
    public IborIndex FxQuoteIndex { get; }

    /// <summary>Gets the spread over the quote-currency leg's index.</summary>
    public double FxQuoteSpread { get; }

    /// <summary>Gets the gearing of the quote-currency leg's index.</summary>
    public double FxQuoteGearing { get; }

    /// <summary>Gets the index selection for an Ibor quote-currency leg's irregular coupons.</summary>
    public StubIndexSelection FxQuoteStubIndexSelection { get; }

    /// <summary>Gets the paid leg's notional.</summary>
    public double PayNominal => PaysFxBaseCurrency ? FxBaseNominal : FxQuoteNominal;

    /// <summary>Gets the paid leg's currency.</summary>
    public Currency? PayCurrency => PaysFxBaseCurrency ? FxBaseCurrency : FxQuoteCurrency;

    /// <summary>Gets the paid leg's schedule.</summary>
    public Schedule PaySchedule => PaysFxBaseCurrency ? FxBaseSchedule : FxQuoteSchedule;

    /// <summary>Gets the paid leg's index.</summary>
    public IborIndex PayIndex => PaysFxBaseCurrency ? FxBaseIndex : FxQuoteIndex;

    /// <summary>Gets the spread over the paid leg's index.</summary>
    public double PaySpread => PaysFxBaseCurrency ? FxBaseSpread : FxQuoteSpread;

    /// <summary>Gets the gearing of the paid leg's index.</summary>
    public double PayGearing => PaysFxBaseCurrency ? FxBaseGearing : FxQuoteGearing;

    /// <summary>Gets the received leg's notional.</summary>
    public double RecNominal => PaysFxBaseCurrency ? FxQuoteNominal : FxBaseNominal;

    /// <summary>Gets the received leg's currency.</summary>
    public Currency? RecCurrency => PaysFxBaseCurrency ? FxQuoteCurrency : FxBaseCurrency;

    /// <summary>Gets the received leg's schedule.</summary>
    public Schedule RecSchedule => PaysFxBaseCurrency ? FxQuoteSchedule : FxBaseSchedule;

    /// <summary>Gets the received leg's index.</summary>
    public IborIndex RecIndex => PaysFxBaseCurrency ? FxQuoteIndex : FxBaseIndex;

    /// <summary>Gets the spread over the received leg's index.</summary>
    public double RecSpread => PaysFxBaseCurrency ? FxQuoteSpread : FxBaseSpread;

    /// <summary>Gets the gearing of the received leg's index.</summary>
    public double RecGearing => PaysFxBaseCurrency ? FxQuoteGearing : FxBaseGearing;

    /// <summary>Gets a value indicating whether the base-currency (first) leg is the resettable leg.</summary>
    public bool IsFxBaseCurrencyLegResettable { get; }

    /// <summary>Gets the index of the resettable leg.</summary>
    public int ResettingLegIndex => IsFxBaseCurrencyLegResettable ? 0 : 1;

    /// <summary>Gets the index of the constant-notional leg.</summary>
    public int ConstantLegIndex => IsFxBaseCurrencyLegResettable ? 1 : 0;

    /// <summary>Gets the resettable leg.</summary>
    public ImmutableArray<CashFlow> ResettingLeg => Legs[ResettingLegIndex];

    /// <summary>Gets the constant-notional leg.</summary>
    public ImmutableArray<CashFlow> ConstantLeg => Legs[ConstantLegIndex];

    /// <summary>Gets the notional of the constant leg, in its own currency.</summary>
    public double ConstantLegNotional => IsFxBaseCurrencyLegResettable ? FxQuoteNominal : FxBaseNominal;

    /// <summary>Gets the convention of the FX fixings and their spot value dates.</summary>
    public FxResetConvention FxResetConvention { get; }

    /// <summary>Gets the fixing days of the FX fixings.</summary>
    public int FxResetFixingDays => FxResetConvention.FixingDays;

    /// <summary>Gets the calendar of the FX fixings, <see langword="null"/> for QuantLib's empty one.</summary>
    public Calendar? FxResetFixingCalendar => FxResetConvention.FixingCalendar;

    /// <summary>Gets the payment convention of the base-currency leg and its notional exchanges.</summary>
    public BusinessDayConvention FxBasePaymentConvention { get; }

    /// <summary>Gets the payment convention of the quote-currency leg and its notional exchanges.</summary>
    public BusinessDayConvention FxQuotePaymentConvention { get; }

    /// <summary>The paid leg's fair spread in an engine's results: the FX base-currency leg's if this swap pays
    /// it.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The fair spread, <see langword="null"/> where the results hold none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    public double? FairPaySpread(MtMCrossCurrencyBasisSwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return PaysFxBaseCurrency ? results.FairFxBaseSpread : results.FairFxQuoteSpread;
    }

    /// <summary>
    /// The received leg's fair spread in an engine's results: the FX quote-currency leg's if this swap pays the base.
    /// </summary>
    /// <param name="results">The results.</param>
    /// <returns>The fair spread, <see langword="null"/> where the results hold none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    public double? FairRecSpread(MtMCrossCurrencyBasisSwapResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return PaysFxBaseCurrency ? results.FairFxQuoteSpread : results.FairFxBaseSpread;
    }

    /// <inheritdoc/>
    public override MtMCrossCurrencyBasisSwapResults SetupExpired() => new(base.SetupExpired())
    {
        FairFxBaseSpread = null,
        FairFxQuoteSpread = null,
        FxResetRates = [],
        FxResetNotionals = [],
    };

    /// <inheritdoc/>
    /// <remarks>
    /// The fair spreads, reset rates and notionals are the engine's when it gives them; otherwise each fair spread is
    /// derived from the NPV and its leg's BPS, and is <see langword="null"/> when that BPS is unknown.
    /// </remarks>
    /// <exception cref="NanoQLException">A fair spread is derived from a leg whose BPS is zero.</exception>
    public override MtMCrossCurrencyBasisSwapResults FetchResults(InstrumentResults results)
    {
        CrossCurrencySwapResults r = base.FetchResults(results);
        var fetched = r as MtMCrossCurrencyBasisSwapResults;
        double? fairFxBaseSpread = fetched?.FairFxBaseSpread;
        double? fairFxQuoteSpread = fetched?.FairFxQuoteSpread;

        if (fairFxBaseSpread is null && r.LegBPS[0] is double baseBps)
        {
            if (baseBps == 0.0)
            {
                throw new NanoQLException("cannot calculate fair FX-base spread: null leg BPS");
            }

            fairFxBaseSpread = FxBaseSpread - (r.NPV / (baseBps / BasisPoint));
        }

        if (fairFxQuoteSpread is null && r.LegBPS[1] is double quoteBps)
        {
            if (quoteBps == 0.0)
            {
                throw new NanoQLException("cannot calculate fair FX-quote spread: null leg BPS");
            }

            fairFxQuoteSpread = FxQuoteSpread - (r.NPV / (quoteBps / BasisPoint));
        }

        return fetched is not null
            ? fetched with { FairFxBaseSpread = fairFxBaseSpread, FairFxQuoteSpread = fairFxQuoteSpread }
            : new MtMCrossCurrencyBasisSwapResults(r)
            {
                FairFxBaseSpread = fairFxBaseSpread,
                FairFxQuoteSpread = fairFxQuoteSpread,
            };
    }

    // An overnight leg's options.
    private sealed record LegOptions(
        bool CompoundSpread,
        int? LookbackDays,
        bool ObservationShift,
        int LockoutDays,
        RateAveragingType AveragingMethod);

    // QuantLib's constructor and initialize, run before the swap's own constructor takes the legs: the terms checked
    // and the legs built, in QuantLib's order.
    private sealed class Terms
    {
        internal Terms(
            MtMCrossCurrencyBasisSwapType type,
            double fxBaseNominal,
            Currency? fxBaseCurrency,
            Schedule fxBaseSchedule,
            IborIndex fxBaseIndex,
            double fxBaseSpread,
            double fxBaseGearing,
            double fxQuoteNominal,
            Currency? fxQuoteCurrency,
            Schedule fxQuoteSchedule,
            IborIndex fxQuoteIndex,
            double fxQuoteSpread,
            double fxQuoteGearing,
            bool isFxBaseCurrencyLegResettable,
            EvaluationContext context,
            int fxResetFixingDays,
            Calendar? fxResetFixingCalendar,
            int fxBasePaymentLag,
            int fxQuotePaymentLag,
            BusinessDayConvention fxBasePaymentConvention,
            BusinessDayConvention fxQuotePaymentConvention,
            LegOptions fxBaseOptions,
            LegOptions fxQuoteOptions,
            bool telescopicValueDates,
            bool? useIndexedCoupons,
            StubIndexSelection fxBaseStubIndexSelection,
            StubIndexSelection fxQuoteStubIndexSelection)
        {
            EnumArguments.Check(type, nameof(type));
            ArgumentNullException.ThrowIfNull(fxBaseSchedule);
            ArgumentNullException.ThrowIfNull(fxBaseIndex);
            ArgumentNullException.ThrowIfNull(fxQuoteSchedule);
            ArgumentNullException.ThrowIfNull(fxQuoteIndex);
            ArgumentNullException.ThrowIfNull(context);
            EnumArguments.Check(fxBasePaymentConvention, nameof(fxBasePaymentConvention));
            EnumArguments.Check(fxQuotePaymentConvention, nameof(fxQuotePaymentConvention));

            Type = type;
            FxBaseNominal = fxBaseNominal;
            FxBaseCurrency = fxBaseCurrency;
            FxBaseSchedule = fxBaseSchedule;
            FxBaseIndex = fxBaseIndex;
            FxBaseSpread = fxBaseSpread;
            FxBaseGearing = fxBaseGearing;
            FxBaseStubIndexSelection = fxBaseStubIndexSelection;
            FxQuoteNominal = fxQuoteNominal;
            FxQuoteCurrency = fxQuoteCurrency;
            FxQuoteSchedule = fxQuoteSchedule;
            FxQuoteIndex = fxQuoteIndex;
            FxQuoteSpread = fxQuoteSpread;
            FxQuoteGearing = fxQuoteGearing;
            FxQuoteStubIndexSelection = fxQuoteStubIndexSelection;
            IsFxBaseCurrencyLegResettable = isFxBaseCurrencyLegResettable;
            FxResetConvention = new FxResetConvention(
                fxResetFixingDays,
                fxResetFixingDays != 0 && fxResetFixingCalendar is null
                    ? (isFxBaseCurrencyLegResettable ? fxBaseSchedule.Calendar : fxQuoteSchedule.Calendar)
                    : fxResetFixingCalendar);
            FxBasePaymentConvention = fxBasePaymentConvention;
            FxQuotePaymentConvention = fxQuotePaymentConvention;

            // Base-currency leg
            ImmutableArray<CashFlow> baseLeg = Leg(
                fxBaseSchedule,
                fxBaseIndex,
                fxBaseNominal,
                fxBaseSpread,
                fxBaseGearing,
                fxBasePaymentConvention,
                fxBasePaymentLag,
                fxBaseOptions,
                telescopicValueDates,
                useIndexedCoupons,
                fxBaseStubIndexSelection,
                context);

            // Quote-currency leg
            ImmutableArray<CashFlow> quoteLeg = Leg(
                fxQuoteSchedule,
                fxQuoteIndex,
                fxQuoteNominal,
                fxQuoteSpread,
                fxQuoteGearing,
                fxQuotePaymentConvention,
                fxQuotePaymentLag,
                fxQuoteOptions,
                telescopicValueDates,
                useIndexedCoupons,
                fxQuoteStubIndexSelection,
                context);

            Date baseStart = CashFlows.StartDate(baseLeg);
            Date quoteStart = CashFlows.StartDate(quoteLeg);
            Date earliestDate = quoteStart < baseStart ? quoteStart : baseStart;
            Date baseMaturity = CashFlows.MaturityDate(baseLeg);
            Date quoteMaturity = CashFlows.MaturityDate(quoteLeg);
            Date maturityDate = baseMaturity < quoteMaturity ? quoteMaturity : baseMaturity;

            // The resettable leg's coupons are replaced by FX-resetting equivalents, whose notional is the
            // constant-leg notional converted at each period's reset, and the netted notional exchanges of a
            // mark-to-market leg are added: the first notional at inception, the reset difference at each period
            // boundary, and the last notional at maturity. The FX rates the flows convert at are attached later by the
            // pricing engine.
            double constantLegNotional = isFxBaseCurrencyLegResettable ? fxQuoteNominal : fxBaseNominal;
            if (constantLegNotional == 0.0)
            {
                throw new ArgumentException(
                    "Constant leg notional cannot be zero",
                    isFxBaseCurrencyLegResettable ? nameof(fxQuoteNominal) : nameof(fxBaseNominal));
            }

            int resettingLegNo = isFxBaseCurrencyLegResettable ? 0 : 1;
            Schedule resettingSchedule = resettingLegNo == 0 ? fxBaseSchedule : fxQuoteSchedule;
            BusinessDayConvention paymentConvention =
                resettingLegNo == 0 ? fxBasePaymentConvention : fxQuotePaymentConvention;
            Calendar paymentCalendar = resettingSchedule.Calendar ?? EmptyCalendar.Instance;

            ImmutableArray<CashFlow> legToReset = resettingLegNo == 0 ? baseLeg : quoteLeg;
            var resettingLeg = new List<CashFlow>((2 * legToReset.Length) + 1);
            FxReset? previousReset = null; // empty: first exchange has no maturing period
            foreach (CashFlow cf in legToReset)
            {
                if (cf is not FloatingRateCoupon coupon)
                {
                    throw new NanoQLException("unexpected non-coupon cash flow on the resettable leg");
                }

                FxReset reset = FxResetConvention.Reset(coupon.AccrualStartDate);

                // An interim exchange establishes the new notional at the current accrual period's FX value date.
                // Coupon payment lag does not apply to notional exchanges. The initial exchange is made on the
                // effective date, adjusted with the leg's payment convention.
                Date exchangeDate = previousReset is not null
                    ? reset.ValueDate
                    : paymentCalendar.Adjust(earliestDate, paymentConvention);
                resettingLeg.Add(new FxResetNotionalExchange(exchangeDate, constantLegNotional, previousReset, reset));
                resettingLeg.Add(new FxResetCoupon(coupon, constantLegNotional, reset));
                previousReset = reset;
            }

            resettingLeg.Add(new FxResetNotionalExchange(
                paymentCalendar.Adjust(maturityDate, paymentConvention), constantLegNotional, previousReset, null));
            ImmutableArray<CashFlow> sorted = SortLegByDate([.. resettingLeg]);

            // Only the constant-notional leg gets the inception/maturity exchange flows.
            if (resettingLegNo == 0)
            {
                ImmutableArray<CashFlow> quoteExchanged = Exchanges(
                    quoteLeg, fxQuoteSchedule, earliestDate, maturityDate, fxQuotePaymentConvention, fxQuoteNominal);
                Legs = [sorted, quoteExchanged];
            }
            else
            {
                ImmutableArray<CashFlow> baseExchanged = Exchanges(
                    baseLeg, fxBaseSchedule, earliestDate, maturityDate, fxBasePaymentConvention, fxBaseNominal);
                Legs = [baseExchanged, sorted];
            }
        }

        internal MtMCrossCurrencyBasisSwapType Type { get; }

        internal double FxBaseNominal { get; }

        internal Currency? FxBaseCurrency { get; }

        internal Schedule FxBaseSchedule { get; }

        internal IborIndex FxBaseIndex { get; }

        internal double FxBaseSpread { get; }

        internal double FxBaseGearing { get; }

        internal StubIndexSelection FxBaseStubIndexSelection { get; }

        internal double FxQuoteNominal { get; }

        internal Currency? FxQuoteCurrency { get; }

        internal Schedule FxQuoteSchedule { get; }

        internal IborIndex FxQuoteIndex { get; }

        internal double FxQuoteSpread { get; }

        internal double FxQuoteGearing { get; }

        internal StubIndexSelection FxQuoteStubIndexSelection { get; }

        internal bool IsFxBaseCurrencyLegResettable { get; }

        internal FxResetConvention FxResetConvention { get; }

        internal BusinessDayConvention FxBasePaymentConvention { get; }

        internal BusinessDayConvention FxQuotePaymentConvention { get; }

        internal ImmutableArray<CashFlow>[] Legs { get; }

        private static ImmutableArray<CashFlow> Leg(
            Schedule schedule,
            IborIndex index,
            double nominal,
            double spread,
            double gearing,
            BusinessDayConvention paymentConvention,
            int paymentLag,
            LegOptions options,
            bool telescopicValueDates,
            bool? useIndexedCoupons,
            StubIndexSelection stubIndexSelection,
            EvaluationContext context) =>
            index is OvernightIndex on
                ? new OvernightLeg(schedule, on)
                    .WithNotionals(nominal)
                    .WithSpreads(spread)
                    .WithGearings(gearing)
                    .WithPaymentAdjustment(paymentConvention)
                    .WithPaymentCalendar(schedule.Calendar)
                    .WithPaymentLag(paymentLag)
                    .CompoundingSpreadDaily(options.CompoundSpread)
                    .WithLookbackDays(options.LookbackDays)
                    .WithObservationShift(options.ObservationShift)
                    .WithLockoutDays(options.LockoutDays)
                    .WithAveragingMethod(options.AveragingMethod)
                    .WithTelescopicValueDates(telescopicValueDates)
                    .ToLeg()
                : new IborLeg(schedule, index)
                    .WithNotionals(nominal)
                    .WithSpreads(spread)
                    .WithGearings(gearing)
                    .WithPaymentAdjustment(paymentConvention)
                    .WithPaymentCalendar(schedule.Calendar)
                    .WithPaymentLag(paymentLag)
                    .WithIndexedCoupons(useIndexedCoupons)
                    .WithStubIndexSelection(stubIndexSelection)
                    .ToLeg(context);

        private static ImmutableArray<CashFlow> Exchanges(
            ImmutableArray<CashFlow> leg,
            Schedule schedule,
            Date earliestDate,
            Date maturityDate,
            BusinessDayConvention convention,
            double nominal) =>
            AddNotionalExchangesToLeg(
                leg, schedule.Calendar ?? EmptyCalendar.Instance, earliestDate, maturityDate, convention, nominal);
    }
}
