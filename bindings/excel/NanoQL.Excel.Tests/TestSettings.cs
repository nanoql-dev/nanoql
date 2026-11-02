// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel.Tests;

/// <summary>
/// The runtime is one process's: its store, its host and the default conventions are shared, so every test class is in
/// this collection, which runs alone and one test at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TestSettings
{
    /// <summary>The collection's name.</summary>
    public const string Name = "The Excel runtime";
}
