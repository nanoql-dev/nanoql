// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/hestonslvfdmmodel.hpp ql/models/equity/hestonslvfdmmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>The Fokker-Planck finite-difference calibration's settings for a <see cref="HestonSLVFDMModel"/>.</summary>
/// <param name="XGrid">The log spot's number of points.</param>
/// <param name="VGrid">The variance's number of points.</param>
/// <param name="TMaxStepsPerYear">The time steps per year at the start, the largest steps' inverse.</param>
/// <param name="TMinStepsPerYear">The time steps per year the grid decays to.</param>
/// <param name="TStepNumberDecay">The exponential decay rate from the first to the second.</param>
/// <param name="NRannacherTimeSteps">The implicit Euler (Rannacher smoothing) steps at the start.</param>
/// <param name="PredictionCorretionSteps">The predictor-corrector steps at each time.</param>
/// <param name="X0Density">The local volatility forward equation's first grid's density at the mean.</param>
/// <param name="LocalVolEpsProb">The local volatility forward equation's probability beyond its grids' ends.</param>
/// <param name="MaxIntegrationIterations">The local volatility forward equation's integrals' maximum
/// evaluations.</param>
/// <param name="VLowerEps">The probability below the variance mesher's lower bound.</param>
/// <param name="VUpperEps">The probability above the variance mesher's upper bound.</param>
/// <param name="VMin">The variance mesher's lowest lower bound.</param>
/// <param name="V0Density">The variance mesher's density at v₀.</param>
/// <param name="VLowerBoundDensity">The variance mesher's density at its lower bound.</param>
/// <param name="VUpperBoundDensity">The variance mesher's density at its upper bound.</param>
/// <param name="LeverageFctPropEps">The probability in each tail beyond which the leverage isn't calibrated but held at
/// its last calibrated value.</param>
/// <param name="GreensAlgorithm">The Green's function's approximation at the first time.</param>
/// <param name="TrafoType">The variable the density is written in, in the variance's direction.</param>
/// <param name="SchemeDesc">The finite-difference scheme after the smoothing steps.</param>
public sealed record HestonSLVFokkerPlanckFdmParams(
    int XGrid,
    int VGrid,
    int TMaxStepsPerYear,
    int TMinStepsPerYear,
    double TStepNumberDecay,
    int NRannacherTimeSteps,
    int PredictionCorretionSteps,
    double X0Density,
    double LocalVolEpsProb,
    int MaxIntegrationIterations,
    double VLowerEps,
    double VUpperEps,
    double VMin,
    double V0Density,
    double VLowerBoundDensity,
    double VUpperBoundDensity,
    double LeverageFctPropEps,
    FdmHestonGreensFctAlgorithm GreensAlgorithm,
    FdmSquareRootFwdOpTransformationType TrafoType,
    FdmSchemeDesc SchemeDesc);
