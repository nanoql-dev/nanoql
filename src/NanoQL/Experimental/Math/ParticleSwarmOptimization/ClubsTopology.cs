// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp ql/experimental/math/particleswarmoptimization.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Own-tested: Divergences/ParticleSwarmTopologyTests.cs (QuantLib's reads past its clubs, takes a club's index for a particle and leaves every club at every iteration)

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The clubs topology, after H. M. Emara, "Adaptive Clubs-based Particle Swarm Optimization": each particle belongs to a
/// default number of clubs, and its social best is the best of its clubs' members; a club's best leaves a random club
/// (down to a minimum) and its worst joins one (up to a maximum), and every reset iterations the others return to the
/// default: QuantLib's <c>ClubsTopology</c>.
/// </summary>
/// <remarks>
/// <para>
/// Fixes (<c>Divergences/ParticleSwarmTopologyTests.cs</c>): the initial clubs are drawn in [0, totalClubs − 1], where
/// QuantLib's draws in [1, totalClubs] and reads one past each particle's clubs; a particle's social best is its best
/// club's best particle, where QuantLib's takes the club's index for a particle; each club's worst is weighed over
/// every member, where QuantLib's tests it only on a member that isn't the new best, so a club whose values fall in
/// order names particle 0 its worst, a member or not; and a best particle leaves a club only above the minimum and a
/// worst one joins only below the maximum, as the class documents, where QuantLib's never tests either, so a particle
/// that stays best leaves every club and its social best is then the largest double. A club none of whose members
/// passes QuantLib's comparisons, one with no members or whose personal bests are all NaN, has no best, and a particle
/// reads only its clubs with a best, or its own personal best where none has one, where QuantLib's reads particle 0, a
/// member or not, for such a club; a minimum below 1 is refused, as a particle in no club has no club-mates and so no
/// social best, where QuantLib's best particle leaves every club and keeps particle 0's position at the largest double.
/// </para>
/// <para>
/// Its integers are <c>std::uniform_int_distribution</c>'s on a <c>std::mt19937</c> on the seed, drawn as MSVC draws
/// them; the seed is required, QuantLib's default seeding from the clock. Each minimization starts the generator from
/// the seed and the iterations from 0.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class ClubsTopology : ParticleSwarmOptimizationTopology
{
    private readonly int totalClubs, maxClubs, minClubs, defaultClubs;
    private readonly int resetIteration;
    private readonly long seed;
    private readonly MersenneTwisterUniformRng generator;
    private readonly int[] bestByClub;
    private readonly int[] worstByClub;
    private int iteration;
    private int M;
    private bool[][] clubs4particles = [];
    private bool[][] particles4clubs = [];

    /// <summary>Initializes a new instance of the <see cref="ClubsTopology"/> class.</summary>
    /// <param name="defaultClubs">The clubs each particle starts in.</param>
    /// <param name="totalClubs">The clubs.</param>
    /// <param name="maxClubs">The most clubs a particle joins.</param>
    /// <param name="minClubs">The fewest clubs a particle keeps.</param>
    /// <param name="resetIteration">The iterations between resets to the default.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentException">The club counts are out of order, with QuantLib's texts.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minClubs"/> is below 1, or <paramref name="seed"/> is outside [1, 4294967295].
    /// </exception>
    public ClubsTopology(int defaultClubs, int totalClubs, int maxClubs, int minClubs, int resetIteration, long seed)
    {
        // a particle in no club would have no social best
        ArgumentOutOfRangeException.ThrowIfLessThan(minClubs, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(resetIteration);
        this.totalClubs = totalClubs;
        this.maxClubs = maxClubs;
        this.minClubs = minClubs;
        this.defaultClubs = defaultClubs;
        this.resetIteration = resetIteration;
        this.seed = seed;
        generator = new MersenneTwisterUniformRng(seed);
        if (!(totalClubs >= defaultClubs))
        {
            throw new ArgumentException("Total number of clubs must be larger or equal than default clubs", nameof(totalClubs));
        }

        if (!(defaultClubs >= minClubs))
        {
            throw new ArgumentException("Number of default clubs must be larger or equal than minimum clubs", nameof(defaultClubs));
        }

        if (!(maxClubs >= defaultClubs))
        {
            throw new ArgumentException("Number of maximum clubs must be larger or equal than default clubs", nameof(maxClubs));
        }

        if (!(totalClubs >= maxClubs))
        {
            throw new ArgumentException("Total number of clubs must be larger or equal than maximum clubs", nameof(totalClubs));
        }

        bestByClub = new int[totalClubs];
        worstByClub = new int[totalClubs];
    }

    /// <inheritdoc/>
    public override void SetSize(int M)
    {
        this.M = M;
        if (defaultClubs < totalClubs)
        {
            clubs4particles = [.. Enumerable.Range(0, M).Select(_ => new bool[totalClubs])];
            particles4clubs = [.. Enumerable.Range(0, totalClubs).Select(_ => new bool[M])];

            // assign particles to clubs randomly, among the clubs 0 to totalClubs − 1
            for (int i = 0; i < M; i++)
            {
                bool[] clubSet = clubs4particles[i];
                for (int j = 0; j < defaultClubs; j++)
                {
                    int index = (int)MsvcRandom.UniformInt(generator, 0, totalClubs - 1);
                    while (clubSet[index])
                    {
                        index = (int)MsvcRandom.UniformInt(generator, 0, totalClubs - 1);
                    }

                    clubSet[index] = true;
                    particles4clubs[index][i] = true;
                }
            }
        }
        else
        {
            // since totalClubs == defaultClubs, just initialize to true
            clubs4particles = [.. Enumerable.Range(0, M).Select(_ => Enumerable.Repeat(true, totalClubs).ToArray())];
            particles4clubs = [.. Enumerable.Range(0, totalClubs).Select(_ => Enumerable.Repeat(true, M).ToArray())];
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The topology isn't bound to a minimization's swarm, where QuantLib reads clubs <c>setSize</c> hasn't made.
    /// </exception>
    public override void FindSocialBest()
    {
        RequireBound();

        // update iteration
        iteration++;
        bool reset = false;
        if (iteration == resetIteration)
        {
            iteration = 0;
            reset = true;
        }

        // find best by current club, the worst weighed over every member; a club where no member passes the
        // comparisons, with no members or only NaN personal bests, has none, −1
        for (int i = 0; i < totalClubs; i++)
        {
            double bestByClubF = NumericLimits.QL_MAX_REAL;
            double worstByClubF = -NumericLimits.QL_MAX_REAL;
            int bestP = -1;
            int worstP = -1;
            bool[] particlesSet = particles4clubs[i];
            for (int j = 0; j < M; j++)
            {
                if (particlesSet[j])
                {
                    if (bestByClubF > PersonalBestF[j])
                    {
                        bestByClubF = PersonalBestF[j];
                        bestP = j;
                    }

                    if (worstByClubF < PersonalBestF[j])
                    {
                        worstByClubF = PersonalBestF[j];
                        worstP = j;
                    }
                }
            }

            bestByClub[i] = bestP;
            worstByClub[i] = worstP;
        }

        // update clubs && global best
        for (int i = 0; i < M; i++)
        {
            bool[] clubSet = clubs4particles[i];
            bool best = true;
            bool worst = true;
            int currentClubs = 0;
            for (int j = 0; j < totalClubs; j++)
            {
                if (clubSet[j])
                {
                    // if still thought of the best, check if best in club j
                    if (best && i != bestByClub[j])
                    {
                        best = false;
                    }

                    // if still thought of the worst, check if worst in club j
                    if (worst && i != worstByClub[j])
                    {
                        worst = false;
                    }

                    // update currentClubs
                    currentClubs++;
                }
            }

            // update clubs, within the minimum and the maximum
            if (best)
            {
                // leave random club
                if (currentClubs > minClubs)
                {
                    LeaveRandomClub(i, currentClubs);
                }
            }
            else if (worst)
            {
                // join random club
                if (currentClubs < maxClubs)
                {
                    JoinRandomClub(i, currentClubs);
                }
            }
            else if (reset && currentClubs != defaultClubs)
            {
                // if membership != defaultClubs, then leave or join accordingly
                if (currentClubs < defaultClubs)
                {
                    JoinRandomClub(i, currentClubs);
                }
                else
                {
                    LeaveRandomClub(i, currentClubs);
                }
            }

            // update global best: the best club's best particle, over the clubs with a best; where none has one, its
            // clubs' personal bests all NaN, its own
            double bestNeighborF = NumericLimits.QL_MAX_REAL;
            int bestNeighborX = -1;
            for (int j = 0; j < totalClubs; j++)
            {
                if (clubSet[j] && bestByClub[j] >= 0 && bestNeighborF > PersonalBestF[bestByClub[j]])
                {
                    bestNeighborF = PersonalBestF[bestByClub[j]];
                    bestNeighborX = bestByClub[j];
                }
            }

            if (bestNeighborX < 0)
            {
                bestNeighborX = i;
                bestNeighborF = PersonalBestF[i];
            }

            SetSocialBest(i, bestNeighborX, bestNeighborF);
        }
    }

    /// <inheritdoc/>
    public override ParticleSwarmOptimizationTopology Clone() =>
        new ClubsTopology(defaultClubs, totalClubs, maxClubs, minClubs, resetIteration, seed);

    private void LeaveRandomClub(int particle, int currentClubs)
    {
        int randIndex = (int)MsvcRandom.UniformInt(generator, 1, currentClubs);
        int index = 1;
        bool[] clubSet = clubs4particles[particle];
        for (int j = 0; j < totalClubs; j++)
        {
            if (clubSet[j])
            {
                if (index == randIndex)
                {
                    clubSet[j] = false;
                    particles4clubs[j][particle] = false;
                    break;
                }

                index++;
            }
        }
    }

    private void JoinRandomClub(int particle, int currentClubs)
    {
        int randIndex = totalClubs == currentClubs ? 1 : (int)MsvcRandom.UniformInt(generator, 1, totalClubs - currentClubs);
        int index = 1;
        bool[] clubSet = clubs4particles[particle];
        for (int j = 0; j < totalClubs; j++)
        {
            if (!clubSet[j])
            {
                if (index == randIndex)
                {
                    clubSet[j] = true;
                    particles4clubs[j][particle] = true;
                    break;
                }

                index++;
            }
        }
    }
}
