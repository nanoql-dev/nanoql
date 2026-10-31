// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace NanoQL.QLNetVectors;

/// <summary>
/// QLNet's vector generator: <c>QLNetVectors &lt;subcommand&gt; &lt;output root&gt;</c> writes one vector file under
/// the output root, at its path under <c>tests/vectors</c>. Its shim, <c>tools/vectors/gen/qlnet/&lt;QLNet
/// path&gt;.py</c>, runs it (docs/porting-playbook.md §7.2).
/// </summary>
internal static class Program
{
    private static readonly Dictionary<string, Func<VectorDocument>> Subcommands = new()
    {
        ["psacurve"] = PSACurveVectors.Build,
        ["constantcpr"] = ConstantCPRVectors.Build,
        ["mbsfixedratebond"] = MBSFixedRateBondVectors.Build,
        ["amortizingbond"] = AmortizingBondVectors.Build,
    };

    private static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length != 2 || !Subcommands.TryGetValue(args[0], out Func<VectorDocument>? build))
        {
            Console.Error.WriteLine(
                $"usage: QLNetVectors <subcommand> <output root>; subcommands: {string.Join(", ", Subcommands.Keys)}");
            return 2;
        }

        build().Write(args[1]);
        return 0;
    }
}
