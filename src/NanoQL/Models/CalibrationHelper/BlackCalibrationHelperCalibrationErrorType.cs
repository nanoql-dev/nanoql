// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/calibrationhelper.hpp ql/models/calibrationhelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>How a Black calibration helper measures its error: QuantLib's <c>BlackCalibrationHelper::CalibrationErrorType</c>.</summary>
/// <remarks>QuantLib's enum has no stream operator; the helper refuses a value outside it on entry.</remarks>
public enum BlackCalibrationHelperCalibrationErrorType
{
    /// <summary>The absolute difference of the market and model values, relative to the market value.</summary>
    RelativePriceError = 0,

    /// <summary>The market value less the model value.</summary>
    PriceError = 1,

    /// <summary>The volatility the model value implies, less the market volatility.</summary>
    ImpliedVolError = 2,
}
