// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomsequencegenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A random-number generator that also draws 32-bit integers, QuantLib's <c>nextInt32</c>, which
/// <c>RandomSequenceGenerator::nextInt32Sequence</c> needs of its generator.
/// </summary>
public interface IInt32Rng : IRng
{
    /// <summary>Draws the next integer, in [0, 4294967295], changing the generator.</summary>
    /// <returns>The integer, as QuantLib's 32-bit unsigned value.</returns>
    long NextInt32();
}
