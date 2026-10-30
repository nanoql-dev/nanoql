// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Reflection.Emit;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The provider guard, an IL scan of the core: nothing reads <see cref="ConventionProvider"/> while an object prices.
/// A provider-reading constructor reads <see cref="ConventionProvider.Current"/> itself or through a constructor it
/// calls. Only the presets' own constructors and reference factories, the provider's own code, the members placed below
/// with their rules and the bodies the registry defaults pass to QuantLib's provider may build one, or a market builder,
/// or read <see cref="ConventionProvider.Current"/>, and a placed member builds exactly what its entry names; no static
/// initializer reaches either through its calls; and nothing in the core sets <see cref="ConventionProvider.Default"/>.
/// A clone copies through a constructor that reads none, so it passes as a member that builds nothing provider-reading.
/// </summary>
public class ProviderGuardTests
{
    // The members that build a provider-reading object or a builder, or read Current, after a construction or at their
    // own: each with the rule that fixes the provider it builds in, and exactly what it builds ("Current" for a read).
    private static readonly IReadOnlyDictionary<string, (string Rule, string Builds)> Placed =
        new Dictionary<string, (string Rule, string Builds)>
        {
            // 1: a constructor building a preset or an index at its user's construction, reading Current then
            ["NanoQL.TermStructures.SofrFutureRateHelper..ctor"] = ("1: its Sofr, at its construction", "Sofr"),
            ["NanoQL.TermStructures.DepositRateHelper..ctor"] = ("1: its index from explicit conventions", "IborIndex"),
            ["NanoQL.TermStructures.FraRateHelper..ctor"] = ("1: its index from explicit conventions", "IborIndex"),
            ["NanoQL.Instruments.CCTEU..ctor"] = ("1: its Euribor6M indexes, at its construction", "Euribor6M"),
            ["NanoQL.Experimental.CommoditySettings..ctor"] = ("1: its USD, at its construction", "USDCurrency"),

            // 3: a builder, or a builder built on an index, building in the index's provider
            ["NanoQL.Instruments.MakeCreditDefaultSwap..ctor"] = ("3: no index, so Current when it is created", "Current"),
            ["NanoQL.Instruments.MakeVanillaSwap.QuantLibDefaults"] = (
                "3: the defaults' currency chain, in its index's provider",
                "AUDCurrency, CHFCurrency, EURCurrency, GBPCurrency, HKDCurrency, JPYCurrency, SEKCurrency, THBCurrency, USDCurrency"),
            ["NanoQL.Instruments.MakeCreditDefaultSwap.ToCreditDefaultSwap"] = ("3: its swap, in its provider", "CreditDefaultSwap"),
            ["NanoQL.Instruments.MakeCreditDefaultSwap.QuantLibDefaults"] = (
                "3: the defaults' weekends-only calendar, in its provider", "WeekendsOnly"),
            ["NanoQL.Coupons.OvernightLeg.ToLeg"] = ("3: its fallback calendar, in its index's provider", "WeekendsOnly"),
            ["NanoQL.Instruments.RendistatoCalculator..ctor"] = ("3: its swaps' builders, on its Euribor index", "MakeVanillaSwap"),
            ["NanoQL.TermStructures.CmsMarket..ctor"] = ("3: its CMS builders, on its swap indexes", "MakeCms"),
            ["NanoQL.Experimental.YoYOptionletHelper..ctor"] = ("3: its cap builder, on its YoY index", "MakeYoYInflationCapFloor"),

            // 4: an object building a builder inside, passing its own provider
            ["NanoQL.Indexes.SwapIndex.Builder"] = ("4: the underlying swap's builder, on the swap index's provider", "MakeVanillaSwap"),
            ["NanoQL.Instruments.MakeSwaption.ToSwaption"] = ("4: its swap builders, on its provider", "MakeOIS, MakeVanillaSwap"),
            ["NanoQL.Instruments.MakeCapFloor..ctor"] = ("4: its swap builder, on its provider", "MakeVanillaSwap"),

            // 5: built on an index after its construction, following the index
            ["NanoQL.Indexes.OvernightIndexedSwapIndex.UnderlyingSwap"] = ("5: its swap's builder, on its provider", "MakeOIS"),
            ["NanoQL.TermStructures.SwapRateHelper.BuildSwap"] = ("5: the re-dated swap's builder, on its index", "MakeVanillaSwap"),
            ["NanoQL.TermStructures.OISRateHelper.BuildSwap"] = ("5: the re-dated swap's builder, on its index", "MakeOIS"),
            ["NanoQL.TermStructures.MultipleResetsSwapRateHelper.BuildSwap"] = (
                "5: the re-dated swap's builder, on its index", "MakeMultipleResetsSwap"),
            ["NanoQL.TermStructures.BMASwapRateHelper.WithTermStructure"] = ("5: a fresh BMA index, in its BMA index's provider", "BMAIndex"),
            ["NanoQL.TermStructures.BMASwapRateHelper.BuildSwap"] = ("5: a fresh BMA index, in its BMA index's provider", "BMAIndex"),
            ["NanoQL.Coupons.StubIborCouponSelection.MakeStubIndex"] = (
                "5: the weighted index, in its first component's provider", "StubIborCouponWeightedIndex"),
            ["NanoQL.TermStructures.Gaussian1dSmileSection.OptionPrice"] = ("5: the swaption's builder, on its swap index", "MakeSwaption"),
            ["NanoQL.TermStructures.Gaussian1dSmileSection.Caplet"] = ("5: the caplet's builder, on its Ibor index", "MakeCapFloor"),

            // 6: a static engine or function building on its inputs' indexes
            ["NanoQL.Experimental.HaganIrregularSwaptionEngine+Basket.Component"] = (
                "6: the basket's builders, on the swap's index", "MakeVanillaSwap"),
            ["NanoQL.PricingEngines.CounterpartyAdjSwapEngine.Swaptionlets"] = (
                "6: the swaptionlets' builders, on the swap's index", "MakeVanillaSwap"),
            ["NanoQL.Models.HistoricalForwardRatesAnalysisFunctions.HistoricalForwardRatesAnalysis"] = (
                "6: each date's deposit helpers, in the provider of the index they copy", "DepositRateHelper"),

            // 7: an object building after its construction with no index to follow, in the provider of its construction
            ["NanoQL.Instruments.CreditDefaultSwap..ctor"] = ("7: keeps Current from its construction", "Current"),
            ["NanoQL.Instruments.CreditDefaultSwap.Engine"] = ("7: the implied quantities' calendar, in its provider", "WeekendsOnly"),
            ["NanoQL.TermStructures.CdsHelper..ctor"] = ("7: keeps Current from its construction", "Current"),
            ["NanoQL.TermStructures.SpreadCdsHelper.BuildSwap"] = ("7: the bound swap, in the helper's provider", "CreditDefaultSwap"),
            ["NanoQL.TermStructures.UpfrontCdsHelper.BuildSwap"] = ("7: the bound swap, in the helper's provider", "CreditDefaultSwap"),
            ["NanoQL.Experimental.InterpolatedYoYOptionletStripper..ctor"] = ("7: keeps Current from its construction", "Current"),
            ["NanoQL.Experimental.InterpolatedYoYOptionletStripper.Strip"] = ("7: the strip's index, in its provider", "YYGenericCPI"),
            ["NanoQL.Experimental.InterpolatedYoYOptionletStripper.ObjectiveFunction"] = (
                "7: the strip's caps and curves, in its provider", "MakeYoYInflationCapFloor, TARGET"),
        };

