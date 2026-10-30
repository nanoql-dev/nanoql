// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

// The names records take: non-empty, without leading or trailing white space. A user's own name may not hold '/',
// which the generated names of ConventionRecords.Describe use (name/n) and FpML's codes (ACT/360) hold.
internal static class ConventionNames
{
    public static string Checked(string name, string paramName)
    {
        ArgumentNullException.ThrowIfNull(name, paramName);
        if (name.Length == 0 || char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1]))
        {
            throw new ArgumentException($"\"{name}\" is not a name: empty, or with leading or trailing white space", paramName);
        }

        return name;
    }
}
