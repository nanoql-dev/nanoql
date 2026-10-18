// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/stubiborcoupon.hpp ql/cashflows/stubiborcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// An index fixing as a fixed-weight linear combination of Ibor indices: QuantLib's
/// <c>StubIborCoupon::WeightedIndex</c>.
/// </summary>
/// <remarks>
/// <para>
/// Like a swap-spread index, it has no fixings and no forwarding curve of its own: it delegates both to its components,
/// so the store refuses fixings for it (<see cref="AllowsNativeFixings"/> is <see langword="false"/>), and it can't be
/// cloned onto a single curve. The components must share their conventions (fixing days, fixing calendar, day counter,
/// currency); only their tenors differ, and the maturity of the combination is that of the longest component.
/// </para>
/// <para>
/// Its name lists each component's name with its weight, written with four decimals as QuantLib's
/// <c>std::fixed</c> and <c>std::setprecision(4)</c> write it: the exact binary value, ties rounded to even
/// (0.03125 gives "0.0312").
/// </para>
/// </remarks>
public sealed class StubIborCouponWeightedIndex : IborIndex
{
    private readonly string name;

    /// <summary>Initializes a new instance of the <see cref="StubIborCouponWeightedIndex"/> class.</summary>
    /// <param name="components">Each component index and the weight it carries in the fixing.</param>
    /// <exception cref="ArgumentException">No components are given, one is null, or their fixing days, fixing calendars,
    /// day counters or currencies differ.</exception>
    public StubIborCouponWeightedIndex(IEnumerable<(IborIndex Index, double Weight)> components)
        : this(Checked(components))
    {
    }

    private StubIborCouponWeightedIndex(ImmutableArray<(IborIndex Index, double Weight)> components)
        : base(
            "Weighted",
            LongestTenor(components),
            components[0].Index.FixingDays,
            components[0].Index.Currency,
            components[0].Index.FixingCalendar,
            components[0].Index.BusinessDayConvention,
            components[0].Index.EndOfMonth,
            components[0].Index.DayCounter)
    {
        /* The combination fixes on a single date and its clients derive
           dates from a single set of conventions.
        */
        IborIndex first = components[0].Index;
        foreach ((IborIndex index, _) in components)
        {
            if (index.FixingDays != first.FixingDays)
            {
                throw new ArgumentException(
                    $"{index.Name} fixing days ({index.FixingDays}) do not match {first.Name} ({first.FixingDays})",
                    nameof(components));
            }

            if (index.FixingCalendar != first.FixingCalendar)
            {
                throw new ArgumentException($"{index.Name} fixing calendar does not match {first.Name}", nameof(components));
            }

            if (index.DayCounter != first.DayCounter)
            {
                throw new ArgumentException($"{index.Name} day counter does not match {first.Name}", nameof(components));
            }

            if (index.Currency != first.Currency)
            {
                throw new ArgumentException($"{index.Name} currency does not match {first.Name}", nameof(components));
            }
        }

        Components = components;

        var output = new StringBuilder();
        for (int i = 0; i < components.Length; ++i)
        {
            if (i > 0)
            {
                output.Append(" + ");
            }

            output.Append(components[i].Index.Name)
                .Append('(')
                .Append(components[i].Weight.ToString("F4", CultureInfo.InvariantCulture))
                .Append(')');
        }

        name = output.ToString();
    }

    /// <summary>Gets each component index and the weight it carries in the fixing.</summary>
    public ImmutableArray<(IborIndex Index, double Weight)> Components { get; }

    /// <inheritdoc/>
    public override string Name => name;

    /// <inheritdoc/>
    public override bool AllowsNativeFixings => false;

    /// <summary>The maturity of the longest component.</summary>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The latest of the components' maturity dates.</returns>
    public override Date MaturityDate(Date valueDate)
    {
        Date.ThrowIfDefault(valueDate, nameof(valueDate));
        Date maturity = Date.MinDate;
        foreach ((IborIndex index, _) in Components)
        {
            Date d = index.MaturityDate(valueDate);
            maturity = maturity < d ? d : maturity;
        }

        return maturity;
    }

    /// <summary>
    /// The forecast: the weighted sum of the components' fixings, each of which may be a stored fixing on the
    /// evaluation date.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The forecast.</returns>
    public override double ForecastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // as in SwapSpreadIndex, this also handles the case where one of the
        // components has a historic fixing on the evaluation date
        double fixing = 0.0;
        foreach ((IborIndex index, double weight) in Components)
        {
            fixing += weight * index.Fixing(fixingDate, context, false);
        }

        return fixing;
    }

    /// <summary>The weighted sum of the components' stored fixings.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The sum, or <see langword="null"/> when a component has no stored fixing for the date.</returns>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is not a valid fixing date of a
    /// component.</exception>
    public override double? PastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double fixing = 0.0;
        foreach ((IborIndex index, double weight) in Components)
        {
            // a missing component means a missing fixing for the combination
            if (index.PastFixing(fixingDate, context) is not double componentFixing)
            {
                return null;
            }

            fixing += weight * componentFixing;
        }

        return fixing;
    }

    /// <summary>Refuses: a combination of indices can't be cloned onto a single curve; clone the components instead.</summary>
    /// <param name="forwarding">The forwarding curve.</param>
    /// <returns>Nothing: it always throws.</returns>
    /// <exception cref="NanoQLException">Always.</exception>
    public override IborIndex Clone(YieldTermStructure? forwarding) =>
        throw new NanoQLException(
            "StubIborCoupon::WeightedIndex cannot be relinked to a single forwarding curve; relink its components instead");

    private static ImmutableArray<(IborIndex Index, double Weight)> Checked(
        IEnumerable<(IborIndex Index, double Weight)> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        ImmutableArray<(IborIndex Index, double Weight)> c = [.. components];
        if (c.IsEmpty)
        {
            throw new ArgumentException("no components given for weighted index", nameof(components));
        }

        // QuantLib reads every component's tenor, and the first one's conventions, before its null check.
        foreach ((IborIndex index, _) in c)
        {
            if (index is null)
            {
                throw new ArgumentException("null component index", nameof(components));
            }
        }

        return c;
    }

    private static Period LongestTenor(ImmutableArray<(IborIndex Index, double Weight)> components)
    {
        // std::max_element: the first of the longest tenors.
        Period longest = components[0].Index.Tenor;
        for (int i = 1; i < components.Length; ++i)
        {
            Period tenor = components[i].Index.Tenor;
            if (longest < tenor)
            {
                longest = tenor;
            }
        }

        return longest;
    }
}
