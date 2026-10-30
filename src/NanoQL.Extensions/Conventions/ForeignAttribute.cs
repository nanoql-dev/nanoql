// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>An attribute another tool put on a record or a root, in its own namespace, kept as read.</summary>
/// <param name="Namespace">The attribute's namespace.</param>
/// <param name="LocalName">Its local name.</param>
/// <param name="Prefix">The prefix it was written with.</param>
/// <param name="Value">Its value.</param>
public sealed record ForeignAttribute(string Namespace, string LocalName, string Prefix, string Value);
