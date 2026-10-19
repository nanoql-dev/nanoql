// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/functional.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>Functionals and combinators not included in the standard library.</summary>
public static class FunctionalFunctions
{
    /// <summary>The square of <paramref name="x"/>, <c>x * x</c>.</summary>
    /// <typeparam name="T">
    /// The number type: any type with .NET's generic arithmetic, a <see cref="double"/> or a
    /// <see cref="System.Numerics.Complex"/> among them, as QuantLib's template serves <c>Real</c> and
    /// <c>std::complex&lt;Real&gt;</c>.
    /// </typeparam>
    /// <param name="x">The value.</param>
    /// <returns>x · x.</returns>
    public static T Squared<T>(T x)
        where T : INumberBase<T> => x * x;
}
