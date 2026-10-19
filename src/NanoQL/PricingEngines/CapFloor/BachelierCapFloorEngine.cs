// SPDX-FileCopyrightText: Copyright (C) 2014 Michael von den Driesch
// SPDX-FileCopyrightText: Copyright (C) 2014, 2015 Michael von den Driesch
// SPDX-FileCopyrightText: Copyright (C) 2019 Wojciech Ślusarski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/bacheliercapfloorengine.hpp ql/pricingengines/capfloor/bacheliercapfloorengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>The Bachelier-Black-formula cap/floor engine: normal volatilities.</summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) and returns the instrument's results,
/// with <see cref="BlackCapFloorEngine"/>'s additional results under the same keys, from the Bachelier formulas, which
/// take no displacement and accept negative strikes and forwards. A collar's results hold no <c>optionletsStdDev</c>,
/// as QuantLib's don't, and a caplet the engine prices whose coupon is paid before the evaluation date has no
/// forward, refused where QuantLib prices on its <c>Null&lt;Rate&gt;()</c> sentinel, a documented divergence.
/// </para>
/// <para>
/// QuantLib's constructors on a volatility value and on a quote coincide once a quote is a <see cref="double"/>: the
/// overload on a <see cref="double"/> builds, at each call, the constant volatility QuantLib's builds, on no calendar,
/// with its reference date the context's evaluation date and QuantLib's default type, shifted lognormal, which the
/// engine reads as a normal volatility. Its day counter defaults to QuantLib's, <see cref="Actual365Fixed"/>, for
/// <see langword="null"/>.
/// </para>
/// </remarks>
public static class BachelierCapFloorEngine
{
    /// <summary>Prices a cap, floor or collar on a constant normal volatility.</summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="vol">The normal volatility.</param>
    /// <param name="dc">The volatility's day counter; <see cref="Actual365Fixed"/>, QuantLib's default, when
    /// <see langword="null"/>.</param>
    /// <returns>The results: the expired results when the instrument has expired.</returns>
    /// <exception cref="ArgumentNullException">The instrument, the context or the discount curve is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A Bachelier formula refuses its inputs.</exception>
    /// <exception cref="NanoQLException">
    /// A cash flow isn't a floating-rate coupon, a forward can't be computed, or a priced caplet has no forward.
    /// </exception>
    public static InstrumentResults Calculate(
        CapFloor capFloor,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        double vol,
        DayCounter? dc = null)
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        OptionletVolatilityStructure volatility = ConstantOptionletVolatility.Create(
            0, new NullCalendar(), BusinessDayConvention.Following, vol, dc ?? new Actual365Fixed(), context);
        return Calculate(capFloor, context, args => Body(args, discountCurve, volatility));
    }

    /// <summary>Prices a cap, floor or collar on an optionlet volatility structure.</summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="vol">The volatility structure, normal.</param>
    /// <returns>The results: the expired results when the instrument has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The structure's volatilities aren't normal, or a Bachelier formula refuses its inputs.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// A cash flow isn't a floating-rate coupon, a forward can't be computed, a priced caplet has no forward, or the
    /// structure refuses a fixing date or a strike.
    /// </exception>
    public static InstrumentResults Calculate(
        CapFloor capFloor,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        OptionletVolatilityStructure vol)
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentNullException.ThrowIfNull(vol);
        if (!(vol.VolatilityType == VolatilityType.Normal))
        {
            throw new ArgumentException(
                "BachelierCapFloorEngine should only be used for vol surfaces stripped with normal model. Options were "
                + $"stripped with model {vol.VolatilityType}",
                nameof(vol));
        }

        return Calculate(capFloor, context, args => Body(args, discountCurve, vol));
    }

    /// <summary>The engine's body: QuantLib's <c>calculate()</c>, on the arguments it reads.</summary>
    /// <param name="arguments">The instrument's arguments.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="vol">The volatility structure, read as normal.</param>
    /// <returns>The results.</returns>
    internal static InstrumentResults Body(
        CapFloorArguments arguments, YieldTermStructure discountCurve, OptionletVolatilityStructure vol)
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
                        vegas[i] = BachelierBlackFormulaStdDevDerivative(strike, forward, stdDevs[i], discountedAccrual)
                            * sqrtTime;
                        deltas[i] = BachelierBlackFormulaAssetItmProbability(OptionType.Call, strike, forward, stdDevs[i]);
                    }

                    // include caplets with past fixing date
                    values[i] = BachelierBlackFormula(OptionType.Call, strike, forward, stdDevs[i], discountedAccrual);
                }

                if (type is CapFloorType.Floor or CapFloorType.Collar)
                {
                    double strike = arguments.FloorRates[i]!.Value;
                    double floorletVega = 0.0;
                    double floorletDelta = 0.0;
                    if (sqrtTime > 0.0)
                    {
                        stdDevs[i] = Math.Sqrt(vol.BlackVariance(fixingDate, strike));
                        floorletVega = BachelierBlackFormulaStdDevDerivative(strike, forward, stdDevs[i], discountedAccrual)
                            * sqrtTime;
                        floorletDelta = (int)OptionType.Put
                            * BachelierBlackFormulaAssetItmProbability(OptionType.Put, strike, forward, stdDevs[i]);
                    }

                    double floorlet = BachelierBlackFormula(OptionType.Put, strike, forward, stdDevs[i], discountedAccrual);
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
