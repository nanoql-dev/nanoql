// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.hpp ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A swaption whose implied volatility <see cref="VolatilityBumpInstrumentJacobian"/> bumps: on the swap from the start
/// index to the end index. QuantLib's nested <c>VolatilityBumpInstrumentJacobian::Swaption</c> (R2's top-level name).
/// </summary>
/// <param name="StartIndex">The swap's first rate.</param>
/// <param name="EndIndex">The swap's end, after its last rate.</param>
public readonly record struct VolatilityBumpInstrumentJacobianSwaption(int StartIndex, int EndIndex);
