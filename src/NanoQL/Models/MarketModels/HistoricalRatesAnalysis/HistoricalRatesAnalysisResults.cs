// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/historicalratesanalysis.hpp ql/models/marketmodels/historicalratesanalysis.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// What <see cref="HistoricalRatesAnalysisFunctions.HistoricalRatesAnalysis"/> leaves in QuantLib's reference
/// outputs: the dates skipped, where a fixing was missing or refused, each with its error's message (S2R5).
/// </summary>
/// <param name="SkippedDates">The dates skipped, in order.</param>
/// <param name="SkippedDatesErrorMessage">Each skipped date's error message.</param>
public sealed record HistoricalRatesAnalysisResults(
    ImmutableArray<Date> SkippedDates, ImmutableArray<string> SkippedDatesErrorMessage);
