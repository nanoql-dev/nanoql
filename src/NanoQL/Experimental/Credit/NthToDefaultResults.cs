// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/nthtodefault.hpp ql/experimental/credit/nthtodefault.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The results an engine returns for an <see cref="NthToDefault"/>: the instrument's, with its legs' values
/// and its fair premium.</summary>
/// <remarks>A result QuantLib leaves at <c>Null&lt;Real&gt;()</c>, whose accessor refuses it ("… not available"), is
/// <see langword="null"/>, as the error estimate of the integral engine.</remarks>
[Experimental("NQL0001")]
public sealed record NthToDefaultResults : InstrumentResults
{
    /// <summary>Gets the premium leg's value, signed by the side, without the upfront.</summary>
    public double? PremiumValue { get; init; }

    /// <summary>Gets the protection leg's value, signed by the side.</summary>
    public double? ProtectionValue { get; init; }

    /// <summary>Gets the upfront's value, signed by the side.</summary>
    public double? UpfrontPremiumValue { get; init; }

    /// <summary>Gets the running premium that zeroes the value, the upfront left out.</summary>
    public double? FairPremium { get; init; }

    /// <summary>Gets the premium leg's value with the upfront: QuantLib's <c>premiumLegNPV()</c>, null where either is
    /// unavailable.</summary>
    public double? PremiumLegNPV => PremiumValue + UpfrontPremiumValue;

    /// <summary>Gets the protection leg's value: QuantLib's <c>protectionLegNPV()</c>.</summary>
    public double? ProtectionLegNPV => ProtectionValue;
}
