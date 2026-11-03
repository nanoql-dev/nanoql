// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>A user's function on a nanoql type off nanoql's list: withheld, printed with its remedy.</summary>
public static class Solver
{
    /// <summary>The solver's accuracy, read through it.</summary>
    /// <param name="solver">nanoql's Brent solver, which nanoql's list leaves out.</param>
    /// <returns>A value.</returns>
    public static double Root(Brent solver) => solver == null ? 0 : 1;
}
