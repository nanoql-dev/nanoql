// SPDX-FileCopyrightText: Copyright (C) 2004 Jeff Yu
// SPDX-FileCopyrightText: Copyright (C) 2004 M-Dimension Consulting Inc.
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2022 Oleg Kulkov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bond.hpp ql/instruments/bond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>Whether a bond price includes the accrued amount: QuantLib's <c>Bond::Price::Type</c>.</summary>
/// <remarks>QuantLib has no stream operator for it, so the member names are its only text.</remarks>
public enum BondPriceType
{
    /// <summary>The price including the accrued amount.</summary>
    Dirty = 0,

    /// <summary>The price excluding the accrued amount.</summary>
    Clean = 1,
}
