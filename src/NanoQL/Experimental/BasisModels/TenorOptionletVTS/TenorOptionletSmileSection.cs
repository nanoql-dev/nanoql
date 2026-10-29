// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/basismodels/tenoroptionletvts.hpp ql/experimental/basismodels/tenoroptionletvts.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

// QuantLib's protected TenorOptionletVTS::TenorOptionletSmileSection: the target optionlet's normal smile at an option
// time, from the base smiles at the base periods' fixing dates.
[Experimental("NQL0001")]
internal sealed class TenorOptionletSmileSection : SmileSection
{
    private readonly TenorOptionletVTSCorrelationStructure correlation;
    private readonly ImmutableArray<SmileSection> baseSmileSection;
    private readonly ImmutableArray<double> startTimeBase; // for correlation parametrisation
    private readonly ImmutableArray<double> fraRateBase;
    private readonly double fraRateTarg;
    private readonly ImmutableArray<double> v;

    internal TenorOptionletSmileSection(TenorOptionletVTS volTS, double optionTime)
        : base(optionTime, volTS.BaseVTS.DayCounter, VolatilityType.Normal, 0.0)
    {
        correlation = volTS.Correlation;
        EvaluationContext context = volTS.Context;

        // we assume that long (target) tenor is a multiple of short (base) tenor
        // first we need the long tenor start and end date
        double oneDayAsYear = volTS.DayCounter.YearFraction(volTS.ReferenceDate, volTS.ReferenceDate + 1);
        Date exerciseDate = volTS.ReferenceDate + (int)new ClosestRounding(0).Evaluate(optionTime / oneDayAsYear);
        Calendar fixingCalendar = volTS.BaseIndex.FixingCalendar;
        Date effectiveDate = fixingCalendar.Advance(exerciseDate, volTS.BaseIndex.FixingDays, TimeUnit.Days);
        Date maturityDate = fixingCalendar.Advance(
            effectiveDate, volTS.TargIndex.Tenor, BusinessDayConvention.Unadjusted, false);

        // now we can set up the short tenor schedule
        var baseFloatSchedule = new Schedule(
            effectiveDate,
            maturityDate,
            volTS.BaseIndex.Tenor,
            fixingCalendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);

        // set up scalar attributes
        fraRateTarg = volTS.TargIndex.Fixing(exerciseDate, context);
        double yfTarg = volTS.TargIndex.DayCounter.YearFraction(effectiveDate, maturityDate);
        var smiles = ImmutableArray.CreateBuilder<SmileSection>();
        var starts = ImmutableArray.CreateBuilder<double>();
        var fras = ImmutableArray.CreateBuilder<double>();
        var weights = ImmutableArray.CreateBuilder<double>();
        for (int k = 0; k < baseFloatSchedule.Dates.Length - 1; ++k)
        {
            Date startDate = baseFloatSchedule.Dates[k];
            Date fixingDate = fixingCalendar.Advance(startDate, -1 * volTS.BaseIndex.FixingDays, TimeUnit.Days);
            double yearFrac = volTS.BaseIndex.DayCounter.YearFraction(
                baseFloatSchedule.Dates[k], baseFloatSchedule.Dates[k + 1]);

            // set up vector attributes
            smiles.Add(volTS.BaseVTS.SmileSection(fixingDate, true));
            starts.Add(volTS.DayCounter.YearFraction(volTS.ReferenceDate, startDate));
            fras.Add(volTS.BaseIndex.Fixing(fixingDate, context));
            weights.Add(yearFrac / yfTarg * (1.0 + (yfTarg * fraRateTarg)) / (1.0 + (yearFrac * fras[k])));
        }

        baseSmileSection = smiles.ToImmutable();
        startTimeBase = starts.ToImmutable();
        fraRateBase = fras.ToImmutable();
        v = weights.ToImmutable();
    }

    public override double MinStrike() => baseSmileSection[0].MinStrike() + fraRateTarg - fraRateBase[0];

    public override double MaxStrike() => baseSmileSection[0].MaxStrike() + fraRateTarg - fraRateBase[0];

    public override double? AtmLevel() => fraRateTarg;

    protected override double VolatilityImpl(double strike)
    {
        double sumV = 0.0;
        foreach (double k in v)
        {
            sumV += k;
        }

        double[] volBase = new double[v.Length];
        for (int k = 0; k < fraRateBase.Length; ++k)
        {
            double strikeK = (strike - (fraRateTarg - (sumV * fraRateBase[k]))) / sumV;
            volBase[k] = baseSmileSection[k].Volatility(strikeK, VolatilityType.Normal, 0.0);
        }

        double var = 0.0;
        for (int i = 0; i < volBase.Length; ++i)
        {
            var += v[i] * v[i] * volBase[i] * volBase[i];
            for (int j = i + 1; j < volBase.Length; ++j)
            {
                double corr = correlation.Evaluate(startTimeBase[i], startTimeBase[j]);
                var += 2.0 * corr * v[i] * v[j] * volBase[i] * volBase[j];
            }
        }

        double vol = Math.Sqrt(var);
        return vol;
    }
}
