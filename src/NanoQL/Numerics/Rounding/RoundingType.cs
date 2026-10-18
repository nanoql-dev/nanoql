// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/rounding.hpp ql/math/rounding.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The rounding methods of <see cref="Rounding"/>: QuantLib's <c>Rounding::Type</c>, with its values.
/// </summary>
/// <remarks>
/// The methods follow the OMG specification available at <c>http://www.omg.org/cgi-bin/doc?formal/00-06-29.pdf</c>.
/// The names of <see cref="Floor"/> and <see cref="Ceiling"/> might be misleading: check the reference.
/// </remarks>
public enum RoundingType
{
    /// <summary>Do not round: return the number unmodified.</summary>
    None = 0,

    /// <summary>
    /// The first decimal place past the precision is rounded up. This differs from the OMG rule, which rounds up only
    /// if the decimal to be rounded is greater than or equal to the rounding digit.
    /// </summary>
    Up = 1,

    /// <summary>All decimal places past the precision are truncated.</summary>
    Down = 2,

    /// <summary>
    /// The first decimal place past the precision is rounded up if greater than or equal to the rounding digit; this
    /// corresponds to the OMG round-up rule. When the rounding digit is 5, the result is the one closest to the
    /// original number, hence the name.
    /// </summary>
    Closest = 3,

    /// <summary>
    /// Positive numbers are rounded up and negative numbers rounded down, using the OMG round-up and round-down rules.
    /// </summary>
    Floor = 4,

    /// <summary>
    /// Positive numbers are rounded down and negative numbers rounded up, using the OMG round-up and round-down rules.
    /// </summary>
    Ceiling = 5,
}
