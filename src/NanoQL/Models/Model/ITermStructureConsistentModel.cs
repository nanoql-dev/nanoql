// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/model.hpp ql/models/model.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Models;

/// <summary>A term-structure consistent model: one that can reprice exactly any discount bond of its curve.</summary>
/// <remarks>QuantLib's <c>TermStructureConsistentModel</c>, a base its models derive from beside their class base, is an interface.</remarks>
public interface ITermStructureConsistentModel
{
    /// <summary>Gets the curve the model fits.</summary>
    YieldTermStructure TermStructure { get; }
}
