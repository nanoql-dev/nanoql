// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Reading;

/// <summary>An assembly the bindings cross, and the prefix of its C names (<c>nql</c> for nanoql's own).</summary>
/// <param name="Prefix">The C names' prefix, without the underscore.</param>
/// <param name="Path">The built assembly's path.</param>
internal sealed record AssemblyInput(string Prefix, string Path)
{
    /// <summary>Reads <c>prefix=path</c>.</summary>
    /// <param name="text">The argument.</param>
    /// <returns>The input.</returns>
    public static AssemblyInput Parse(string text)
    {
        int eq = text.IndexOf('=', StringComparison.Ordinal);
        if (eq <= 0 || eq == text.Length - 1)
        {
            throw new ArgumentException($"an assembly is given as <prefix>=<path>, not '{text}'", nameof(text));
        }

        string prefix = text[..eq];
        if (!prefix.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9')) || !char.IsAsciiLetterLower(prefix[0]))
        {
            throw new ArgumentException($"a prefix is lower-case letters and digits, starting with a letter: '{prefix}'", nameof(text));
        }

        return new AssemblyInput(prefix, System.IO.Path.GetFullPath(text[(eq + 1)..]));
    }
}
