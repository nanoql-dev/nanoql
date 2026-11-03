// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/statistics/convergencestatistics.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The default sampling rule of <see cref="ConvergenceStatistics{TStats, TRule}"/>: the mean is stored at 1 sample,
/// then at 2n + 1 after n, so at 1, 3, 7, 15, … (2ᵏ − 1) samples. QuantLib's comment says 2ⁿ⁻¹; its code, which its
/// test asserts and this follows, gives 2ⁿ − 1. Its zero value is QuantLib's default construction.
/// </summary>
public readonly struct DoublingConvergenceSteps : IConvergenceSteps
{
    /// <inheritdoc/>
    public int InitialSamples => 1;

    /// <inheritdoc/>
    public int NextSamples(int current) => (2 * current) + 1;
}
