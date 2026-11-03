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
/// What <see cref="HistoricalForwardRatesAnalysisFunctions.HistoricalForwardRatesAnalysis"/> leaves in QuantLib's
/// reference outputs: the dates skipped, where a fixing was missing or refused, the dates failed, where the curve
/// or a forward rate failed, each with its error's message, and the forwards' fixing periods.
/// </summary>
/// <param name="SkippedDates">The dates skipped, in order.</param>
/// <param name="SkippedDatesErrorMessage">Each skipped date's error message.</param>
/// <param name="FailedDates">The dates failed, in order.</param>
/// <param name="FailedDatesErrorMessage">Each failed date's error message.</param>
/// <param name="FixingPeriods">The periods from each date to the forwards' fixings.</param>
public sealed record HistoricalForwardRatesAnalysisResults(
    ImmutableArray<Date> SkippedDates,
    ImmutableArray<string> SkippedDatesErrorMessage,
    ImmutableArray<Date> FailedDates,
    ImmutableArray<string> FailedDatesErrorMessage,
    ImmutableArray<Period> FixingPeriods);
