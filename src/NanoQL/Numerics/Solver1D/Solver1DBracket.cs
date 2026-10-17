// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solver1d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// What a solver's bracketing hands its implementation, QuantLib's protected members as they stand before
/// <c>solveImpl</c>: <see cref="XMin"/> and <see cref="XMax"/> form a valid bracket; <see cref="FxMin"/> and
/// <see cref="FxMax"/> are the function's values there; <see cref="Root"/> is a valid initial guess.
/// </summary>
/// <param name="Root">The initial guess, inside the bracket.</param>
/// <param name="XMin">The bracket's lower end.</param>
/// <param name="XMax">The bracket's upper end.</param>
/// <param name="FxMin">The function's value at <paramref name="XMin"/>.</param>
/// <param name="FxMax">The function's value at <paramref name="XMax"/>.</param>
/// <param name="EvaluationNumber">The function evaluations the bracketing has counted.</param>
public sealed record Solver1DBracket(double Root, double XMin, double XMax, double FxMin, double FxMax, int EvaluationNumber);
