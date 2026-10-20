// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/makeyoyinflationcapfloor.hpp, which the wheel doesn't bind, and YoYInflationCapFloor::Type's stream
// text (ql/instruments/inflationcapfloor.hpp). "MakeYoYInflationCapFloor::build" takes the index (cashflows/
// inflationspec.hpp's) and a builder spec {"type", "length", "calendar", "observationLag", "interpolation", then the
// setters' arguments, each null for a setter not called: "nominal", "effectiveDate", "paymentDayCounter",
// "paymentAdjustment", "fixingDays", "asOptionlet", "strike", "atmStrike" (a nominal curve spec of
// indexes/indexspec.hpp), "forwardStart" ([length, units])}, called in that order on a builder made inside the call;
// the result is the instrument's type, capRates, floorRates, startDate and maturityDate and its coupons'
// accrualStartDate, accrualEndDate, date, nominal, fixingDate, fixingDays and dayCounter (its name).
// "YoYInflationCapFloor::typeText" takes a type as QuantLib's integer and gives its stream text.

#include "cashflows/inflationspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflows/yoyinflationcoupon.hpp>
#include <ql/instruments/inflationcapfloor.hpp>
#include <ql/instruments/makeyoyinflationcapfloor.hpp>

#include <initializer_list>
#include <optional>

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

        json::value reals(const std::vector<Real>& xs) {
            json::array out;
            for (Real x : xs)
                out.push_back(json::value(x));
            return out;
        }

        struct BuilderSpec {
            YoYInflationCapFloor::Type type = YoYInflationCapFloor::Cap;
            Integer length = 0;
            indexes::CalendarSpec calendar;
            cashflows::LagPeriod lag;
            CPI::InterpolationType interpolation = CPI::Flat;
            std::optional<Real> nominal;
            std::optional<DateSpec> effectiveDate;
            std::optional<DayCounterSpec> paymentDayCounter;
            std::optional<BusinessDayConvention> paymentAdjustment;
            std::optional<Integer> fixingDays;
            std::optional<bool> asOptionlet;
            std::optional<Real> strike;
            std::optional<indexes::CurveSpec> atmStrike;
            std::optional<cashflows::LagPeriod> forwardStart;
        };

        BuilderSpec builder(const json::value& v) {
            const std::string what = "a MakeYoYInflationCapFloor";
            const json::object& o = object(v, what);
            onlyKeys(o, {"type", "length", "calendar", "observationLag", "interpolation", "nominal", "effectiveDate",
                         "paymentDayCounter", "paymentAdjustment", "fixingDays", "asOptionlet", "strike", "atmStrike",
                         "forwardStart"},
                     what);
            BuilderSpec s;
            s.type = enumeration<YoYInflationCapFloor::Type>(key(o, "type", what));
            s.length = integer(key(o, "length", what));
            if (s.length == Null<Integer>() || s.length < 0)
                throw InvalidParams("expected the length as a Size");
            s.calendar = indexes::calendar(key(o, "calendar", what));
            s.lag = cashflows::lagPeriod(key(o, "observationLag", what));
            s.interpolation = enumeration<CPI::InterpolationType>(key(o, "interpolation", what));
            if (const json::value& x = key(o, "nominal", what); !x.is_null())
                s.nominal = real(x);
            if (const json::value& x = key(o, "effectiveDate", what); !x.is_null())
                s.effectiveDate = date(x);
            if (const json::value& x = key(o, "paymentDayCounter", what); !x.is_null())
                s.paymentDayCounter = dayCounter(x);
            if (const json::value& x = key(o, "paymentAdjustment", what); !x.is_null())
                s.paymentAdjustment = enumeration<BusinessDayConvention>(x);
            if (const json::value& x = key(o, "fixingDays", what); !x.is_null()) {
                s.fixingDays = integer(x);
                if (*s.fixingDays < 0)
                    throw InvalidParams("expected the fixing days as a Natural");
            }
            if (const json::value& x = key(o, "asOptionlet", what); !x.is_null())
                s.asOptionlet = boolean(x);
            if (const json::value& x = key(o, "strike", what); !x.is_null())
                s.strike = real(x);
            if (const json::value& x = key(o, "atmStrike", what); !x.is_null())
                s.atmStrike = indexes::curve(x);
            if (const json::value& x = key(o, "forwardStart", what); !x.is_null())
                s.forwardStart = cashflows::lagPeriod(x);
            return s;
        }

        json::value build(const BuilderSpec& s, const ext::shared_ptr<YoYInflationIndex>& index) {
            MakeYoYInflationCapFloor b(s.type, index, static_cast<Size>(s.length), indexes::makeCalendar(s.calendar),
                                       cashflows::makePeriod(s.lag), s.interpolation);
            if (s.nominal)
                b.withNominal(*s.nominal);
            if (s.effectiveDate)
                b.withEffectiveDate(makeDate(*s.effectiveDate));
            if (s.paymentDayCounter)
                b.withPaymentDayCounter(makeDayCounter(*s.paymentDayCounter));
            if (s.paymentAdjustment)
                b.withPaymentAdjustment(*s.paymentAdjustment);
            if (s.fixingDays)
                b.withFixingDays(static_cast<Natural>(*s.fixingDays));
            if (s.asOptionlet)
                b.asOptionlet(*s.asOptionlet);
            if (s.strike)
                b.withStrike(*s.strike);
            if (s.atmStrike)
                b.withAtmStrike(indexes::makeCurve(*s.atmStrike));
            if (s.forwardStart)
                b.withForwardStart(cashflows::makePeriod(*s.forwardStart));
            ext::shared_ptr<YoYInflationCapFloor> c = b;
            json::array coupons;
            for (const auto& cf : c->yoyLeg()) {
                auto coupon = ext::dynamic_pointer_cast<YoYInflationCoupon>(cf);
                coupons.push_back(json::object{
                    {"accrualStartDate", result(coupon->accrualStartDate())},
                    {"accrualEndDate", result(coupon->accrualEndDate())},
                    {"date", result(coupon->date())},
                    {"nominal", coupon->nominal()},
                    {"fixingDate", result(coupon->fixingDate())},
                    {"fixingDays", coupon->fixingDays()},
                    {"dayCounter", coupon->dayCounter().name()},
                });
            }
            return json::object{
                {"type", static_cast<int>(c->type())},
                {"capRates", reals(c->capRates())},
                {"floorRates", reals(c->floorRates())},
                {"startDate", result(c->startDate())},
                {"maturityDate", result(c->maturityDate())},
                {"coupons", coupons},
            };
        }

        const Register unit({
            // (index, builder)
            {"MakeYoYInflationCapFloor::build",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 BuilderSpec s = builder(a[1]);
                 return [=] { return build(s, cashflows::makeYoYIndex(i)); };
             }},
            // (type)
            {"YoYInflationCapFloor::typeText",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Integer t = integer(a[0]);
                 if (t == Null<Integer>() || t < 0 || t > 3)
                     throw InvalidParams("expected a type in the enum's range 0 to 3");
                 return [=] { return json::value(streamed(static_cast<YoYInflationCapFloor::Type>(t))); };
             }},
        });

    }

}
