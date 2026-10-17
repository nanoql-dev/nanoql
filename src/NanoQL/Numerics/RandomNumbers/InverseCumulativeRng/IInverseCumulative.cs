// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/inversecumulativerng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's inverse cumulative distribution concept, the <c>IC</c> its templates take (<c>InverseCumulativeRng</c>,
/// <c>InverseCumulativeRsg</c>, the RNG traits): a configuration struct mapping a probability to a deviate.
/// </summary>
/// <remarks>
/// Its implementations are <see langword="readonly struct"/>s whose zero value is QuantLib's default construction, so
/// a template's never-assigned functor member reads as QuantLib's.
/// </remarks>
public interface IInverseCumulative
{
    /// <summary>The deviate whose cumulative probability is <paramref name="x"/> (QuantLib's <c>operator()</c>).</summary>
    /// <param name="x">The probability.</param>
    /// <returns>The deviate.</returns>
    double Evaluate(double x);
}
