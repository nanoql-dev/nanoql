// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Model;

/// <summary>A member, or a whole type, that doesn't cross, and why: a line of the coverage report.</summary>
/// <param name="Key">The member's metadata key, or the type's name.</param>
/// <param name="Display">The member as a reader names it: <c>Type.Member</c>.</param>
/// <param name="Reason">Why it doesn't cross.</param>
internal sealed record SkipRow(string Key, string Display, string Reason);
