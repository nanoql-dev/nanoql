// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/genericindexes.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;

namespace NanoQL.Experimental;

/// <summary>A generic geographical or economic region: name "Generic", code "GENERIC".</summary>
[Experimental("NQL0001")]
public sealed class GenericRegion : Region
{
    /// <summary>Initializes a new instance of the <see cref="GenericRegion"/> class.</summary>
    public GenericRegion()
        : base("Generic", "GENERIC")
    {
    }
}
