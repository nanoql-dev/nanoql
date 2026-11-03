// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/riskyassetswap.hpp ql/experimental/credit/riskyassetswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A risky-asset-swap helper for a default-probability curve's bootstrap: a payer swap of nominal 100 from the
/// settlement date, whose fair spread on the curve under construction is the helper's quote.
/// </summary>
/// <remarks>
/// On <see cref="CdsHelper"/>'s form: the helper's dates follow the evaluation date, its settlement the settlement
/// days after it on the calendar and its latest date the maturity adjusted to the fixed convention; a bound copy
/// (<see cref="DefaultProbabilityHelperExtensions.SetTermStructure{T}"/>) builds its swap on the bound curve. The
/// integration step is stored and unread, as QuantLib's is.
/// </remarks>
[Experimental("NQL0001")]
public sealed class AssetSwapHelper : RelativeDateBootstrapHelper<DefaultProbabilityTermStructure>
{
    private readonly Period tenor;
    private readonly int settlementDays;
    private readonly Calendar calendar;
    private readonly BusinessDayConvention fixedConvention;
    private readonly Period fixedPeriod;
    private readonly DayCounter fixedDayCount;
    private readonly BusinessDayConvention floatConvention;
    private readonly Period floatPeriod;
    private readonly DayCounter floatDayCount;
    private readonly double recoveryRate;
    private readonly YieldTermStructure yieldTS;

    // Written by the dates' body, on a helper under construction or a fresh copy (InitializeDates), never after.
    private Schedule fixedSchedule = null!;
    private Schedule floatSchedule = null!;

    // Written on a bound copy (WithTermStructure), never after.
    private RiskyAssetSwap? asw;

    /// <summary>Initializes a new instance of the <see cref="AssetSwapHelper"/> class.</summary>
    /// <param name="spread">The quoted asset-swap spread.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="settlementDays">The business days from the evaluation date to the settlement.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="fixedPeriod">The fixed leg's period.</param>
    /// <param name="fixedConvention">The fixed leg's convention.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter.</param>
    /// <param name="floatPeriod">The floating leg's period.</param>
    /// <param name="floatConvention">The floating leg's convention.</param>
    /// <param name="floatDayCount">The floating leg's day counter.</param>
    /// <param name="recoveryRate">The bond's recovery rate.</param>
    /// <param name="yieldTS">The discount curve.</param>
    /// <param name="context">The evaluation context whose evaluation date the helper's dates follow.</param>
    /// <param name="integrationStepSize">The integration step, unread, as QuantLib's; <see langword="null"/> for
    /// QuantLib's <c>Period()</c>.</param>
    /// <exception cref="ArgumentNullException">An argument but <paramref name="integrationStepSize"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A convention isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">A schedule can't be generated.</exception>
    public AssetSwapHelper(
        double spread,
        Period tenor,
        int settlementDays,
        Calendar calendar,
        Period fixedPeriod,
        BusinessDayConvention fixedConvention,
        DayCounter fixedDayCount,
        Period floatPeriod,
        BusinessDayConvention floatConvention,
        DayCounter floatDayCount,
        double recoveryRate,
        YieldTermStructure yieldTS,
        EvaluationContext context,
        Period? integrationStepSize = null)
        : base(spread, context ?? throw new ArgumentNullException(nameof(context)))
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(fixedDayCount);
        ArgumentNullException.ThrowIfNull(floatDayCount);
        ArgumentNullException.ThrowIfNull(yieldTS);
        EnumArguments.Check(fixedConvention, nameof(fixedConvention));
        EnumArguments.Check(floatConvention, nameof(floatConvention));
        this.tenor = tenor;
        this.settlementDays = settlementDays;
        this.calendar = calendar;
        this.fixedConvention = fixedConvention;
        this.fixedPeriod = fixedPeriod;
        this.fixedDayCount = fixedDayCount;
        this.floatConvention = floatConvention;
        this.floatPeriod = floatPeriod;
        this.floatDayCount = floatDayCount;
        this.recoveryRate = recoveryRate;
        this.yieldTS = yieldTS;
        IntegrationStepSize = integrationStepSize;
        Dates(context);
    }

    /// <summary>Gets the integration step, unread, as QuantLib's; <see langword="null"/> for QuantLib's
    /// <c>Period()</c>.</summary>
    public Period? IntegrationStepSize { get; }

    /// <inheritdoc/>
    /// <remarks>The swap's fair spread on the bound curve.</remarks>
    /// <exception cref="NanoQLException">The helper isn't bound to a curve ("default term structure not set"), or the
    /// swap has expired, which has no fair spread.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (asw is null)
        {
            throw new NanoQLException("default term structure not set");
        }

        return asw.Calculate(context).FairSpread ?? throw new NanoQLException("fair spread not available");
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<DefaultProbabilityTermStructure> WithTermStructure(
        DefaultProbabilityTermStructure t)
    {
        var copy = (AssetSwapHelper)base.WithTermStructure(t);
        copy.asw = new RiskyAssetSwap(
            true,
            100.0,
            copy.fixedSchedule,
            copy.floatSchedule,
            fixedDayCount,
            floatDayCount,
            0.01,
            recoveryRate,
            yieldTS,
            t);
        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Dates(context);
    }

    // QuantLib's initializeDates(), its swap built on the bound copy.
    private void Dates(EvaluationContext context)
    {
        Date evaluationDate = EvaluationDate!.Value;
        earliestDate = calendar.Advance(evaluationDate, settlementDays, TimeUnit.Days);
        Date maturity = earliestDate.Value + tenor;
        latestDate = calendar.Adjust(maturity, fixedConvention);
        fixedSchedule = new Schedule(
            earliestDate,
            maturity,
            fixedPeriod,
            calendar,
            fixedConvention,
            fixedConvention,
            DateGenerationRule.Forward,
            false,
            context);
        floatSchedule = new Schedule(
            earliestDate,
            maturity,
            floatPeriod,
            calendar,
            floatConvention,
            floatConvention,
            DateGenerationRule.Forward,
            false,
            context);
    }
}
