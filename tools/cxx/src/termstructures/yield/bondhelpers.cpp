// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/yield/bondhelpers.hpp: what the wheel can't reach of BondHelper and FixedRateBondHelper, each
// helper built inside the call under the settings already set: its priceType(), and its implied quote and quote error
// against a given curve, the helper bound to it by setTermStructure, which the wheel doesn't bind (a curve spec of
// termstructures/yield/ratehelperspec.hpp, built inside the call and alive while the helper is read); and the helper's
// dates, on bonds the wheel can't build.
//
// A helper is {"type": "BondHelper", "price", "bond", "priceType"}, its bond a spec of instruments/bondspec.hpp, or
// {"type": "FixedRateBondHelper", "price", "settlementDays", "faceAmount", "schedule", "coupons", "dayCounter",
// "paymentConv", "redemption", "issueDate", "paymentCalendar", "exCouponPeriod", "priceType"}, with bondspec.hpp's
// schedule, calendar and ex-coupon period; the price is the quote's value.
//
//     BondHelper::priceType                        (helper): the price type, as QuantLib's integer
//     BondHelper::earliestDate, maturityDate, latestRelevantDate, pillarDate, latestDate
//                                                  (helper): the date
//     BondHelper::impliedQuote, quoteError         (helper, curve): the value bound to the curve

#include "convert.hpp"
#include "instruments/bondspec.hpp"
#include "termstructures/yield/ratehelperspec.hpp"

#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/yield/bondhelpers.hpp>

