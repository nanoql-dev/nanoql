// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/inflationhelpers.hpp ql/termstructures/inflation/inflationhelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib's <c>setTermStructure</c> for year-on-year inflation helpers, as a copy of the helper's own static type.
/// </summary>
/// <remarks>
/// The year-on-year inflation family's form beside its helpers, as
/// <see cref="BootstrapHelperExtensions.SetTermStructure{T}(T, YieldTermStructure)"/> is the yield family's.
/// </remarks>
public static class YoYInflationHelperExtensions
{
    /// <summary>
    /// A copy of the helper bound to a year-on-year inflation term structure, of the receiver's static type: QuantLib's
    /// <c>setTermStructure</c>, which links the curve into the helper in place. The copy prices against the curve; the
    /// helper it is called on stays unbound.
    /// </summary>
    /// <typeparam name="T">The helper's type.</typeparam>
    /// <param name="helper">The helper.</param>
    /// <param name="t">The term structure to price against.</param>
    /// <returns>The bound copy.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="helper"/> or <paramref name="t"/> is <see langword="null"/>.
    /// </exception>
    public static T SetTermStructure<T>(this T helper, YoYInflationTermStructure t)
        where T : BootstrapHelper<YoYInflationTermStructure>
    {
        ArgumentNullException.ThrowIfNull(helper);
        return (T)helper.WithTermStructure(t);
    }
}
