// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using QLNet;

namespace NanoQL.QLNetVectors;

/// <summary>
/// <c>tests/vectors/qlnet/Instruments/Bonds/ConstantCPR.json</c>: QLNet's <c>ConstantCPR</c> at CPRs from 0 to 1, the
/// ends of SIFMA's range included: each case's CPR and SMM on three dates.
/// </summary>
internal static class ConstantCPRVectors
{
    private static readonly double[] Rates = [0.0, 0.001, 0.06, 0.5, 1.0];

    /// <summary>Builds the file's cases.</summary>
    /// <returns>The document.</returns>
    public static VectorDocument Build()
    {
        var doc = new VectorDocument(
            "qlnet/Instruments/Bonds/ConstantCPR", "src/QLNet/Instruments/Bonds/ConstantCPR.cs");
        var dates = new[] { new Date(1, 3, 2007), new Date(31, 12, 2019), new Date(29, 2, 2036) };
        foreach (double cpr in Rates)
        {
            Obj settings = CaseSettings.Set(dates[0]);
            var model = new ConstantCPR(cpr);
            doc.Case(
                $"CPR {cpr}",
                new Obj
                {
                    ["settings"] = settings,
                    ["cpr"] = cpr,
                    ["dates"] = dates.Select(d => d.serialNumber()).ToList(),
                },
                new Obj
                {
                    ["CPR"] = dates.Select(model.getCPR).ToList(),
                    ["SMM"] = dates.Select(model.getSMM).ToList(),
                });
        }

        return doc;
    }
}
