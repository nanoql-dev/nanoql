// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>The value kinds of convention records, each with names of its own.</summary>
public enum ConventionKind
{
    /// <summary>Calendars: presets, named calendars, joint and bespoke calendars.</summary>
    Calendar,

    /// <summary>Day counters: presets and named day counters.</summary>
    DayCounter,

    /// <summary>Currencies: presets.</summary>
    Currency,

    /// <summary>Regions: presets and regions of a user's own.</summary>
    Region,

    /// <summary>Indexes: Ibor, overnight, swap and inflation indexes and Ibor families, which share one name space.</summary>
    Index,

    /// <summary>Schedule conventions.</summary>
    ScheduleConvention,

    /// <summary>Swap conventions, and the swap builder's defaults by currency.</summary>
    SwapConvention,

    /// <summary>Overnight-indexed swap conventions, and the OIS builder's defaults.</summary>
    OisConvention,

    /// <summary>Credit default swap conventions, and the CDS builder's defaults.</summary>
    CdsConvention,

    /// <summary>Zero-coupon inflation swap conventions.</summary>
    ZeroCouponInflationSwapConvention,

    /// <summary>Year-on-year inflation swap conventions.</summary>
    YearOnYearInflationSwapConvention,

    /// <summary>Cross-currency basis swap conventions.</summary>
    CrossCurrencyBasisSwapConvention,

    /// <summary>Cross-currency fixed-against-floating swap conventions.</summary>
    CrossCurrencySwapConvention,

    /// <summary>FX swap and forward conventions.</summary>
    FxSwapConvention,

    /// <summary>Cap and floor conventions, and the cap builder's defaults.</summary>
    CapFloorConvention,

    /// <summary>Swaption conventions, and the swaption builder's defaults.</summary>
    SwaptionConvention,

    /// <summary>CMS swap conventions, and the CMS builder's defaults.</summary>
    CmsConvention,

    /// <summary>Multiple-resets swap conventions, and their builder's defaults.</summary>
    MultipleResetsSwapConvention,

    /// <summary>Year-on-year inflation cap and floor conventions, and their builder's defaults.</summary>
    YoYInflationCapFloorConvention,

    /// <summary>Bond conventions, the defaults of a set's bonds and the bond presets.</summary>
    BondConvention,

    /// <summary>Aliases, each in its base's kind.</summary>
    Alias,

    /// <summary>A member of one of the core's enumerations, which an alias may name.</summary>
    Enumeration,
}