    // The registry defaults' bodies built under QuantLib's conventions, by their members: the functions each registry
    // type's static initializer passes to ConventionProvider.BuildUnderQuantLib, and nothing else of the type.
    private static readonly string[] UnderQuantLib =
    [
        "NanoQL.Currencies.ExchangeRates.KnownRates",
        "NanoQL.EvaluationContext..cctor",
        "NanoQL.Experimental.UnitOfMeasureConversions.KnownConversionFactors",
    ];

    // The market builders: a construction of one outside a placed member is a failure, its own copies apart.
    private static readonly HashSet<string> Builders =
    [
        "MakeVanillaSwap", "MakeOIS", "MakeCreditDefaultSwap", "MakeCapFloor", "MakeSwaption", "MakeCms",
        "MakeMultipleResetsSwap", "MakeYoYInflationCapFloor",
    ];

    private static readonly Lazy<Scan> Core = new(() => new Scan(typeof(ConventionProvider).Assembly));

    [Fact]
    public void NothingOutsideThePlacedMembersBuildsAConventionOrReadsTheProvider()
    {
        Scan scan = Core.Value;
        var failures = new List<string>();
        foreach ((string owner, SortedSet<string> builds) in scan.BuildsByOwner())
        {
            if (Placed.TryGetValue(owner, out (string Rule, string Builds) entry))
            {
                if (string.Join(", ", builds) != entry.Builds)
                {
                    failures.Add($"{owner} builds {string.Join(", ", builds)}, where its entry names {entry.Builds}");
                }
            }
            else
            {
                failures.Add($"{owner} builds {string.Join(", ", builds)}");
            }
        }

        Assert.True(failures.Count == 0, "unplaced:\n" + string.Join("\n", failures.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void EachPlacedMemberStillBuildsWhatItsEntryNames()
    {
        // A placed member that no longer builds, or no longer exists, is dropped from the list.
        IReadOnlyDictionary<string, SortedSet<string>> builds = Core.Value.BuildsByOwner();

        Assert.All(Placed.Keys, k => Assert.True(builds.ContainsKey(k), $"{k} builds nothing its entry names"));
    }

    [Fact]
    public void OnlyTheRegistryDefaultsBodiesAreBuiltUnderQuantLibs()
    {
        Assert.Equal(UnderQuantLib, Core.Value.UnderQuantLib.Select(IlScanner.Owner).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NoStaticInitializerReachesAConventionOrTheProvider()
    {
        Scan scan = Core.Value;
        var failures = new List<string>();
        foreach (MethodBase cctor in scan.Bodies.Keys.Where(m => m is ConstructorInfo { IsStatic: true }))
        {
            if (cctor.DeclaringType != typeof(ConventionProvider) && scan.Reach(cctor) is string reached)
            {
                failures.Add($"{IlScanner.Owner(cctor)} reaches {reached}");
            }
        }

        Assert.True(failures.Count == 0, "static initializers:\n" + string.Join("\n", failures.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void NothingInTheCoreSetsTheDefault()
    {
        MethodInfo setter = typeof(ConventionProvider).GetProperty(nameof(ConventionProvider.Default))!.SetMethod!;
        string[] setters = [.. Core.Value.Bodies.Where(b => b.Value.Any(r => r.Member == setter)).Select(b => IlScanner.Owner(b.Key))];

        Assert.Empty(setters);
    }

    [Fact]
    public void ThePresetsAndIndexesConstructorsReadTheProviderAndTheClonesCopyThroughOnesThatDont()
    {
        Scan scan = Core.Value;
        ConstructorInfo Ctor(Type t, params Type[] parameters) => t.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, parameters)!;

        Assert.Contains(Ctor(typeof(TARGET)), scan.Reading);
        Assert.Contains(Ctor(typeof(Currencies.EURCurrency)), scan.Reading);
        Assert.Contains(Ctor(typeof(Business252)), scan.Reading);
        Assert.DoesNotContain(Ctor(typeof(Business252), typeof(Calendar)), scan.Reading);
        Assert.DoesNotContain(Ctor(typeof(NullCalendar)), scan.Reading);
        Assert.Contains(Ctor(typeof(Euribor), typeof(Period), typeof(TermStructures.YieldTermStructure)), scan.Reading);

        // Each index type's clone builds an index, through a constructor that reads no provider.
        string[] clones =
        [
            "NanoQL.Indexes.IborIndex.Clone", "NanoQL.Indexes.OvernightIndex.Clone", "NanoQL.Indexes.CustomIborIndex.Clone",
            "NanoQL.Indexes.Libor.Clone", "NanoQL.Indexes.EURLibor.Clone", "NanoQL.Indexes.Cdi.Clone",
            "NanoQL.Indexes.Shibor.Clone", "NanoQL.Indexes.SwapIndex.Clone", "NanoQL.Indexes.ZeroInflationIndex.Clone",
            "NanoQL.Indexes.YoYInflationIndex.Clone", "NanoQL.Indexes.EquityIndex.Clone",
        ];
        foreach (string clone in clones)
        {
            ConstructorInfo[] built = [.. scan.Bodies.Where(b => IlScanner.Owner(b.Key) == clone)
                .SelectMany(b => b.Value).Where(r => r.OpCode == OpCodes.Newobj).Select(r => Scan.Definition((ConstructorInfo)r.Member))
                .Where(c => typeof(FinancialIndex).IsAssignableFrom(c.DeclaringType))];
            Assert.NotEmpty(built);
            Assert.All(built, c => Assert.DoesNotContain(c, scan.Reading));
        }
    }

    // A builder's construction, its own copy (a record's with) apart.
    private static bool IsBuilder(ConstructorInfo c) =>
        Builders.Contains(c.DeclaringType!.Name)
        && !(c.GetParameters() is [{ } only] && only.ParameterType == c.DeclaringType);

    // The core's bodies and what they read.
    private sealed class Scan
    {
        private readonly HashSet<MethodBase> currentReaders = [];

        public Scan(Assembly core)
        {
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (Type type in core.GetTypes())
            {
                foreach (MethodBase m in type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All)))
                {
                    Bodies[m] = IlScanner.References(m);
                }
            }

            // The provider's own members that read Current, directly or through one another.
            MethodInfo current = typeof(ConventionProvider).GetProperty(nameof(ConventionProvider.Current))!.GetMethod!;
            currentReaders.Add(current);
            for (bool changed = true; changed;)
            {
                changed = false;
                foreach ((MethodBase m, List<(OpCode OpCode, MethodBase Member)> refs) in Bodies)
                {
                    if (m.DeclaringType == typeof(ConventionProvider) && m.IsStatic && !currentReaders.Contains(m)
                        && refs.Any(r => currentReaders.Contains(Definition(r.Member))))
                    {
                        changed |= currentReaders.Add(m);
                    }
                }
            }

            // The provider-reading constructors: reading Current directly or through a constructor they call.
            for (bool changed = true; changed;)
            {
                changed = false;
                foreach ((MethodBase m, List<(OpCode OpCode, MethodBase Member)> refs) in Bodies)
                {
                    if (m is ConstructorInfo { IsStatic: false } c && !Reading.Contains(c)
                        && refs.Any(r => ReadsCurrent(r.Member) || (r.Member is ConstructorInfo t && Reading.Contains(Definition(t)))))
                    {
                        changed |= Reading.Add(c);
                    }
                }
            }

            // The functions a static initializer passes to BuildUnderQuantLib: each the function pointer loaded last
            // before the call.
            foreach ((MethodBase m, List<(OpCode OpCode, MethodBase Member)> refs) in Bodies)
            {
                MethodBase? loaded = null;
                foreach ((OpCode op, MethodBase member) in refs)
                {
                    if (op == OpCodes.Ldftn)
                    {
                        loaded = member;
                    }
                    else if (member is MethodInfo { Name: "BuildUnderQuantLib" } b && b.DeclaringType == typeof(ConventionProvider) && loaded is not null)
                    {
                        UnderQuantLib.Add(Definition(loaded));
                        loaded = null;
                    }
                }
            }
        }

        public Dictionary<MethodBase, List<(OpCode OpCode, MethodBase Member)>> Bodies { get; } = [];

        public HashSet<ConstructorInfo> Reading { get; } = [];

        // The bodies built under QuantLib's provider, whatever is current.
        public HashSet<MethodBase> UnderQuantLib { get; } = [];

        // A generic instantiation's member as its definition declares it, whose body was read.
        public static T Definition<T>(T m)
            where T : MethodBase
        {
            MethodBase d = m;
            if (d is MethodInfo { IsGenericMethod: true, IsGenericMethodDefinition: false } g)
            {
                d = g.GetGenericMethodDefinition();
            }

            if (d.DeclaringType is { IsGenericType: true, IsGenericTypeDefinition: false } t)
            {
                d = MethodBase.GetMethodFromHandle(d.MethodHandle, t.GetGenericTypeDefinition().TypeHandle) ?? d;
            }

            return (T)d;
        }

        // A direct read of Current: the property, or one of the provider's own members that reads it.
        // A generic provider member reads as its definition does.
        public bool ReadsCurrent(MethodBase member) => currentReaders.Contains(Definition(member));

        // What each member, its lambdas' and state machines' bodies included, builds of provider-reading constructions
        // and builders, and whether it reads Current: the presets' own constructors and reference factories, the
        // provider's code and the bodies built under QuantLib's apart.
        public IReadOnlyDictionary<string, SortedSet<string>> BuildsByOwner()
        {
            var result = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach ((MethodBase method, List<(OpCode OpCode, MethodBase Member)> refs) in Bodies)
            {
                string owner = IlScanner.Owner(method);
                if (Exempt(method, owner))
                {
                    continue;
                }

                foreach ((OpCode op, MethodBase member) in refs)
                {
                    string? built = op == OpCodes.Newobj && member is ConstructorInfo c && (Reading.Contains(Definition(c)) || IsBuilder(c))
                        ? Short(c.DeclaringType!)
                        : ReadsCurrent(member) ? "Current" : null;
                    if (built is not null)
                    {
                        if (!result.TryGetValue(owner, out SortedSet<string>? set))
                        {
                            result[owner] = set = new SortedSet<string>(StringComparer.Ordinal);
                        }

                        set.Add(built);
                    }
                }
            }

            return result;
        }

        // The first provider-reading construction, builder or read of Current a static initializer reaches through
        // its calls, the bodies it passes to QuantLib's provider apart, or null.
        public string? Reach(MethodBase start)
        {
            var seen = new HashSet<MethodBase>();
            var queue = new Queue<MethodBase>([start]);
            while (queue.Count > 0)
            {
                MethodBase m = Definition(queue.Dequeue());
                if (!seen.Add(m) || UnderQuantLib.Contains(m) || !Bodies.TryGetValue(m, out List<(OpCode OpCode, MethodBase Member)>? refs))
                {
                    continue;
                }

                foreach ((OpCode op, MethodBase member) in refs)
                {
                    if (ReadsCurrent(member) && m.DeclaringType != typeof(ConventionProvider))
                    {
                        return $"ConventionProvider.Current through {IlScanner.Owner(m)}";
                    }

                    if (op == OpCodes.Newobj && member is ConstructorInfo c && (Reading.Contains(Definition(c)) || IsBuilder(c)))
                    {
                        return $"{IlScanner.TypeName(c.DeclaringType!)} through {IlScanner.Owner(m)}";
                    }

                    if (member.Module == start.Module)
                    {
                        queue.Enqueue(member);
                    }
                }
            }

            return null;
        }

        private static string Short(Type t) => t.Name.Split('`')[0];

        // The presets' own constructors and reference factories, the provider's own code, and the bodies built under
        // QuantLib's.
        private bool Exempt(MethodBase method, string owner)
        {
            if (UnderQuantLib.Contains(Definition(method)))
            {
                return true;
            }

            Type type = method.DeclaringType!;
            while (type.DeclaringType is not null && type.Name.StartsWith('<'))
            {
                type = type.DeclaringType;
            }

            if (type == typeof(ConventionProvider) || type.DeclaringType == typeof(ConventionProvider))
            {
                return true;
            }

            bool preset = typeof(Calendar).IsAssignableFrom(type) || typeof(Currency).IsAssignableFrom(type)
                || typeof(FinancialIndex).IsAssignableFrom(type) || typeof(Region).IsAssignableFrom(type) || type == typeof(Business252)
                || type == typeof(BTP);
            return preset && (owner.EndsWith("..ctor", StringComparison.Ordinal) || owner.EndsWith(".Reference", StringComparison.Ordinal));
        }
    }
}
