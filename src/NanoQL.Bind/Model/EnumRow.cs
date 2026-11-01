// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Model;

/// <summary>An enum of the surface: its C name and its members' values, as <c>int32_t</c>.</summary>
/// <param name="Type">The enum.</param>
/// <param name="Prefix">Its assembly's prefix.</param>
/// <param name="CName">Its C name.</param>
/// <param name="Members">Its members' names and values, in declaration order.</param>
/// <param name="Experimental">Whether it is experimental.</param>
/// <param name="Flags">Whether it is a flags enum.</param>
internal sealed record EnumRow(Type Type, string Prefix, string CName, IReadOnlyList<(string Name, long Value)> Members, bool Experimental, bool Flags);
