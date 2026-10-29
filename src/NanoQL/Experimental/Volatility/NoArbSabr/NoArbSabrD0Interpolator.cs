// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/noarbsabr.hpp ql/experimental/volatility/noarbsabr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// QuantLib's <c>detail::D0Interpolator</c>: the no-arbitrage SABR model's absorption probability, interpolated
/// multilinearly in the absorption table's φ = Q⁻¹(γ, d₀)·τ on Doust's grid of expiries, initial volatilities,
/// correlations, volatilities of volatility and betas, then mapped back through d₀ = Q(γ, φ/τ).
/// </summary>
internal sealed class NoArbSabrD0Interpolator
{
    // QuantLib's grids
    private static readonly double[] TauG =
    [
        0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 2.25, 2.5, 2.75, 3.0, 3.25, 3.5, 3.75, 4.0, 4.25, 4.5, 4.75, 5.0, 5.25,
        5.5, 5.75, 6.0, 6.25, 6.5, 6.75, 7.0, 7.25, 7.5, 7.75, 8.0, 8.25, 8.5, 8.75, 9.0, 9.25, 9.5, 9.75, 10.0, 10.25, 10.5,
        10.75, 11.0, 11.25, 11.5, 11.75, 12.0, 12.25, 12.5, 12.75, 13.0, 13.25, 13.5, 13.75, 14.0, 14.25, 14.5, 14.75, 15.0,
        15.25, 15.5, 15.75, 16.0, 16.25, 16.5, 16.75, 17.0, 17.25, 17.5, 17.75, 18.0, 18.25, 18.5, 18.75, 19.0, 19.25, 19.5,
        19.75, 20.0, 20.25, 20.5, 20.75, 21.0, 21.25, 21.5, 21.75, 22.0, 22.25, 22.5, 22.75, 23.0, 23.25, 23.5, 23.75, 24.0,
        24.25, 24.5, 24.75, 25.0, 25.25, 25.5, 25.75, 26.0, 26.25, 26.5, 26.75, 27.0, 27.25, 27.5, 27.75, 28.0, 28.25, 28.5,
        28.75, 29.0, 29.25, 29.5, 29.75, 30.0,
    ];

    private static readonly double[] SigmaIG =
        [1.0, 0.8, 0.7, 0.6, 0.5, 0.45, 0.4, 0.35, 0.3, 0.27, 0.24, 0.21, 0.18, 0.15, 0.125, 0.1, 0.075, 0.05];

    private static readonly double[] RhoG = [0.75, 0.50, 0.25, 0.00, -0.25, -0.50, -0.75];

