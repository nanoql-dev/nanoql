// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The particle swarm's topologies (fixed). QuantLib's global topology keeps the largest personal best and skips the
/// best particle, whose social best stays at the zero vector, so it steers the swarm toward the worst; its k-neighbours
/// window runs from i − K − 1 to i + K − 1 and a particle that is its window's best takes particle 0's position; its
/// clubs topology reads one past its clubs, takes a club's index for a particle, weighs a club's worst only on a member
/// that isn't its new best, leaves and joins clubs past its bounds, and reads particle 0 for a club left empty.
/// nanoql's set every particle's social best to the best personal best of its neighbourhood, the whole swarm, the
/// window [i − K, i + K] wrapping around, or its clubs' members, which a recording inertia checks after each
/// <c>FindSocialBest</c>; each converges on the shifted sphere; and a clubs topology driven directly keeps its
/// invariants at every call: each club's best and worst over its members, each particle's one leave or join within
/// [min, max] and at a reset toward the default, and each social best the best personal best among its clubs' members,
/// on configurations where particles join clubs left empty and particle 0, the swarm's best, isn't their club-mate, and
/// with one particle's personal best NaN, which QuantLib's comparisons pass over and nanoql's too; and the clubs
/// topology converges on a cost that is NaN near the box's centre, the Sobol start's first point, on twelve particles
/// over twenty seeds, where QuantLib's comparisons replaced by a first member's NaN stall up to 3.1e-4.
/// </summary>
public class ParticleSwarmTopologyTests
{
    private static readonly double[] Center = [1.2, -0.7, 0.3];

