// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/cmsmarketcalibration.hpp ql/termstructures/volatility/swaption/cmsmarketcalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>What a CMS market calibration fits: QuantLib's <c>CmsMarketCalibration::CalibrationType</c>.</summary>
public enum CmsMarketCalibrationCalibrationType
{
    /// <summary>The spreads over the Ibor leg.</summary>
    OnSpread,

    /// <summary>The spot CMS legs' NPVs.</summary>
    OnPrice,

    /// <summary>The forward CMS legs' NPVs.</summary>
    OnForwardCmsPrice,
}
