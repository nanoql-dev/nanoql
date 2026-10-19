// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/event.hpp ql/event.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL;

/// <summary>
/// An event on a date and nothing more: QuantLib's <c>detail::simple_event</c>, which an instrument builds to ask
/// whether a date of its own has occurred (a swaption's last exercise date).
/// </summary>
internal sealed class SimpleEvent : Event
{
    private readonly Date date;

    /// <summary>Initializes a new instance of the <see cref="SimpleEvent"/> class.</summary>
    /// <param name="date">The event's date.</param>
    internal SimpleEvent(Date date)
    {
        this.date = date;
    }

    /// <inheritdoc/>
    public override Date Date => date;
}
