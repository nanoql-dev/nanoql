// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/rounding.hpp ql/math/rounding.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Down-rounding: <see cref="RoundingType.Down"/>.</summary>
public sealed class DownRounding : Rounding
{
    /// <summary>Initializes a new instance of the <see cref="DownRounding"/> class.</summary>
    /// <param name="precision">The number of decimal places kept, in [0, 16].</param>
    /// <param name="digit">The rounding digit, unchecked as in QuantLib.</param>
    /// <exception cref="ArgumentOutOfRangeException">The precision is outside [0, 16].</exception>
    public DownRounding(int precision, int digit = 5)
        : base(precision, RoundingType.Down, digit)
    {
    }
}
