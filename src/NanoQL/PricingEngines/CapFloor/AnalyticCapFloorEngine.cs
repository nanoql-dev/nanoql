// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/analyticcapfloorengine.hpp ql/pricingengines/capfloor/analyticcapfloorengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The analytic cap/floor engine: each optionlet an option on a discount bond of an affine model.</summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) on the model, an argument where
/// QuantLib's engine holds it. An optionlet whose payment is past (or on the reference date, unless the context includes
/// that day's payments) is skipped; one fixed on or before the reference date pays its known amount, discounted on the
/// model; any other is (1 + Kτ) times the model's option on the discount bond from its start to its payment, struck at
/// 1/(1 + Kτ), a put for a caplet and a call for a floorlet.
/// </para>
/// <para>
/// <b>The option's expiry</b>: QuantLib's option expires at the optionlet's start
/// (<c>analyticcapfloorengine.cpp:94-112</c>), where the instrument fixes its rate and QuantLib's Monte Carlo engine
/// reads it at the fixing date, so an optionlet with a fixing lag is priced on the variance to its start. On a
/// <see cref="HullWhite"/> model, an optionlet fixed before its start is the option expiring at its fixing on the bond
/// from its start to its payment, QuantLib's own five-argument bond option, as its Jamshidian engine prices one; one
/// fixed on its start is QuantLib's value, bit for bit. The other models keep QuantLib's expiry at the start, since
/// QuantLib has no such option for them: its affine model's five-argument option ignores the bond's start. A divergence
/// (<c>Divergences/CapletFixingExpiryTests.cs</c>).
/// </para>
/// <para>
/// <b>An in-arrears caplet</b> still to be fixed, fixed after its start, is refused, on every model, since no closed
/// form values it: QuantLib prices it as the caplet in advance, on its start and its accrual period, reading neither
/// its fixing nor its index's period (<c>Divergences/CapletInArrearsTests.cs</c>). The tree and Monte Carlo engines
/// value it at its fixing.
/// </para>
/// <para>
/// The term structure is needed only when the model can't provide one: a model that is an
/// <see cref="ITermStructureConsistentModel"/> reads its own curve's reference date and day counter, as QuantLib's
/// engine does, whatever curve is given.
/// </para>
/// </remarks>
public static class AnalyticCapFloorEngine
{
    /// <summary>Prices a cap, floor or collar on an affine model.</summary>
    /// <param name="capFloor">The cap, floor or collar.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The affine model.</param>
    /// <param name="termStructure">
    /// The curve that gives the reference date and day counter of the times, for a model that has no curve of its own;
    /// <see langword="null"/>, QuantLib's empty handle, by default.
    /// </param>
    /// <returns>The instrument's results: the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The cap, the context or the model is null.</exception>
    /// <exception cref="NanoQLException">
    /// The cap's terms are inconsistent, a cash flow isn't a floating-rate coupon, a forward can't be computed, the
    /// model has no curve and none is given, or a caplet still to be fixed is fixed after its start (in arrears).
    /// </exception>
    public static InstrumentResults Calculate(
        CapFloor capFloor, EvaluationContext context, IAffineModel model, YieldTermStructure? termStructure = null)
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib's "null model", in its calculate, is the argument check here.
        ArgumentNullException.ThrowIfNull(model);
        if (capFloor.IsExpired(context))
        {
            return capFloor.SetupExpired();
        }

