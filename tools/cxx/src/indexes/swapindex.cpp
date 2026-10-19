// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/indexes/swapindex.hpp: what the wheel doesn't reach, exogenousDiscount() and SwapIndex::underlyingSwap, the
// vanilla swap the base builds, which an OvernightIndexedSwapIndex's forecast and maturity date use too, though its own
// underlyingSwap hides it. A swap index is {"type": "SwapIndex", "familyName", "tenor", "settlementDays", "currency",
// "fixingCalendar", "fixedLegTenor", "fixedLegConvention", "fixedLegDayCounter", "iborIndex", "exogenous",
// "discountingTermStructure"}, built with the constructor taking a discounting curve when "exogenous" is set; or
// {"type": "OvernightIndexedSwapIndex", "familyName", "tenor", "settlementDays", "currency", "overnightIndex",
// "telescopicValueDates", "averagingMethod"} (tools/vectors/gen/indexes/swapindex.py's specs). A period is [length,
// units]; a currency "EUR", "USD", "GBP", or null for QuantLib's empty one; a calendar and a curve are specs of
// indexes/indexspec.hpp, a day counter one of convert.hpp, the Ibor index a spec of
// termstructures/yield/ratehelperspec.hpp and the overnight index an OvernightIndex spec of indexes/indexspec.hpp.

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/swapspec.hpp"
#include "termstructures/yield/ratehelperspec.hpp"

#include <ql/currencies/america.hpp>
#include <ql/currencies/europe.hpp>
#include <ql/indexes/swapindex.hpp>
#include <ql/instruments/vanillaswap.hpp>
#include <ql/utilities/null.hpp>

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <initializer_list>
#include <string>
#include <utility>

using namespace QuantLib;

namespace harness {

    namespace {

        struct SwapIndexSpec {
            std::string type;
            std::string familyName;
            std::pair<Integer, TimeUnit> tenor{0, Days};
            Natural settlementDays = 0;
            std::string currency;  // empty for QuantLib's empty currency
            indexes::CalendarSpec fixingCalendar;
            std::pair<Integer, TimeUnit> fixedLegTenor{0, Days};
            BusinessDayConvention fixedLegConvention = Following;
            DayCounterSpec fixedLegDayCounter;
            termstructures::HelperIndexSpec iborIndex;
            bool exogenous = false;
            indexes::CurveSpec discount;
            indexes::IndexSpec overnightIndex;
            bool telescopicValueDates = false;
            RateAveraging::Type averagingMethod = RateAveraging::Compound;
        };

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

        std::pair<Integer, TimeUnit> period(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected a period as [length, units], got " + json::serialize(v));
            return {integer(v.get_array()[0]), enumeration<TimeUnit>(v.get_array()[1])};
        }

        Natural natural(const json::value& v) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(n);
        }

        std::string currency(const json::value& v) {
            if (v.is_null())
                return {};
            std::string code = text(v);
            if (code != "EUR" && code != "USD" && code != "GBP")
                throw InvalidParams("unknown currency " + code);
            return code;
        }

        SwapIndexSpec swapIndex(const json::value& v) {
            const json::object& o = object(v, "a swap index");
            SwapIndexSpec s;
            s.type = text(key(o, "type", "a swap index"));
            if (s.type == "SwapIndex") {
                onlyKeys(o, {"type", "familyName", "tenor", "settlementDays", "currency", "fixingCalendar",
                             "fixedLegTenor", "fixedLegConvention", "fixedLegDayCounter", "iborIndex", "exogenous",
                             "discountingTermStructure"},
                         s.type);
                s.fixingCalendar = indexes::calendar(key(o, "fixingCalendar", s.type));
                s.fixedLegTenor = period(key(o, "fixedLegTenor", s.type));
                s.fixedLegConvention = enumeration<BusinessDayConvention>(key(o, "fixedLegConvention", s.type));
                s.fixedLegDayCounter = dayCounter(key(o, "fixedLegDayCounter", s.type));
                s.iborIndex = termstructures::helperIndex(key(o, "iborIndex", s.type));
                s.exogenous = boolean(key(o, "exogenous", s.type));
                s.discount = indexes::curve(key(o, "discountingTermStructure", s.type));
            } else if (s.type == "OvernightIndexedSwapIndex") {
                onlyKeys(o, {"type", "familyName", "tenor", "settlementDays", "currency", "overnightIndex",
                             "telescopicValueDates", "averagingMethod"},
                         s.type);
                s.overnightIndex = indexes::iborIndex(key(o, "overnightIndex", s.type));
                if (s.overnightIndex.type != "OvernightIndex")
                    throw InvalidParams("an overnight-indexed swap index takes an OvernightIndex spec");
                s.telescopicValueDates = boolean(key(o, "telescopicValueDates", s.type));
                s.averagingMethod = enumeration<RateAveraging::Type>(key(o, "averagingMethod", s.type));
            } else {
                throw InvalidParams("unknown swap index " + s.type);
            }
            s.familyName = text(key(o, "familyName", s.type));
            s.tenor = period(key(o, "tenor", s.type));
            s.settlementDays = natural(key(o, "settlementDays", s.type));
            s.currency = currency(key(o, "currency", s.type));
            return s;
        }

