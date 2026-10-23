// SPDX-FileCopyrightText: Copyright (C) 2007 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/fittedbonddiscountcurve.hpp ql/termstructures/yield/fittedbonddiscountcurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// A discount curve fitted to a set of bonds: a discount function d(t), a fitting method's, fitted so that the bonds'
/// cash flows discounted on it reproduce their prices in an optimized sense, the price errors weighted by the inverse of
/// the bonds' durations.
/// </summary>
/// <remarks>
/// <para>
/// The curve is a generic wrapper, and its <see cref="FittedBondDiscountCurveFittingMethod"/> the implementation: a new
/// fitting method derives from that. The fit can be slow with many bonds, and its speed depends on the method and its
/// convergence under the optimization. Built from given parameters, the curve doesn't fit: it evaluates the method's
/// discount function on them, up to a given maximum date, for example to use parameters fitted to bonds in one currency
/// as a credit spread curve over a discount curve in another.
/// </para>
/// <para>
/// The curve fits in its constructor, as a <c>LazyObject</c>'s work is done at construction, so a fit that fails, or an
/// input it refuses, refuses the curve where QuantLib throws at the first read. While it fits, the curve answers at the
/// fit's trial parameters, and its bond helpers are copies bound to the curve itself, as QuantLib binds them to
/// <c>this</c>; nothing outside the constructor sees the curve change. QuantLib's quotes are doubles here, always valid,
/// so its check of an invalid price quote has nothing to refuse. QuantLib's constructors taking settlement days, whose
/// reference date moves with the evaluation date, are the <see cref="Create(int, Calendar, IEnumerable{BondHelper}, DayCounter, FittedBondDiscountCurveFittingMethod, EvaluationContext, double, int, IEnumerable{double}, double, int)"/>
/// factories.
/// </para>
/// <para>
/// A curve is always built afresh, fitted from the given guess, the initial guess the L2 penalty's header doc anchors
/// it to. QuantLib's curve keeps its last solution as its guess (<c>fittedbonddiscountcurve.cpp:294-295</c>), so
/// recalculated after a quote moves it starts from, and is penalized towards, its previous fit. A documented divergence.
/// QuantLib's <c>resetGuess</c>, which refits in place, is <see cref="ResetGuess"/>, which returns the curve refitted.
/// </para>
/// </remarks>
public sealed class FittedBondDiscountCurve : YieldTermStructure
{
    private readonly FittedBondDiscountCurveFittingMethod givenMethod;
    private readonly FittedBondDiscountCurveFittingMethod fittingMethod;
    private readonly ImmutableArray<BondHelper> bondHelpers;
    private readonly Date maxDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="FittedBondDiscountCurve"/> class with a reference date fixed for the
    /// life of the curve, and fits it.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="bonds">The bond helpers the curve is fitted to.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="fittingMethod">The fitting method; the curve fits a copy of it.</param>
    /// <param name="context">The evaluation context the bonds are settled and priced in.</param>
    /// <param name="accuracy">The optimization's root, function and gradient-norm accuracy.</param>
    /// <param name="maxEvaluations">The optimization's maximum number of iterations; 0 to use the guess as the parameters.</param>
    /// <param name="guess">The fit's starting point, or <see langword="null"/> or empty for zeros.</param>
    /// <param name="simplexLambda">The scale of the <see cref="Numerics.Simplex"/> used when the method gives no optimizer.</param>
    /// <param name="maxStationaryStateIterations">The maximum number of iterations without improvement.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="bonds"/>, <paramref name="dayCounter"/>, <paramref name="fittingMethod"/> or
    /// <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// No bond is given, a helper is <see langword="null"/>, the guess or the method's weights or penalties don't fit
    /// the bonds or the parameters, or the end criteria refuse the iteration counts.
    /// </exception>
    /// <exception cref="NanoQLException">A bond settles before the reference date or isn't tradable there.</exception>
    public FittedBondDiscountCurve(
        Date referenceDate,
        IEnumerable<BondHelper> bonds,
        DayCounter dayCounter,
        FittedBondDiscountCurveFittingMethod fittingMethod,
        EvaluationContext context,
        double accuracy = 1.0e-10,
        int maxEvaluations = 10000,
        IEnumerable<double>? guess = null,
        double simplexLambda = 1.0,
        int maxStationaryStateIterations = 100)
        : this(
            referenceDate,
            null,
            null,
            Helpers(bonds),
            dayCounter,
            fittingMethod,
            context,
            accuracy,
            maxEvaluations,
            guess is null ? [] : [.. guess],
            simplexLambda,
            maxStationaryStateIterations,
            default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FittedBondDiscountCurve"/> class with a reference date fixed for the
    /// life of the curve, which doesn't fit but evaluates the method on given parameters.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="fittingMethod">The fitting method whose discount function the curve evaluates.</param>
    /// <param name="parameters">The precalculated parameters, the method's <see cref="FittedBondDiscountCurveFittingMethod.Size"/> of them.</param>
    /// <param name="maxDate">The latest date the curve gives values for.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fittingMethod"/>, <paramref name="parameters"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The maximum date is <c>default(Date)</c> (QuantLib's "no bond helpers or max date given"), or the parameters
    /// aren't the method's number.
    /// </exception>
    public FittedBondDiscountCurve(
        Date referenceDate,
        FittedBondDiscountCurveFittingMethod fittingMethod,
        IEnumerable<double> parameters,
        Date maxDate,
        DayCounter dayCounter)
        : this(referenceDate, null, null, [], dayCounter, fittingMethod, null, 1e-10, 0, Parameters(parameters), 1.0, 100, maxDate)
    {
    }

    // The constructors' work, QuantLib's performCalculations(): the checks, the maximum date, the helpers bound to the
    // curve, the method's init() and calculate().
    private FittedBondDiscountCurve(
        Date referenceDate,
        Calendar? calendar,
        int? settlementDays,
        ImmutableArray<BondHelper> bondHelpers,
        DayCounter dayCounter,
        FittedBondDiscountCurveFittingMethod fittingMethod,
        EvaluationContext? context,
        double accuracy,
        int maxEvaluations,
        ImmutableArray<double> guess,
        double simplexLambda,
        int maxStationaryStateIterations,
        Date maxDate)
        : base(referenceDate, calendar, dayCounter, settlementDays)
    {
        ArgumentNullException.ThrowIfNull(fittingMethod);
        if (!bondHelpers.IsEmpty)
        {
            ArgumentNullException.ThrowIfNull(context);
        }

        givenMethod = fittingMethod;
        this.bondHelpers = bondHelpers;
        Accuracy = accuracy;
        MaxEvaluations = maxEvaluations;
        Guess = guess;
        SimplexLambda = simplexLambda;
        MaxStationaryStateIterations = maxStationaryStateIterations;
        this.fittingMethod = fittingMethod.CopyFor(this);

        if (maxEvaluations != 0)
        {
            // we need to fit, so we require helpers
            if (bondHelpers.IsEmpty)
            {
                throw new ArgumentException("no bond helpers given", "bonds");
            }
        }

        if (maxEvaluations == 0)
        {
            // no fit, but we need either an explicit max date or helpers from which to deduce it
            if (!(maxDate != default || !bondHelpers.IsEmpty))
            {
                throw new ArgumentException("no bond helpers or max date given", nameof(maxDate));
            }
        }

        if (!bondHelpers.IsEmpty)
        {
            maxDate = Date.MinDate;
            Date refDate = ReferenceDate;

            // double check bond quotes still valid and/or instruments not expired
            var bound = ImmutableArray.CreateBuilder<BondHelper>(bondHelpers.Length);
            for (int i = 0; i < bondHelpers.Length; ++i)
            {
                Bond bond = bondHelpers[i].Bond;
                Date bondSettlement = bond.SettlementDate(context!);
                if (!(bondSettlement >= refDate))
                {
                    throw new NanoQLException(
                        $"{DataFormattersFunctions.Ordinal(i + 1)} bond settlemente date ({bondSettlement}) before curve "
                        + $"reference date ({refDate})");
                }

                if (!BondFunctions.IsTradable(bond, context!, bondSettlement))
                {
                    throw new NanoQLException(
                        $"{DataFormattersFunctions.Ordinal(i + 1)} bond non tradable at {bondSettlement} settlement date "
                        + $"(maturity being {bond.MaturityDate()})");
                }

                Date pillar = bondHelpers[i].PillarDate!.Value;
                maxDate = maxDate < pillar ? pillar : maxDate;
                bound.Add(bondHelpers[i].SetTermStructure(this));
            }

            FitHelpers = bound.MoveToImmutable();
        }

        this.maxDate = maxDate;
        FitContext = context;
        this.fittingMethod.InitFit();
        this.fittingMethod.Calculate();

        // the fit's working state goes: the helpers bound to the curve and the context they priced in
        FitHelpers = [];
        FitContext = null;
    }

    /// <summary>Gets the total number of bonds used to fit the curve.</summary>
    public int NumberOfBonds => bondHelpers.Length;

    /// <summary>Gets the latest date for which the curve can return values: the last bond's pillar, or the given date.</summary>
    public override Date MaxDate => maxDate;

    /// <summary>
    /// Gets the fit's results: the curve's copy of its fitting method, holding the solution, the iterations, the cost,
    /// the end criterion and the weights used.
    /// </summary>
    public FittedBondDiscountCurveFittingMethod FitResults => fittingMethod;

    // The fit's settings, which the fitting method reads.
    internal double Accuracy { get; }

    internal int MaxEvaluations { get; }

    internal ImmutableArray<double> Guess { get; }

    internal double SimplexLambda { get; }

    internal int MaxStationaryStateIterations { get; }

    // The helpers bound to the curve and the context they are priced in, while the curve is built.
    internal ImmutableArray<BondHelper> FitHelpers { get; private set; } = [];

    internal EvaluationContext? FitContext { get; private set; }

    /// <summary>
    /// A curve whose reference date is the evaluation date advanced by settlement days on a calendar, fitted: QuantLib's
    /// constructor taking settlement days, whose reference date moves with the evaluation date. The curve is built for
    /// the context's evaluation date.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar the settlement days are counted on.</param>
    /// <param name="bonds">The bond helpers the curve is fitted to.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="fittingMethod">The fitting method; the curve fits a copy of it.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="accuracy">The optimization's root, function and gradient-norm accuracy.</param>
    /// <param name="maxEvaluations">The optimization's maximum number of iterations; 0 to use the guess as the parameters.</param>
    /// <param name="guess">The fit's starting point, or <see langword="null"/> or empty for zeros.</param>
    /// <param name="simplexLambda">The scale of the <see cref="Numerics.Simplex"/> used when the method gives no optimizer.</param>
    /// <param name="maxStationaryStateIterations">The maximum number of iterations without improvement.</param>
    /// <returns>The fitted curve.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="calendar"/>, <paramref name="bonds"/>, <paramref name="dayCounter"/>,
    /// <paramref name="fittingMethod"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// No bond is given, a helper is <see langword="null"/>, the guess or the method's weights or penalties don't fit
    /// the bonds or the parameters, or the end criteria refuse the iteration counts.
    /// </exception>
    /// <exception cref="NanoQLException">A bond settles before the reference date or isn't tradable there.</exception>
    public static FittedBondDiscountCurve Create(
        int settlementDays,
        Calendar calendar,
        IEnumerable<BondHelper> bonds,
        DayCounter dayCounter,
        FittedBondDiscountCurveFittingMethod fittingMethod,
        EvaluationContext context,
        double accuracy = 1.0e-10,
        int maxEvaluations = 10000,
        IEnumerable<double>? guess = null,
        double simplexLambda = 1.0,
        int maxStationaryStateIterations = 100)
    {
        Date referenceDate = SettlementDate(settlementDays, calendar, context);
        return new FittedBondDiscountCurve(
            referenceDate,
            calendar,
            settlementDays,
            Helpers(bonds),
            dayCounter,
            fittingMethod,
            context,
            accuracy,
            maxEvaluations,
            guess is null ? [] : [.. guess],
            simplexLambda,
            maxStationaryStateIterations,
            default);
    }

    /// <summary>
    /// A curve whose reference date is the evaluation date advanced by settlement days on a calendar, which doesn't fit
    /// but evaluates the method on given parameters: QuantLib's constructor taking settlement days. The curve is built
    /// for the context's evaluation date.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar the settlement days are counted on.</param>
    /// <param name="fittingMethod">The fitting method whose discount function the curve evaluates.</param>
    /// <param name="parameters">The precalculated parameters, the method's <see cref="FittedBondDiscountCurveFittingMethod.Size"/> of them.</param>
    /// <param name="maxDate">The latest date the curve gives values for.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="calendar"/>, <paramref name="fittingMethod"/>, <paramref name="parameters"/>,
    /// <paramref name="dayCounter"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The maximum date is <c>default(Date)</c>, or the parameters aren't the method's number.
    /// </exception>
    public static FittedBondDiscountCurve Create(
        int settlementDays,
        Calendar calendar,
        FittedBondDiscountCurveFittingMethod fittingMethod,
        IEnumerable<double> parameters,
        Date maxDate,
        DayCounter dayCounter,
        EvaluationContext context)
    {
        Date referenceDate = SettlementDate(settlementDays, calendar, context);
        return new FittedBondDiscountCurve(
            referenceDate,
            calendar,
            settlementDays,
            [],
            dayCounter,
            fittingMethod,
            null,
            1e-10,
            0,
            Parameters(parameters),
            1.0,
            100,
            maxDate);
    }

    /// <summary>
    /// The curve refitted from another guess, to try out several and avoid local minima: QuantLib's <c>resetGuess</c>,
    /// which refits the curve in place. A curve built from given parameters takes the guess as its parameters. A curve
    /// built from settlement days is dated for the context's evaluation date.
    /// </summary>
    /// <param name="guess">The guess, empty or the method's number of parameters.</param>
    /// <param name="context">The evaluation context the bonds are settled and priced in.</param>
    /// <returns>The refitted curve, which allows extrapolation as this one does.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="guess"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The guess is neither empty nor the method's number of parameters, or the curve refuses it as its constructor
    /// does.
    /// </exception>
    /// <exception cref="NanoQLException">The curve's constructor refuses its bonds for the context.</exception>
    public FittedBondDiscountCurve ResetGuess(IEnumerable<double> guess, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(guess);
        ArgumentNullException.ThrowIfNull(context);
        ImmutableArray<double> g = [.. guess];
        if (!(g.IsEmpty || g.Length == fittingMethod.Size))
        {
            throw new ArgumentException("guess is of wrong size", nameof(guess));
        }

        Date referenceDate = SettlementDays is int days && Calendar is not null
            ? SettlementDate(days, Calendar, context)
            : ReferenceDate;
        var refitted = new FittedBondDiscountCurve(
            referenceDate,
            Calendar,
            SettlementDays,
            bondHelpers,
            DayCounter,
            givenMethod,
            bondHelpers.IsEmpty ? null : context,
            Accuracy,
            MaxEvaluations,
            g,
            SimplexLambda,
            MaxStationaryStateIterations,
            bondHelpers.IsEmpty ? maxDate : default);
        return refitted.WithExtrapolationOf(this);
    }

    /// <summary>The fitting method's discount at the fitted parameters, or at the fit's trial ones while it runs.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    protected override double DiscountImpl(double t) => fittingMethod.Discount(fittingMethod.Solution.AsSpan(), t);

    private static ImmutableArray<BondHelper> Helpers(IEnumerable<BondHelper> bonds)
    {
        ArgumentNullException.ThrowIfNull(bonds);
        ImmutableArray<BondHelper> helpers = [.. bonds];
        foreach (BondHelper h in helpers)
        {
            if (h is null)
            {
                throw new ArgumentException("null bond helper", nameof(bonds));
            }
        }

        return helpers;
    }

    private static ImmutableArray<double> Parameters(IEnumerable<double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return [.. parameters];
    }

    private static Date SettlementDate(int settlementDays, Calendar calendar, EvaluationContext context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(context);
        return calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
    }

    // This curve, or a copy of it allowing extrapolation as another does.
    private FittedBondDiscountCurve WithExtrapolationOf(FittedBondDiscountCurve other) =>
        other.AllowsExtrapolation == AllowsExtrapolation ? this : (FittedBondDiscountCurve)WithExtrapolation(other.AllowsExtrapolation);
}
