// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The only tests that set <see cref="ConventionProvider.Default"/>: they run alone, in parallel with no other test,
/// and restore it, so every other test runs on QuantLib's conventions.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DefaultProviderCollection
{
    /// <summary>The collection's name.</summary>
    public const string Name = "ConventionProvider.Default";
}
