// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/money.hpp ql/money.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>QuantLib's <c>Decimal * Currency</c> operators (<c>money.hpp</c>), so that <c>100.0 * eur</c> is a
/// <see cref="Money"/>.</summary>
public static class CurrencyExtensions
{
    extension(Currency)
    {
        /// <summary>An amount in a currency.</summary>
        /// <param name="value">The amount.</param>
        /// <param name="c">The currency.</param>
        /// <returns>The amount of cash.</returns>
        public static Money operator *(double value, Currency c) => new(value, c);

        /// <summary>An amount in a currency.</summary>
        /// <param name="c">The currency.</param>
        /// <param name="value">The amount.</param>
        /// <returns>The amount of cash.</returns>
        public static Money operator *(Currency c, double value) => new(value, c);
    }
}
