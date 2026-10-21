// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/money.hpp ql/money.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>
/// How amounts in different currencies are combined: QuantLib's <c>Money::ConversionType</c>, read from
/// <see cref="MoneySettings.ConversionType"/>.
/// </summary>
public enum MoneyConversionType
{
    /// <summary>Do not convert: combining amounts in different currencies is refused.</summary>
    NoConversion = 0,

    /// <summary>Convert both operands to the base currency before combining them.</summary>
    BaseCurrencyConversion = 1,

    /// <summary>Convert the second operand into the first's currency, where the result is.</summary>
    AutomatedConversion = 2,
}
