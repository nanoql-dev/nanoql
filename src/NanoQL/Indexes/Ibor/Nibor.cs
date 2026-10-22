// SPDX-FileCopyrightText: Copyright (C) 2026 SoftSolution srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/nibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The NOK-NIBOR index: the NOK-NIBOR rate published by Oslo Boers (http://www.oslobors.no/ob_eng).</summary>
/// <remarks>
/// See https://nore-benchmarks.com/,
/// https://finansfag.no/wp-content/uploads/2024/10/Rentekonvensjon-6.0_engelsk_oppdatert-18.09.2024_final.pdf and
/// https://papers.ssrn.com/sol3/papers.cfm?abstract_id=5099269. To do: check roll convention and end of month.
/// </remarks>
public sealed class Nibor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Nibor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Nibor(Period tenor, YieldTermStructure? h = null)
        : base(
            "NOK-NIBOR",
            tenor,
            2,
            new NOKCurrency(),
            new Norway(),
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual360(),
            h)
    {
    }
}
