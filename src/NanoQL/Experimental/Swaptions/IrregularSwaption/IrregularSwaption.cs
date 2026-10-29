// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/swaptions/irregularswaption.hpp ql/experimental/swaptions/irregularswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: IrregularSwaption::impliedVolatility, never ported: every call throws, unpriced "null pricing engine", priced "wrong argument type" (its Black helper's arguments can't take an irregular swaption's)
// Partial: IrregularSwaption::arguments and IrregularSwaption::engine, never ported: Hagan's engine reads the swaption's swap, exercise and settlement type, and derives from no engine class but the generic one

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>An option to enter an irregular swap: QuantLib's <c>IrregularSwaption</c>.</summary>
/// <remarks>
/// It has no payoff, and its results are an engine's <see cref="InstrumentResults"/>: Hagan's engine prices it
/// (<see cref="HaganIrregularSwaptionEngine"/>). QuantLib's <c>impliedVolatility</c> isn't ported: on QuantLib 1.44
/// every call throws, "null pricing engine" on an unpriced swaption and "wrong argument type" on a priced one, its Black
/// helper's arguments being a vanilla swaption's (the S-xrt plan's XRD9).
/// </remarks>
[Experimental("NQL0001")]
public sealed class IrregularSwaption : Option
{
    /// <summary>Initializes a new instance of the <see cref="IrregularSwaption"/> class.</summary>
    /// <param name="swap">The underlying irregular swap.</param>
    /// <param name="exercise">The exercise.</param>
    /// <param name="delivery">The settlement type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="swap"/> or <paramref name="exercise"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delivery"/> isn't a member of its type.</exception>
    public IrregularSwaption(
        IrregularSwap swap, Exercise exercise, IrregularSettlementType delivery = IrregularSettlementType.Physical)
        : base(null, ExerciseOf(swap, exercise))
    {
        if (delivery is not (IrregularSettlementType.Physical or IrregularSettlementType.Cash))
        {
            throw new ArgumentOutOfRangeException(
                nameof(delivery), delivery, $"unknown IrregularSettlement::Type({(int)delivery})");
        }

        Underlying = swap;
        SettlementType = delivery;
    }

    /// <summary>Gets the settlement type.</summary>
    public IrregularSettlementType SettlementType { get; }

    /// <summary>Gets the underlying swap's type: payer or receiver.</summary>
    public SwapType Type => Underlying.Type;

    /// <summary>Gets the underlying swap: QuantLib's <c>underlyingSwap()</c>.</summary>
    public IrregularSwap Underlying { get; }

    /// <summary>Whether the swaption's last exercise date has occurred.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns><see langword="true"/> when the swaption has expired.</returns>
    /// <exception cref="NanoQLException">The exercise has no dates, where QuantLib reads past them.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(Exercise.LastDate()).HasOccurred(context);
    }

    /// <summary>Checks the swaption's terms before an engine prices it: the underlying swap's.</summary>
    /// <exception cref="NanoQLException">The swap's legs and multipliers differ in number.</exception>
    /// <remarks>
    /// QuantLib's "Irregular swap not set" and "exercise not set" can't fail: both are refused at construction when
    /// null. Its arguments' vectors, one per coupon, are equal in size by construction.
    /// </remarks>
    public override void Validate() => Underlying.Validate();

    // The swap is refused before the exercise, in the parameters' order; the base refuses a null exercise.
    private static Exercise ExerciseOf(IrregularSwap swap, Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(swap);
        return exercise;
    }
}
