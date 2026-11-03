// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>A generator's snapshot behind a handle: the generator, never drawn from itself.</summary>
/// <param name="generator">The generator.</param>
public sealed class XlSnapshot(object generator)
{
    /// <summary>Gets the generator.</summary>
    public object Generator { get; } = generator;
}
