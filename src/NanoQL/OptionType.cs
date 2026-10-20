// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/option.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>The type of an option: QuantLib's <c>Option::Type</c>.</summary>
/// <remarks>Its <see cref="object.ToString"/> gives QuantLib's stream text, "Call" or "Put".</remarks>
public enum OptionType
{
    /// <summary>A put.</summary>
    Put = -1,

    /// <summary>A call.</summary>
    Call = 1,
}
