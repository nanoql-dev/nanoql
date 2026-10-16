// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/types.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

// QuantLib's scalar type names, kept as global aliases so ported code reads as QuantLib's does. They are compile-time
// only: public signatures show double, int and long. Decimal and Time are not aliased, and are written as double:
// Decimal would clash with System.Decimal, and Time with the NanoQL.Time namespace. BigNatural is not aliased:
// internals that rely on unsigned arithmetic use ulong.
global using Integer = int;
global using BigInteger = long;
global using Natural = int;
global using Real = double;
global using Size = int;
global using DiscountFactor = double;
global using Rate = double;
global using Spread = double;
global using Volatility = double;
global using Probability = double;

// QuantLib's Leg, a sequence of cash flows. Public signatures write the type out.
global using Leg = System.Collections.Immutable.ImmutableArray<NanoQL.CashFlow>;
