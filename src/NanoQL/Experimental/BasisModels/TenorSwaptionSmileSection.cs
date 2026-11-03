// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/basismodels/tenorswaptionvts.hpp ql/experimental/basismodels/tenorswaptionvts.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

// QuantLib's protected TenorSwaptionVTS::TenorSwaptionSmileSection: the target swaption's normal smile at an option
// time and swap length, from the base smile through an affine terminal swap rate model.
[Experimental("NQL0001")]
internal sealed class TenorSwaptionSmileSection : SmileSection
{
    private readonly SmileSection baseSmileSection;
    private readonly double swapRateBase;
    private readonly double swapRateTarg;
    private readonly double swapRateFinl;
    private readonly double lambda;
    private readonly double annuityScaling;

    internal TenorSwaptionSmileSection(TenorSwaptionVTS volTS, double optionTime, double swapLength)
        : base(optionTime, volTS.BaseVTS.DayCounter, VolatilityType.Normal, 0.0)
    {
        EvaluationContext context = volTS.Context;
        baseSmileSection = volTS.BaseVTS.SmileSection(optionTime, swapLength, true);

        // first we need the swap start and end date
        double oneDayAsYear = volTS.DayCounter.YearFraction(volTS.ReferenceDate, volTS.ReferenceDate + 1);
        Date exerciseDate = volTS.ReferenceDate + (int)new ClosestRounding(0).Evaluate(optionTime / oneDayAsYear);
        Calendar baseCalendar = volTS.BaseIndex.FixingCalendar;
        Date effectiveDate = baseCalendar.Advance(exerciseDate, volTS.BaseIndex.FixingDays, TimeUnit.Days);

        // the length in whole months, rounded, where QuantLib's truncates it to whole years first
        int months = (int)Math.Round(swapLength * 12.0, MidpointRounding.AwayFromZero);
        Date maturityDate = baseCalendar.Advance(
            effectiveDate, new Period(months, TimeUnit.Months), BusinessDayConvention.Unadjusted, false);

        // now we can set up the schedules
        Schedule baseFixedSchedule = ScheduleOf(effectiveDate, maturityDate, volTS.BaseFixedFreq, baseCalendar, context);
        Schedule finlFixedSchedule = ScheduleOf(
            effectiveDate, maturityDate, volTS.TargFixedFreq, volTS.TargIndex.FixingCalendar, context);
        Schedule baseFloatSchedule = ScheduleOf(effectiveDate, maturityDate, volTS.BaseIndex.Tenor, baseCalendar, context);
        Schedule targFloatSchedule = ScheduleOf(effectiveDate, maturityDate, volTS.TargIndex.Tenor, baseCalendar, context);

        // and swaps
        var baseSwap = new VanillaSwap(
            SwapType.Payer, 1.0, baseFixedSchedule, 1.0, volTS.BaseFixedDC, baseFloatSchedule, volTS.BaseIndex, 0.0,
            volTS.BaseIndex.DayCounter, context);
        var targSwap = new VanillaSwap(
            SwapType.Payer, 1.0, baseFixedSchedule, 1.0, volTS.BaseFixedDC, targFloatSchedule, volTS.TargIndex, 0.0,
            volTS.TargIndex.DayCounter, context);
        var finlSwap = new VanillaSwap(
            SwapType.Payer, 1.0, finlFixedSchedule, 1.0, volTS.TargFixedDC, targFloatSchedule, volTS.TargIndex, 0.0,
            volTS.TargIndex.DayCounter, context);

        // swap rates
        FixedVsFloatingSwapResults baseResults = DiscountingSwapEngine.Calculate(baseSwap, context, volTS.DiscountCurve);
        FixedVsFloatingSwapResults targResults = DiscountingSwapEngine.Calculate(targSwap, context, volTS.DiscountCurve);
        FixedVsFloatingSwapResults finlResults = DiscountingSwapEngine.Calculate(finlSwap, context, volTS.DiscountCurve);
        swapRateBase = Available(baseResults.FairRate);
        swapRateTarg = Available(targResults.FairRate);
        swapRateFinl = Available(finlResults.FairRate);
        var cfs = new SwaptionCashFlows(
            new Swaption(baseSwap, new EuropeanExercise(exerciseDate)), volTS.DiscountCurve, context);
        var cf2 = new SwaptionCashFlows(
            new Swaption(targSwap, new EuropeanExercise(exerciseDate)), volTS.DiscountCurve, context);

        // the flows QuantLib reads the last of, refused when empty
        if (cfs.FixedTimes.IsEmpty || cfs.FloatTimes.IsEmpty || cf2.FloatTimes.IsEmpty)
        {
            throw new NanoQLException(
                "the swap has no coupon left on or after the discount curve's reference date: no flows to map");
        }

        // calculate affine TSR model u and v
        // Sum tau_j   (fixed leg)
        double sumTauj = 0.0;
        foreach (double k in cfs.AnnuityWeights)
        {
            sumTauj += k;
        }

        // Sum tau_j (T_M - T_j)   (fixed leg)
        double sumTaujDeltaT = 0.0;
        for (int k = 0; k < cfs.AnnuityWeights.Length; ++k)
        {
            sumTaujDeltaT += cfs.AnnuityWeights[k] * (cfs.FixedTimes[^1] - cfs.FixedTimes[k]);
        }

        // Sum w_i   (float leg)
        double sumWi = 0.0;
        foreach (double k in cfs.FloatWeights)
        {
            sumWi += k;
        }

        // Sum w_i (T_N - T_i)    (float leg)
        double sumWiDeltaT = 0.0;
        for (int k = 0; k < cfs.FloatWeights.Length; ++k)
        {
            sumWiDeltaT += cfs.FloatWeights[k] * (cfs.FloatTimes[^1] - cfs.FloatTimes[k]);
        }

        // assemble u, v and a(T_p)
        double den = (sumTaujDeltaT * sumWi) - (sumWiDeltaT * sumTauj);
        double u = -sumTauj / den;
        double v = sumTaujDeltaT / den;

        // a(T_p) = u (T_p - T_N) + v
        double tN = cfs.FixedTimes[^1];
        double sumBase = 0.0;
        double sumTarg = 0.0;

        // we skip the first and last weight as they represent the notional flows
        for (int k = 1; k < cfs.FloatWeights.Length - 1; ++k)
        {
            sumBase += cfs.FloatWeights[k] * ((u * (tN - cfs.FloatTimes[k])) + v);
        }

        for (int k = 1; k < cf2.FloatWeights.Length - 1; ++k)
        {
            sumTarg += cf2.FloatWeights[k] * ((u * (tN - cf2.FloatTimes[k])) + v);
        }

        lambda = sumTarg - sumBase;

        // Annuity scaling
        annuityScaling = Available(targResults.FixedLegBPS) / Available(finlResults.FixedLegBPS);
    }

    public override double MinStrike() => baseSmileSection.MinStrike() + swapRateTarg - swapRateBase;

    public override double MaxStrike() => baseSmileSection.MaxStrike() + swapRateTarg - swapRateBase;

    public override double? AtmLevel() => swapRateFinl;

    protected override double VolatilityImpl(double strike)
    {
        double strikeBase = (strike - (swapRateTarg - ((1.0 + lambda) * swapRateBase))) / (1.0 + lambda) / annuityScaling;
        double volBase = baseSmileSection.Volatility(strikeBase, VolatilityType.Normal, 0.0);
        double volTarg = annuityScaling * (1.0 + lambda) * volBase;
        return volTarg;
    }

    // QuantLib's result accessors, which throw on a missing result.
    private static double Available(double? x) => x ?? throw new NanoQLException("result not available");

    private static Schedule ScheduleOf(Date start, Date end, Period tenor, Calendar calendar, EvaluationContext context) =>
        new(
            start,
            end,
            tenor,
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);
}
