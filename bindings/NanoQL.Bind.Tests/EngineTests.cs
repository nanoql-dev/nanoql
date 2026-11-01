// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind.Tests;

/// <summary>
/// Bound engines (docs/bindings.md, "Bound engines"): a Bind per engine input set, each input optional; the pairs a
/// member's supplied arguments form, placed by role; the Black callable engines refused on the OAS members; a helper's
/// trial model by the roles table's names every time, so a user's calibrated curve leaves nanoql's pairs as they are;
/// the generator's stops.
/// </summary>
public class EngineTests
{
    private static int Formed(Generation g, string member) =>
        g.Surface.EnginePairs.Count(p => p.Formed && EngineBinder.MemberName(p.Member) == member);

    [Fact]
    public void EachInputIsOptionalInBind()
    {
        Assert.Equal(
            "NQL_API nql_status nql_DiscountingSwapEngine_Bind(nql_handle discountCurve, const bool* includeSettlementDateFlows, nql_date settlementDate, nql_date npvDate, nql_handle* result);",
            Surfaces.Declaration(Surfaces.Nanoql, "nql_DiscountingSwapEngine_Bind"));
        Assert.NotNull(Surfaces.Declaration(Surfaces.Nanoql, "nql_DiscountingSwapEngine_BindOn"));
    }

    [Fact]
    public void TheHelpersPairsFormAsMeasured()
    {
        Generation g = Surfaces.Nanoql;
        Assert.Equal(35, Formed(g, "NanoQL.Models.HestonModelHelper.SetPricingEngine"));
        Assert.Equal(9, Formed(g, "NanoQL.Models.SwaptionHelper.SetPricingEngine"));
        Assert.Equal(5, Formed(g, "NanoQL.Models.CapHelper.SetPricingEngine"));
    }

    [Fact]
    public void TheBlackCallableEnginesAreRefusedOnTheOasMembers()
    {
        EnginePair[] refused = [.. Surfaces.Nanoql.Surface.EnginePairs.Where(p => p.Refused)];
        Assert.Equal(8, refused.Length);
        Assert.All(refused, p => Assert.Contains("BlackCallable", p.Set.Engine.Name, StringComparison.Ordinal));
        Assert.All(refused, p => Assert.Contains("'spread'", p.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void TheOasSpreadGoesIntoTheTreeEnginesSpreadByName()
    {
        EnginePair tree = Surfaces.Nanoql.Surface.EnginePairs.First(p => p.Formed && p.Set.Engine.Name == "TreeCallableFixedRateBondEngine"
            && EngineBinder.MemberName(p.Member) == "NanoQL.Experimental.CallableBond.OAS");
        Assert.Equal(["Double by name into spread"], tree.How);
    }

    [Fact]
    public void AUsersCalibratedCurveLeavesNanoqlsHelperPairsAsTheyAre()
    {
        Generation without = Surfaces.Nanoql, with = Surfaces.WithFixtures;
        foreach (string helper in new[] { "HestonModelHelper", "SwaptionHelper", "CapHelper" })
        {
            Assert.Equal(Formed(without, "NanoQL.Models." + helper + ".SetPricingEngine"), Formed(with, "NanoQL.Models." + helper + ".SetPricingEngine"));
        }

        // The calculations whose one parameter a calibrated model can be is a curve: no pair without the user's curve,
        // refused for want of a named parameter with it, never formed.
        int refusedWithout = without.Surface.EnginePairs.Count(p => p.Refused);
        int refusedWith = with.Surface.EnginePairs.Count(p => p.Refused && p.Member.Prefix == "nql");
        Assert.Equal(14, refusedWith - refusedWithout);
    }

    [Fact]
    public void AValueWithNoRoleStopsTheGenerator()
    {
        string roles = Path.Combine(Path.GetTempPath(), "nanoql-bind-roles-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(roles, """{ "schema": 1, "roles": [] }""");
        try
        {
            BindException e = Assert.Throws<BindException>(() => Surfaces.Run(null, roles: roles));
            Assert.Contains("has no entry in the roles table", e.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(roles);
        }
    }

    [Fact]
    public void AnObjectTwoParametersTakeWithNoRoleStopsTheGenerator()
    {
        // The repository's table without the Heston helper's entry: its trial model, an object, has no named place.
        var table = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Surfaces.Repository, "bindings", "roles.json")))!;
        System.Text.Json.Nodes.JsonArray entries = table["roles"]!.AsArray();
        entries.Remove(entries.Single(r => r!["member"]!.GetValue<string>() == "NanoQL.Models.HestonModelHelper.SetPricingEngine"));
        string roles = Path.Combine(Path.GetTempPath(), "nanoql-bind-roles-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(roles, table.ToJsonString());
        try
        {
            BindException e = Assert.Throws<BindException>(() => Surfaces.Run(null, roles: roles));
            Assert.Contains("HestonModelHelper", e.Message, StringComparison.Ordinal);
            Assert.Contains("the roles table names neither", e.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(roles);
        }
    }

    [Fact]
    public void TwoCalculationsTyingForAMembersInstrumentStopTheGenerator()
    {
        var inputs = new List<AssemblyInput>
        {
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.dll")),
            new("nql", Path.Combine(AppContext.BaseDirectory, "NanoQL.Extensions.dll")),
            new("st", Path.Combine(AppContext.BaseDirectory, "NanoQL.Bind.StopFixtures.dll")),
        };
        BindException e = Assert.Throws<BindException>(() => Generation.Run(inputs, [Path.Combine(Surfaces.Repository, "bindings", "roles.json")], null));
        Assert.Contains("two calculations of NanoQL.Bind.StopFixtures.TieEngine tie for its instrument TieInstrument", e.Message, StringComparison.Ordinal);
    }
}
