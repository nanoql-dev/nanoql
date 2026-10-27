// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.hpp ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A cap whose implied volatility <see cref="VolatilityBumpInstrumentJacobian"/> bumps: the caplets from the start
/// index to the end index at a strike. QuantLib's nested <c>VolatilityBumpInstrumentJacobian::Cap</c> (R2's top-level
/// name).
/// </summary>
/// <param name="StartIndex">The first caplet's rate.</param>
/// <param name="EndIndex">The end of the caplets, after the last caplet's rate.</param>
/// <param name="Strike">The strike.</param>
public readonly record struct VolatilityBumpInstrumentJacobianCap(int StartIndex, int EndIndex, double Strike);
