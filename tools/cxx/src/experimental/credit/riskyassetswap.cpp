// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/riskyassetswap.hpp and riskyassetswapoption.hpp, which the wheel doesn't expose, on
// termstructures/credit/creditspec.hpp's schedules, credit and discount curves:
//
//     RiskyAssetSwap::values (fixedPayer, nominal, fixed schedule, float schedule, fixed and float day counters,
//         spread, recovery, discount, credit, coupon or null): the swap's NPV, fair spread and float annuity
//     AssetSwapHelper::bootstrap (helpers, reference date, sample times): a backward-flat hazard-rate curve on asset
//         swap helpers, each [spread, tenor length, tenor units, settlement days, calendar, fixed period length and
//         units, fixed convention, fixed day counter, float period length and units, float convention, float day
//         counter, recovery, discount]: its dates and data and its survival probability at each time
//     RiskyAssetSwapOption::NPV (the swap's arguments, expiry, market spread, spread volatility): the option's NPV
//
// nanoql fixes the swap's fixed annuity and recovery integral (the S-xcr plan's XR8 (xii), (xxi)), so its generator
// reads the NPV and fair spread where the schedules coincide at recovery 0.

#include "termstructures/credit/creditspec.hpp"

#include <ql/experimental/credit/riskyassetswap.hpp>
#include <ql/experimental/credit/riskyassetswapoption.hpp>
#include <ql/math/interpolations/backwardflatinterpolation.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/credit/piecewisedefaultcurve.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct SwapSpec {
            bool fixedPayer = true;
            Real nominal = 0.0, spread = 0.0, recovery = 0.0, coupon = Null<Real>();
            credit::ScheduleSpec fixedSchedule, floatSchedule;
            credit::DayCounterSpec fixedDayCounter, floatDayCounter;
            credit::DiscountSpec discount;
            credit::CurveSpec credit;
        };

        SwapSpec swap(const Args& a) {
            SwapSpec s;
            s.fixedPayer = boolean(a[0]);
            s.nominal = real(a[1]);
            s.fixedSchedule = credit::schedule(a[2]);
            s.floatSchedule = credit::schedule(a[3]);
            s.fixedDayCounter = credit::dayCounter(a[4]);
            s.floatDayCounter = credit::dayCounter(a[5]);
            s.spread = real(a[6]);
            s.recovery = real(a[7]);
            s.discount = credit::discount(a[8]);
            s.credit = credit::curve(a[9]);
            s.coupon = real(a[10]);
            return s;
        }

        ext::shared_ptr<RiskyAssetSwap> makeSwap(const SwapSpec& s) {
            return ext::make_shared<RiskyAssetSwap>(
                s.fixedPayer, s.nominal, credit::makeSchedule(s.fixedSchedule), credit::makeSchedule(s.floatSchedule),
                credit::makeDayCounter(s.fixedDayCounter), credit::makeDayCounter(s.floatDayCounter), s.spread,
                s.recovery, credit::makeDiscount(s.discount),
                Handle<DefaultProbabilityTermStructure>(credit::makeCurve(s.credit)), s.coupon);
        }

        struct HelperSpec {
            Real spread = 0.0, recovery = 0.0;
            Integer tenorLength = 0, settlementDays = 0, fixedLength = 0, floatLength = 0;
            TimeUnit tenorUnits = Years, fixedUnits = Years, floatUnits = Months;
            credit::CalendarSpec calendar;
            BusinessDayConvention fixedConvention = Following, floatConvention = Following;
            credit::DayCounterSpec fixedDayCounter, floatDayCounter;
            credit::DiscountSpec discount;
        };

        HelperSpec helper(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 15)
                throw InvalidParams("expected an asset swap helper as fifteen values");
            const json::array& h = v.get_array();
            HelperSpec s;
            s.spread = real(h[0]);
            s.tenorLength = integer(h[1]);
            s.tenorUnits = enumeration<TimeUnit>(h[2]);
            s.settlementDays = integer(h[3]);
            s.calendar = credit::calendar(h[4]);
            s.fixedLength = integer(h[5]);
            s.fixedUnits = enumeration<TimeUnit>(h[6]);
            s.fixedConvention = enumeration<BusinessDayConvention>(h[7]);
            s.fixedDayCounter = credit::dayCounter(h[8]);
            s.floatLength = integer(h[9]);
            s.floatUnits = enumeration<TimeUnit>(h[10]);
            s.floatConvention = enumeration<BusinessDayConvention>(h[11]);
            s.floatDayCounter = credit::dayCounter(h[12]);
            s.recovery = real(h[13]);
            s.discount = credit::discount(h[14]);
            return s;
        }

        const Register unit({
            {"RiskyAssetSwap::values",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 SwapSpec s = swap(a);
                 return [=] {
                     ext::shared_ptr<RiskyAssetSwap> asw = makeSwap(s);
                     return json::value(json::object{{"NPV", asw->NPV()},
                                                     {"fairSpread", asw->fairSpread()},
                                                     {"floatAnnuity", asw->floatAnnuity()}});
                 };
             }},
            {"AssetSwapHelper::bootstrap",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<HelperSpec> specs;
                 if (!a[0].is_array())
                     throw InvalidParams("expected the helpers as an array");
                 for (const json::value& h : a[0].get_array())
                     specs.push_back(helper(h));
                 DateSpec reference = date(a[1]);
                 std::vector<Real> times = reals(a[2]);
                 return [=] {
                     std::vector<ext::shared_ptr<DefaultProbabilityHelper>> helpers;
                     for (const HelperSpec& s : specs) {
                         Handle<YieldTermStructure> d = credit::makeDiscount(s.discount);
                         RelinkableHandle<YieldTermStructure> yts;
                         yts.linkTo(*d);
                         helpers.push_back(ext::make_shared<AssetSwapHelper>(
                             Handle<Quote>(ext::make_shared<SimpleQuote>(s.spread)), Period(s.tenorLength, s.tenorUnits),
                             s.settlementDays, credit::makeCalendar(s.calendar), Period(s.fixedLength, s.fixedUnits),
                             s.fixedConvention, credit::makeDayCounter(s.fixedDayCounter),
                             Period(s.floatLength, s.floatUnits), s.floatConvention,
                             credit::makeDayCounter(s.floatDayCounter), s.recovery, yts));
                     }
                     PiecewiseDefaultCurve<HazardRate, BackwardFlat> curve(makeDate(reference), helpers,
                                                                           Actual365Fixed());
                     json::array dates, data, survival;
                     for (const Date& d : curve.dates())
                         dates.push_back(result(d));
                     for (Real x : curve.data())
                         data.push_back(json::value(x));
                     for (Time t : times)
                         survival.push_back(json::value(curve.survivalProbability(t, true)));
                     return json::value(json::object{{"dates", dates}, {"data", data}, {"survivalProbability", survival}});
                 };
             }},
            {"RiskyAssetSwapOption::NPV",
             [](const Args& a) -> Call {
                 arity(a, 14);
                 SwapSpec s = swap(a);
                 DateSpec expiry = date(a[11]);
                 Real market = real(a[12]), vol = real(a[13]);
                 return [=] {
                     RiskyAssetSwapOption o(makeSwap(s), makeDate(expiry), market, vol);
                     return json::value(o.NPV());
                 };
             }},
        });

    }

}
