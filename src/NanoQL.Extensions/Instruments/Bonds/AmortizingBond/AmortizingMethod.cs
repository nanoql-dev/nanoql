// SPDX-FileCopyrightText: Copyright (C) 2008-2013 Andrea Maggiulli (a.maggiulli@gmail.com)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: src/QLNet/Instruments/Bonds/AmortizingBond.cs @ QLNet-v1.13.2
// Ported from QLNet and modified by the nanoql Authors.

namespace NanoQL.Extensions.Instruments;

/// <summary>How an <see cref="AmortizingBond"/> amortises the difference between its cost and its face
/// amount.</summary>
public enum AmortizingMethod
{
    /// <summary>The effective interest method: interest at the effective rate on the carrying amount.</summary>
    EffectiveInterestRate = 0,
}