    [Fact]
    public void TheGlobalSocialBestIsTheBestPersonalBest()
    {
        var recorder = new List<Snapshot>();
        Run(new GlobalTopology(), recorder, 12, 40);
        Assert.NotEmpty(recorder);
        foreach (Snapshot s in recorder)
        {
            int best = ArgMin(s.PersonalBestF, Enumerable.Range(0, s.M));
            for (int i = 0; i < s.M; i++)
            {
                Assert.Equal(s.PersonalBestF[best], s.GlobalBestF[i]);
                Assert.Equal(s.PersonalBestX[best], s.GlobalBestX[i]);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void EachKNeighboursSocialBestIsItsWindowsBest(int K)
    {
        var recorder = new List<Snapshot>();
        Run(new KNeighbors(K), recorder, 12, 40);
        Assert.NotEmpty(recorder);
        foreach (Snapshot s in recorder)
        {
            for (int i = 0; i < s.M; i++)
            {
                // the window [i − K, i + K], wrapping around, from the particle itself
                int m = s.M;
                int best = ArgMin(s.PersonalBestF, [i, .. Enumerable.Range(-K, (2 * K) + 1).Select(d => (((i + d) % m) + m) % m)]);
                Assert.Equal(s.PersonalBestF[best], s.GlobalBestF[i]);
                Assert.Equal(s.PersonalBestX[best], s.GlobalBestX[i]);
            }
        }
    }

    [Fact]
    public void EachClubsSocialBestIsAParticlesPersonalBest()
    {
        var recorder = new List<Snapshot>();
        Run(new ClubsTopology(2, 4, 3, 1, 5, 7), recorder, 12, 40);
        Assert.NotEmpty(recorder);
        foreach (Snapshot s in recorder)
        {
            for (int i = 0; i < s.M; i++)
            {
                // a member of one of its clubs: a particle whose personal best is the social best, at its position
                int[] matching = [.. Enumerable.Range(0, s.M).Where(k => s.PersonalBestF[k] == s.GlobalBestF[i])];
                Assert.NotEmpty(matching);
                Assert.Contains(matching, k => s.PersonalBestX[k].SequenceEqual(s.GlobalBestX[i]));
            }
        }
    }

    [Theory]
    [InlineData(1, 5, 3, 1, 4, 11L, true, -1)]
    [InlineData(3, 10, 6, 1, 10, 12L, true, -1)]
    [InlineData(1, 6, 2, 1, 3, 14L, true, -1)]
    [InlineData(2, 4, 3, 1, 5, 13L, false, -1)]
    [InlineData(1, 5, 3, 1, 4, 11L, true, 1)]
    [InlineData(2, 4, 3, 1, 5, 13L, false, 1)]
    public void EachClubsCallKeepsTheClubsInvariants(int defaultClubs, int totalClubs, int maxClubs, int minClubs, int reset, long seed, bool emptiesClubs, int nanParticle)
    {
        const int M = 12;
        int emptyJoins = 0;
        for (long s = seed; s < seed + 10; s++)
        {
            emptyJoins += ClubsCalls(defaultClubs, totalClubs, maxClubs, minClubs, reset, s, M, nanParticle);
        }

        // the first three configurations reach a particle in a club left empty, apart from particle 0; the convergence
        // fact's keeps its clubs filled
        Assert.True(!emptiesClubs || emptyJoins > 0, $"no particle joined an empty club ({emptyJoins})");
    }

    [Fact]
    public void AClubsWorstIsWeighedOverEveryMember()
    {
        // QuantLib's else-if names the third particle worst on values 9, 1, 5
        var clubs = new ClubsDriver(new ClubsTopology(1, 1, 1, 1, 1000, 1), 3);
        clubs.SetPersonalBests([9.0, 1.0, 5.0]);
        clubs.Call();
        Assert.Equal(1, clubs.BestByClub()[0]);
        Assert.Equal(0, clubs.WorstByClub()[0]);
    }

    // 400 calls on a seed, each call's invariants asserted; returns the particles that read a club with no best, apart
    // from particle 0.
    private static int ClubsCalls(int defaultClubs, int totalClubs, int maxClubs, int minClubs, int reset, long seed, int M, int nanParticle)
    {
        var clubs = new ClubsDriver(new ClubsTopology(defaultClubs, totalClubs, maxClubs, minClubs, reset, seed), M);
        var rng = new MersenneTwisterUniformRng(seed + 100);

        // particle 0 the swarm's best throughout, the others' personal bests improving at random, one NaN throughout
        double[] f = [0.0, .. Enumerable.Range(1, M - 1).Select(_ => 1.0 - rng.NextReal())];
        if (nanParticle >= 0)
        {
            f[nanParticle] = double.NaN;
        }

        int emptyJoins = 0;
        for (int call = 0; call < 400; call++)
        {
            for (int k = 1; k < M; k++)
            {
                f[k] = Math.Min(f[k], 1.0 - rng.NextReal());
            }

            clubs.SetPersonalBests(f);
            bool[][] pre = clubs.Members();
            bool[][] preClubs = clubs.ClubsOf();
            bool isReset = clubs.Iteration + 1 == reset;
            clubs.Call();
            bool[][] post = clubs.ClubsOf();
            int[] bestBy = clubs.BestByClub(), worstBy = clubs.WorstByClub();

            // each club's best and worst over its members before the call, a NaN passed over
            int[] best = new int[totalClubs], worst = new int[totalClubs];
            for (int j = 0; j < totalClubs; j++)
            {
                int[] members = [.. Enumerable.Range(0, M).Where(k => pre[j][k] && !double.IsNaN(f[k]))];
                best[j] = members.Length == 0 ? -1 : members.MinBy(k => f[k]);
                worst[j] = members.Length == 0 ? -1 : members.MaxBy(k => f[k]);
                Assert.Equal(best[j], bestBy[j]);
                Assert.Equal(worst[j], worstBy[j]);
            }

            for (int i = 0; i < M; i++)
            {
                int[] mine = [.. Enumerable.Range(0, totalClubs).Where(j => preClubs[i][j])];
                int c = mine.Length;
                bool isBest = mine.All(j => best[j] == i), isWorst = mine.All(j => worst[j] == i);
                int expected = isBest ? (c > minClubs ? c - 1 : c)
                    : isWorst ? (c < maxClubs ? c + 1 : c)
                    : isReset && c != defaultClubs ? (c < defaultClubs ? c + 1 : c - 1)
                    : c;
                int[] now = [.. Enumerable.Range(0, totalClubs).Where(j => post[i][j])];

                // one leave or join, within the bounds
                Assert.Equal(expected, now.Length);
                Assert.InRange(now.Length, minClubs, maxClubs);
                Assert.True(expected <= c ? now.All(j => preClubs[i][j]) : mine.All(j => post[i][j]));

                // the social best: the best personal best among the members its clubs had before the call, or its own
                // where they're all NaN
                int[] mates = [.. now.Where(j => best[j] >= 0).SelectMany(j => Enumerable.Range(0, M).Where(k => pre[j][k] && !double.IsNaN(f[k]))).Distinct()];
                Assert.True(mates.Length > 0 || i == nanParticle);
                int mate = mates.Length > 0 ? mates.MinBy(k => f[k]) : i;
                Assert.Equal(f[mate], clubs.SocialBestF(i));
                Assert.Equal(mate + 0.5, clubs.SocialBestX(i));
                if (now.Any(j => best[j] < 0) && !mates.Contains(0))
                {
                    emptyJoins++;
                }
            }
        }

        return emptyJoins;
    }

    [Theory]
    [InlineData("global")]
    [InlineData("kneighbors")]
    [InlineData("clubs")]
    public void EachTopologyConvergesOnTheShiftedSphere(string topology)
    {
        ParticleSwarmOptimizationTopology t = topology switch
        {
            "global" => new GlobalTopology(),
            "kneighbors" => new KNeighbors(2),
            _ => new ClubsTopology(2, 4, 3, 1, 5, 7),
        };
        ProblemResults r = Run(t, [], 20, 300);
        Assert.True(r.FunctionValue < 1e-10, $"{topology}: {r.FunctionValue}");
    }

    [Fact]
    public void TheClubsSwarmConvergesWhereTheBoxsCentreIsNaN()
    {
        // twelve particles over twenty seeds: a NaN first member made its club-mates' social best NaN, stalling up to
        // 3.1e-4
        for (long seed = 1; seed <= 20; seed++)
        {
            var pso = new ParticleSwarmOptimization(12, new ClubsTopology(2, 4, 3, 1, 5, seed), new TrivialInertia(), 2.05, 2.05, seed + 40);
            var problem = new Problem(new ShiftedSphere(true), new NonhomogeneousBoundaryConstraint([-5.0, -5.0, -5.0], [5.0, 5.0, 5.0]), Center);
            ProblemResults r = pso.Minimize(problem, new EndCriteria(300, 299, 1e-8, 1e-8, 1e-8));
            Assert.True(r.FunctionValue < 1e-10, $"seed {seed}: {r.FunctionValue}");
        }
    }

    private static int ArgMin(Vector f, IEnumerable<int> candidates)
    {
        int best = -1;
        foreach (int k in candidates)
        {
            if (best < 0 || f[k] < f[best])
            {
                best = k;
            }
        }

        return best;
    }

    private static ProblemResults Run(ParticleSwarmOptimizationTopology topology, List<Snapshot> recorder, int M, int iterations)
    {
        var pso = new ParticleSwarmOptimization(M, topology, new RecordingInertia(recorder), 2.05, 2.05, 42);
        var problem = new Problem(new ShiftedSphere(), new NonhomogeneousBoundaryConstraint([-5.0, -5.0, -5.0], [5.0, 5.0, 5.0]), Center);
        return pso.Minimize(problem, new EndCriteria(iterations, iterations - 1, 1e-8, 1e-8, 1e-8));
    }

    internal sealed record Snapshot(int M, Vector PersonalBestF, Vector GlobalBestF, double[][] PersonalBestX, double[][] GlobalBestX);

    // A clubs topology bound to a swarm of its own, read through reflection: one-dimensional personal bests at k + 0.5.
    private sealed class ClubsDriver
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly ClubsTopology topology;
        private readonly object swarm;

        public ClubsDriver(ClubsTopology topology, int M)
        {
            this.topology = topology;
            topology.SetSize(M);
            Type type = typeof(ParticleSwarmOptimization).GetNestedType("Swarm", BindingFlags.NonPublic)!;
            swarm = Activator.CreateInstance(type)!;
            Set("PersonalBestX", Enumerable.Range(0, M).Select(k => new Vector(1, k + 0.5)).ToArray());
            Set("GlobalBestX", Enumerable.Range(0, M).Select(_ => new Vector(1, 0.0)).ToArray());
            Set("PersonalBestF", new Vector(M, 0.0));
            Set("GlobalBestF", new Vector(M, 0.0));
            typeof(ParticleSwarmOptimizationTopology).GetMethod("Bind", Hidden)!.Invoke(topology, [swarm]);
        }

        public int Iteration => (int)Field("iteration")!;

        public void SetPersonalBests(double[] f)
        {
            var pBF = (Vector)Get("PersonalBestF");
            for (int k = 0; k < f.Length; k++)
            {
                pBF[k] = f[k];
            }
        }

        public void Call() => topology.FindSocialBest();

        public bool[][] Members() => [.. ((bool[][])Field("particles4clubs")!).Select(c => (bool[])c.Clone())];

        public bool[][] ClubsOf() => [.. ((bool[][])Field("clubs4particles")!).Select(c => (bool[])c.Clone())];

        public int[] BestByClub() => (int[])((int[])Field("bestByClub")!).Clone();

        public int[] WorstByClub() => (int[])((int[])Field("worstByClub")!).Clone();

        public double SocialBestF(int i) => ((Vector)Get("GlobalBestF"))[i];

        public double SocialBestX(int i) => ((Vector[])Get("GlobalBestX"))[i][0];

        private object? Field(string name) => typeof(ClubsTopology).GetField(name, Hidden)!.GetValue(topology);

        private object Get(string name) => swarm.GetType().GetField(name)!.GetValue(swarm)!;

        private void Set(string name, object value) => swarm.GetType().GetField(name)!.SetValue(swarm, value);
    }

    // The trivial inertia, recording the swarm's bests after each FindSocialBest.
    internal sealed class RecordingInertia(List<Snapshot> recorder) : ParticleSwarmOptimizationInertia
    {
        private double c0;
        private int M;

        public override void SetSize(int M, int N, double c0, EndCriteria endCriteria)
        {
            this.M = M;
            this.c0 = c0;
        }

        public override void SetValues()
        {
            recorder.Add(new Snapshot(
                M,
                PersonalBestF.Clone(),
                GlobalBestF.Clone(),
                [.. PersonalBestX.Select(x => x.ToArray())],
                [.. GlobalBestX.Select(x => x.ToArray())]));
            for (int i = 0; i < M; i++)
            {
                Vector v = V[i];
                v *= c0;
            }
        }

        public override ParticleSwarmOptimizationInertia Clone() => new RecordingInertia(recorder);
    }

    // the sphere at Center, NaN within 0.5 of the box's centre where asked
    private sealed class ShiftedSphere(bool nanAtCentre = false) : CostFunction
    {
        public override double Value(Vector x)
        {
            if (nanAtCentre && Math.Sqrt((x[0] * x[0]) + (x[1] * x[1]) + (x[2] * x[2])) < 0.5)
            {
                return double.NaN;
            }

            double s = 0.0;
            for (int i = 0; i < x.Size; i++)
            {
                s += (x[i] - Center[i]) * (x[i] - Center[i]);
            }

            return s;
        }

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
