// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/swaptions/irregularswap.hpp, irregularswaption.hpp and haganirregularswaptionengine.hpp, with their
// .cpp files: the irregular swap, its swaption and Hagan's engine, which the wheel doesn't bind.
//
// "HaganIrregularSwaptionEngine::values" takes (referenceDate, rate, type, fixedNotionals, fixedRates, floatNotionals,
// spreads, vol): a flat continuous curve at the rate from the reference date on Actual/365 (Fixed), Euribor 6M on it, a
// five-year swap starting a TARGET year after the reference date, its fixed leg annual on 30/360 (Bond Basis) with a
// notional and a rate per coupon, its floating leg semiannual with a notional and a spread per coupon and QuantLib's
// default Black pricer, both schedules backward on TARGET Modified Following; type 1 a payer, −1 a receiver. It returns
// {"npv", "legNPV", "legBPS", "fixedLegNPV", "floatingLegNPV", "hagan"}: the swap on the discounting swap engine, and
// its European swaption exercised at the swap's start on Hagan's engine with a constant normal swaption volatility;
// and {"type", "settlementType", "settlementTypes"}: the swaption's type and settlement type, and each settlement
// type's text.

#include "convert.hpp"

#include <ql/cashflows/couponpricer.hpp>
#include <ql/cashflows/fixedratecoupon.hpp>
#include <ql/cashflows/iborcoupon.hpp>
#include <ql/exercise.hpp>
#include <ql/experimental/swaptions/haganirregularswaptionengine.hpp>
#include <ql/experimental/swaptions/irregularswap.hpp>
#include <ql/experimental/swaptions/irregularswaption.hpp>
#include <ql/indexes/ibor/euribor.hpp>
#include <ql/pricingengines/swap/discountingswapengine.hpp>
#include <ql/termstructures/volatility/swaption/swaptionconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/target.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>
#include <ql/time/daycounters/thirty360.hpp>
#include <ql/time/schedule.hpp>

#include <cstdint>
#include <sstream>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::string streamed(IrregularSettlement::Type t) {
            std::ostringstream s;
            s << t;
            return s.str();
        }

        const Register unit({
            {"HaganIrregularSwaptionEngine::values",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 DateSpec reference = date(a[0]);
                 Real rate = real(a[1]);
                 Integer type = integer(a[2]);
                 std::vector<Real> fixedNotionals = reals(a[3]), fixedRates = reals(a[4]);
                 std::vector<Real> floatNotionals = reals(a[5]), spreads = reals(a[6]);
                 Real vol = real(a[7]);
                 return [=] {
                     Date ref = makeDate(reference);
                     Handle<YieldTermStructure> curve(ext::make_shared<FlatForward>(ref, rate, Actual365Fixed()));
                     auto index = ext::make_shared<Euribor6M>(curve);
                     Date start = TARGET().advance(ref, 1, Years);
                     Date end = start + 5 * Years;
                     Schedule fixedSchedule = MakeSchedule().from(start).to(end).withTenor(1 * Years)
                         .withCalendar(TARGET()).withConvention(ModifiedFollowing).backwards();
                     Schedule floatSchedule = MakeSchedule().from(start).to(end).withTenor(6 * Months)
                         .withCalendar(TARGET()).withConvention(ModifiedFollowing).backwards();
                     Leg fixedLeg = FixedRateLeg(fixedSchedule).withNotionals(fixedNotionals)
                         .withCouponRates(fixedRates, Thirty360(Thirty360::BondBasis));
                     Leg floatLeg = IborLeg(floatSchedule, index).withNotionals(floatNotionals).withSpreads(spreads);
                     Swap::Type t = type > 0 ? Swap::Payer : Swap::Receiver;
                     auto swap = ext::make_shared<IrregularSwap>(t, fixedLeg, floatLeg);
                     swap->setPricingEngine(ext::make_shared<DiscountingSwapEngine>(curve));
                     json::object out;
                     out["npv"] = swap->NPV();
                     out["legNPV"] = json::array{swap->legNPV(0), swap->legNPV(1)};
                     out["legBPS"] = json::array{swap->legBPS(0), swap->legBPS(1)};
                     out["fixedLegNPV"] = swap->fixedLegNPV();
                     out["floatingLegNPV"] = swap->floatingLegNPV();
                     Handle<SwaptionVolatilityStructure> v(ext::make_shared<ConstantSwaptionVolatility>(
                         ref, TARGET(), ModifiedFollowing, vol, Actual365Fixed(), Normal));
                     IrregularSwaption swaption(swap, ext::make_shared<EuropeanExercise>(start));
                     swaption.setPricingEngine(ext::make_shared<HaganIrregularSwaptionEngine>(v, curve));
                     out["hagan"] = swaption.NPV();
                     out["type"] = static_cast<std::int64_t>(swaption.type());
                     out["settlementType"] = streamed(swaption.settlementType());
                     out["settlementTypes"] =
                         json::array{streamed(IrregularSettlement::Physical), streamed(IrregularSettlement::Cash)};
                     return json::value(out);
                 };
             }},
        });

    }

}
