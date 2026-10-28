// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/pool.hpp ql/experimental/credit/pool.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A pool of issuers by name, each with the default key it enters with and a time.</summary>
/// <remarks>
/// <para>
/// Immutable: QuantLib's <c>add</c>, <c>clear</c> and <c>setTime</c> return new pools (PR6.4b).
/// </para>
/// <para>
/// <see cref="DefaultKeys"/> lists each name's key in the pool's order, the order of <see cref="Names"/>, which every
/// basket and model read pairs it with. QuantLib's lists them in name order, iterating a map keyed by name
/// (<c>pool.cpp:77-82</c>), so a pool not added in name order reads each name's probability on another name's key: names
/// C and A entering with keys KA and KB read C's probability on KB's curve. And QuantLib's <c>clear</c> keeps the
/// cleared names' keys (<c>pool.cpp:34-38</c>), which a name added later then reads; <see cref="Clear"/> returns an
/// empty pool. A documented divergence (the S-xcr plan's XR8 (i); <c>Divergences/PoolKeyOrderTests.cs</c>); on a pool
/// added in name order, QuantLib's.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class Pool
{
    private static readonly NorthAmericaCorpDefaultKey DefaultContractTrigger =
        new(null, Seniority.SeniorSec, new Period(0, TimeUnit.Days), 1.0);

    private readonly ImmutableDictionary<string, Entry> data;

    /// <summary>Initializes a new instance of the <see cref="Pool"/> class, empty.</summary>
    public Pool()
        : this([], ImmutableDictionary<string, Entry>.Empty)
    {
    }

    private Pool(ImmutableArray<string> names, ImmutableDictionary<string, Entry> data)
    {
        Names = names;
        this.data = data;
    }

    /// <summary>Gets the names, in the order they were added.</summary>
    public ImmutableArray<string> Names { get; }

    /// <summary>Gets the number of names.</summary>
    public int Size => Names.Length;

    /// <summary>Gets each name's default key, in the order of <see cref="Names"/>.</summary>
    public ImmutableArray<DefaultProbKey> DefaultKeys => [.. Names.Select(n => data[n].Key)];

    /// <summary>An empty pool: QuantLib's <c>clear</c>.</summary>
    /// <returns>An empty pool.</returns>
    public Pool Clear() => data.IsEmpty ? this : new();

    /// <summary>Whether the pool holds a name.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public bool Has(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return data.ContainsKey(name);
    }

    /// <summary>A pool with a name added, at time 0; the pool itself where it holds the name already.</summary>
    /// <param name="name">The name.</param>
    /// <param name="issuer">The issuer.</param>
    /// <param name="contractTrigger">The default key the name enters with, the events and probabilities the pool
    /// refers to; QuantLib's default, a North American corporate key of QuantLib's empty currency, senior secured,
    /// with no grace period and a required amount of 1, when <see langword="null"/>.</param>
    /// <returns>The pool with the name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="issuer"/> is
    /// <see langword="null"/>.</exception>
    public Pool Add(string name, Issuer issuer, DefaultProbKey? contractTrigger = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(issuer);
        if (Has(name))
        {
            return this;
        }

        return new Pool(Names.Add(name), data.Add(name, new Entry(issuer, contractTrigger ?? DefaultContractTrigger, 0.0)));
    }

    /// <summary>A name's issuer.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The issuer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The pool doesn't hold the name ("<c>name</c> not found").</exception>
    public Issuer Get(string name) => Find(name).Issuer;

    /// <summary>A name's default key.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The pool doesn't hold the name ("<c>name</c> not found").</exception>
    public DefaultProbKey DefaultKey(string name) => Find(name).Key;

    /// <summary>A name's time.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The pool doesn't hold the name ("<c>name</c> not found").</exception>
    public double GetTime(string name) => Find(name).Time;

    /// <summary>A pool with a name's time set; the pool itself for a name it doesn't hold, whose time QuantLib stores
    /// where no read reaches it.</summary>
    /// <param name="name">The name.</param>
    /// <param name="time">The time.</param>
    /// <returns>The pool with the time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public Pool SetTime(string name, double time)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!data.TryGetValue(name, out Entry? e))
        {
            return this;
        }

        return new Pool(Names, data.SetItem(name, e with { Time = time }));
    }

    // A subset of the pool's names, in the pool's order: a basket's live names.
    internal Pool Subset(IEnumerable<int> rows)
    {
        ImmutableArray<string> names = [.. rows.Select(i => Names[i])];
        return new Pool(names, data.Where(kv => names.Contains(kv.Key)).ToImmutableDictionary());
    }

    private Entry Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!data.TryGetValue(name, out Entry? e))
        {
            throw new ArgumentException(name + " not found", nameof(name));
        }

        return e;
    }

    private sealed record Entry(Issuer Issuer, DefaultProbKey Key, double Time);
}
