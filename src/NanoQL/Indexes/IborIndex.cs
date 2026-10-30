// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/iborindex.hpp ql/indexes/iborindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class of inter-bank offered rate indexes (Euribor, Tibor, ...).</summary>
/// <remarks>
/// Unsealed, as in QuantLib, where the presets derive from it. The index holds its forecasting curve, QuantLib's
/// handle; <see langword="null"/> is QuantLib's empty handle, with which past fixings still work and a forecast throws.
/// QuantLib's relinking of the handle is building the index, and what holds it, on another curve, which
/// <see cref="Clone"/> does.
/// </remarks>
public class IborIndex : InterestRateIndex
{
    /// <summary>Initializes a new instance of the <see cref="IborIndex"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="settlementDays">The fixing days.</param>
    /// <param name="currency">The currency, or <see langword="null"/>, QuantLib's empty currency.</param>
    /// <param name="fixingCalendar">The fixing calendar.</param>
    /// <param name="convention">The convention adjusting the maturity date.</param>
    /// <param name="endOfMonth">Whether the maturity date follows the end-of-month rule.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="forwardingTermStructure">The curve that forecasts fixings, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="convention"/> isn't a member of its type.</exception>
    public IborIndex(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        DayCounter dayCounter,
        YieldTermStructure? forwardingTermStructure = null)
        : this(
            familyName,
            tenor,
            settlementDays,
            currency,
            fixingCalendar,
            convention,
            endOfMonth,
            dayCounter,
            forwardingTermStructure,
            ConventionProvider.Current)
    {
    }

    // An index keeping a given provider: a preset's, read once, or a clone's source's.
    private protected IborIndex(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency? currency,
        Calendar fixingCalendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        DayCounter dayCounter,
        YieldTermStructure? forwardingTermStructure,
        ConventionProvider provider)
        : base(familyName, tenor, settlementDays, currency, fixingCalendar, dayCounter, provider)
    {
        EnumArguments.Check(convention, nameof(convention));
        BusinessDayConvention = convention;
        EndOfMonth = endOfMonth;
        ForwardingTermStructure = forwardingTermStructure;
    }

    /// <summary>Gets the convention adjusting the maturity date.</summary>
    public BusinessDayConvention BusinessDayConvention { get; }

    /// <summary>Gets a value indicating whether the maturity date follows the end-of-month rule.</summary>
    public bool EndOfMonth { get; }

    /// <summary>Gets the curve that forecasts fixings, or <see langword="null"/> for none.</summary>
    public YieldTermStructure? ForwardingTermStructure { get; }

    /// <inheritdoc/>
    public override Date MaturityDate(Date valueDate)
    {
        Date.ThrowIfDefault(valueDate, nameof(valueDate));
        return FixingCalendar.Advance(valueDate, Tenor, BusinessDayConvention, EndOfMonth);
    }

    /// <summary>The fixing forecast from the forwarding curve: the simple rate from the value date to the maturity.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context; not read.</param>
    /// <returns>The forecast.</returns>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is not a valid fixing date.</exception>
    /// <exception cref="NanoQLException">The index has no forwarding curve, or the period has no positive length.</exception>
    public override double ForecastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date d1 = ValueDate(fixingDate);
        Date d2 = MaturityDate(d1);
        double t = DayCounter.YearFraction(d1, d2);
        if (!(t > 0.0))
        {
            throw new NanoQLException(
                $"\n cannot calculate forward rate between {d1} and {d2}:\n non positive time "
                + $"({t.ToString(System.Globalization.CultureInfo.InvariantCulture)}) using {DayCounter.Name} daycounter");
        }

        return ForecastFixing(d1, d2, t);
    }

    /// <summary>
    /// A copy of the index on another forwarding curve. As in QuantLib, the copy is a plain <see cref="IborIndex"/>
    /// with the same conventions and name, not the preset's own type.
    /// </summary>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public virtual IborIndex Clone(YieldTermStructure? forwarding) =>
        new(
            FamilyName,
            Tenor,
            FixingDays,
            Currency,
            FixingCalendar,
            BusinessDayConvention,
            EndOfMonth,
            DayCounter,
            forwarding,
            Provider);

    // Whether a coupon on the index forecasts its fixing through ForecastFixing(fixingDate, context), the index's own
    // forecast, rather than the curve-based forecast between its dates: false for an index forecasting from its
    // forwarding curve, true for one whose forecast is its own (ProxyIbor, the S-xrt plan's XRR5 (xxi)).
    internal virtual bool ForecastsThroughFixingDate => false;

    /// <summary>
    /// The forecast between given dates, for coupons that know their dates; QuantLib keeps it private to the index and
    /// its Ibor coupon, since wrong dates could ask a 6-month index for a 1-year fixing.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="t">The year fraction between them.</param>
    /// <returns>The forecast.</returns>
    /// <exception cref="NanoQLException">The index has no forwarding curve.</exception>
    internal double ForecastFixing(Date valueDate, Date endDate, double t)
    {
        YieldTermStructure curve = ForwardingTermStructure
            ?? throw new NanoQLException($"null term structure set to this instance of {Name}");
        double disc1 = curve.Discount(valueDate);
        double disc2 = curve.Discount(endDate);
        return ((disc1 / disc2) - 1.0) / t;
    }
}
