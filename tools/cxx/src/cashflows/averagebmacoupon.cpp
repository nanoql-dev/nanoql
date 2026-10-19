// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/averagebmacoupon.hpp: what the wheel doesn't reach, which binds neither class: an AverageBMACoupon built
// directly, its fixingDates(), indexFixings() and rate(); and an AverageBMALeg built with every setter, the gearings
// and spreads the wheel's BMASwap never sets among them. A coupon is {"paymentDate", "nominal", "startDate", "endDate",
// "indexCurve", "gearing", "spread", "refPeriodStart", "refPeriodEnd", "dayCounter"}, on a BMAIndex forecasting on
// "indexCurve", a flat forward spec of indexes/indexspec.hpp or null for QuantLib's empty handle; a null reference date
// is Date(), a null day counter QuantLib's empty one. A leg is {"schedule", "indexCurve", "notionals",
// "paymentDayCounter", "paymentAdjustment", "gearings", "spreads"}, its schedule a spec of cashflows/couponspec.hpp; a
// number of gearings or spreads calls the setter taking one value, a list the one taking a vector, and an empty list
// leaves its setter uncalled, so that an empty notionals list is QuantLib's "no notional given". The leg's
// result is, per coupon, {"date", "nominal", "accrualStartDate", "accrualEndDate", "referencePeriodStart",
// "referencePeriodEnd", "dayCounter", "gearing", "spread", "fixingDates"}.

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflows/averagebmacoupon.hpp>
#include <ql/indexes/bmaindex.hpp>

