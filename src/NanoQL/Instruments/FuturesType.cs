// SPDX-FileCopyrightText: Copyright (C) 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Maddalena Zanzi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/futures.hpp ql/instruments/futures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The kinds of futures, by the rule their dates follow: QuantLib's <c>Futures::Type</c>.</summary>
/// <remarks>
/// QuantLib's stream operator writes each member's name, so the enum's own text is QuantLib's; for a value outside the
/// members it writes "Unknown(n)", which the refusals that embed the type build themselves.
/// </remarks>
public enum FuturesType
{
    /// <summary>Chicago Mercantile International Money Market: the third Wednesday of March, June, September and December.</summary>
    IMM = 0,

    /// <summary>Australian Securities Exchange: the second Friday of March, June, September and December.</summary>
    ASX = 1,

    /// <summary>Other rules.</summary>
    Custom = 2,
}
