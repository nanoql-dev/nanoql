// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Michael von den Driesch
// SPDX-FileCopyrightText: Copyright (C) 2019 Wojciech Ślusarski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/blackcapfloorengine.hpp ql/pricingengines/capfloor/blackcapfloorengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>The Black-formula cap/floor engine.</summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) and returns the instrument's results,
/// with QuantLib's additional results, read by <see cref="InstrumentResults.Result{T}"/>: <c>vega</c> (a
/// <see cref="double"/>), and per optionlet <c>optionletsPrice</c>, <c>optionletsVega</c>, <c>optionletsDelta</c>,
/// <c>optionletsDiscountFactor</c> and <c>optionletsStdDev</c> (each an <see cref="ImmutableArray{T}"/> of
/// <see cref="double"/>) and <c>optionletsAtmForward</c> (of <see cref="Nullable{T}"/> <see cref="double"/>, null for
/// a coupon paid before the evaluation date, where QuantLib's is <c>Null&lt;Rate&gt;()</c>). A collar's results hold no
/// <c>optionletsStdDev</c>, as QuantLib's don't.
/// </para>
/// <para>
/// The volatility's reference date is today and the discount curve's the settlement date: a caplet paid on or before
/// the settlement date is skipped, and one fixed on or before today is priced at its intrinsic value, its vega and
/// delta 0. A caplet the engine prices whose coupon is paid before the evaluation date has no forward, where QuantLib
/// prices on its <c>Null&lt;Rate&gt;()</c> sentinel, 3.4028234663852886e+38 (a discount curve dated before the
/// evaluation date reaches it): it is refused, a documented divergence.
/// </para>
/// <para>
/// QuantLib's constructors on a volatility value and on a quote coincide once a quote is a <see cref="double"/>: the
/// overload on a <see cref="double"/> builds, at each call, the constant volatility QuantLib's builds, shifted
/// lognormal on no calendar, with its reference date the context's evaluation date. Its day counter defaults to
/// QuantLib's, <see cref="Actual365Fixed"/>, for <see langword="null"/>.
/// </para>
/// </remarks>
public static class BlackCapFloorEngine
{
    /// <summary>Prices a cap, floor or collar on a constant volatility.</summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="vol">The shifted lognormal volatility.</param>
    /// <param name="dc">The volatility's day counter; <see cref="Actual365Fixed"/>, QuantLib's default, when
    /// <see langword="null"/>.</param>
    /// <param name="displacement">The displacement the Black formula takes.</param>
    /// <returns>The results: the expired results when the instrument has expired.</returns>
    /// <exception cref="ArgumentNullException">The instrument, the context or the discount curve is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A Black formula refuses its inputs.</exception>
    /// <exception cref="NanoQLException">
    /// A cash flow isn't a floating-rate coupon, a forward can't be computed, or a priced caplet has no forward.
    /// </exception>
    public static InstrumentResults Calculate(
        CapFloor capFloor,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        double vol,
        DayCounter? dc = null,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        OptionletVolatilityStructure volatility = ConstantOptionletVolatility.Create(
            0, new NullCalendar(), BusinessDayConvention.Following, vol, dc ?? new Actual365Fixed(), context);
        return Calculate(capFloor, context, args => Body(args, discountCurve, volatility, displacement));
    }

    /// <summary>Prices a cap, floor or collar on an optionlet volatility structure.</summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="vol">The volatility structure, shifted lognormal.</param>
    /// <param name="displacement">The displacement, which must be the structure's; the structure's when
    /// <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>.</param>
    /// <returns>The results: the expired results when the instrument has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The structure's volatilities aren't shifted lognormal, the displacement isn't the structure's, or a Black
    /// formula refuses its inputs.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// A cash flow isn't a floating-rate coupon, a forward can't be computed, a priced caplet has no forward, or the
    /// structure refuses a fixing date or a strike.
    /// </exception>
    public static InstrumentResults Calculate(
        CapFloor capFloor,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        OptionletVolatilityStructure vol,
        double? displacement = null)
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentNullException.ThrowIfNull(vol);
        if (!(vol.VolatilityType == VolatilityType.ShiftedLognormal))
        {
            throw new ArgumentException(
                "BlackCapFloorEngine should only be used for vol surfaces stripped with shifted log normal model. "
                + $"Options were stripped with model {vol.VolatilityType}",
                nameof(vol));
        }

        double usedDisplacement;
        if (displacement is double given)
        {
            usedDisplacement = given;
            if (!(vol.Displacement == usedDisplacement))
            {
                throw new ArgumentException(
                    "Displacement used for stripping and provided for pricing differ. Model displacement was : "
                    + vol.Displacement.ToString(CultureInfo.InvariantCulture),
                    nameof(displacement));
            }
        }
        else
        {
            usedDisplacement = vol.Displacement;
        }

