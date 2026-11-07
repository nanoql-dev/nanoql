// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Kahale's interpolated segments that miss their right end's price (fixed). With interpolation, QuantLib's fit solves
/// for a segment's slope a by a Brent search (<c>kahalesmilesection.cpp:151-155</c>), but a forms the tails of the
/// segment's lognormal law at its ends by subtraction, so where the root's law puts less than about 1e-16 of its mass
/// beyond an end, adjacent doubles of a skip over the root and the search's bracket closes on a non-root
/// (<c>brent.hpp:87-90</c>). The segment then misses its right end's price and the section jumps there, a call-spread
/// arbitrage. On the wheel, on a lognormal SABR smile (a forward of 5% for a year, α 0.3, β 1, ν 0.3, ρ −0.3),
/// QuantLib's call a millionth of the forward below a strike of 7.5% is 0.000326 against 0.000646 at it. On its port
/// here, where QuantLib's own section crashes on many of them, QuantLib's fit misses 69 segments on 60 of 163 SABR
/// smiles of a sweep, 14 of them with arbitrage at its 199 strikes, and 111 on 98 of 300 more drawn the same way, 16
/// with arbitrage. nanoql refits such a segment to the same ends and slopes on the law's tail parameter
/// (<see cref="KahaleSmileSection"/>'s remarks), so it meets its end, and nothing outside it moves. QuantLib's checks:
/// the source's prices at the grid points, which every segment meets, and QuantLib's own bits outside the refitted
/// segments, on the wheel.
/// </summary>
public class KahaleMissedSegmentTests
{
    // Each smile the wheel shows missing: its SABR parameters (unshifted), the refitted segment's right end as
    // moneyness, QuantLib's section a millionth of the forward below that end and at it, and QuantLib's section at
    // moneyness outside the segment, in its wings or in its own segments.
    private static readonly Dictionary<string, Missed> Smiles = new()
    {
        ["lognormal"] = new(
            1.0, 0.05, [0.3, 1.0, 0.3, -0.3], 1.5, 0.00032632057630468947, 0.0006457470895479232,
            [(0.005, 0.04975), (2.0, 1.696601492748158e-18), (3.0, 2.913182468252088e-77)]),
        ["random 7"] = new(
            0.5, 0.06614014419511677, [0.10890035476356417, 0.5, 0.5197775224467609, 0.1265609022246117], 1.5,
            0.0004293333206804584, 0.0009174830968388253,
            [(0.005, 0.06580944362527585), (2.0, 1.2201202871481248e-16), (3.0, 8.43173803360355e-68)]),
        ["random 21"] = new(
            0.5, 0.03104549762350077, [0.020917040044483776, 0.3, 0.4040336896765665, 0.27024382281113757], 1.25,
            9.292074900095135e-05, 0.0002518466727509019,
            [(0.05, 0.029493222742325728), (1.75, 1.149709787048914e-52), (2.5, 2.0117755643181234e-199)]),
        ["random 69"] = new(
            1.0, 0.05600905167748975, [0.4427151040926217, 1.0, 0.2929295882635708, 0.24646847716277498], 1.25,
            0.00527019060056344, 0.005725991484654913,
            [
                (0.005, 0.0557290064191023), (1.375, 0.0042949049316823775), (1.875, 0.0016118602887983113),
                (3.0, 0.0005322002559682065), (6.0, 7.12887860286846e-08),
            ]),
        ["lognormal at 5e8"] = new(
            1.0, 5e8, [0.3, 1.0, 0.3, -0.3], 0.7, 158838900.1266853, 158907586.65472913,
            [(0.05, 475000000.0), (0.85, 101792407.19500148), (1.125, 36967485.40835923), (3.0, 8682.322609065115)]),
    };

    // The first sample's fixed smiles: T, F, α, β, ν, ρ and the shift.
    private static readonly double[][] FixedSmiles =
    [
        [5.0, 0.04, 0.02, 0.5, 0.3, -0.2, 0.0], [1.0, 0.04, 0.02, 0.5, 0.3, -0.2, 0.0],
        [10.0, 0.04, 0.02, 0.5, 0.3, -0.2, 0.0], [5.0, 0.04, 0.02, 0.5, 0.5, -0.2, 0.0],
        [5.0, 0.02, 0.02, 0.5, 0.3, -0.2, 0.0], [5.0, 0.06, 0.02, 0.5, 0.3, -0.2, 0.0],
        [5.0, 0.04, 0.008, 0.0, 0.3, 0.0, 0.0], [1.0, 0.05, 0.3, 1.0, 0.3, -0.3, 0.0],
        [1.0, 50.0, 0.3, 1.0, 0.3, -0.3, 0.0], [1.0, 5e4, 0.3, 1.0, 0.3, -0.3, 0.0],
        [1.0, 5e6, 0.3, 1.0, 0.3, -0.3, 0.0], [1.0, 5e8, 0.3, 1.0, 0.3, -0.3, 0.0],
        [5.0, 0.01, 0.02, 0.5, 0.3, -0.2, 0.02],
    ];

