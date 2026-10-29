// SPDX-FileCopyrightText: Copyright (C) 2011 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/cpicapfloorengines.hpp ql/experimental/inflation/cpicapfloorengines.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// Prices a CPI cap or floor off a CPI cap and floor price surface: QuantLib's <c>InterpolatingCPICapFloorEngine</c>.
/// </summary>
/// <remarks>
/// <para>
/// The surface's time axis is its quoted instruments' maturities, so the instrument's payment date moves back by the
/// difference between its observation lag and the surface's, which must be non-negative. A flat observation reads the
/// surface at the start of the effective maturity's inflation period, where the fixing can't change; a linear one
/// interpolates between that period's start and the next period's by days. The value is the surface's price, on the
/// surface's nominal, as QuantLib's. The lag difference of periods in units QuantLib's period arithmetic can't subtract,
/// years from weeks for one, is refused, QuantLib's own refusal (its <c>\TODO</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class InterpolatingCPICapFloorEngine
{
    /// <summary>Prices a CPI cap or floor.</summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="priceSurface">The price surface.</param>
    /// <returns>The results: the expired results when the instrument has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The two observation lags' units can't be subtracted, with QuantLib's
    /// text.</exception>
    /// <exception cref="NanoQLException">The lag difference is negative, with QuantLib's text, or the surface can't
    /// price.</exception>
    public static InstrumentResults Calculate(
        CPICapFloor capFloor, EvaluationContext context, CPICapFloorTermPriceSurface priceSurface)
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(priceSurface);
        if (capFloor.IsExpired(context))
        {
            return capFloor.SetupExpired();
        }

        capFloor.Validate();
        CPICapFloorArguments arguments = CPICapFloorArguments.Of(capFloor);
        double npv;

        // what is the difference between the observationLag of the surface
        // and the observationLag of the cap/floor?
        Period lagDiff = arguments.ObservationLag - priceSurface.ObservationLag;
        if (!(lagDiff >= new Period(0, TimeUnit.Months)))
        {
            throw new NanoQLException($"InterpolatingCPICapFloorEngine: lag difference must be non-negative: {lagDiff}");
        }

        // we now need an effective maturity to use in the price surface because this uses
        // maturity of calibration instruments as its time axis, N.B. this must also
        // use the roll because the surface does
        Date effectiveMaturity = arguments.PayDate - lagDiff;

        (Date start, Date end) = InflationTermStructureFunctions.InflationPeriod(effectiveMaturity, arguments.Index.Frequency);
        double priceStart = arguments.Type == OptionType.Call
            ? priceSurface.CapPrice(start, arguments.Strike)
            : priceSurface.FloorPrice(start, arguments.Strike);

        // if we use a flat index vs the interpolated one ...
        if (arguments.ObservationInterpolation == CPIInterpolationType.Flat)
        {
            // then use the price for the first day in the period because the value cannot change after then
            npv = priceStart;
        }
        else
        {
            // linear interpolation will be very close
            Date next = end + new Period(1, TimeUnit.Days);
            double priceEnd = arguments.Type == OptionType.Call
                ? priceSurface.CapPrice(next, arguments.Strike)
                : priceSurface.FloorPrice(next, arguments.Strike);

            npv = priceStart + ((priceEnd - priceStart) * (effectiveMaturity - start) / (next - start)); // can't get to next period'
        }

        return capFloor.FetchResults(new InstrumentResults { NPV = npv });
    }
}