        return Calculate(capFloor, context, args => Body(args, discountCurve, vol, usedDisplacement));
    }

    /// <summary>The engine's body: QuantLib's <c>calculate()</c>, on the arguments it reads.</summary>
    /// <param name="arguments">The instrument's arguments.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="vol">The volatility structure.</param>
    /// <param name="displacement">The displacement the Black formula takes.</param>
    /// <returns>The results.</returns>
    internal static InstrumentResults Body(
        CapFloorArguments arguments, YieldTermStructure discountCurve, OptionletVolatilityStructure vol, double displacement)
    {
        double value = 0.0;
        double vega = 0.0;
        int optionlets = arguments.StartDates.Length;
        var values = new double[optionlets];
        var deltas = new double[optionlets];
        var vegas = new double[optionlets];
        var stdDevs = new double[optionlets];
        var discountFactors = new double[optionlets];
        CapFloorType type = arguments.Type;
        Date today = vol.ReferenceDate;
        Date settlement = discountCurve.ReferenceDate;

        for (int i = 0; i < optionlets; ++i)
        {
            Date paymentDate = arguments.EndDates[i];

            // handling of settlementDate, npvDate and includeSettlementFlows
            // should be implemented.
            // For the time being just discard expired caplets
            if (paymentDate > settlement)
            {
                double d = discountCurve.Discount(paymentDate);
                discountFactors[i] = d;
                double accrualFactor = arguments.Nominals[i] * arguments.Gearings[i] * arguments.AccrualTimes[i];
                double discountedAccrual = d * accrualFactor;

                // QuantLib prices a coupon paid before the evaluation date on its Null<Rate>() forward.
                double forward = arguments.Forwards[i]
                    ?? throw new NanoQLException("no forward for a caplet paid before the evaluation date");

                Date fixingDate = arguments.FixingDates[i];
                double sqrtTime = 0.0;
                if (fixingDate > today)
                {
                    sqrtTime = Math.Sqrt(vol.TimeFromReference(fixingDate));
                }

                if (type is CapFloorType.Cap or CapFloorType.Collar)
                {
                    double strike = arguments.CapRates[i]!.Value;
                    if (sqrtTime > 0.0)
                    {
                        stdDevs[i] = Math.Sqrt(vol.BlackVariance(fixingDate, strike));
                        vegas[i] = BlackFormulaStdDevDerivative(strike, forward, stdDevs[i], discountedAccrual, displacement)
                            * sqrtTime;
                        deltas[i] = BlackFormulaAssetItmProbability(OptionType.Call, strike, forward, stdDevs[i], displacement);
                    }

                    // include caplets with past fixing date
                    values[i] = BlackFormula(OptionType.Call, strike, forward, stdDevs[i], discountedAccrual, displacement);
                }

                if (type is CapFloorType.Floor or CapFloorType.Collar)
                {
                    double strike = arguments.FloorRates[i]!.Value;
                    double floorletVega = 0.0;
                    double floorletDelta = 0.0;
                    if (sqrtTime > 0.0)
                    {
                        stdDevs[i] = Math.Sqrt(vol.BlackVariance(fixingDate, strike));
                        floorletVega = BlackFormulaStdDevDerivative(strike, forward, stdDevs[i], discountedAccrual, displacement)
                            * sqrtTime;
                        floorletDelta = (int)OptionType.Put
                            * BlackFormulaAssetItmProbability(OptionType.Put, strike, forward, stdDevs[i], displacement);
                    }

                    double floorlet = BlackFormula(OptionType.Put, strike, forward, stdDevs[i], discountedAccrual, displacement);
                    if (type == CapFloorType.Floor)
                    {
                        values[i] = floorlet;
                        vegas[i] = floorletVega;
                        deltas[i] = floorletDelta;
                    }
                    else
                    {
                        // a collar is long a cap and short a floor
                        values[i] -= floorlet;
                        vegas[i] -= floorletVega;
                        deltas[i] -= floorletDelta;
                    }
                }

                value += values[i];
                vega += vegas[i];
            }
        }

        var additionalResults = ImmutableSortedDictionary.CreateBuilder<string, object>();
        additionalResults["vega"] = vega;

        additionalResults["optionletsPrice"] = ImmutableArray.Create(values);
        additionalResults["optionletsVega"] = ImmutableArray.Create(vegas);
        additionalResults["optionletsDelta"] = ImmutableArray.Create(deltas);
        additionalResults["optionletsDiscountFactor"] = ImmutableArray.Create(discountFactors);
        additionalResults["optionletsAtmForward"] = arguments.Forwards;
        if (type != CapFloorType.Collar)
        {
            additionalResults["optionletsStdDev"] = ImmutableArray.Create(stdDevs);
        }

        return new InstrumentResults { NPV = value, AdditionalResults = additionalResults.ToImmutable() };
    }

    // QuantLib's Instrument::calculate around the body: the expired results, or the arguments, their checks, the body
    // and the results.
    private static InstrumentResults Calculate(
        CapFloor capFloor, EvaluationContext context, Func<CapFloorArguments, InstrumentResults> body)
    {
        if (capFloor.IsExpired(context))
        {
            return capFloor.SetupExpired();
        }

        CapFloorArguments arguments = capFloor.SetupArguments(context);
        arguments.Validate();
        InstrumentResults results = body(arguments);
        return capFloor.FetchResults(results);
    }
}
