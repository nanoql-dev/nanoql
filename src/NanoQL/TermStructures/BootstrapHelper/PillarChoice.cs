// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/bootstraphelper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>The alternative ways of determining a bootstrap helper's pillar date: QuantLib's <c>Pillar::Choice</c>.</summary>
/// <remarks>
/// QuantLib's stream operator writes "MaturityPillarDate", "LastRelevantPillarDate" and "CustomPillarDate", which
/// differ from the member names: <see cref="BootstrapHelperFunctions.ToString(PillarChoice)"/> gives them.
/// </remarks>
public enum PillarChoice
{
    /// <summary>The instrument's maturity date.</summary>
    MaturityDate = 0,

    /// <summary>The last date relevant for the instrument's pricing.</summary>
    LastRelevantDate = 1,

    /// <summary>A date chosen by the user.</summary>
    CustomDate = 2,
}
