// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/coupons/proxyibor.hpp ql/experimental/coupons/proxyibor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// An Ibor index whose forecast is another index's fixing, geared and spread: QuantLib's <c>ProxyIbor</c>.
/// </summary>
/// <remarks>
/// <para>
/// The index has no forwarding curve of its own: its forecast at a fixing date is the gearing times the proxied index's
/// fixing there, plus the spread. QuantLib's multiplies by the spread, gearing × proxied × spread, 5.96e-5 where a
/// gearing of 2 and a spread of 0.001 on a 2.98% fixing mean 0.0606 (the S-xrt plan's XRR5 (xxi);
/// <c>Divergences/ProxyIborTests.cs</c>). A coupon on it forecasts through this forecast; QuantLib's coupons forecast
/// from the index's forwarding curve between their dates, which it lacks ("null term structure set to this instance of
/// …"), so they can't price.
/// </para>
/// <para>
/// <see cref="IborIndex.Clone"/> is QuantLib's inherited one: a plain Ibor index with the proxy's conventions on the
/// curve given (<c>iborindex.cpp:61-73</c>), which QuantLib's rate helpers rely on when they clone every index onto the
/// curve they build; such a clone forecasts from that curve, not from the proxied index.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class ProxyIbor : IborIndex
{
    private readonly double gearing;
    private readonly IborIndex iborIndex;
    private readonly double spread;

    /// <summary>Initializes a new instance of the <see cref="ProxyIbor"/> class.</summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="fixingCalendar">The fixing calendar.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the end-of-month rule applies.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="gearing">The gearing of the proxied index's fixing.</param>
    /// <param name="iborIndex">The proxied index.</param>
    /// <param name="spread">The spread added to the geared fixing.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="convention"/> isn't a member of its enum.</exception>
    public ProxyIbor(
        string familyName,
        Period tenor,
        int settlementDays,
        Currency currency,
        Calendar fixingCalendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        DayCounter dayCounter,
        double gearing,
        IborIndex iborIndex,
        double spread)
        : base(familyName, tenor, settlementDays, currency, fixingCalendar, convention, endOfMonth, dayCounter)
    {
        this.gearing = gearing;
        this.iborIndex = iborIndex ?? throw new ArgumentNullException(nameof(iborIndex));
        this.spread = spread;
    }

    /// <inheritdoc/>
    /// <remarks>The gearing times the proxied index's fixing at the date, plus the spread (XRR5 (xxi)).</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The proxied index can't give its fixing at the date.</exception>
    public override double ForecastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double proxy = iborIndex.Fixing(fixingDate, context);
        return (gearing * proxy) + spread;
    }

    /// <inheritdoc/>
    internal override bool ForecastsThroughFixingDate => true;
}
