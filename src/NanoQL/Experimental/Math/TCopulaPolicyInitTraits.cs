// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/tcopulapolicy.hpp ql/experimental/math/tcopulapolicy.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The Student t copula policy's initialization parameters: the orders of the factors' t distributions, the systemic
/// factors' and then one for every idiosyncratic factor.
/// </summary>
/// <remarks>The zero value holds no order, QuantLib's default-constructed parameters.</remarks>
/// <param name="TOrders">The orders, each above 2; one per systemic factor plus one.</param>
[Experimental("NQL0001")]
public readonly record struct TCopulaPolicyInitTraits(ImmutableArray<int> TOrders);
