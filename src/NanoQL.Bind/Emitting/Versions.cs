// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Emitting;

/// <summary>
/// The release the generated surface belongs to: major and minor, a patch only for a fix (2.1.1); the ABI's version is
/// the major times 1,000 plus the minor, which a fix keeps (docs/bindings.md, "Versions").
/// </summary>
internal static class Versions
{
    /// <summary>The major version.</summary>
    public const int Major = 2;

    /// <summary>The minor version.</summary>
    public const int Minor = 1;

    /// <summary>The patch, 0 but for a fix.</summary>
    public const int Patch = 0;

    /// <summary>Gets the version as its text: two parts, three for a fix.</summary>
    public static string Text => Patch == 0 ? $"{Major}.{Minor}" : $"{Major}.{Minor}.{Patch}";

    /// <summary>Gets the ABI's version.</summary>
    public static int Abi => (Major * 1000) + Minor;
}
