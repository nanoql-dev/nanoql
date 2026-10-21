// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/exchangerate.hpp ql/exchangerate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>How an exchange rate was obtained: QuantLib's <c>ExchangeRate::Type</c>.</summary>
public enum ExchangeRateType
{
    /// <summary>Given directly by the user.</summary>
    Direct = 0,

    /// <summary>Derived from exchange rates between other currencies.</summary>
    Derived = 1,
}