        CapFloorArguments arguments = capFloor.SetupArguments(context);
        arguments.Validate();
        InstrumentResults results = Body(arguments, context, model, termStructure);
        return capFloor.FetchResults(results);
    }

    // QuantLib's calculate().
    private static InstrumentResults Body(
        CapFloorArguments arguments, EvaluationContext context, IAffineModel model, YieldTermStructure? termStructure)
    {
        Date referenceDate;
        DayCounter dayCounter;

        if (model is ITermStructureConsistentModel tsmodel)
        {
            referenceDate = tsmodel.TermStructure.ReferenceDate;
            dayCounter = tsmodel.TermStructure.DayCounter;
        }
        else
        {
            YieldTermStructure curve = termStructure ?? throw new NanoQLException("empty Handle cannot be dereferenced");
            referenceDate = curve.ReferenceDate;
            dayCounter = curve.DayCounter;
        }

        double value = 0.0;
        CapFloorType type = arguments.Type;
        int nPeriods = arguments.EndDates.Length;

        bool includeRefDatePayments = context.IncludeReferenceDateEvents;
        if (referenceDate == context.EvaluationDate && context.IncludeTodaysCashFlows is bool includeTodaysPayments)
        {
            includeRefDatePayments = includeTodaysPayments;
        }

        for (int i = 0; i < nPeriods; i++)
        {
            double fixingTime = dayCounter.YearFraction(referenceDate, arguments.FixingDates[i]);
            double paymentTime = dayCounter.YearFraction(referenceDate, arguments.EndDates[i]);

            bool notExpired = includeRefDatePayments ? paymentTime >= 0.0 : paymentTime > 0.0;

            if (notExpired)
            {
                double tenor = arguments.AccrualTimes[i];
                if (fixingTime <= 0.0)
                {
                    double fixing = arguments.Forwards[i] ?? throw new NanoQLException("the fixed optionlet's forward is not available");
                    if (type is CapFloorType.Cap or CapFloorType.Collar)
                    {
                        double discount = model.Discount(paymentTime);
                        double strike = arguments.CapRates[i]!.Value;
                        value += discount * arguments.Nominals[i] * tenor * arguments.Gearings[i] * Max0(fixing - strike);
                    }

                    if (type is CapFloorType.Floor or CapFloorType.Collar)
                    {
                        double discount = model.Discount(paymentTime);
                        double strike = arguments.FloorRates[i]!.Value;
                        double mult = type == CapFloorType.Floor ? 1.0 : -1.0;
                        value += discount * arguments.Nominals[i] * tenor * mult * arguments.Gearings[i] * Max0(strike - fixing);
                    }
                }
                else
                {
                    double maturity = dayCounter.YearFraction(referenceDate, arguments.StartDates[i]);
                    if (fixingTime > maturity)
                    {
                        // No closed form for an in-arrears caplet
                        throw new NanoQLException(
                            $"an in-arrears caplet still to be fixed (on {arguments.FixingDates[i]}) has no closed form: "
                            + "price it on the tree or the Monte Carlo engine");
                    }


                    // On Hull-White, the option at the fixing on the bond from the start, where it is earlier
                    HullWhite? atFixing = model is HullWhite hullWhite && fixingTime < maturity ? hullWhite : null;
                    if (type is CapFloorType.Cap or CapFloorType.Collar)
                    {
                        double temp = 1.0 + (arguments.CapRates[i]!.Value * tenor);
                        double option = atFixing is null
                            ? model.DiscountBondOption(OptionType.Put, 1.0 / temp, maturity, paymentTime)
                            : atFixing.DiscountBondOption(OptionType.Put, 1.0 / temp, fixingTime, maturity, paymentTime);
                        value += arguments.Nominals[i] * arguments.Gearings[i] * temp * option;
                    }

                    if (type is CapFloorType.Floor or CapFloorType.Collar)
                    {
                        double temp = 1.0 + (arguments.FloorRates[i]!.Value * tenor);
                        double mult = type == CapFloorType.Floor ? 1.0 : -1.0;
                        double option = atFixing is null
                            ? model.DiscountBondOption(OptionType.Call, 1.0 / temp, maturity, paymentTime)
                            : atFixing.DiscountBondOption(OptionType.Call, 1.0 / temp, fixingTime, maturity, paymentTime);
                        value += arguments.Nominals[i] * arguments.Gearings[i] * temp * mult * option;
                    }
                }
            }
        }

        return new InstrumentResults { NPV = value };
    }

    // std::max(0.0, x): 0.0 < x ? x : 0.0, so a NaN gives 0.
    private static double Max0(double x) => 0.0 < x ? x : 0.0;
}
