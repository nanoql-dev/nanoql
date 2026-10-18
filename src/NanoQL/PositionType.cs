// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/position.hpp ql/position.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>The side of a position: QuantLib's <c>Position::Type</c>.</summary>
/// <remarks>Its <see cref="object.ToString"/> gives QuantLib's stream text, "Long" or "Short".</remarks>
public enum PositionType
{
    /// <summary>A long position.</summary>
    Long,

    /// <summary>A short position.</summary>
    Short,
}
