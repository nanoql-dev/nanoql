// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflationindex.hpp ql/indexes/inflationindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Indexes;

/// <summary>How an observation of an inflation index interpolates between fixings: QuantLib's
/// <c>CPI::InterpolationType</c>.</summary>
/// <remarks>
/// QuantLib's values. Its <c>AsIndex = 0</c>, deprecated in 1.43, isn't ported: a member given 0 refuses it with
/// QuantLib's "unknown CPI interpolation type: 0".
/// </remarks>
public enum CPIInterpolationType
{
    /// <summary>Flat from the previous fixing.</summary>
    Flat = 1,

    /// <summary>Linearly between the bracketing fixings.</summary>
    Linear = 2,
}
