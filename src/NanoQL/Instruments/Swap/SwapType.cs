// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/swap.hpp ql/instruments/swap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The type of a two-leg swap, usually defined with respect to the leg paying a fixed rate; derived swaps document any
/// exception to the rule.
/// </summary>
public enum SwapType
{
    /// <summary>Receives the fixed rate.</summary>
    Receiver = -1,

    /// <summary>Pays the fixed rate.</summary>
    Payer = 1,
}
