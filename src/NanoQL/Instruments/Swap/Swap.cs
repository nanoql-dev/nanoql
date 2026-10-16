// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/swap.hpp ql/instruments/swap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>An interest rate swap: the cash flows of a paid leg and of a received one, or of several legs.</summary>
/// <remarks>
/// Unsealed, as in QuantLib, where many swaps derive from it; a derived swap passes its legs to the multi-leg
/// constructor. The per-leg results that QuantLib's swap holds (<c>legNPV</c>, <c>legBPS</c>, the start and end
/// discounts, <c>npvDateDiscount</c>) are members of the <see cref="SwapResults"/> an engine returns.
/// </remarks>
public class Swap : Instrument
{
    private readonly ImmutableArray<double> payer;

    /// <summary>Initializes a new instance of the <see cref="Swap"/> class with a paid and a received leg.</summary>
    /// <param name="firstLeg">The leg that is paid.</param>
    /// <param name="secondLeg">The leg that is received.</param>
    public Swap(ImmutableArray<CashFlow> firstLeg, ImmutableArray<CashFlow> secondLeg)
    {
        Legs = [CheckLeg(firstLeg, nameof(firstLeg)), CheckLeg(secondLeg, nameof(secondLeg))];
        payer = [-1.0, 1.0];
    }

    /// <summary>Initializes a new instance of the <see cref="Swap"/> class with any number of legs.</summary>
    /// <param name="legs">The legs.</param>
    /// <param name="payer">For each leg, whether it is paid.</param>
    /// <exception cref="ArgumentException">The two sequences differ in size.</exception>
    public Swap(IEnumerable<ImmutableArray<CashFlow>> legs, IEnumerable<bool> payer)
    {
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(payer);
        Legs = [.. legs.Select(leg => CheckLeg(leg, nameof(legs)))];
        ImmutableArray<bool> paid = [.. payer];
        if (paid.Length != Legs.Length)
        {
            throw new ArgumentException(
                $"size mismatch between payer ({paid.Length}) and legs ({Legs.Length})", nameof(payer));
        }

        this.payer = [.. paid.Select(p => p ? -1.0 : 1.0)];
    }

    /// <summary>Gets the number of legs.</summary>
    public int NumberOfLegs => Legs.Length;

    /// <summary>Gets the legs.</summary>
    public ImmutableArray<ImmutableArray<CashFlow>> Legs { get; }

    /// <summary>Gets each leg's multiplier: -1 for a paid leg, 1 for a received one.</summary>
    internal ImmutableArray<double> PayerMultipliers => payer;

    /// <inheritdoc/>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (ImmutableArray<CashFlow> leg in Legs)
        {
            foreach (CashFlow i in leg)
            {
                if (!i.HasOccurred(context))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override SwapResults SetupExpired()
    {
        InstrumentResults expired = base.SetupExpired();
        ImmutableArray<double?> zeros = [.. Enumerable.Repeat<double?>(0.0, NumberOfLegs)];
        return new SwapResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
            LegBPS = zeros,
            LegNPV = zeros,
            StartDiscounts = zeros,
            EndDiscounts = zeros,
            NpvDateDiscount = 0.0,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The number of legs and of multipliers differ.</exception>
    public override void Validate()
    {
        if (Legs.Length != payer.Length)
        {
            throw new NanoQLException("number of legs and multipliers differ");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// The results aren't a swap's, or a per-leg result has the wrong number of legs.
    /// </exception>
    public override SwapResults FetchResults(InstrumentResults results)
    {
        InstrumentResults fetched = base.FetchResults(results);
        if (fetched is not SwapResults r)
        {
            throw new ArgumentException("wrong result type", nameof(results));
        }

        return r with
        {
            LegNPV = PerLeg(r.LegNPV, "wrong number of leg NPV returned"),
            LegBPS = PerLeg(r.LegBPS, "wrong number of leg BPS returned"),
            StartDiscounts = PerLeg(r.StartDiscounts, "wrong number of leg start discounts returned"),
            EndDiscounts = PerLeg(r.EndDiscounts, "wrong number of leg end discounts returned"),
        };
    }

    /// <summary>The earliest start date of the legs.</summary>
    /// <returns>The start date.</returns>
    /// <exception cref="NanoQLException">The swap has no legs.</exception>
    public virtual Date StartDate()
    {
        if (Legs.IsEmpty)
        {
            throw new NanoQLException("no legs given");
        }

        Date d = CashFlows.StartDate(Legs[0]);
        for (int j = 1; j < Legs.Length; ++j)
        {
            Date start = CashFlows.StartDate(Legs[j]);
            d = start < d ? start : d;
        }

        return d;
    }

    /// <summary>The latest maturity date of the legs.</summary>
    /// <returns>The maturity date.</returns>
    /// <exception cref="NanoQLException">The swap has no legs.</exception>
    public virtual Date MaturityDate()
    {
        if (Legs.IsEmpty)
        {
            throw new NanoQLException("no legs given");
        }

        Date d = CashFlows.MaturityDate(Legs[0]);
        for (int j = 1; j < Legs.Length; ++j)
        {
            Date maturity = CashFlows.MaturityDate(Legs[j]);
            d = maturity > d ? maturity : d;
        }

        return d;
    }

    /// <summary>The leg at an index.</summary>
    /// <param name="j">The index.</param>
    /// <returns>The leg.</returns>
    /// <exception cref="ArgumentException">There's no leg at the index.</exception>
    public ImmutableArray<CashFlow> Leg(int j)
    {
        CheckIndex(j);
        return Legs[j];
    }

    /// <summary>Whether the leg at an index is paid.</summary>
    /// <param name="j">The index.</param>
    /// <returns><see langword="true"/> when the leg is paid.</returns>
    /// <exception cref="ArgumentException">There's no leg at the index.</exception>
    public bool Payer(int j)
    {
        CheckIndex(j);
        return payer[j] < 0.0;
    }

    private static ImmutableArray<CashFlow> CheckLeg(ImmutableArray<CashFlow> leg, string paramName)
    {
        if (leg.IsDefault)
        {
            throw new ArgumentException("default(ImmutableArray) is not a leg; an empty leg is []", paramName);
        }

        if (leg.Any(cf => cf is null))
        {
            throw new ArgumentException("null cash flow in leg", paramName);
        }

        return leg;
    }

    private void CheckIndex(int j)
    {
        if (!(j >= 0 && j < Legs.Length))
        {
            throw new ArgumentException($"leg #{j} doesn't exist!", nameof(j));
        }
    }

    private ImmutableArray<double?> PerLeg(ImmutableArray<double?> values, string message)
    {
        if (values.IsDefaultOrEmpty)
        {
            return [.. Enumerable.Repeat<double?>(null, NumberOfLegs)];
        }

        if (values.Length != NumberOfLegs)
        {
            throw new ArgumentException(message, "results");
        }

        return values;
    }
}
