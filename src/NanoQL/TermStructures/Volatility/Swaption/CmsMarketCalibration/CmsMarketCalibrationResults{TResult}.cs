// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/cmsmarketcalibration.hpp ql/termstructures/volatility/swaption/cmsmarketcalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// A CMS market calibration's results: QuantLib's <c>compute</c> result and the members it leaves on the calibration
/// and its cube and market (<c>sparseSabrParameters_</c>, <c>denseSabrParameters_</c>, <c>browseCmsMarket_</c>,
/// <c>error()</c>, <c>endCriteria()</c>).
/// </summary>
/// <typeparam name="TResult">The result's type: a <see cref="Vector"/> of the βs and the reversion, or a
/// <see cref="Matrix"/> of a β term structure or its parameters, with the reversion.</typeparam>
/// <param name="Result">The calibrated βs (or their term structure's parameters) and, when given or calibrated, the
/// mean reversion, laid out as QuantLib's result.</param>
/// <param name="VolCube">The cube recalibrated at the result, after QuantLib's <c>updateAfterRecalibration</c>; the
/// calibration's cube is unchanged.</param>
/// <param name="CmsMarket">The market repriced on <see cref="VolCube"/> at the calibrated reversion, whose
/// <see cref="TermStructures.CmsMarket.Browse"/> is QuantLib's <c>browseCmsMarket_</c>.</param>
/// <param name="SparseSabrParameters"><see cref="VolCube"/>'s sparse parameters.</param>
/// <param name="DenseSabrParameters"><see cref="VolCube"/>'s dense parameters, or <see langword="null"/> for a cube
/// that has none, neither at-the-money-calibrated nor single-pass, where QuantLib browses an empty cube.</param>
/// <param name="Error">The cost at the result, before the cube's update.</param>
/// <param name="EndCriteria">The optimizer's end criteria.</param>
public sealed record CmsMarketCalibrationResults<TResult>(
    TResult Result,
    XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> VolCube,
    CmsMarket CmsMarket,
    Matrix SparseSabrParameters,
    Matrix? DenseSabrParameters,
    double Error,
    EndCriteriaType EndCriteria)
    where TResult : class;
