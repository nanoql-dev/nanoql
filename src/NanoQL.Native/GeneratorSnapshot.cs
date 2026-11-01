// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Native;

/// <summary>
/// A generator as the C ABI crosses it: an immutable snapshot holding a private copy of the generator, never drawn from
/// itself. A batch of draws copies it, draws, and returns the copy as the next snapshot, so the same snapshot always
/// gives the same draws and any thread may draw from it.
/// </summary>
/// <param name="generator">The private copy.</param>
internal sealed class GeneratorSnapshot(object generator)
{
    /// <summary>Gets the private copy.</summary>
    public object Generator { get; } = generator;
}
