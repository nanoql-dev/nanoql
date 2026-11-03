// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/historicalforwardratesanalysis.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The outcome of a statistical analysis of historical forward rates: the dates skipped and failed, their messages,
/// and the forwards' fixing periods.
/// </summary>
/// <remarks>An extension point, abstract as QuantLib's; immutable once built.</remarks>
public abstract class HistoricalForwardRatesAnalysis
{
    /// <summary>Gets the dates skipped, where a fixing was missing or refused.</summary>
    public abstract ImmutableArray<Date> SkippedDates { get; }

    /// <summary>Gets each skipped date's error message.</summary>
    public abstract ImmutableArray<string> SkippedDatesErrorMessage { get; }

    /// <summary>Gets the dates failed, where the curve or a forward rate failed.</summary>
    public abstract ImmutableArray<Date> FailedDates { get; }

    /// <summary>Gets each failed date's error message.</summary>
    public abstract ImmutableArray<string> FailedDatesErrorMessage { get; }

    /// <summary>Gets the periods from each date to the forwards' fixings.</summary>
    public abstract ImmutableArray<Period> FixingPeriods { get; }
}
