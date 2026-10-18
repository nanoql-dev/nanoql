// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/bootstraphelper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>QuantLib's <c>setTermStructure</c>, as a copy of the helper's own static type.</summary>
/// <remarks>
/// One method per family of term structures, since a family's helpers take that family's base: the yield family's is
/// here, and each later family adds its own beside its helpers.
/// </remarks>
public static class BootstrapHelperExtensions
{
    /// <summary>
    /// A copy of the helper bound to a yield term structure, of the receiver's static type: QuantLib's
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
    public static T SetTermStructure<T>(this T helper, YieldTermStructure t)
        where T : BootstrapHelper<YieldTermStructure>
    {
        ArgumentNullException.ThrowIfNull(helper);
        return (T)helper.WithTermStructure(t);
    }
}