        Currency makeCurrency(const std::string& code) {
            if (code.empty())
                return Currency();
            if (code == "EUR")
                return EURCurrency();
            if (code == "USD")
                return USDCurrency();
            if (code == "GBP")
                return GBPCurrency();
            throw InvalidParams("the currency " + code + " was not checked");
        }

        ext::shared_ptr<SwapIndex> makeSwapIndex(const SwapIndexSpec& s) {
            Period tenor(s.tenor.first, s.tenor.second);
            if (s.type == "OvernightIndexedSwapIndex") {
                auto overnight = ext::dynamic_pointer_cast<OvernightIndex>(indexes::makeIborIndex(s.overnightIndex));
                if (!overnight)
                    throw InvalidParams("the overnight index spec built no OvernightIndex");
                return ext::make_shared<OvernightIndexedSwapIndex>(s.familyName, tenor, s.settlementDays,
                                                                   makeCurrency(s.currency), overnight,
                                                                   s.telescopicValueDates, s.averagingMethod);
            }
            Period fixedLegTenor(s.fixedLegTenor.first, s.fixedLegTenor.second);
            if (s.exogenous)
                return ext::make_shared<SwapIndex>(s.familyName, tenor, s.settlementDays, makeCurrency(s.currency),
                                                   indexes::makeCalendar(s.fixingCalendar), fixedLegTenor,
                                                   s.fixedLegConvention, makeDayCounter(s.fixedLegDayCounter),
                                                   termstructures::makeHelperIndex(s.iborIndex),
                                                   indexes::makeCurve(s.discount));
            return ext::make_shared<SwapIndex>(s.familyName, tenor, s.settlementDays, makeCurrency(s.currency),
                                               indexes::makeCalendar(s.fixingCalendar), fixedLegTenor,
                                               s.fixedLegConvention, makeDayCounter(s.fixedLegDayCounter),
                                               termstructures::makeHelperIndex(s.iborIndex));
        }

        // A schedule's size and its first and last few dates: a daily schedule's dates would be thousands.
        json::value scheduleEnds(const Schedule& schedule, std::size_t n) {
            const std::vector<Date>& dates = schedule.dates();
            json::array front, back;
            std::size_t k = std::min(n, dates.size());
            for (std::size_t i = 0; i < k; ++i) {
                front.emplace_back(result(dates[i]));
                back.emplace_back(result(dates[dates.size() - k + i]));
            }
            json::object o;
            o["size"] = static_cast<std::int64_t>(dates.size());
            o["front"] = std::move(front);
            o["back"] = std::move(back);
            return o;
        }

        const Register unit({
            {"SwapIndex::exogenousDiscount",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 SwapIndexSpec s = swapIndex(a[0]);
                 return [=] { return json::value(makeSwapIndex(s)->exogenousDiscount()); };
             }},
            // The base's swap, called on the base for an overnight-indexed swap index too: its start and maturity
            // dates, its fixed rate, the fixed schedule's dates, the floating schedule's size and first and last 5
            // dates, and each leg's number of cash flows.
            {"SwapIndex::underlyingSwap",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 SwapIndexSpec s = swapIndex(a[0]);
                 DateSpec d = date(a[1]);
                 return [=] {
                     ext::shared_ptr<SwapIndex> index = makeSwapIndex(s);
                     ext::shared_ptr<VanillaSwap> swap = index->SwapIndex::underlyingSwap(makeDate(d));
                     json::object o;
                     o["startDate"] = result(swap->startDate());
                     o["maturityDate"] = result(swap->maturityDate());
                     o["fixedRate"] = swap->fixedRate();
                     o["fixedSchedule"] = swaps::scheduleDates(swap->fixedSchedule());
                     o["floatingSchedule"] = scheduleEnds(swap->floatingSchedule(), 5);
                     o["fixedLegSize"] = static_cast<std::int64_t>(swap->fixedLeg().size());
                     o["floatingLegSize"] = static_cast<std::int64_t>(swap->floatingLeg().size());
                     return json::value(std::move(o));
                 };
             }},
        });

    }

}
