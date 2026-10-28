// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/cdo.hpp ql/experimental/credit/cdo.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The results of a <see cref="CDO"/>'s calculation, signed by the protection side.</summary>
[Experimental("NQL0001")]
public sealed record CDOResults : InstrumentResults
{
    /// <summary>Gets the premium leg's value.</summary>
    public double? PremiumValue { get; init; }

    /// <summary>Gets the protection leg's value, negative for the protection seller.</summary>
    public double? ProtectionValue { get; init; }

    /// <summary>Gets the upfront premium's value.</summary>
    public double? UpfrontPremiumValue { get; init; }

    /// <summary>Gets the running premium at which the two legs are equal.</summary>
    public double? FairPremium { get; init; }

    /// <summary>Gets the number of steps at which the expected tranche loss fell.</summary>
    public int? Error { get; init; }
}