#include <cstdint>
#include <initializer_list>
#include <optional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct HelperSpec {
            bool fixedRate = false;
            Real price = 0.0;
            bonds::BondSpec bond;
            Natural settlementDays = 0;
            Real faceAmount = 0.0, redemption = 100.0;
            bonds::ScheduleSpec schedule;
            std::vector<Real> coupons;
            DayCounterSpec dayCounter;
            BusinessDayConvention paymentConv = Following;
            DateSpec issueDate{true, 0};
            std::optional<indexes::CalendarSpec> paymentCalendar;
            bool exCoupon = false;
            Integer exCouponLength = 0;
            TimeUnit exCouponUnits = Days;
            std::optional<indexes::CalendarSpec> exCouponCalendar;
            BusinessDayConvention exCouponConvention = Unadjusted;
            bool exCouponEndOfMonth = false;
            Bond::Price::Type priceType = Bond::Price::Clean;
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

        HelperSpec helper(const json::value& v) {
            const json::object& o = object(v, "a bond helper");
            HelperSpec s;
            std::string type = text(key(o, "type", "a bond helper"));
            s.price = real(key(o, "price", type));
            s.priceType = enumeration<Bond::Price::Type>(key(o, "priceType", type));
            if (type == "BondHelper") {
                onlyKeys(o, {"type", "price", "bond", "priceType"}, type);
                s.bond = bonds::bond(key(o, "bond", type));
                return s;
            }
            if (type != "FixedRateBondHelper")
                throw InvalidParams("unknown bond helper " + type);
            onlyKeys(o, {"type", "price", "settlementDays", "faceAmount", "schedule", "coupons", "dayCounter",
                         "paymentConv", "redemption", "issueDate", "paymentCalendar", "exCouponPeriod", "priceType"},
                     type);
            s.fixedRate = true;
            Integer days = integer(key(o, "settlementDays", type));
            if (days == Null<Integer>() || days < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(key(o, "settlementDays", type)));
            s.settlementDays = static_cast<Natural>(days);
            s.faceAmount = real(key(o, "faceAmount", type));
            s.schedule = bonds::schedule(key(o, "schedule", type));
            s.coupons = reals(key(o, "coupons", type));
            s.dayCounter = dayCounter(key(o, "dayCounter", type));
            s.paymentConv = enumeration<BusinessDayConvention>(key(o, "paymentConv", type));
            s.redemption = real(key(o, "redemption", type));
            s.issueDate = date(key(o, "issueDate", type));
            s.paymentCalendar = bonds::optionalCalendar(key(o, "paymentCalendar", type));
            if (const json::value& e = key(o, "exCouponPeriod", type); !e.is_null()) {
                const std::string ex = "an ex-coupon period";
                const json::object& eo = object(e, ex);
                onlyKeys(eo, {"period", "calendar", "convention", "endOfMonth"}, ex);
                const json::value& p = key(eo, "period", ex);
                if (!p.is_array() || p.get_array().size() != 2)
                    throw InvalidParams("expected a period as [length, units], got " + json::serialize(p));
                s.exCoupon = true;
                s.exCouponLength = integer(p.get_array()[0]);
                s.exCouponUnits = enumeration<TimeUnit>(p.get_array()[1]);
                s.exCouponCalendar = bonds::optionalCalendar(key(eo, "calendar", ex));
                s.exCouponConvention = enumeration<BusinessDayConvention>(key(eo, "convention", ex));
                s.exCouponEndOfMonth = boolean(key(eo, "endOfMonth", ex));
            }
            return s;
        }

        ext::shared_ptr<BondHelper> makeHelper(const HelperSpec& s) {
            Handle<Quote> price(ext::make_shared<SimpleQuote>(s.price));
            if (!s.fixedRate)
                return ext::make_shared<BondHelper>(price, bonds::makeBond(s.bond), s.priceType);
            return ext::make_shared<FixedRateBondHelper>(
                price, s.settlementDays, s.faceAmount, bonds::makeSchedule(s.schedule), s.coupons,
                makeDayCounter(s.dayCounter), s.paymentConv, s.redemption, makeDate(s.issueDate),
                bonds::makeCalendar(s.paymentCalendar),
                s.exCoupon ? Period(s.exCouponLength, s.exCouponUnits) : Period(),
                bonds::makeCalendar(s.exCouponCalendar), s.exCouponConvention, s.exCouponEndOfMonth, s.priceType);
        }

        // A member of the helper as built: f(helper).
        template <class F>
        Call built(const Args& a, F f) {
            arity(a, 1);
            HelperSpec h = helper(a[0]);
            return [=] { return json::value(f(*makeHelper(h))); };
        }

        // A member of the helper bound to the curve; the curve outlives the helper's use in the call.
        template <class F>
        Call bound(const Args& a, F f) {
            arity(a, 2);
            HelperSpec h = helper(a[0]);
            termstructures::BoundCurveSpec c = termstructures::boundCurve(a[1]);
            return [=] {
                ext::shared_ptr<YieldTermStructure> curve = termstructures::makeBoundCurve(c);
                ext::shared_ptr<BondHelper> x = makeHelper(h);
                x->setTermStructure(curve.get());
                return json::value(f(*x));
            };
        }

        const Register unit({
            {"BondHelper::priceType",
             [](const Args& a) {
                 return built(a, [](const BondHelper& x) { return static_cast<std::int64_t>(x.priceType()); });
             }},
            {"BondHelper::earliestDate",
             [](const Args& a) { return built(a, [](const BondHelper& x) { return result(x.earliestDate()); }); }},
            {"BondHelper::maturityDate",
             [](const Args& a) { return built(a, [](const BondHelper& x) { return result(x.maturityDate()); }); }},
            {"BondHelper::latestRelevantDate",
             [](const Args& a) {
                 return built(a, [](const BondHelper& x) { return result(x.latestRelevantDate()); });
             }},
            {"BondHelper::pillarDate",
             [](const Args& a) { return built(a, [](const BondHelper& x) { return result(x.pillarDate()); }); }},
            {"BondHelper::latestDate",
             [](const Args& a) { return built(a, [](const BondHelper& x) { return result(x.latestDate()); }); }},
            {"BondHelper::impliedQuote",
             [](const Args& a) { return bound(a, [](const BondHelper& x) { return x.impliedQuote(); }); }},
            {"BondHelper::quoteError",
             [](const Args& a) { return bound(a, [](const BondHelper& x) { return x.quoteError(); }); }},
        });

    }

}
