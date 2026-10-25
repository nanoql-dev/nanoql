// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/hestonslvfdmmodel.hpp ql/models/equity/hestonslvfdmmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>A step of a <see cref="HestonSLVFDMModel"/>'s calibration: the joint density of the log spot and the
/// variance at a time, on its mesher.</summary>
/// <param name="T">The time, on the model's clock.</param>
/// <param name="Prob">The density at each point of the mesher's layout, in the transformation's variable.</param>
/// <param name="Mesher">The mesher: the log spot in direction 0, the variance or its transformation in direction
/// 1.</param>
public sealed record HestonSLVFDMModelLogEntry(double T, ImmutableArray<double> Prob, FdmMesherComposite Mesher);