#include <initializer_list>
#include <optional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct BmaCouponSpec {
            DateSpec paymentDate{true, 0}, startDate{true, 0}, endDate{true, 0};
            Real nominal = 0.0;
            indexes::CurveSpec indexCurve;
            Real gearing = 1.0, spread = 0.0;
            DateSpec refPeriodStart{true, 0}, refPeriodEnd{true, 0};
            bool emptyDayCounter = true;
            DayCounterSpec dayCounter;
        };

        struct BmaLegSpec {
            cashflows::ScheduleSpec schedule;
            indexes::CurveSpec indexCurve;
            std::vector<Real> notionals;
            bool emptyPaymentDayCounter = true;
            DayCounterSpec paymentDayCounter;
            BusinessDayConvention paymentAdjustment = Following;
            std::optional<Real> gearing, spread;  // a value for the setter taking one
            std::vector<Real> gearings, spreads;
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

        BmaCouponSpec coupon(const json::value& v) {
            const std::string what = "an average-BMA coupon";
            const json::object& o = object(v, what);
            onlyKeys(o, {"paymentDate", "nominal", "startDate", "endDate", "indexCurve", "gearing", "spread",
                         "refPeriodStart", "refPeriodEnd", "dayCounter"},
                     what);
            BmaCouponSpec s;
            s.paymentDate = date(key(o, "paymentDate", what));
            s.nominal = real(key(o, "nominal", what));
            s.startDate = date(key(o, "startDate", what));
            s.endDate = date(key(o, "endDate", what));
            s.indexCurve = indexes::curve(key(o, "indexCurve", what));
            s.gearing = real(key(o, "gearing", what));
            s.spread = real(key(o, "spread", what));
            s.refPeriodStart = date(key(o, "refPeriodStart", what));
            s.refPeriodEnd = date(key(o, "refPeriodEnd", what));
            const json::value& dc = key(o, "dayCounter", what);
            s.emptyDayCounter = dc.is_null();
            if (!s.emptyDayCounter)
                s.dayCounter = dayCounter(dc);
            return s;
        }

        BmaLegSpec leg(const json::value& v) {
            const std::string what = "an average-BMA leg";
            const json::object& o = object(v, what);
            onlyKeys(o, {"schedule", "indexCurve", "notionals", "paymentDayCounter", "paymentAdjustment", "gearings",
                         "spreads"},
                     what);
            BmaLegSpec s;
            s.schedule = cashflows::schedule(key(o, "schedule", what));
            s.indexCurve = indexes::curve(key(o, "indexCurve", what));
            s.notionals = reals(key(o, "notionals", what));
            const json::value& dc = key(o, "paymentDayCounter", what);
            s.emptyPaymentDayCounter = dc.is_null();
            if (!s.emptyPaymentDayCounter)
                s.paymentDayCounter = dayCounter(dc);
            s.paymentAdjustment = enumeration<BusinessDayConvention>(key(o, "paymentAdjustment", what));
            const json::value& gearings = key(o, "gearings", what);
            if (gearings.is_number())
                s.gearing = real(gearings);
            else
                s.gearings = reals(gearings);
            const json::value& spreads = key(o, "spreads", what);
            if (spreads.is_number())
                s.spread = real(spreads);
            else
                s.spreads = reals(spreads);
            return s;
        }

        ext::shared_ptr<AverageBMACoupon> makeCoupon(const BmaCouponSpec& s) {
            return ext::make_shared<AverageBMACoupon>(
                makeDate(s.paymentDate), s.nominal, makeDate(s.startDate), makeDate(s.endDate),
                ext::make_shared<BMAIndex>(indexes::makeCurve(s.indexCurve)), s.gearing, s.spread,
                makeDate(s.refPeriodStart), makeDate(s.refPeriodEnd),
                s.emptyDayCounter ? DayCounter() : makeDayCounter(s.dayCounter));
        }

        Leg makeLeg(const BmaLegSpec& s) {
            AverageBMALeg builder(cashflows::makeSchedule(s.schedule),
                                  ext::make_shared<BMAIndex>(indexes::makeCurve(s.indexCurve)));
            if (!s.notionals.empty())
                builder.withNotionals(s.notionals);
            if (!s.emptyPaymentDayCounter)
                builder.withPaymentDayCounter(makeDayCounter(s.paymentDayCounter));
            builder.withPaymentAdjustment(s.paymentAdjustment);
            if (s.gearing)
                builder.withGearings(*s.gearing);
            else if (!s.gearings.empty())
                builder.withGearings(s.gearings);
            if (s.spread)
                builder.withSpreads(*s.spread);
            else if (!s.spreads.empty())
                builder.withSpreads(s.spreads);
            return builder;
        }

        json::value dates(const std::vector<Date>& d) {
            json::array out;
            for (const Date& x : d)
                out.emplace_back(result(x));
            return out;
        }

        const Register unit({
            {"AverageBMACoupon::fixingDates",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 BmaCouponSpec s = coupon(a[0]);
                 return [=] { return dates(makeCoupon(s)->fixingDates()); };
             }},
            {"AverageBMACoupon::indexFixings",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 BmaCouponSpec s = coupon(a[0]);
                 return [=] {
                     json::array out;
                     for (Real x : makeCoupon(s)->indexFixings())
                         out.emplace_back(x);
                     return json::value(std::move(out));
                 };
             }},
            {"AverageBMACoupon::rate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 BmaCouponSpec s = coupon(a[0]);
                 return [=] { return json::value(makeCoupon(s)->rate()); };
             }},
            {"AverageBMALeg::operator Leg",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 BmaLegSpec s = leg(a[0]);
                 return [=] {
                     json::array out;
                     for (const auto& cf : makeLeg(s)) {
                         auto c = ext::dynamic_pointer_cast<AverageBMACoupon>(cf);
                         if (!c)
                             throw InvalidParams("an average-BMA leg's cash flow isn't an AverageBMACoupon");
                         out.emplace_back(json::object{{"date", result(c->date())},
                                                       {"nominal", c->nominal()},
                                                       {"accrualStartDate", result(c->accrualStartDate())},
                                                       {"accrualEndDate", result(c->accrualEndDate())},
                                                       {"referencePeriodStart", result(c->referencePeriodStart())},
                                                       {"referencePeriodEnd", result(c->referencePeriodEnd())},
                                                       {"dayCounter", c->dayCounter().name()},
                                                       {"gearing", c->gearing()},
                                                       {"spread", c->spread()},
                                                       {"fixingDates", dates(c->fixingDates())}});
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
