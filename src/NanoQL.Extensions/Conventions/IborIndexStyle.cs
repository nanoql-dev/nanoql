// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>The class a user's own Ibor index is built as.</summary>
public enum IborIndexStyle
{
    /// <summary>An <see cref="Indexes.IborIndex"/> from the record's fields.</summary>
    Ibor,

    /// <summary>A <see cref="Indexes.Libor"/>: its family, tenor, settlement days, currency, financial-centre
    /// calendar and day counter, its fixing calendar and conventions Libor's own.</summary>
    Libor,

    /// <summary>A <see cref="Indexes.CustomIborIndex"/>, with value and maturity calendars of its own.</summary>
    Custom,
}
