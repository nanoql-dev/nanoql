// SPDX-FileCopyrightText: Copyright (C) 2008-2016  Andrea Maggiulli (a.maggiulli@gmail.com)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: src/QLNet/Instruments/Bonds/IPrepayModel.cs @ QLNet-v1.13.2
// Ported from QLNet and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Extensions.Instruments;

/// <summary>
/// A mortgage pool's prepayment model: the conditional prepayment rate (CPR), an annual rate, and the single monthly
/// mortality (SMM), its monthly equivalent, at a date.
/// </summary>
/// <remarks>
/// The extension point for a user's own model (a vector of CPRs, a refinancing model). An implementation is immutable
/// and thread-safe, like every nanoql object: its rates depend on the date given alone. <see cref="PSACurve"/> and
/// <see cref="ConstantCPR"/> are QLNet's.
/// </remarks>
public interface IPrepayModel
{
    /// <summary>The conditional prepayment rate at a date, an annual fraction of the outstanding balance.</summary>
    /// <param name="valDate">The date.</param>
    /// <returns>The rate, a fraction in [0, 1].</returns>
    double GetCPR(Date valDate);

    /// <summary>The single monthly mortality at a date, the monthly equivalent of <see cref="GetCPR"/>.</summary>
    /// <param name="valDate">The date.</param>
    /// <returns>The rate, a fraction in [0, 1].</returns>
    double GetSMM(Date valDate);
}
