// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's discretized asset vectors, <c>tests/vectors/cxx/discretizedasset.json</c>:
/// <see cref="DiscretizedDiscountBond"/> and <see cref="DiscretizedOption"/> over one, European, Bermudan and American,
/// initialized on Hull-White's fitted <see cref="OneFactorModelShortRateTree"/> (<see cref="ModelInputs"/>) and rolled
/// back in turn: after each <see cref="DiscretizedAsset.Rollback"/> and <see cref="DiscretizedAsset.PartialRollback"/>
/// the asset's <see cref="DiscretizedAsset.Time"/> and <see cref="DiscretizedAsset.Values"/>, and its bond's, after
/// each rollback its <see cref="DiscretizedAsset.PresentValue"/>, and its
/// <see cref="DiscretizedAsset.MandatoryTimes"/>; the lattice's <see cref="Methods.TreeLattice.StatePrices"/> and
/// <see cref="Methods.TreeLattice1D.Grid(double)"/>, one lattice with a spread
/// (<see cref="OneFactorModelShortRateTree.SetSpread"/>); then the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscretizedAssetCxxVectorTests
{
    [Fact]
    public void DiscretizedAssetCxxVector()
    {
        var file = VectorFile.Load("cxx/discretizedasset");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement latticeSpec = c.Input("lattice");
            OneFactorModelShortRateTree lattice = ModelInputs.LatticeOf(latticeSpec);
            if (c.Inputs.TryGetProperty("steps", out JsonElement steps))
            {
                if (c.Expected.TryGetProperty("statePrices", out _))
                {
                    check.Output(c, "statePrices", steps.EnumerateArray()
                        .Select(i => (object?)lattice.StatePrices(i.GetInt32()))
                        .ToArray());
                }

                check.Output(c, "grid", Numbers(c.Input("gridTimes"))
                    .Select(t => (object?)(Func<object?>)(() => ModelInputs.Elements(lattice.Grid(t))))
                    .ToArray());
                continue;
            }

            JsonElement asset = c.Input("asset");
            double t0 = c.Double("t0");
            double[] times = Numbers(c.Input("times"));
            if (c.Expected.TryGetProperty("rollback", out _))
            {
                check.Output(c, "rollback", () => Rolled(latticeSpec, lattice, asset, t0, times, partial: false));
                continue;
            }

            CheckRolled(check, c, "rollback", Rolled(latticeSpec, lattice, asset, t0, times, partial: false));
            CheckRolled(check, c, "partialRollback", Rolled(latticeSpec, lattice, asset, t0, times, partial: true));
            Built pv = Initialized(latticeSpec, lattice, asset, t0);
            check.Output(c, "presentValue", times.Select(t =>
            {
                pv.Asset.Rollback(t);
                return (object?)pv.Asset.PresentValue();
            }).ToArray());
            check.Output(c, "mandatoryTimes", AssetOf(asset).Asset.MandatoryTimes());
        }

        check.Complete();
    }

    private static void CheckRolled(VectorAssert check, VectorCase c, string method, Rollbacks r)
    {
        check.Output(c, method + "Times", r.Times);
        check.Output(c, method + "Values", r.Values);
        if (r.UnderlyingTimes is not null)
        {
            check.Output(c, method + "UnderlyingTimes", r.UnderlyingTimes);
            check.Output(c, method + "UnderlyingValues", r.UnderlyingValues);
        }
    }

    // The harness's asset spec, its option over a DiscretizedDiscountBond, unconnected to any lattice.
    private static Built AssetOf(JsonElement spec)
    {
        if (spec.GetProperty("type").GetString() == "DiscretizedDiscountBond")
        {
            return new Built(new DiscretizedDiscountBond(), null);
        }

        var bond = new DiscretizedDiscountBond();
        var option = new DiscretizedOption(
            bond,
            (ExerciseType)spec.GetProperty("exerciseType").GetInt32(),
            Numbers(spec.GetProperty("exerciseTimes")));
        return new Built(option, bond);
    }

    // The asset initialized at t0, after its bond: on the lattice, on a second tree from the same spec, or never.
    private static Built Initialized(
        JsonElement latticeSpec, OneFactorModelShortRateTree lattice, JsonElement spec, double t0)
    {
        Built b = AssetOf(spec);
        if (b.Underlying is not null && spec.GetProperty("underlyingTime") is { ValueKind: not JsonValueKind.Null } at)
        {
            Lattice bondLattice =
                spec.GetProperty("otherLattice").GetBoolean() ? ModelInputs.LatticeOf(latticeSpec) : lattice;
            b.Underlying.Initialize(bondLattice, Number(at));
        }

        b.Asset.Initialize(lattice, t0);
        return b;
    }

    // After each rollback, the asset's time and values, and its bond's.
    private static Rollbacks Rolled(
        JsonElement latticeSpec,
        OneFactorModelShortRateTree lattice,
        JsonElement spec,
        double t0,
        double[] times,
        bool partial)
    {
        Built b = Initialized(latticeSpec, lattice, spec, t0);
        var r = new Rollbacks(b.Underlying is not null);
        foreach (double t in times)
        {
            if (partial)
            {
                b.Asset.PartialRollback(t);
            }
            else
            {
                b.Asset.Rollback(t);
            }

            r.Times.Add(b.Asset.Time);
            r.Values.Add(b.Asset.Values.ToArray().Select(x => (object?)x).ToArray());
            if (b.Underlying is not null)
            {
                r.UnderlyingTimes!.Add(b.Underlying.Time);
                r.UnderlyingValues!.Add(b.Underlying.Values.ToArray().Select(x => (object?)x).ToArray());
            }
        }

        return r;
    }

    private sealed record Built(DiscretizedAsset Asset, DiscretizedDiscountBond? Underlying);

    private sealed class Rollbacks(bool option)
    {
        public List<object?> Times { get; } = [];

        public List<object?> Values { get; } = [];

        public List<object?>? UnderlyingTimes { get; } = option ? [] : null;

        public List<object?>? UnderlyingValues { get; } = option ? [] : null;
    }
}
