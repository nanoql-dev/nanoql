// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/money.hpp ql/money.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>
/// The settings that say how <see cref="Money"/> amounts in different currencies combine: QuantLib's per-session
/// <c>Money::Settings</c>, as a value that <see cref="EvaluationContext.MoneySettings"/> holds.
/// </summary>
/// <remarks>
/// The defaults are QuantLib's: no conversion, and no base currency. QuantLib's proxies kept for its old syntax
/// (<c>Money::baseCurrency</c>, <c>Money::conversionType</c>) aren't ported.
/// </remarks>
public sealed record MoneySettings
{
    /// <summary>Gets how amounts in different currencies combine; QuantLib's default is
    /// <see cref="MoneyConversionType.NoConversion"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Set to a value that isn't a member of the enum.</exception>
    public MoneyConversionType ConversionType
    {
        get;
        init
        {
            EnumArguments.Check(value, nameof(ConversionType));
            field = value;
        }
    }

    /// <summary>Gets the currency both operands convert to under
    /// <see cref="MoneyConversionType.BaseCurrencyConversion"/>; QuantLib's default is none, a
    /// <see langword="null"/>.</summary>
    public Currency? BaseCurrency { get; init; }
}
