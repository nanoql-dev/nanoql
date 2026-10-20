// SPDX-FileCopyrightText: Copyright (C) 2008 Piero Del Boca
// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2015 Bernd Lewerenz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/seasonality.hpp ql/termstructures/inflation/seasonality.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A seasonality: a correction of an inflation term structure's zero and year-on-year rates for seasonal effects in the
/// price index.
/// </summary>
/// <remarks>
/// Immutable, and an extension point. QuantLib implements only the multiplicative price seasonality
/// (<see cref="MultiplicativePriceSeasonality"/>); an additive one isn't implemented.
/// </remarks>
public abstract class Seasonality
{
    /// <summary>A zero-coupon inflation rate corrected for seasonality.</summary>
    /// <param name="d">The date the rate is for.</param>
    /// <param name="r">The uncorrected rate.</param>
    /// <param name="iTS">The term structure the rate is read from.</param>
    /// <returns>The corrected rate.</returns>
    public abstract double CorrectZeroRate(Date d, double r, InflationTermStructure iTS);

    /// <summary>A year-on-year inflation rate corrected for seasonality.</summary>
    /// <param name="d">The date the rate is for.</param>
    /// <param name="r">The uncorrected rate.</param>
    /// <param name="iTS">The term structure the rate is read from.</param>
    /// <returns>The corrected rate.</returns>
    public abstract double CorrectYoYRate(Date d, double r, InflationTermStructure iTS);

    /// <summary>
    /// Whether the seasonality is consistent with a term structure: a multi-year seasonality may not be, and an
    /// implementation may refuse it.
    /// </summary>
    /// <param name="iTS">The term structure.</param>
    /// <returns><see langword="true"/>; a seasonality that can be inconsistent overrides it.</returns>
    public virtual bool IsConsistent(InflationTermStructure iTS) => true;
}
