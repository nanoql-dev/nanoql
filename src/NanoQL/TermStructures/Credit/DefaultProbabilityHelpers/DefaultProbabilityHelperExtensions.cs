// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2023 Andrea Pellegatta
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/defaultprobabilityhelpers.hpp ql/termstructures/credit/defaultprobabilityhelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib's <c>setTermStructure</c> for default-probability helpers, as a copy of the helper's own static type.
/// </summary>
/// <remarks>
/// The default-probability family's form beside its helpers, as
/// <see cref="BootstrapHelperExtensions.SetTermStructure{T}(T, YieldTermStructure)"/> is the yield family's. QuantLib's
/// aliases <c>DefaultProbabilityHelper</c> and <c>RelativeDateDefaultProbabilityHelper</c> are
/// <see cref="BootstrapHelper{TTermStructure}"/> and <see cref="RelativeDateBootstrapHelper{TTermStructure}"/> on
/// <see cref="DefaultProbabilityTermStructure"/>.
/// </remarks>
public static class DefaultProbabilityHelperExtensions
{
    /// <summary>
    /// A copy of the helper bound to a default-probability term structure, of the receiver's static type: QuantLib's
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
    public static T SetTermStructure<T>(this T helper, DefaultProbabilityTermStructure t)
        where T : BootstrapHelper<DefaultProbabilityTermStructure>
    {
        ArgumentNullException.ThrowIfNull(helper);
        return (T)helper.WithTermStructure(t);
    }
}
