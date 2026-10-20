// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/dividendschedule.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

// QuantLib's DividendSchedule, a sequence of dividends, as a global alias so ported code reads as QuantLib's does.
// Public signatures write the type out.
global using DividendSchedule = System.Collections.Immutable.ImmutableArray<NanoQL.Coupons.Dividend>;
