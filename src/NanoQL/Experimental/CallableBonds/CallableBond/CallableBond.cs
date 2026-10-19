// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2017 BN Algorithms Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/callablebond.hpp ql/experimental/callablebonds/callablebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The callable bond base class: a bond with a schedule of calls and puts, European or Bermudan (no American
/// optionality), for fixed-rate and zero-coupon bonds.
/// </summary>
/// <remarks>
/// <para>
/// Its engines read what QuantLib's <c>setupArguments</c> computes for the settlement date of the context they price
/// under: the coupons still to be paid, and the callabilities still to be exercised, their clean prices made dirty
/// with the accrued amount at the call date. A plain bond's engine (<see cref="PricingEngines.DiscountingBondEngine"/>)
/// refuses a callable bond, as QuantLib's refuses it when it sets up its arguments, with "no arguments given".
/// </para>
/// <para>
/// <b>The option-adjusted spread.</b> QuantLib's <c>OAS</c>, <c>cleanPriceOAS</c>, <c>effectiveDuration</c> and
/// <c>effectiveConvexity</c> re-run the bond's attached engine with a spread written into its arguments. Here they take
/// the engine as a delegate that takes the spread, such as
/// <c>(bond, context, spread) =&gt; TreeCallableFixedRateBondEngine.Calculate(bond, context, model, 240, null, spread)</c>,
/// and compute as QuantLib does: on the engine's NPV, at its curve's reference date, not its settlement value, so the
/// spread of a bond's own clean price isn't 0; with Brent's solver bracketing from QuantLib's step of 0.001; and with
/// the spread converted between continuous compounding and the given conventions on the given curve, which serves
/// only that conversion. An engine that ignores the spread, such as the Black engine, gives a flat price, which the
/// solver can't bracket, as QuantLib's can't.
/// </para>
/// <para>
/// Unsealed, an extension point, as QuantLib's base is. Its protected constructor follows <see cref="Bond"/>'s
/// construction contract: a derived constructor writes the cash flows and builds the redemptions.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class CallableBond : Bond
{
    private readonly ImmutableArray<Callability> putCallSchedule;

    /// <summary>Initializes a new instance of the <see cref="CallableBond"/> class, with no cash flows yet.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="calendar">The settlement calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="paymentDayCounter">
    /// The day counter of the payments, which the Black engine's forward yield reads, or <see langword="null"/>,
    /// QuantLib's empty day counter.
    /// </param>
    /// <param name="faceAmount">The face amount.</param>
    /// <param name="frequency">
    /// The coupon frequency, which the Black engine's forward yield reads; QuantLib leaves it to the derived class.
    /// </param>
    /// <param name="issueDate">The issue date, or <see langword="null"/> for none.</param>
    /// <param name="putCallSchedule">The calls and puts, copied; none by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or <paramref name="frequency"/> isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A date is <c>default(Date)</c>; a callability is <see langword="null"/>; or a callability is after the maturity
    /// date.
    /// </exception>
    protected CallableBond(
        int settlementDays,
        Date maturityDate,
        Calendar? calendar,
        DayCounter? paymentDayCounter,
        double faceAmount,
        Frequency frequency,
        Date? issueDate = null,
        IEnumerable<Callability>? putCallSchedule = null)
        : base(settlementDays, calendar, issueDate)
    {
        Date.ThrowIfDefault(maturityDate, nameof(maturityDate));
        EnumArguments.Check(frequency, nameof(frequency));
        PaymentDayCounter = paymentDayCounter;
        Frequency = frequency;
        FaceAmount = faceAmount;
        this.putCallSchedule = putCallSchedule is null ? [] : [.. putCallSchedule];

        this.maturityDate = maturityDate;

        if (!this.putCallSchedule.IsEmpty)
        {
            Date finalOptionDate = Date.MinDate;
            foreach (Callability i in this.putCallSchedule)
            {
                // QuantLib dereferences a null callability here.
                if (i is null)
                {
                    throw new ArgumentException("null callability in schedule", nameof(putCallSchedule));
                }

                // std::max(finalOptionDate, i->date())
                finalOptionDate = finalOptionDate < i.Date ? i.Date : finalOptionDate;
            }

            if (!(finalOptionDate <= maturityDate))
            {
                throw new ArgumentException("Bond cannot mature before last call/put date", nameof(putCallSchedule));
            }
        }

        // derived classes must set cashflows
    }

    /// <summary>Gets the bond's put/call schedule.</summary>
    public ImmutableArray<Callability> Callability => putCallSchedule;

    /// <summary>Gets the day counter of the payments, or <see langword="null"/> for QuantLib's empty one.</summary>
    protected DayCounter? PaymentDayCounter { get; }

    /// <summary>Gets the coupon frequency.</summary>
    protected Frequency Frequency { get; }

    /// <summary>Gets the face amount.</summary>
    protected double FaceAmount { get; }

    /// <inheritdoc/>
    public override CallableBondResults SetupExpired()
    {
        BondResults expired = base.SetupExpired();
        return new CallableBondResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
            SettlementValue = expired.SettlementValue,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't a callable bond's.</exception>
    public override CallableBondResults FetchResults(InstrumentResults results)
    {
        BondResults fetched = base.FetchResults(results);
        if (fetched is not CallableBondResults r)
        {
            throw new ArgumentException("wrong result type", nameof(results));
        }

        return r;
    }

    /// <summary>
    /// The Black implied forward yield volatility (Hull, fourth edition, chapter 20, p. 536): the volatility at which the
    /// Black engine reproduces a target price. Relevant only to a bond with a single call or put date.
    /// </summary>
    /// <param name="targetPrice">The target price, clean or dirty, per 100 of notional.</param>
    /// <param name="discountCurve">The Black engine's discount curve.</param>
    /// <param name="context">The evaluation context, which gives the settlement date and the volatility's reference date.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxEvaluations">The solver's maximum number of evaluations.</param>
    /// <param name="minVol">The lowest volatility tried.</param>
    /// <param name="maxVol">The highest volatility tried.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The bond has expired; it doesn't have exactly one call or put date; or the solver fails.
    /// </exception>
    /// <remarks>
    /// The target is the dirty price times the face amount over 100, matched by the Black engine's settlement value,
    /// with Brent's solver from the middle of the range. Each trial builds the engine's constant volatility at the
    /// trial's value, where QuantLib's NPVSpreadHelper sets a quote its engine observes.
    /// </remarks>
    public double ImpliedVolatility(
        BondPrice targetPrice,
        YieldTermStructure discountCurve,
        EvaluationContext context,
        double accuracy,
        int maxEvaluations,
        double minVol,
        double maxVol)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(targetPrice);
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        if (IsExpired(context))
        {
            throw new NanoQLException("instrument expired");
        }

        double dirtyTargetPrice = targetPrice.Type switch
        {
            BondPriceType.Dirty => targetPrice.Amount,
            BondPriceType.Clean => targetPrice.Amount + AccruedAmount(context),
            _ => throw new NanoQLException("unknown price type"),
        };

        double targetValue = dirtyTargetPrice * FaceAmount / 100.0;
        double guess = 0.5 * (minVol + maxVol);

        // QuantLib's ImpliedVolHelper: the arguments set up once, the engine's settlement value at each trial
        CallableBondArguments arguments = SetupArguments(context);
        double ImpliedVolHelper(double x)
        {
            CallableBondConstantVolatility volatility =
                CallableBondConstantVolatility.Create(0, new NullCalendar(), x, new Actual365Fixed(), context);
            double value = BlackCallableFixedRateBondEngine.Value(arguments, volatility, discountCurve, context)
                .SettlementValue;
            return value - targetValue;
        }

        Brent solver = new Brent().SetMaxEvaluations(maxEvaluations);
        return solver.Solve(ImpliedVolHelper, accuracy, guess, minVol, maxVol);
    }

    /// <summary>
    /// The option-adjusted spread: the spread that, added to the engine's curve, makes its value match a clean price.
    /// </summary>
    /// <param name="cleanPrice">The clean price, per 100 of notional.</param>
    /// <param name="engineTS">The curve the spread is converted on; it doesn't price.</param>
    /// <param name="dayCounter">The day counter of the spread.</param>
    /// <param name="compounding">The compounding of the spread.</param>
    /// <param name="frequency">The frequency of the spread.</param>
    /// <param name="engine">The engine, given the bond, the context and a continuously compounded spread.</param>
    /// <param name="context">The evaluation context, which gives the settlement date.</param>
    /// <param name="settlementDate">The settlement date of the price; the bond's when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxIterations">The solver's maximum number of evaluations.</param>
    /// <param name="guess">The solver's initial guess, a continuously compounded spread.</param>
    /// <returns>The spread, in the given conventions.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxIterations"/> is negative, or the compounding or the frequency isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="settlementDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The solver can't bracket the spread, or exceeds its evaluations.</exception>
    /// <remarks>
    /// The clean price is made dirty with the accrued amount at the settlement date and turned into an amount on the
    /// notional there, which the engine's NPV, at its curve's reference date, must match: QuantLib's basis, so the
    /// spread of the engine's own clean price, whose value is at the settlement date, isn't 0.
    /// </remarks>
    public double OAS(
        double cleanPrice,
        YieldTermStructure engineTS,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        Func<CallableBond, EvaluationContext, double, CallableBondResults> engine,
        EvaluationContext context,
        Date? settlementDate = null,
        double accuracy = 1.0e-10,
        int maxIterations = 100,
        double guess = 0.0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(engineTS);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(engine);
        EnumArguments.Check(compounding, nameof(compounding));
        EnumArguments.Check(frequency, nameof(frequency));
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);

        Date settlement = settlementDate ?? SettlementDate(context);

        double dirtyPrice = cleanPrice + AccruedAmount(context, settlement);
        dirtyPrice /= 100.0 / Notional(context, settlement);

        Func<double, double> f = NPVSpreadHelper(engine, context);

        // QuantLib's OASHelper
        double Obj(double x) => dirtyPrice - f(x);

        Brent solver = new Brent().SetMaxEvaluations(maxIterations);

        double step = 0.001;
        double oas = solver.Solve(Obj, accuracy, guess, step);

        return ContinuousToConv(oas, engineTS, dayCounter, compounding, frequency);
    }

    /// <summary>The clean price at an option-adjusted spread over the engine's curve.</summary>
    /// <param name="oas">The spread, in the given conventions.</param>
    /// <param name="engineTS">The curve the spread is converted on; it doesn't price.</param>
    /// <param name="dayCounter">The day counter of the spread.</param>
    /// <param name="compounding">The compounding of the spread.</param>
    /// <param name="frequency">The frequency of the spread.</param>
    /// <param name="engine">The engine, given the bond, the context and a continuously compounded spread.</param>
    /// <param name="context">The evaluation context, which gives the settlement date.</param>
    /// <param name="settlementDate">The settlement date of the price; the bond's when <see langword="null"/>.</param>
    /// <returns>The clean price, per 100 of notional: the engine's NPV per 100 of the notional at the settlement date,
    /// less the accrued amount there.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="settlementDate"/> is <c>default(Date)</c>.</exception>
    public double CleanPriceOAS(
        double oas,
        YieldTermStructure engineTS,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        Func<CallableBond, EvaluationContext, double, CallableBondResults> engine,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(engineTS);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(engine);
        EnumArguments.Check(compounding, nameof(compounding));
        EnumArguments.Check(frequency, nameof(frequency));
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));

        Date settlement = settlementDate ?? SettlementDate(context);

        oas = ConvToContinuous(oas, engineTS, dayCounter, compounding, frequency);

        Func<double, double> f = NPVSpreadHelper(engine, context);

        double p = (f(oas) * 100.0 / Notional(context, settlement)) - AccruedAmount(context, settlement);

        return p;
    }

    /// <summary>
    /// The effective duration: the first derivative of the dirty price with respect to a parallel shift of the
    /// engine's curve, over the dirty price, by central differences of the option-adjusted spread.
    /// </summary>
    /// <param name="oas">The spread, in the given conventions.</param>
    /// <param name="engineTS">The curve the spread is converted on; it doesn't price.</param>
    /// <param name="dayCounter">The day counter of the spread.</param>
    /// <param name="compounding">The compounding of the spread.</param>
    /// <param name="frequency">The frequency of the spread.</param>
    /// <param name="engine">The engine, given the bond, the context and a continuously compounded spread.</param>
    /// <param name="context">The evaluation context, which gives the settlement date.</param>
    /// <param name="bump">The shift of the spread either side.</param>
    /// <returns>The duration; 0 when the dirty price is 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public double EffectiveDuration(
        double oas,
        YieldTermStructure engineTS,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        Func<CallableBond, EvaluationContext, double, CallableBondResults> engine,
        EvaluationContext context,
        double bump = 2e-4)
    {
        double p = CleanPriceOAS(oas, engineTS, dayCounter, compounding, frequency, engine, context);

        double ppp = CleanPriceOAS(oas + bump, engineTS, dayCounter, compounding, frequency, engine, context);
        double pmm = CleanPriceOAS(oas - bump, engineTS, dayCounter, compounding, frequency, engine, context);

        double accrued = AccruedAmount(context);
        double dirtyP = p + accrued;

        if (dirtyP == 0.0)
        {
            return 0;
        }
        else
        {
            return (pmm - ppp) / (2 * dirtyP * bump);
        }
    }

    /// <summary>
    /// The effective convexity: the second derivative of the dirty price with respect to a parallel shift of the
    /// engine's curve, over the dirty price, by central differences of the option-adjusted spread.
    /// </summary>
    /// <param name="oas">The spread, in the given conventions.</param>
    /// <param name="engineTS">The curve the spread is converted on; it doesn't price.</param>
    /// <param name="dayCounter">The day counter of the spread.</param>
    /// <param name="compounding">The compounding of the spread.</param>
    /// <param name="frequency">The frequency of the spread.</param>
    /// <param name="engine">The engine, given the bond, the context and a continuously compounded spread.</param>
    /// <param name="context">The evaluation context, which gives the settlement date.</param>
    /// <param name="bump">The shift of the spread either side.</param>
    /// <returns>The convexity; 0 when the dirty price is 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public double EffectiveConvexity(
        double oas,
        YieldTermStructure engineTS,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        Func<CallableBond, EvaluationContext, double, CallableBondResults> engine,
        EvaluationContext context,
        double bump = 2e-4)
    {
        double p = CleanPriceOAS(oas, engineTS, dayCounter, compounding, frequency, engine, context);

        double ppp = CleanPriceOAS(oas + bump, engineTS, dayCounter, compounding, frequency, engine, context);
        double pmm = CleanPriceOAS(oas - bump, engineTS, dayCounter, compounding, frequency, engine, context);

        double accrued = AccruedAmount(context);
        double dirtyP = p + accrued;

        if (dirtyP == 0.0)
        {
            return 0;
        }
        else
        {
            return (ppp + pmm - (2 * p)) / (Math.Pow(bump, 2) * dirtyP);
        }
    }

    /// <summary>
    /// What the bond's engines read, for the settlement date of a context: QuantLib's <c>setupArguments</c>. The
    /// coupons neither paid nor trading ex-coupon at settlement; the redemption; the callabilities not yet occurred,
    /// each with its type and its price, a clean price made dirty with the accrued amount of the first cash flow not
    /// yet paid at the call date, when that is a coupon (with its amount when it trades ex-coupon there, as the call is
    /// an issuer's action that ignores the market's ex-coupon convention: QuantLib's issue #2236).
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The arguments.</returns>
    /// <exception cref="NanoQLException">The bond has no single redemption.</exception>
    internal CallableBondArguments SetupArguments(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Bond::setupArguments
        Date settlement = SettlementDate(context);

        CashFlow redemption = Redemption();
        double redemptionAmount = redemption.Amount(context);
        Date redemptionDate = redemption.Date;

        ImmutableArray<CashFlow> cfs = Cashflows;

        ImmutableArray<Date>.Builder couponDates = ImmutableArray.CreateBuilder<Date>(cfs.Length - 1);
        ImmutableArray<double>.Builder couponAmounts = ImmutableArray.CreateBuilder<double>(cfs.Length - 1);

        for (int i = 0; i < cfs.Length - 1; i++)
        {
            if (!cfs[i].HasOccurred(context, settlement, false) && !cfs[i].TradingExCoupon(context, settlement))
            {
                couponDates.Add(cfs[i].Date);
                couponAmounts.Add(cfs[i].Amount(context));
            }
        }

        ImmutableArray<double>.Builder callabilityPrices = ImmutableArray.CreateBuilder<double>(putCallSchedule.Length);
        ImmutableArray<Date>.Builder callabilityDates = ImmutableArray.CreateBuilder<Date>(putCallSchedule.Length);
        ImmutableArray<CallabilityType>.Builder callabilityTypes =
            ImmutableArray.CreateBuilder<CallabilityType>(putCallSchedule.Length);

        foreach (Callability i in putCallSchedule)
        {
            if (!i.HasOccurred(context, settlement, false))
            {
                callabilityDates.Add(i.Date);

                // QuantLib's fix after v1.44 (PR #2875): each live callability's own type
                callabilityTypes.Add(i.Type);
                double price = i.Price.Amount;

                if (i.Price.Type == BondPriceType.Clean)
                {
                    /* Convert clean call price to dirty using accrued interest
                       at the call date. We ignore ex-coupon conventions here
                       because the call is an issuer action governed by the
                       indenture: the holder receives the call price plus accrued
                       from the last payment date. Using market (ex-coupon) accrued
                       would create an inconsistency with the tree's continuation
                       value, which includes future coupons filtered at the
                       settlement date (see GitHub issue #2236). */
                    Date callDate = i.Date;
                    double callAccrued = 0.0;
                    foreach (CashFlow cf in cashflows)
                    {
                        if (!cf.HasOccurred(context, callDate, false))
                        {
                            if (cf is Coupon coupon)
                            {
                                double acc = coupon.AccruedAmount(callDate, context);
                                if (coupon.TradingExCoupon(context, callDate))
                                {
                                    acc = coupon.Amount(context) + acc;
                                }

                                callAccrued = acc / Notional(context, callDate) * 100.0;
                            }

                            break;
                        }
                    }

                    price += callAccrued;
                }

                callabilityPrices.Add(price);
            }
        }

        return new CallableBondArguments
        {
            SettlementDate = settlement,
            Cashflows = cfs,
            Calendar = Calendar,
            CouponDates = couponDates.ToImmutable(),
            CouponAmounts = couponAmounts.ToImmutable(),
            FaceAmount = FaceAmount,
            Redemption = redemptionAmount,
            RedemptionDate = redemptionDate,
            PaymentDayCounter = PaymentDayCounter,
            Frequency = Frequency,
            PutCallSchedule = putCallSchedule,
            CallabilityPrices = callabilityPrices.ToImmutable(),
            CallabilityDates = callabilityDates.ToImmutable(),
            CallabilityTypes = callabilityTypes.ToImmutable(),
        };
    }

    /// <summary>Refuses a plain bond's engine, as QuantLib's <c>setupArguments</c> refuses a plain bond's arguments.</summary>
    /// <exception cref="NanoQLException">Always.</exception>
    internal override void CheckPlainBondArguments() => throw new NanoQLException("no arguments given");

    // QuantLib's NPVSpreadHelper: the engine's NPV at a spread.
    private Func<double, double> NPVSpreadHelper(
        Func<CallableBond, EvaluationContext, double, CallableBondResults> engine, EvaluationContext context) =>
        x => engine(this, context, x).NPV ?? throw new NanoQLException("NPV not provided");

    // Converts a continuous spread to a conventional spread to a reference yield curve.
    private double ContinuousToConv(
        double oas, YieldTermStructure yts, DayCounter dayCounter, Compounding compounding, Frequency frequency)
    {
        Date maturity = MaturityDate();
        double zz = yts.ZeroRate(maturity, dayCounter, Compounding.Continuous, Frequency.NoFrequency).Rate;
        var baseRate = new InterestRate(zz, dayCounter, Compounding.Continuous, Frequency.NoFrequency);
        var spreadedRate = new InterestRate(oas + zz, dayCounter, Compounding.Continuous, Frequency.NoFrequency);
        double br = baseRate.EquivalentRate(dayCounter, compounding, frequency, yts.ReferenceDate, maturity).Rate;
        double sr = spreadedRate.EquivalentRate(dayCounter, compounding, frequency, yts.ReferenceDate, maturity).Rate;

        // Return the spread
        return sr - br;
    }

    // Converts a conventional spread to a reference yield curve to a continuous spread.
    private double ConvToContinuous(
        double oas, YieldTermStructure yts, DayCounter dayCounter, Compounding compounding, Frequency frequency)
    {
        Date maturity = MaturityDate();
        double zz = yts.ZeroRate(maturity, dayCounter, compounding, frequency).Rate;
        var baseRate = new InterestRate(zz, dayCounter, compounding, frequency);

        var spreadedRate = new InterestRate(oas + zz, dayCounter, compounding, frequency);
        double br = baseRate.EquivalentRate(
            dayCounter, Compounding.Continuous, Frequency.NoFrequency, yts.ReferenceDate, maturity).Rate;
        double sr = spreadedRate.EquivalentRate(
            dayCounter, Compounding.Continuous, Frequency.NoFrequency, yts.ReferenceDate, maturity).Rate;

        // Return the spread
        return sr - br;
    }
}
