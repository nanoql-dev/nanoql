// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2012 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/garch.hpp ql/models/volatility/garch.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A GARCH(1,1) calibration to squared returns, QuantLib's <c>calibrate_r2</c>: the parameters it writes through its
/// references and the minimization it returns (the S-bar plan's BR3 (ii)).
/// </summary>
/// <param name="Alpha">α, the weight of the last squared return.</param>
/// <param name="Beta">β, the weight of the last variance.</param>
/// <param name="Omega">ω, the constant.</param>
/// <param name="Problem">The minimization's results, or <see langword="null"/> where QuantLib returns a null problem:
/// its fallback to the initial guess.</param>
public sealed record Garch11Calibration(double Alpha, double Beta, double Omega, ProblemResults? Problem);
