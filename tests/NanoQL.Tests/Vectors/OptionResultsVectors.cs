// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The vanilla engines' vector files on their shared grid (tools/vectors/gen/pricingengines/vanilla/
/// baroneadesiwhaleyengine.py and the generators that load it): each case a process, an exercise and payoffs, and the
/// NPV and whichever of the eleven Greeks it records, one element per payoff.
/// </summary>
internal static class OptionResultsVectors
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Results =
    [
        ("NPV", r => r.NPV), ("delta", r => r.Delta), ("deltaForward", r => r.DeltaForward),
        ("elasticity", r => r.Elasticity), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
        ("thetaPerDay", r => r.ThetaPerDay), ("vega", r => r.Vega), ("rho", r => r.Rho),
        ("dividendRho", r => r.DividendRho), ("strikeSensitivity", r => r.StrikeSensitivity),
        ("itmCashProbability", r => r.ItmCashProbability),
    ];

    /// <summary>Compares a file's outputs with the engine's, each priced once per payoff.</summary>
    /// <param name="path">The file, relative to tests/vectors without its extension.</param>
    /// <param name="engine">Prices an option of a case on its process.</param>
    public static void Check(
        string path,
        Func<VectorCase, VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            Exercise exercise = ExerciseOf(c.Input("exercise"), context);
            Func<OneAssetOptionResults>[] priced = c.Input("payoffs").EnumerateArray()
                .Select(p =>
                {
                    var option = new VanillaOption(PayoffOf(p), exercise);
                    var results = new Lazy<OneAssetOptionResults>(() => engine(c, option, context, process));
                    return (Func<OneAssetOptionResults>)(() => results.Value);
                })
                .ToArray();
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in Results)
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, priced.Select(f => Lazy(() => get(f()))).ToArray());
                }
            }
        }

        check.Complete();
    }
}
