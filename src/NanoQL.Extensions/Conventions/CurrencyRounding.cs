// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A currency's rounding convention: QuantLib's <c>Rounding</c>, its type, and for every type but
/// <see cref="RoundingType.None"/> its precision and rounding digit.
/// </summary>
/// <param name="Type">The rounding's type.</param>
/// <param name="Precision">The decimal places, or <see langword="null"/> for <see cref="RoundingType.None"/>.</param>
/// <param name="Digit">The rounding digit, or <see langword="null"/> for <see cref="RoundingType.None"/>.</param>
public sealed record CurrencyRounding(RoundingType Type, int? Precision = null, int? Digit = null)
{
    /// <summary>The rounding this describes.</summary>
    /// <returns>QuantLib's no-rounding for <see cref="RoundingType.None"/>, else a rounding of the precision, type and
    /// digit.</returns>
    /// <exception cref="ArgumentException">A type other than <see cref="RoundingType.None"/> without a precision or
    /// digit, or <see cref="RoundingType.None"/> with one.</exception>
    public Rounding ToRounding() => Type == RoundingType.None
        ? Precision is null && Digit is null
            ? new Rounding()
            : throw new ArgumentException("a rounding of type None has no precision or digit")
        : Precision is int p && Digit is int d
            ? new Rounding(p, Type, d)
            : throw new ArgumentException($"a rounding of type {Type} needs a precision and a digit");

    /// <summary>Describes a rounding.</summary>
    /// <param name="rounding">The rounding.</param>
    /// <returns>Its type, and its precision and digit unless its type is <see cref="RoundingType.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rounding"/> is null.</exception>
    public static CurrencyRounding Of(Rounding rounding)
    {
        ArgumentNullException.ThrowIfNull(rounding);
        return rounding.Type == RoundingType.None
            ? new CurrencyRounding(RoundingType.None)
            : new CurrencyRounding(rounding.Type, rounding.Precision(), rounding.RoundingDigit());
    }
}
