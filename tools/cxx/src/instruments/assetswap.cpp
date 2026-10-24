// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/assetswap.hpp: the asset swap's members the wheel hides, on a swap built inside the call under the
// case's settings and priced by DiscountingSwapEngine on the curve:
//
//     AssetSwap  (bond, curve, pay bond coupon, clean price, index, spread, par swap, non-par repayment or null):
//                [fair non-par repayment, floating leg's NPV and BPS, clean price, non-par repayment, spread, par
//                swap, pay bond coupon, the bond's settlement date]
//
// The bond is an instruments/bondspec.hpp spec, priced by DiscountingBondEngine on the curve; the curve an
// indexes/indexspec.hpp curve spec; the index an Ibor spec of indexes/indexspec.hpp; the floating schedule QuantLib's
// default (the bond's settlement date to its maturity), the floating day count the index's. A member QuantLib refuses
// is null.

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/bondspec.hpp"

#include <ql/instruments/assetswap.hpp>
#include <ql/pricingengines/swap/discountingswapengine.hpp>

#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class F>
        json::value attempt(F f) {
            try {
                return json::value(f());
            } catch (const Error&) {
                return nullptr;
            }
        }

        const Register unit({
            {"AssetSwap",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 bonds::BondSpec bond = bonds::bond(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 bool pay = boolean(a[2]);
                 Real clean = real(a[3]);
                 indexes::IndexSpec index = indexes::iborIndex(a[4]);
                 Spread spread = real(a[5]);
                 bool par = boolean(a[6]);
                 Real repayment = real(a[7]);
                 return [=] {
                     auto b = bonds::makePricedBond(bond, curve, std::nullopt);
                     auto ibor = indexes::makeIborIndex(index);
                     AssetSwap swap(pay, b, clean, ibor, spread, Schedule(), ibor->dayCounter(), par, 1.0, repayment);
                     swap.setPricingEngine(ext::make_shared<DiscountingSwapEngine>(indexes::makeCurve(curve)));
                     json::array out;
                     out.emplace_back(attempt([&] { return swap.fairNonParRepayment(); }));
                     out.emplace_back(attempt([&] { return swap.floatingLegNPV(); }));
                     out.emplace_back(attempt([&] { return swap.floatingLegBPS(); }));
                     out.emplace_back(swap.cleanPrice());
                     out.emplace_back(swap.nonParRepayment());
                     out.emplace_back(swap.spread());
                     out.emplace_back(swap.parSwap());
                     out.emplace_back(swap.payBondCoupon());
                     out.emplace_back(result(swap.bond()->settlementDate()));
                     return json::value(out);
                 };
             }},
        });

    }

}
