// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The inflation swaps' and CPIBond's inspectors, which the wheel doesn't bind:
// ql/instruments/zerocouponinflationswap.hpp, yearonyearinflationswap.hpp, cpiswap.hpp and bonds/cpibond.hpp. Each
// method takes the index (cashflows/ inflationspec.hpp's) and the instrument's spec, with the wheel generators' input
// names (tools/vectors/gen/instruments/zerocouponinflationswap.py, yearonyearinflationswap.py, cpiswap.py and
// bonds/cpibond.py; schedules as cashflows/couponspec.hpp reads them, the CPI swap's floating index as
// indexes/indexspec.hpp's), builds the instrument inside the call and gives its inspectors as an object: a calendar or
// day counter by its name (null for an empty one), an index by its name, a lag as [length, units], a schedule as its
// dates, a Null<Real> as null, and the enums as QuantLib's integers.

#include "cashflows/couponspec.hpp"
#include "cashflows/inflationspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/instruments/bonds/cpibond.hpp>
#include <ql/instruments/cpiswap.hpp>
#include <ql/instruments/yearonyearinflationswap.hpp>
#include <ql/instruments/zerocouponinflationswap.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness {

    namespace {

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            return v.get_object();
        }

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        json::value calendarName(const Calendar& c) {
            if (c.empty())
                return nullptr;
            return json::value(c.name());
        }

        json::value dayCounterName(const DayCounter& d) {
            if (d.empty())
                return nullptr;
            return json::value(d.name());
        }

        json::value lagResult(const Period& p) {
            return json::array{json::value(p.length()), json::value(static_cast<int>(p.units()))};
        }

        json::value scheduleDates(const Schedule& s) {
            json::array out;
            for (const Date& d : s.dates())
                out.push_back(result(d));
            return out;
        }

        json::value nullableReal(Real x) {
            if (x == Null<Real>())
                return nullptr;
            return json::value(x);
        }

        int integerOf(int x) { return x; }

        // ZeroCouponInflationSwap

        struct ZeroSwapSpec {
            Swap::Type type = Swap::Payer;
            Real nominal = 0.0;
            DateSpec startDate{true, 0}, maturity{true, 0};
            indexes::CalendarSpec fixCalendar;
            BusinessDayConvention fixConvention = Following;
            DayCounterSpec dayCounter;
            Rate fixedRate = 0.0;
            cashflows::LagPeriod lag;
            CPI::InterpolationType interpolation = CPI::Flat;
            bool hasInfCalendar = false;
            indexes::CalendarSpec infCalendar;
            bool hasInfConvention = false;
            BusinessDayConvention infConvention = Following;
        };

        ZeroSwapSpec zeroSwap(const json::value& v) {
            const std::string what = "a zero-coupon inflation swap";
            const json::object& o = object(v, what);
            onlyKeys(o, {"type", "nominal", "startDate", "maturity", "fixCalendar", "fixConvention", "dayCounter",
                         "fixedRate", "observationLag", "interpolation", "infCalendar", "infConvention"},
                     what);
            ZeroSwapSpec s;
            s.type = enumeration<Swap::Type>(key(o, "type", what));
            s.nominal = real(key(o, "nominal", what));
            s.startDate = date(key(o, "startDate", what));
            s.maturity = date(key(o, "maturity", what));
            s.fixCalendar = indexes::calendar(key(o, "fixCalendar", what));
            s.fixConvention = enumeration<BusinessDayConvention>(key(o, "fixConvention", what));
            s.dayCounter = dayCounter(key(o, "dayCounter", what));
            s.fixedRate = real(key(o, "fixedRate", what));
            s.lag = cashflows::lagPeriod(key(o, "observationLag", what));
            s.interpolation = enumeration<CPI::InterpolationType>(key(o, "interpolation", what));
            const json::value& infCalendar = key(o, "infCalendar", what);
            s.hasInfCalendar = !infCalendar.is_null();
            if (s.hasInfCalendar)
                s.infCalendar = indexes::calendar(infCalendar);
            const json::value& infConvention = key(o, "infConvention", what);
            s.hasInfConvention = !infConvention.is_null();
            if (s.hasInfConvention)
                s.infConvention = enumeration<BusinessDayConvention>(infConvention);
            return s;
        }

        json::value zeroSwapInspectors(const ZeroSwapSpec& s, const ext::shared_ptr<ZeroInflationIndex>& index) {
            Calendar fixCalendar = indexes::makeCalendar(s.fixCalendar);
            // QuantLib's defaults: an empty calendar and BusinessDayConvention(), each replaced by the fixed leg's
            Calendar infCalendar = s.hasInfCalendar ? indexes::makeCalendar(s.infCalendar) : Calendar();
            BusinessDayConvention infConvention = s.hasInfConvention ? s.infConvention : BusinessDayConvention();
            ZeroCouponInflationSwap swap(s.type, s.nominal, makeDate(s.startDate), makeDate(s.maturity), fixCalendar,
                                         s.fixConvention, makeDayCounter(s.dayCounter), s.fixedRate, index,
                                         cashflows::makePeriod(s.lag), s.interpolation, false, infCalendar,
                                         infConvention);
            return json::object{
                {"type", integerOf(swap.type())},
                {"nominal", swap.nominal()},
                {"fixedCalendar", calendarName(swap.fixedCalendar())},
                {"fixedConvention", integerOf(swap.fixedConvention())},
                {"dayCounter", dayCounterName(swap.dayCounter())},
                {"fixedRate", swap.fixedRate()},
                {"inflationIndex", swap.inflationIndex()->name()},
                {"observationLag", lagResult(swap.observationLag())},
                {"observationInterpolation", integerOf(swap.observationInterpolation())},
                {"adjustObservationDates", swap.adjustObservationDates()},
                {"inflationCalendar", calendarName(swap.inflationCalendar())},
                {"inflationConvention", integerOf(swap.inflationConvention())},
            };
        }

        // YearOnYearInflationSwap

        struct YoYSwapSpec {
            Swap::Type type = Swap::Payer;
            Real nominal = 0.0;
            cashflows::ScheduleSpec fixedSchedule, yoySchedule;
            Rate fixedRate = 0.0;
            DayCounterSpec fixedDayCount, yoyDayCount;
            cashflows::LagPeriod lag;
            CPI::InterpolationType interpolation = CPI::Flat;
            Spread spread = 0.0;
            indexes::CalendarSpec paymentCalendar;
            BusinessDayConvention paymentConvention = ModifiedFollowing;
        };

        YoYSwapSpec yoySwap(const json::value& v) {
            const std::string what = "a year-on-year inflation swap";
            const json::object& o = object(v, what);
            onlyKeys(o, {"type", "nominal", "fixedSchedule", "fixedRate", "fixedDayCount", "yoySchedule",
                         "observationLag", "interpolation", "spread", "yoyDayCount", "paymentCalendar",
                         "paymentConvention"},
                     what);
            YoYSwapSpec s;
            s.type = enumeration<Swap::Type>(key(o, "type", what));
            s.nominal = real(key(o, "nominal", what));
            s.fixedSchedule = cashflows::schedule(key(o, "fixedSchedule", what));
            s.fixedRate = real(key(o, "fixedRate", what));
            s.fixedDayCount = dayCounter(key(o, "fixedDayCount", what));
            s.yoySchedule = cashflows::schedule(key(o, "yoySchedule", what));
            s.lag = cashflows::lagPeriod(key(o, "observationLag", what));
            s.interpolation = enumeration<CPI::InterpolationType>(key(o, "interpolation", what));
            s.spread = real(key(o, "spread", what));
            s.yoyDayCount = dayCounter(key(o, "yoyDayCount", what));
            s.paymentCalendar = indexes::calendar(key(o, "paymentCalendar", what));
            s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", what));
            return s;
        }

        json::value yoySwapInspectors(const YoYSwapSpec& s, const ext::shared_ptr<YoYInflationIndex>& index) {
            YearOnYearInflationSwap swap(s.type, s.nominal, cashflows::makeSchedule(s.fixedSchedule), s.fixedRate,
                                         makeDayCounter(s.fixedDayCount), cashflows::makeSchedule(s.yoySchedule),
                                         index, cashflows::makePeriod(s.lag), s.interpolation, s.spread,
                                         makeDayCounter(s.yoyDayCount), indexes::makeCalendar(s.paymentCalendar),
                                         s.paymentConvention);
            return json::object{
                {"type", integerOf(swap.type())},
                {"nominal", swap.nominal()},
                {"fixedSchedule", scheduleDates(swap.fixedSchedule())},
                {"fixedRate", swap.fixedRate()},
                {"fixedDayCount", dayCounterName(swap.fixedDayCount())},
                {"yoySchedule", scheduleDates(swap.yoySchedule())},
                {"yoyInflationIndex", swap.yoyInflationIndex()->name()},
                {"observationLag", lagResult(swap.observationLag())},
                {"spread", swap.spread()},
                {"yoyDayCount", dayCounterName(swap.yoyDayCount())},
                {"paymentCalendar", calendarName(swap.paymentCalendar())},
                {"paymentConvention", integerOf(swap.paymentConvention())},
            };
        }

        // CPISwap

        struct CPISwapSpec {
            Swap::Type type = Swap::Payer;
            Real nominal = 0.0;
            bool subtractInflationNominal = true;
            Spread spread = 0.0;
            DayCounterSpec floatDayCount, fixedDayCount;
            cashflows::ScheduleSpec floatSchedule, fixedSchedule;
            BusinessDayConvention floatRoll = ModifiedFollowing, fixedRoll = ModifiedFollowing;
            Integer fixingDays = 0;
            indexes::IndexSpec floatIndex;
            Rate fixedRate = 0.0;
            Real baseCPI = Null<Real>();
            cashflows::LagPeriod lag;
            CPI::InterpolationType interpolation = CPI::Flat;
            Real inflationNominal = Null<Real>();
        };

        CPISwapSpec cpiSwap(const json::value& v) {
            const std::string what = "a CPI swap";
            const json::object& o = object(v, what);
            onlyKeys(o, {"type", "nominal", "subtractInflationNominal", "spread", "floatDayCount", "floatSchedule",
                         "floatRoll", "fixingDays", "floatIndex", "fixedRate", "baseCPI", "fixedDayCount",
                         "fixedSchedule", "fixedRoll", "observationLag", "interpolation", "inflationNominal"},
                     what);
            CPISwapSpec s;
            s.type = enumeration<Swap::Type>(key(o, "type", what));
            s.nominal = real(key(o, "nominal", what));
            s.subtractInflationNominal = boolean(key(o, "subtractInflationNominal", what));
            s.spread = real(key(o, "spread", what));
            s.floatDayCount = dayCounter(key(o, "floatDayCount", what));
            s.floatSchedule = cashflows::schedule(key(o, "floatSchedule", what));
            s.floatRoll = enumeration<BusinessDayConvention>(key(o, "floatRoll", what));
            s.fixingDays = integer(key(o, "fixingDays", what));
            if (s.fixingDays == Null<Integer>() || s.fixingDays < 0)
                throw InvalidParams("expected the fixing days as a Natural");
            s.floatIndex = indexes::iborIndex(key(o, "floatIndex", what));
            s.fixedRate = real(key(o, "fixedRate", what));
            s.baseCPI = real(key(o, "baseCPI", what));
            s.fixedDayCount = dayCounter(key(o, "fixedDayCount", what));
            s.fixedSchedule = cashflows::schedule(key(o, "fixedSchedule", what));
            s.fixedRoll = enumeration<BusinessDayConvention>(key(o, "fixedRoll", what));
            s.lag = cashflows::lagPeriod(key(o, "observationLag", what));
            s.interpolation = enumeration<CPI::InterpolationType>(key(o, "interpolation", what));
            s.inflationNominal = real(key(o, "inflationNominal", what));
            return s;
        }

        json::value cpiSwapInspectors(const CPISwapSpec& s, const ext::shared_ptr<ZeroInflationIndex>& index) {
            CPISwap swap(s.type, s.nominal, s.subtractInflationNominal, s.spread, makeDayCounter(s.floatDayCount),
                         cashflows::makeSchedule(s.floatSchedule), s.floatRoll, static_cast<Natural>(s.fixingDays),
                         indexes::makeIborIndex(s.floatIndex), s.fixedRate, s.baseCPI, makeDayCounter(s.fixedDayCount),
                         cashflows::makeSchedule(s.fixedSchedule), s.fixedRoll, cashflows::makePeriod(s.lag), index,
                         s.interpolation, s.inflationNominal);
            return json::object{
                {"type", integerOf(swap.type())},
                {"nominal", swap.nominal()},
                {"subtractInflationNominal", swap.subtractInflationNominal()},
                {"spread", swap.spread()},
                {"floatDayCount", dayCounterName(swap.floatDayCount())},
                {"floatSchedule", scheduleDates(swap.floatSchedule())},
                {"floatPaymentRoll", integerOf(swap.floatPaymentRoll())},
                {"fixingDays", swap.fixingDays()},
                {"floatIndex", swap.floatIndex()->name()},
                {"fixedRate", swap.fixedRate()},
                {"baseCPI", nullableReal(swap.baseCPI())},
                {"fixedDayCount", dayCounterName(swap.fixedDayCount())},
                {"fixedSchedule", scheduleDates(swap.fixedSchedule())},
                {"fixedPaymentRoll", integerOf(swap.fixedPaymentRoll())},
                {"observationLag", lagResult(swap.observationLag())},
                {"fixedIndex", swap.fixedIndex()->name()},
                {"observationInterpolation", integerOf(swap.observationInterpolation())},
                {"inflationNominal", swap.inflationNominal()},
            };
        }

        // CPIBond

        struct CPIBondSpec {
            Integer settlementDays = 0;
            Real faceAmount = 0.0;
            Real baseCPI = Null<Real>();
            cashflows::LagPeriod lag;
            CPI::InterpolationType interpolation = CPI::Flat;
            cashflows::ScheduleSpec schedule;
            std::vector<Rate> coupons;
            DayCounterSpec accrualDayCounter;
            BusinessDayConvention paymentConvention = ModifiedFollowing;
            DateSpec issueDate{true, 0};
            indexes::CalendarSpec paymentCalendar;
        };

        CPIBondSpec cpiBond(const json::value& v) {
            const std::string what = "a CPI bond";
            const json::object& o = object(v, what);
            onlyKeys(o, {"settlementDays", "faceAmount", "baseCPI", "observationLag", "interpolation", "schedule",
                         "coupons", "accrualDayCounter", "paymentConvention", "issueDate", "paymentCalendar",
                         "exCouponPeriod"},
                     what);
            CPIBondSpec s;
            s.settlementDays = integer(key(o, "settlementDays", what));
            if (s.settlementDays == Null<Integer>() || s.settlementDays < 0)
                throw InvalidParams("expected the settlement days as a Natural");
            s.faceAmount = real(key(o, "faceAmount", what));
            s.baseCPI = real(key(o, "baseCPI", what));
            s.lag = cashflows::lagPeriod(key(o, "observationLag", what));
            s.interpolation = enumeration<CPI::InterpolationType>(key(o, "interpolation", what));
            s.schedule = cashflows::schedule(key(o, "schedule", what));
            s.coupons = reals(key(o, "coupons", what));
            s.accrualDayCounter = dayCounter(key(o, "accrualDayCounter", what));
            s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", what));
            s.issueDate = date(key(o, "issueDate", what));
            s.paymentCalendar = indexes::calendar(key(o, "paymentCalendar", what));
            if (!key(o, "exCouponPeriod", what).is_null())
                throw InvalidParams("the harness's CPI bonds take no ex-coupon period");
            return s;
        }

        json::value cpiBondInspectors(const CPIBondSpec& s, const ext::shared_ptr<ZeroInflationIndex>& index) {
            CPIBond bond(static_cast<Natural>(s.settlementDays), s.faceAmount, s.baseCPI, cashflows::makePeriod(s.lag),
                         index, s.interpolation, cashflows::makeSchedule(s.schedule), s.coupons,
                         makeDayCounter(s.accrualDayCounter), s.paymentConvention, makeDate(s.issueDate),
                         indexes::makeCalendar(s.paymentCalendar));
            return json::object{
                {"frequency", integerOf(bond.frequency())},
                {"dayCounter", dayCounterName(bond.dayCounter())},
                {"growthOnly", bond.growthOnly()},
                {"baseCPI", nullableReal(bond.baseCPI())},
                {"observationLag", lagResult(bond.observationLag())},
                {"cpiIndex", bond.cpiIndex()->name()},
                {"observationInterpolation", integerOf(bond.observationInterpolation())},
            };
        }

        const Register unit({
            // (index, spec), each
            {"ZeroCouponInflationSwap::inspectors",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 ZeroSwapSpec s = zeroSwap(a[1]);
                 return [=] { return zeroSwapInspectors(s, cashflows::makeZeroIndex(i)); };
             }},
            {"YearOnYearInflationSwap::inspectors",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 YoYSwapSpec s = yoySwap(a[1]);
                 return [=] { return yoySwapInspectors(s, cashflows::makeYoYIndex(i)); };
             }},
            {"CPISwap::inspectors",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 CPISwapSpec s = cpiSwap(a[1]);
                 return [=] { return cpiSwapInspectors(s, cashflows::makeZeroIndex(i)); };
             }},
            {"CPIBond::inspectors",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 CPIBondSpec s = cpiBond(a[1]);
                 return [=] { return cpiBondInspectors(s, cashflows::makeZeroIndex(i)); };
             }},
        });

    }

}
