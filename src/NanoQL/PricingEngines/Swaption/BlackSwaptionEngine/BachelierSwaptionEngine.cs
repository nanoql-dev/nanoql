// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015, 2016, 2017 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Paul Giltinan
// SPDX-FileCopyrightText: Copyright (C) 2017 Werner Kuerzinger
// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/blackswaptionengine.hpp ql/pricingengines/swaption/blackswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The normal Bachelier-formula swaption engine.</summary>
/// <remarks>
/// <para>
/// A static class, as <see cref="BlackSwaptionEngine"/> is, with the same results; the engine assumes that the exercise
/// date lies before the start date of the passed swap.
/// </para>
/// <para>
/// The overload on a <see cref="double"/> volatility keeps QuantLib's behaviour: its constructors pass their
/// <c>model</c> to the base's four-argument constructor, where it lands in the displacement, so the volatility's shift
/// is the model's integer, which a normal volatility never reads, and the cash annuity model is always
/// <see cref="CashAnnuityModel.DiscountCurve"/>, whatever <c>model</c> is given.
/// </para>
/// </remarks>
public static class BachelierSwaptionEngine
{
    /// <summary>Prices a swaption on a constant normal volatility.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve, which the underlying swap is priced on.</param>
    /// <param name="vol">The normal volatility.</param>
    /// <param name="dc">The volatility's day counter; <see cref="Actual365Fixed"/>, QuantLib's default, when
    /// <see langword="null"/>.</param>
    /// <param name="model">Not read for the annuity, as in QuantLib, whose constructor passes it as the volatility's
    /// shift: the annuity is always discounted on the discount curve from the swap's start date.</param>
    /// <returns>The swaption's results: the expired results when the swaption has expired.</returns>
    /// <exception cref="ArgumentNullException">The swaption, the context or the discount curve is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, it isn't European, its swap starts before its exercise date, or the swap
    /// or the formula can't be priced.
    /// </exception>
    public static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        double vol,
        DayCounter? dc = null,
        CashAnnuityModel model = CashAnnuityModel.DiscountCurve)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);

        // QuantLib's (discountCurve, vol, dc, model) calls the base's (discountCurve, vol, dc, displacement, model)
        // with four arguments: the enum converts to the displacement, and the model keeps its default.
        ConstantSwaptionVolatility volatility = BlackStyleSwaptionEngine<BachelierSpec>.ConstantVolatility(
            vol, dc ?? new Actual365Fixed(), (double)model, context);
        return BlackStyleSwaptionEngine<BachelierSpec>.Calculate(
            swaption, context, discountCurve, volatility, CashAnnuityModel.DiscountCurve);
    }

    /// <summary>Prices a swaption on a volatility structure.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve, which the underlying swap is priced on.</param>
    /// <param name="vol">The volatility structure, normal.</param>
    /// <param name="model">The cash annuity model of a cash swaption settled at the par yield curve.</param>
    /// <returns>The swaption's results: the expired results when the swaption has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The structure's volatilities aren't normal.</exception>
    /// <exception cref="NanoQLException">
    /// The swaption's terms are inconsistent, it isn't European, its swap starts before its exercise date, or the swap,
    /// the volatility or the formula can't be priced.
    /// </exception>
    public static InstrumentResults Calculate(
        Swaption swaption,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        SwaptionVolatilityStructure vol,
        CashAnnuityModel model = CashAnnuityModel.DiscountCurve)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentNullException.ThrowIfNull(vol);
        if (!(vol.VolatilityType == VolatilityType.Normal))
        {
            throw new ArgumentException("BachelierSwaptionEngine requires normal input volatility", nameof(vol));
        }

        return BlackStyleSwaptionEngine<BachelierSpec>.Calculate(swaption, context, discountCurve, vol, model);
    }
}
