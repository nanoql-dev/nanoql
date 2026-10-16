// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/event.hpp ql/event.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: detail::simple_event, which only experimental code uses

using NanoQL.Time;

namespace NanoQL;

/// <summary>The base class of events associated with a given date.</summary>
public abstract class Event
{
    /// <summary>Gets the date at which the event occurs.</summary>
    public abstract Date Date { get; }

    /// <summary>Whether the event has already occurred before a date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="refDate">The reference date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="includeRefDate">
    /// Whether an event on the reference date counts as not yet occurred; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>. If
    /// <see langword="true"/>, an event on the reference date has not occurred.
    /// </param>
    /// <returns><see langword="true"/> when the event has occurred.</returns>
    public virtual bool HasOccurred(EvaluationContext context, Date? refDate = null, bool? includeRefDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(refDate, nameof(refDate));
        Date reference = refDate ?? context.EvaluationDate;
        bool includeRefDateEvent = includeRefDate ?? context.IncludeReferenceDateEvents;
        return includeRefDateEvent ? Date < reference : Date <= reference;
    }
}
