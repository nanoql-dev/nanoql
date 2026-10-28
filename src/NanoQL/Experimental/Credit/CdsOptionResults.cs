// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Stamm
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/cdsoption.hpp ql/experimental/credit/cdsoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The results of a credit default swap option's calculation: its value and the underlying's risky
/// annuity.</summary>
/// <remarks>QuantLib's results derive the option's, whose sensitivities the Black engine leaves unset; here they are
/// left out.</remarks>
[Experimental("NQL0001")]
public sealed record CdsOptionResults : InstrumentResults
{
    /// <summary>Gets the underlying swap's risky annuity, the absolute value of its coupon leg's NPV over its running
    /// spread.</summary>
    public double? RiskyAnnuity { get; init; }
}
