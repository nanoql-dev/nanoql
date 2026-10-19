// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/blackcallablebondengine.hpp ql/experimental/callablebonds/blackcallablebondengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The Black-formula engine for callable zero-coupon bonds with a single call or put date: QuantLib's subclass of the
/// fixed-rate bonds' engine, which only adds constructors, so each overload is
/// <see cref="BlackCallableFixedRateBondEngine"/>'s. QuantLib notes it as yet untested.
/// </summary>
[Experimental("NQL0001")]
public static class BlackCallableZeroCouponBondEngine
{
    /// <summary>Prices a callable bond with a constant forward yield volatility.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">
    /// The evaluation context, which gives the bond's settlement date and the volatility's reference date.
    /// </param>
    /// <param name="fwdYieldVol">The forward yield volatility.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>The bond's results, as <see cref="BlackCallableFixedRateBondEngine"/>'s.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The bond's arguments are inconsistent; the bond doesn't have exactly one call or put date, or it has occurred;
    /// or the forward yield can't be solved for.
    /// </exception>
    public static CallableBondResults Calculate(
        CallableBond bond, EvaluationContext context, double fwdYieldVol, YieldTermStructure discountCurve) =>
        BlackCallableFixedRateBondEngine.Calculate(bond, context, fwdYieldVol, discountCurve);

    /// <summary>Prices a callable bond with a forward yield volatility structure.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="yieldVolStructure">The forward yield volatilities.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <returns>The bond's results, as <see cref="BlackCallableFixedRateBondEngine"/>'s.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The bond's arguments are inconsistent; the bond doesn't have exactly one call or put date, or it has occurred;
    /// the forward yield can't be solved for; or the volatility is queried outside its range.
    /// </exception>
    public static CallableBondResults Calculate(
        CallableBond bond,
        EvaluationContext context,
        CallableBondVolatilityStructure yieldVolStructure,
        YieldTermStructure discountCurve) =>
        BlackCallableFixedRateBondEngine.Calculate(bond, context, yieldVolStructure, discountCurve);
}
