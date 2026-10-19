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

/// <summary>The shifted lognormal Black-formula swaption engine.</summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) and returns the swaption's results, with
/// QuantLib's eleven additional results, read by <see cref="InstrumentResults.Result{T}"/>. The engine assumes that
/// the exercise date lies before the start date of the passed swap, and fully supports an overnight-indexed
/// underlying.
/// </para>
/// <para>
/// QuantLib's constructors on a volatility value and on a quote coincide once a quote is a <see cref="double"/>: the
/// overload on a <see cref="double"/> builds, at each call, the constant volatility QuantLib's builds, on no calendar
/// and with its reference date the context's evaluation date. Its day counter defaults to QuantLib's,
/// <see cref="Actual365Fixed"/>, for <see langword="null"/>; QuantLib's empty day counter, which fails at the
/// volatility's first conversion of a date to a time, has no spelling.
/// </para>
/// </remarks>
public static class BlackSwaptionEngine
{
    /// <summary>Prices a swaption on a constant volatility.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve, which the underlying swap is priced on.</param>
    /// <param name="vol">The shifted lognormal volatility.</param>
    /// <param name="dc">The volatility's day counter; <see cref="Actual365Fixed"/>, QuantLib's default, when
    /// <see langword="null"/>.</param>
    /// <param name="displacement">The volatility's shift.</param>
    /// <param name="model">The cash annuity model of a cash swaption settled at the par yield curve.</param>
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
        double displacement = 0.0,
        CashAnnuityModel model = CashAnnuityModel.DiscountCurve)
    {
        ArgumentNullException.ThrowIfNull(swaption);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        ConstantSwaptionVolatility volatility = BlackStyleSwaptionEngine<Black76Spec>.ConstantVolatility(
            vol, dc ?? new Actual365Fixed(), displacement, context);
        return BlackStyleSwaptionEngine<Black76Spec>.Calculate(swaption, context, discountCurve, volatility, model);
    }

    /// <summary>Prices a swaption on a volatility structure.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve, which the underlying swap is priced on.</param>
    /// <param name="vol">The volatility structure, shifted lognormal.</param>
    /// <param name="model">The cash annuity model of a cash swaption settled at the par yield curve.</param>
    /// <returns>The swaption's results: the expired results when the swaption has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The structure's volatilities aren't shifted lognormal.</exception>
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
        if (!(vol.VolatilityType == VolatilityType.ShiftedLognormal))
        {
            throw new ArgumentException("BlackSwaptionEngine requires (shifted) lognormal input volatility", nameof(vol));
        }

        return BlackStyleSwaptionEngine<Black76Spec>.Calculate(swaption, context, discountCurve, vol, model);
    }
}
