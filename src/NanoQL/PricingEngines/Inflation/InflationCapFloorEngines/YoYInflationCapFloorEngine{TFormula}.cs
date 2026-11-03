// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/inflation/inflationcapfloorengines.hpp ql/pricingengines/inflation/inflationcapfloorengines.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The body the year-on-year inflation cap/floor engines share: QuantLib's <c>YoYInflationCapFloorEngine</c>, its
/// optionlet formula the type argument's.
/// </summary>
/// <typeparam name="TFormula">The optionlet formula.</typeparam>
/// <remarks>
/// <para>
/// Each optionlet paid after the nominal curve's reference date is priced on its forward, at a standard deviation from
/// the volatility's total variance, zero when the optionlet fixes at or before the volatility's base date, with no
/// convexity adjustment. The time from the base date is read with no lag, as QuantLib's master reads it after 1.44
/// (#2877), where 1.44's default lag leaves an optionlet fixing within one lag of the base date at its intrinsic value
/// (<c>Divergences/InflationUpstreamFixesTests.cs</c>). QuantLib's public base class has no instance form under the
/// static engines, so it is an internal body generic in its formula, as the Black-style swaption engine is.
/// </para>
/// <para>
/// Each forward is the coupon's own fixing, <see cref="CPI.LaggedYoYRate"/> on the engine's index with the coupon's
/// accrual end, lag and interpolation: the published fixing once it is out, the forecast before. QuantLib's engine
/// reads the index's curve at the coupon's fixing date, <c>yoyTS-&gt;yoyRate(fixingDate)</c>, which misses a fixing
/// already published, refuses a fixing date before the curve's base date, and reads the period's start where a linear
/// coupon interpolates; its own coupon pricer reads the coupon's fixing. The two agree on a flat, unfixed optionlet
/// with no fixing days and a regular reference period. A documented divergence.
/// </para>
/// </remarks>
internal static class YoYInflationCapFloorEngine<TFormula>
    where TFormula : struct, IYoYInflationOptionletFormula
{
    /// <summary>Prices a year-on-year cap, floor or collar.</summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="index">The year-on-year index the forwards are observed on.</param>
    /// <param name="volatility">The optionlet volatility.</param>
    /// <param name="nominalTermStructure">The nominal curve the optionlets are discounted on.</param>
    /// <returns>The results: the expired results when the instrument has expired.</returns>
    internal static InstrumentResults Calculate(
        YoYInflationCapFloor capFloor,
        EvaluationContext context,
        YoYInflationIndex index,
        YoYOptionletVolatilitySurface volatility,
        YieldTermStructure nominalTermStructure)
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(volatility);
        ArgumentNullException.ThrowIfNull(nominalTermStructure);
        if (capFloor.IsExpired(context))
        {
            return capFloor.SetupExpired();
        }

        YoYInflationCapFloorArguments arguments = capFloor.SetupArguments(context);
        arguments.Validate();
        InstrumentResults results = Body(arguments, context, index, volatility, nominalTermStructure);
        return capFloor.FetchResults(results);
    }

    /// <summary>The engine's body: QuantLib's <c>calculate()</c>, on the arguments it reads.</summary>
    /// <param name="arguments">The instrument's arguments.</param>
    /// <param name="context">The evaluation context, whose fixings and forecasts the forwards read.</param>
    /// <param name="index">The year-on-year index.</param>
    /// <param name="volatility">The optionlet volatility.</param>
    /// <param name="nominalTermStructure">The nominal curve.</param>
    /// <returns>The results.</returns>
    internal static InstrumentResults Body(
        YoYInflationCapFloorArguments arguments,
        EvaluationContext context,
        YoYInflationIndex index,
        YoYOptionletVolatilitySurface volatility,
        YieldTermStructure nominalTermStructure)
    {
        double value = 0.0;
        int optionlets = arguments.StartDates.Length;
        var values = new double[optionlets];
        var stdDevs = new double[optionlets];
        var forwards = new double[optionlets];
        YoYInflationCapFloorType type = arguments.Type;

        Date settlement = nominalTermStructure.ReferenceDate;

        for (int i = 0; i < optionlets; ++i)
        {
            Date paymentDate = arguments.PayDates[i];
            if (paymentDate > settlement)
            {
                // discard expired caplets
                double d = arguments.Nominals[i]
                    * arguments.Gearings[i]
                    * nominalTermStructure.Discount(paymentDate)
                    * arguments.AccrualTimes[i];

                // We explicitly have the index and assume that
                // the fixing is natural, i.e. no convexity adjustment.
                // If that was required then we would also need
                // nominal vols in the pricing engine, i.e. a different engine.
                // Here the forward is the coupon's own fixing, where QuantLib's reads the curve at the fixing date.
                forwards[i] = CPI.LaggedYoYRate(
                    index,
                    arguments.AccrualEndDates[i],
                    arguments.ObservationLags[i],
                    arguments.Interpolations[i],
                    context);
                double forward = forwards[i];

                Date fixingDate = arguments.FixingDates[i];
                double sqrtTime = 0.0;
                if (fixingDate > volatility.BaseDate())
                {
                    // QuantLib's fix after v1.44 (#2877): the time read with no lag, as the variance below is, so a
                    // fixing within one lag of the base date has its time value
                    sqrtTime = Math.Sqrt(volatility.TimeFromBase(fixingDate, new Period(0, TimeUnit.Days)));
                }

                if (type is YoYInflationCapFloorType.Cap or YoYInflationCapFloorType.Collar)
                {
                    double strike = arguments.CapRates[i]!.Value;
                    if (sqrtTime > 0.0)
                    {
                        stdDevs[i] = Math.Sqrt(
                            volatility.TotalVariance(fixingDate, strike, new Period(0, TimeUnit.Days)));
                    }

                    // sttDev=0 for already-fixed dates so everything on forward
                    values[i] = TFormula.OptionletImpl(OptionType.Call, strike, forward, stdDevs[i], d);
                }

                if (type is YoYInflationCapFloorType.Floor or YoYInflationCapFloorType.Collar)
                {
                    double strike = arguments.FloorRates[i]!.Value;
                    if (sqrtTime > 0.0)
                    {
                        stdDevs[i] = Math.Sqrt(
                            volatility.TotalVariance(fixingDate, strike, new Period(0, TimeUnit.Days)));
                    }

                    double floorlet = TFormula.OptionletImpl(OptionType.Put, strike, forward, stdDevs[i], d);
                    if (type == YoYInflationCapFloorType.Floor)
                    {
                        values[i] = floorlet;
                    }
                    else
                    {
                        // a collar is long a cap and short a floor
                        values[i] -= floorlet;
                    }
                }

                value += values[i];
            }
        }

        var additionalResults = ImmutableSortedDictionary.CreateBuilder<string, object>();
        additionalResults["optionletsPrice"] = ImmutableArray.Create(values);
        additionalResults["optionletsAtmForward"] = ImmutableArray.Create(forwards);
        if (type != YoYInflationCapFloorType.Collar)
        {
            additionalResults["optionletsStdDev"] = ImmutableArray.Create(stdDevs);
        }

        return new InstrumentResults { NPV = value, AdditionalResults = additionalResults.ToImmutable() };
    }
}