    public static TheoryData<string> SmileNames() => [.. Smiles.Keys];

    [Theory]
    [MemberData(nameof(SmileNames))]
    public void AMissedSegmentMeetsItsEndAndNothingOutsideItMoves(string smile)
    {
        Missed s = Smiles[smile];
        var kahale = new KahaleSmileSection(new SabrSmileSection(s.T, s.Forward, s.Sabr), null, true);
        double end = s.End * s.Forward, h = 1e-6 * s.Forward;

        // QuantLib's section rises toward the end from below, a call spread of negative price; nanoql's is the end's
        // price there, and the call spread just below it a digital's, in [0, 1] times its width
        Assert.True(s.QuantLibBelow < s.QuantLibAt);
        Assert.Equal(s.QuantLibAt, kahale.OptionPrice(end));
        Assert.InRange(kahale.OptionPrice(end - h) - kahale.OptionPrice(end), 0.0, h);

        foreach ((double m, double price) in s.Outside)
        {
            Assert.Equal(price, kahale.OptionPrice(m * s.Forward));
        }

        // no arbitrage on a fine grid over the core: each call between its intrinsic value and the forward, the
        // digitals (centered differences over a millionth of the forward either side) in [0, 1] and falling
        double lo = kahale.LeftCoreStrike(), hi = kahale.RightCoreStrike(), previous = 1.0;
        for (int j = 0; j <= 2000; j++)
        {
            double k = lo + ((hi - lo) * j / 2000.0), call = kahale.OptionPrice(k);
            Assert.True(
                call >= Math.Max(s.Forward - k, 0.0) - (1e-13 * s.Forward) && call <= s.Forward * (1.0 + 1e-13),
                $"{smile} at {k}: {call}");
            double digital = (kahale.OptionPrice(k - h) - kahale.OptionPrice(k + h)) / (2.0 * h);
            Assert.True(digital >= -1e-8 && digital <= previous + 1e-8, $"{smile} at {k}: {digital} after {previous}");
            previous = digital;
        }
    }

    [Fact]
    public void NoCoreSegmentMissesItsEndOnEitherSample()
    {
        // the first sample's 13 fixed smiles and 150 random ones, and 300 drawn the same way: each section's core
        // segments meet their right ends within the refit's tolerance, 1e-8 of the price and 16 ε of the left end's,
        // and its prices at 199 strikes in 2% steps of the shifted forward, are free of arbitrage; the smiles refused
        // are QuantLib's refusals of them, unchanged
        var refused = new List<string>();
        var refitted = new List<(int Segments, int Sections)>();
        IEnumerable<(string Name, double[] P)>[] samples =
        [
            FixedSmiles.Select((p, i) => ($"f{i:D2}", p)).Concat(Sample(20261023, 150, "r")),
            Sample(20261106, 300, "h"),
        ];
        foreach (IEnumerable<(string Name, double[] P)> sample in samples)
        {
            int segments = 0, sections = 0;
            foreach ((string name, double[] p) in sample)
            {
                var source = new SabrSmileSection(p[0], p[1], [p[2], p[3], p[4], p[5]], p[6]);
                KahaleSmileSection kahale;
                try
                {
                    kahale = new KahaleSmileSection(source, null, true);
                }
                catch (NanoQLException)
                {
                    refused.Add(name);
                    continue;
                }

                (int missed, int tails) = Segments(kahale);
                Assert.True(missed == 0, $"{name}: {missed} segments missing their end");
                Assert.True(Arbitrage(kahale, p[1], p[6]) == 0, $"{name}: arbitrage at the 199 strikes");
                segments += tails;
                sections += tails > 0 ? 1 : 0;
            }

            refitted.Add((segments, sections));
        }

        // the segments refitted, on each sample as many on as many sections as QuantLib's fit misses
        Assert.Equal([(69, 60), (111, 98)], refitted);
        string[] quantLibs =
            ["r078", "r094", "r115", "r141", "h059", "h071", "h087", "h139", "h157", "h176", "h195", "h216", "h262", "h275"];
        Assert.Equal(quantLibs, refused);
    }

