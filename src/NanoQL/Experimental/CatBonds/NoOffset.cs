// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/riskynotional.hpp ql/experimental/catbonds/riskynotional.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>No offset: a notional reduction is paid on its event's date.</summary>
[Experimental("NQL0001")]
public sealed class NoOffset : EventPaymentOffset
{
    /// <inheritdoc/>
    public override Date PaymentDate(Date eventDate) => eventDate;
}
