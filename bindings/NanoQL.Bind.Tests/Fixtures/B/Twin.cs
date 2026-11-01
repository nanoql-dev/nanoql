// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.B;

/// <summary>
/// One of two types sharing a simple name across namespaces: each takes its namespace's last segment, unless a
/// release named one, which keeps its name while the other takes the qualified one.
/// </summary>
public sealed class Twin
{
    /// <summary>Gets a value.</summary>
    public double Value => 1.0;
}
