// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/endcriteria.hpp ql/math/optimization/endcriteria.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The outcome of one of <see cref="EndCriteria"/>'s checks that counts stationary iterations: whether the
/// optimization stops, why, and the count after the check. It replaces QuantLib's <c>Size&amp;</c> and
/// <c>EndCriteria::Type&amp;</c> arguments.
/// </summary>
/// <param name="Stop">Whether a criterion is met, so the optimization stops: QuantLib's return value.</param>
/// <param name="Type">
/// The criterion met, where QuantLib sets its <c>ecType</c> argument; <see cref="EndCriteriaType.None"/> unless
/// <paramref name="Stop"/>, where QuantLib leaves the argument as it was.
/// </param>
/// <param name="StationaryStateIterations">The count of stationary iterations after the check.</param>
public readonly record struct EndCriteriaCheck(bool Stop, EndCriteriaType Type, int StationaryStateIterations);