    // The sample's smiles, drawn as Python's random module draws them on its seed: its Mersenne Twister seeded by
    // init_by_array, a uniform from a 53-bit double of two draws, a choice by rejection on a draw's top bits; for each
    // smile T, F, β, the shift, the at-the-money lognormal volatility, then ν and ρ, α set from the volatility.
    private static IEnumerable<(string Name, double[] P)> Sample(long seed, int count, string prefix)
    {
        var mt = new MersenneTwisterUniformRng([seed]);
        double Uniform(double a, double b)
        {
            long hi = mt.NextInt32() >> 5, lo = mt.NextInt32() >> 6;
            return a + ((b - a) * (((hi * 67108864.0) + lo) / 9007199254740992.0));
        }

        double Choice(double[] items)
        {
            int bits = 32 - BitOperations.LeadingZeroCount((uint)items.Length);
            long r;
            do
            {
                r = mt.NextInt32() >> (32 - bits);
            }
            while (r >= items.Length);
            return items[r];
        }

        for (int i = 0; i < count; i++)
        {
            double t = Choice([0.5, 1.0, 2.0, 5.0, 10.0, 20.0]);
            double f = Uniform(0.005, 0.08);
            double beta = Choice([0.0, 0.3, 0.5, 0.7, 1.0]);
            double shift = Choice([0.0, 0.0, 0.01, 0.02]);
            double vol = Uniform(0.1, 0.6);
            double alpha = vol * Math.Pow(f + shift, 1.0 - beta);
            double nu = Uniform(0.1, 0.8);
            double rho = Uniform(-0.6, 0.4);
            yield return ($"{prefix}{i:D3}", [t, f, alpha, beta, nu, rho, shift]);
        }
    }

    // The core segments missing their right end's price, and those in the refit's tail form, read from the section's
    // fit.
    private static (int Missed, int Refitted) Segments(KahaleSmileSection kahale)
    {
        const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
        Type type = typeof(KahaleSmileSection);
        var k = (ImmutableArray<double>)type.GetField("k", Instance)!.GetValue(kahale)!;
        var c = (ImmutableArray<double>)type.GetField("c", Instance)!.GetValue(kahale)!;
        var functions = ((IEnumerable)type.GetField("cFunctions", Instance)!.GetValue(kahale)!).Cast<object>().ToList();
        (int left, int right) = kahale.CoreIndices();
        int missed = 0, refitted = 0;
        for (int s = 1; left > 0 && s <= right - left; s++)
        {
            object f = functions[s];
            refitted += (bool)f.GetType().GetField("tail", Instance)!.GetValue(f)! ? 1 : 0;
            double at = (double)f.GetType().GetMethod("Evaluate")!.Invoke(f, [k[left + s]])!;
            double c0 = c[left + s - 1], c1 = c[left + s];
            if (!(Math.Abs(at - c1) <= (1e-8 * Math.Abs(c1)) + (16.0 * 2.220446049250313e-16 * Math.Abs(c0))))
            {
                missed++;
            }
        }

        return (missed, refitted);
    }

    // The breaches of no-arbitrage at 199 strikes in 2% steps of the shifted forward: a call outside its intrinsic
    // value and the shifted forward, a call rising with the strike, or one whose slope falls, each beyond its rounding.
    private static int Arbitrage(KahaleSmileSection kahale, double forward, double shift)
    {
        double fs = forward + shift, tol = 1e-13 * fs, previous = double.NaN, previousSlope = double.NaN;
        double previousK = double.NaN;
        int breaches = 0;
        for (int j = 1; j < 200; j++)
        {
            double x = (0.02 * j * fs) - shift, call = kahale.OptionPrice(x);
            if (call < Math.Max(forward - x, 0.0) - tol || call > fs + tol)
            {
                breaches++;
            }

            if (!double.IsNaN(previous))
            {
                double slope = (call - previous) / (x - previousK);
                if (slope > tol || (!double.IsNaN(previousSlope) && slope < previousSlope - 1e-9))
                {
                    breaches++;
                }

                previousSlope = slope;
            }

            previous = call;
            previousK = x;
        }

        return breaches;
    }

    private sealed record Missed(
        double T,
        double Forward,
        double[] Sabr,
        double End,
        double QuantLibBelow,
        double QuantLibAt,
        (double Moneyness, double Price)[] Outside);
}
