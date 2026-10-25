// SPDX-FileCopyrightText: Copyright (C) 2017, 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// <see cref="AndreasenHugeVolatilityInterpl"/>'s calibration error in volatility units, QuantLib's
/// <c>calibrationError()</c> tuple: each calibrated option's price error divided by its vega.
/// </summary>
/// <param name="Min">The smallest error.</param>
/// <param name="Max">The largest error.</param>
/// <param name="Avg">The errors' sum over the number of options in the calibration set.</param>
public readonly record struct AndreasenHugeCalibrationError(double Min, double Max, double Avg);
