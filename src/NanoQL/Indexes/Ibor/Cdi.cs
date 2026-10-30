// SPDX-FileCopyrightText: Copyright (C) 2025 Sotirios Papathanasopoulos
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/cdi.hpp ql/indexes/ibor/cdi.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The BRL-CDI index, relevant for https://en.wikipedia.org/wiki/Brazilian_Swap.</summary>
/// <remarks>
/// <para>
/// CDI quotes an annually compounded rate on Business/252: the forecast is <c>(P(s)/P(e))^(1/τ) − 1</c>, and a coupon
/// grows a fixing over a span τ by <c>(1 + r)^τ</c> (<see cref="GrowthFactor"/>), CDI's daily factor, and an averaged
/// coupon accrues <c>(1 + r)^τ − 1</c> (<see cref="SimpleAccrual"/>), where QuantLib's overnight pricers grow it as a
/// simple rate, so that a coupon's amount depends on the route taken: a documented divergence.
/// </para>
/// <para>
/// <see cref="Clone"/> returns a <see cref="Cdi"/>, where QuantLib's returns a plain overnight index, which forecasts
/// by the generic formula: a documented divergence.
/// </para>
/// </remarks>
public sealed class Cdi : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Cdi"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Cdi(YieldTermStructure? h = null)
        : base("CDI", 0, new BRLCurrency(), new Brazil(BrazilMarket.Settlement), new Business252(), h)
    {
    }

    // A clone: the source's conventions and provider, on another forwarding curve.
    private Cdi(Cdi source, YieldTermStructure? h)
        : base(
            source.FamilyName, source.FixingDays, source.Currency, source.FixingCalendar, source.DayCounter, h, source.Provider)
    {
    }

    /// <summary>
    /// The fixing forecast from the forwarding curve: the annually compounded rate from the value date to the maturity
    /// on Business/252.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context; not read.</param>
    /// <returns>The forecast.</returns>
    /// <exception cref="ArgumentException"><paramref name="fixingDate"/> is not a valid fixing date.</exception>
    /// <exception cref="NanoQLException">The period has no positive length, or the index has no forwarding
    /// curve.</exception>
    public override double ForecastFixing(Date fixingDate, EvaluationContext context)
    {
        // Reference: Zine-eddine, Arroub. "OpenGamma Quantitative research Brazilian Swaps", London, December 2013.
        // paragraph 5
        ArgumentNullException.ThrowIfNull(context);
        Date startDate = ValueDate(fixingDate);
        Date endDate = MaturityDate(startDate);
        double yf = DayCounter.YearFraction(startDate, endDate);
        if (!(yf > 0.0))
        {
            throw new NanoQLException(
                $"year fraction ({yf.ToString(System.Globalization.CultureInfo.InvariantCulture)}) must be positive");
        }

        YieldTermStructure curve = ForwardingTermStructure
            ?? throw new NanoQLException($"null term structure set to this instance of {Name}");
        double discountStart = curve.Discount(startDate);
        double discountEnd = curve.Discount(endDate);
        return Math.Pow(discountStart / discountEnd, 1.0 / yf) - 1.0;
    }

    /// <summary>The factor a fixing grows a unit by over a span: <c>(1 + fixing)^span</c>, CDI's daily factor.</summary>
    /// <param name="fixing">The fixing, an annually compounded rate.</param>
    /// <param name="span">The span, as a year fraction on Business/252.</param>
    /// <returns>The growth factor.</returns>
    public override double GrowthFactor(double fixing, double span) => Math.Pow(1.0 + fixing, span);

    /// <summary>The interest a fixing accrues per unit over a span: <c>(1 + fixing)^span − 1</c>, its growth.</summary>
    /// <param name="fixing">The fixing, an annually compounded rate.</param>
    /// <param name="span">The span, as a year fraction on Business/252.</param>
    /// <returns>The accrual.</returns>
    public override double SimpleAccrual(double fixing, double span) => GrowthFactor(fixing, span) - 1.0;

    /// <summary>A copy of the index on another forwarding curve: a <see cref="Cdi"/>.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public override OvernightIndex Clone(YieldTermStructure? h) => new Cdi(this, h);
}
