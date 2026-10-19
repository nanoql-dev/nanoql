// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/capfloor.hpp: what the wheel doesn't bind of CapFloor, on the instrument spec of
// instruments/capfloorspec.hpp built inside each call.
//
//     CapFloor::CapFloor                (cap): the built instrument's inspectors, either constructor by the spec
//     CapFloor::optionlet               (cap, i): the optionlet's inspectors, and its cash flow's payment date
//                                       ("date"), with the optionlet's start and maturity dates
//     CapFloor::lastFloatingRateCoupon  (cap): the last cash flow's floating-rate inspectors (couponspec.hpp's
//                                       inspectors()), or null when it isn't a floating-rate coupon
//
// An index is a Size, so a negative one is the harness's refusal; lastFloatingRateCoupon reads the back of the leg,
// undefined for an empty one, which the harness refuses.

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "instruments/capfloorspec.hpp"

#include <ql/cashflows/floatingratecoupon.hpp>
#include <ql/instruments/capfloor.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using capfloors::CapFloorSpec;

        const Register unit({
            {"CapFloor::CapFloor",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 CapFloorSpec s = capfloors::capFloor(a[0]);
                 return [=] { return capfloors::inspectors(*capfloors::makeCapFloor(s)); };
             }},
            {"CapFloor::optionlet",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 CapFloorSpec s = capfloors::capFloor(a[0]);
                 Integer i = integer(a[1]);
                 if (i == Null<Integer>() || i < 0)
                     throw InvalidParams("expected a Size, got " + json::serialize(a[1]));
                 return [=] {
                     ext::shared_ptr<CapFloor> optionlet = capfloors::makeCapFloor(s)->optionlet(static_cast<Size>(i));
                     json::value out = capfloors::inspectors(*optionlet);
                     out.get_object()["date"] = result(optionlet->floatingLeg()[0]->date());
                     out.get_object()["startDate"] = result(optionlet->startDate());
                     out.get_object()["maturityDate"] = result(optionlet->maturityDate());
                     return out;
                 };
             }},
            {"CapFloor::lastFloatingRateCoupon",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 CapFloorSpec s = capfloors::capFloor(a[0]);
                 if (s.empty)
                     throw InvalidParams("lastFloatingRateCoupon reads the back of the leg, undefined for an empty one");
                 return [=] {
                     ext::shared_ptr<FloatingRateCoupon> c = capfloors::makeCapFloor(s)->lastFloatingRateCoupon();
                     return c == nullptr ? json::value(nullptr) : cashflows::inspectors(*c);
                 };
             }},
        });

    }

}
