// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2010 Andre Miemiec
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/swaptions/irregularswap.hpp ql/experimental/swaptions/irregularswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: IrregularSwap::arguments and IrregularSwap::engine, never ported: no engine derives from the engine class, and no engine reads the arguments' vectors (Hagan's engine reads the swaption's swap and exercise)

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// An irregular swap: a fixed leg against a floating one, each given whole, their nominals and rates free to change
/// from coupon to coupon: QuantLib's <c>IrregularSwap</c>.
/// </summary>
/// <remarks>
/// <para>
/// The fixed leg is paid on a payer swap and received on a receiver swap. Any swap engine prices it, and its results are
/// an <see cref="IrregularSwapResults"/>, whose leg accessors read the fixed leg first. Hagan's engine prices a swaption
/// on it (<see cref="HaganIrregularSwaptionEngine"/>), reading its fixed leg's cash flows as
/// <see cref="FixedRateCoupon"/>s and its floating leg's as <see cref="IborCoupon"/>s.
/// </para>
/// <para>
/// The fair rate and the fair spread are an engine's own results only, null on a swap engine's: QuantLib's
/// <c>fetchResults</c> sets both to a debug 0 there (<c>// Debug: legs_[0]->fixedRate_ - …</c>), 0 where the vanilla
/// swap's fair rate is 0.0305, and an irregular swap has no single fixed rate or spread to solve for; null is QuantLib's
/// own "result not available" on reading it (the S-xrt plan's XRR5 (xx); <c>Conventions/IrregularSwapTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class IrregularSwap : Swap
{
    /// <summary>Initializes a new instance of the <see cref="IrregularSwap"/> class.</summary>
    /// <param name="type">Whether the fixed leg is paid (<see cref="SwapType.Payer"/>) or received.</param>
    /// <param name="fixLeg">The fixed leg.</param>
    /// <param name="floatLeg">The floating leg.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="type"/> isn't a member of its type, with QuantLib's text.
    /// </exception>
    /// <exception cref="ArgumentNullException">A leg is null.</exception>
    /// <exception cref="ArgumentException">A leg holds a null cash flow.</exception>
    public IrregularSwap(SwapType type, IEnumerable<CashFlow> fixLeg, IEnumerable<CashFlow> floatLeg)
        : base(CheckedLegs(type, fixLeg, floatLeg), [type == SwapType.Payer, type == SwapType.Receiver])
    {
        Type = type;
    }

    /// <summary>Gets whether the fixed leg is paid or received.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the fixed leg.</summary>
    public ImmutableArray<CashFlow> FixedLeg => Legs[0];

    /// <summary>Gets the floating leg.</summary>
    public ImmutableArray<CashFlow> FloatingLeg => Legs[1];

    /// <inheritdoc/>
    /// <remarks>No fair rate and no fair spread, as QuantLib's.</remarks>
    public override IrregularSwapResults SetupExpired() =>
        new(base.SetupExpired()) { FairRate = null, FairSpread = null };

    /// <inheritdoc/>
    /// <remarks>
    /// The fair rate and the fair spread an irregular-swap engine's results give, and null otherwise, where QuantLib's
    /// sets a debug 0 for each leg with a basis-point sensitivity (XRR5 (xx)).
    /// </remarks>
    public override IrregularSwapResults FetchResults(InstrumentResults results)
    {
        SwapResults r = base.FetchResults(results);

        // might be a swap engine's, so no error is thrown
        return r is IrregularSwapResults fetched ? fetched : new IrregularSwapResults(r) { FairRate = null, FairSpread = null };
    }

    // QuantLib's switch on the type, before it takes the legs.
    private static ImmutableArray<ImmutableArray<CashFlow>> CheckedLegs(
        SwapType type, IEnumerable<CashFlow> fixLeg, IEnumerable<CashFlow> floatLeg)
    {
        if (type is not (SwapType.Payer or SwapType.Receiver))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown Irregular-swap type");
        }

        ArgumentNullException.ThrowIfNull(fixLeg);
        ArgumentNullException.ThrowIfNull(floatLeg);
        return [[.. fixLeg], [.. floatLeg]];
    }
}
