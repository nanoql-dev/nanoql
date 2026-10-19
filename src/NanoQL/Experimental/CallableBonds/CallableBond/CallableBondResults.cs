// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2017 BN Algorithms Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/callablebond.hpp ql/experimental/callablebonds/callablebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// The results a callable bond's engines return: a bond's, with nothing added, as QuantLib's
/// <c>CallableBond::results</c> adds nothing.
/// </summary>
/// <remarks>
/// The engines set the NPV, at the discount curve's reference date, and the settlement value; the error estimate and
/// the valuation date stay <see langword="null"/>, as QuantLib's engines leave them unset.
/// </remarks>
[Experimental("NQL0001")]
public sealed record CallableBondResults : BondResults;
