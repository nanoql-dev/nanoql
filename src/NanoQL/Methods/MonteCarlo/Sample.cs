// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/sample.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>A weighted sample: a generator's draw and its weight.</summary>
/// <remarks>
/// A struct whatever <typeparamref name="T"/> is, as .NET's own generic pairs (<c>KeyValuePair</c>, <c>ValueTuple</c>)
/// are, so that no draw allocates. A sequence or a path a generator returns is a view of its buffer, valid until its
/// next draw: the sample is a value, its <see cref="Value"/> may be such a view.
/// </remarks>
/// <typeparam name="T">The sample's type.</typeparam>
/// <param name="Value">The sample.</param>
/// <param name="Weight">Its weight.</param>
public readonly record struct Sample<T>(T Value, double Weight);
