// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.TestTypes;

/// <summary>
/// A user's convention provider, QuantLib's conventions throughout but no convention set: as the default, a NULL set
/// has no names to look up.
/// </summary>
public sealed class UserProvider : ConventionProvider
{
}