    private static readonly double[] NuG = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8];

    private static readonly double[] BetaG = [0.01, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9];

    private readonly double expiryTime;
    private readonly double beta;
    private readonly double nu;
    private readonly double rho;
    private readonly double gamma;
    private readonly double sigmaI;

    /// <summary>Initializes a new instance of the <see cref="NoArbSabrD0Interpolator"/> class.</summary>
    /// <param name="forward">The forward.</param>
    /// <param name="expiryTime">The expiry time.</param>
    /// <param name="alpha">The SABR α.</param>
    /// <param name="beta">The SABR β.</param>
    /// <param name="nu">The SABR ν.</param>
    /// <param name="rho">The SABR ρ.</param>
    internal NoArbSabrD0Interpolator(double forward, double expiryTime, double alpha, double beta, double nu, double rho)
    {
        this.expiryTime = expiryTime;
        this.beta = beta;
        this.nu = nu;
        this.rho = rho;
        gamma = 1.0 / (2.0 * (1.0 - beta));
        sigmaI = alpha * Math.Pow(forward, beta - 1.0);
    }

    /// <summary>The absorption probability.</summary>
    /// <returns>d₀.</returns>
    /// <exception cref="NanoQLException">The table's index is out of range ("absorption matrix index … invalid").</exception>
    internal double Value()
    {
        int tauInd = UpperBound(TauG, expiryTime);
        if (tauInd == TauG.Length)
        {
            --tauInd; // tau at upper bound
        }

        double expiryTimeTmp = expiryTime;
        if (tauInd == 0)
        {
            ++tauInd;
            expiryTimeTmp = TauG[0];
        }

        double tauL = (expiryTimeTmp - TauG[tauInd - 1]) / (TauG[tauInd] - TauG[tauInd - 1]);

        // the descending grids searched in ascending order, as QuantLib's reverse iterators do
        int sigmaIInd = SigmaIG.Length - UpperBoundReversed(SigmaIG, sigmaI);
        if (sigmaIInd == 0)
        {
            ++sigmaIInd; // sigmaI at upper bound
        }

        double sigmaIL = (sigmaI - SigmaIG[sigmaIInd - 1]) / (SigmaIG[sigmaIInd] - SigmaIG[sigmaIInd - 1]);

        int rhoInd = RhoG.Length - UpperBoundReversed(RhoG, rho);
        if (rhoInd == 0)
        {
            rhoInd++;
        }

        if (rhoInd == RhoG.Length)
        {
            rhoInd--;
        }

        double rhoL = (rho - RhoG[rhoInd - 1]) / (RhoG[rhoInd] - RhoG[rhoInd - 1]);

        int nuInd = UpperBound(NuG, nu);
        if (nuInd == NuG.Length)
        {
            --nuInd; // nu at upper bound
        }

        double tmpNuG = nuInd > 0 ? NuG[nuInd - 1] : 0.0;
        double nuL = (nu - tmpNuG) / (NuG[nuInd] - tmpNuG);

        int betaInd = UpperBound(BetaG, beta);
        double tmpBetaG = betaInd == BetaG.Length ? 1.0 : BetaG[betaInd];
        double betaL = (beta - BetaG[betaInd - 1]) / (tmpBetaG - BetaG[betaInd - 1]);

        int[] table = NoArbSabrAbsProbs.Values;
        double phiRes = 0.0;
        for (int iTau = -1; iTau <= 0; ++iTau)
        {
            for (int iSigma = -1; iSigma <= 0; ++iSigma)
            {
                for (int iRho = -1; iRho <= 0; ++iRho)
                {
                    for (int iNu = -1; iNu <= 0; ++iNu)
                    {
                        for (int iBeta = -1; iBeta <= 0; ++iBeta)
                        {
                            double phiTmp;
                            if (iNu == -1 && nuInd == 0)
                            {
                                // this is 0.5*z_F^2
                                phiTmp = 0.5 / (sigmaI * sigmaI * (1.0 - beta) * (1.0 - beta));
                            }
                            else if (iBeta == 0 && betaInd == BetaG.Length)
                            {
                                phiTmp = Phi(NoArbSabrModel.TinyProb);
                            }
                            else
                            {
                                int ind = tauInd + iTau + ((sigmaIInd + iSigma + ((rhoInd + iRho
                                    + ((nuInd + iNu + ((betaInd + iBeta) * NuG.Length)) * RhoG.Length)) * SigmaIG.Length))
                                    * TauG.Length);
                                if (!(ind >= 0 && ind < NoArbSabrAbsProbs.Size))
                                {
                                    throw new NanoQLException($"absorption matrix index ({ind}) invalid");
                                }

                                phiTmp = Phi(table[ind] / NoArbSabrModel.Nsim);
                            }

                            phiRes += phiTmp * (iTau == -1 ? (1.0 - tauL) : tauL)
                                * (iSigma == -1 ? (1.0 - sigmaIL) : sigmaIL)
                                * (iRho == -1 ? (1.0 - rhoL) : rhoL)
                                * (iNu == -1 ? (1.0 - nuL) : nuL)
                                * (iBeta == -1 ? (1.0 - betaL) : betaL);
                        }
                    }
                }
            }
        }

        return D0(phiRes);
    }

    // std::upper_bound: the first index whose element exceeds the value, on an ascending grid
    private static int UpperBound(double[] grid, double value)
    {
        int i = 0;
        while (i < grid.Length && !(value < grid[i]))
        {
            ++i;
        }

        return i;
    }

    // std::upper_bound over a descending grid's reverse iterators: the count of its elements at most the value
    private static int UpperBoundReversed(double[] grid, double value)
    {
        int count = 0;
        for (int k = grid.Length - 1; k >= 0 && !(value < grid[k]); --k)
        {
            ++count;
        }

        return count;
    }

    private double Phi(double d0) =>
        d0 < 1e-14
            ? NoArbSabrModel.PhiByTauCutoff * expiryTime
            : IgammaInverseFunctions.GammaQInv(gamma, d0) * expiryTime;

    private double D0(double phi) => GammaFunctions.GammaQ(gamma, Math.Max(0.0, phi / expiryTime));
}
