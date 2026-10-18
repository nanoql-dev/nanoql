// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "cashflows/couponspec.hpp"

#include <ql/cashflows/capflooredcoupon.hpp>
#include <ql/cashflows/coupon.hpp>
#include <ql/cashflows/iborcoupon.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/optionlet/constantoptionletvol.hpp>
#include <ql/utilities/null.hpp>

#include <algorithm>
#include <initializer_list>
#include <utility>

using namespace QuantLib;

namespace harness::cashflows {

    namespace {

        // The harness's pricer of any floating-rate coupon (couponspec.hpp).
        class IndexFixingPricer : public FloatingRateCouponPricer {
          public:
            explicit IndexFixingPricer(Real adjustment) : adjustment_(adjustment) {}
            void initialize(const FloatingRateCoupon& coupon) override { coupon_ = &coupon; }
            Rate swapletRate() const override {
                return coupon_->gearing() * (coupon_->indexFixing() + adjustment_) + coupon_->spread();
            }
            Real swapletPrice() const override { return swapletRate() * coupon_->accrualPeriod(); }
            Rate capletRate(Rate effectiveCap) const override {
                return coupon_->gearing() * std::max(coupon_->indexFixing() - effectiveCap, 0.0);
            }
            Real capletPrice(Rate effectiveCap) const override {
                return capletRate(effectiveCap) * coupon_->accrualPeriod();
            }
            Rate floorletRate(Rate effectiveFloor) const override {
                return coupon_->gearing() * std::max(effectiveFloor - coupon_->indexFixing(), 0.0);
            }
            Real floorletPrice(Rate effectiveFloor) const override {
                return floorletRate(effectiveFloor) * coupon_->accrualPeriod();
            }

          private:
            Real adjustment_;
            const FloatingRateCoupon* coupon_ = nullptr;
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

        const json::array& array(const json::value& v, const std::string& what) {
            if (!v.is_array())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            return v.get_array();
        }

        // A Natural, or null for Null<Natural>(); a negative count is the harness's refusal.
        Natural natural(const json::value& v) {
            if (v.is_null())
                return Null<Natural>();
            Integer n = integer(v);
            if (n < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(n);
        }

        // A day counter, or null for QuantLib's empty one.
        void optionalDayCounter(const json::value& v, bool& empty, DayCounterSpec& s) {
            empty = v.is_null();
            if (!empty)
                s = dayCounter(v);
        }

        DayCounter makeOptionalDayCounter(bool empty, const DayCounterSpec& s) {
            return empty ? DayCounter() : makeDayCounter(s);
        }

    }

    VolatilitySpec volatility(const json::value& v) {
        VolatilitySpec s;
        if (v.is_null())
            return s;
        const std::string what = "a volatility";
        const json::object& o = object(v, what);
        onlyKeys(o, {"referenceDate", "calendar", "convention", "volatility", "dayCounter", "volatilityType",
                     "displacement"},
                 what);
        s.null = false;
        s.referenceDate = date(key(o, "referenceDate", what));
        s.calendar = indexes::calendar(key(o, "calendar", what));
        s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
        s.volatility = real(key(o, "volatility", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        s.type = enumeration<VolatilityType>(key(o, "volatilityType", what));
        s.displacement = real(key(o, "displacement", what));
        return s;
    }

    Handle<OptionletVolatilityStructure> makeVolatility(const VolatilitySpec& s) {
        if (s.null)
            return {};
        return Handle<OptionletVolatilityStructure>(ext::make_shared<ConstantOptionletVolatility>(
            makeDate(s.referenceDate), indexes::makeCalendar(s.calendar), s.convention, s.volatility,
            makeDayCounter(s.dayCounter), s.type, s.displacement));
    }

    PricerSpec pricer(const json::value& v) {
        PricerSpec s;
        if (v.is_null())
            return s;
        const json::object& o = object(v, "a pricer");
        s.type = text(key(o, "type", "a pricer"));
        if (s.type == "BlackIborCouponPricer") {
            onlyKeys(o, {"type", "volatility", "timingAdjustment", "correlation", "useIndexedCoupon"}, s.type);
            s.volatility = volatility(key(o, "volatility", s.type));
            s.timing = enumeration<BlackIborCouponPricer::TimingAdjustment>(key(o, "timingAdjustment", s.type));
            s.correlation = real(key(o, "correlation", s.type));
            s.useIndexedCoupon = optionalBoolean(key(o, "useIndexedCoupon", s.type));
        } else if (s.type == "IndexFixingPricer") {
            onlyKeys(o, {"type", "adjustment"}, s.type);
            s.adjustment = real(key(o, "adjustment", s.type));
        } else {
            throw InvalidParams("unknown pricer " + s.type);
        }
        return s;
    }

    std::vector<PricerSpec> pricers(const json::value& v) {
        std::vector<PricerSpec> out;
        for (const json::value& p : array(v, "a list of pricers"))
            out.push_back(pricer(p));
        return out;
    }

    ext::shared_ptr<FloatingRateCouponPricer> makePricer(const PricerSpec& s) {
        if (s.type.empty())
            return {};
        if (s.type == "BlackIborCouponPricer")
            return ext::make_shared<BlackIborCouponPricer>(
                makeVolatility(s.volatility), s.timing,
                Handle<Quote>(ext::make_shared<SimpleQuote>(s.correlation)), s.useIndexedCoupon);
        if (s.type == "IndexFixingPricer")
            return ext::make_shared<IndexFixingPricer>(s.adjustment);
        throw InvalidParams("the pricer " + s.type + " was not checked");
    }

    CouponSpec coupon(const json::value& v) {
        const json::object& o = object(v, "a coupon");
        CouponSpec s;
        s.type = text(key(o, "type", "a coupon"));
        if (s.type == "CappedFlooredCoupon") {
            onlyKeys(o, {"type", "underlying", "cap", "floor", "pricer"}, s.type);
            s.underlying = std::make_shared<CouponSpec>(coupon(key(o, "underlying", s.type)));
        } else if (s.type == "IborCoupon" || s.type == "FloatingRateCoupon" || s.type == "CappedFlooredIborCoupon") {
            if (s.type == "CappedFlooredIborCoupon")
                onlyKeys(o, {"type", "paymentDate", "nominal", "startDate", "endDate", "fixingDays", "index",
                             "gearing", "spread", "refPeriodStart", "refPeriodEnd", "dayCounter", "isInArrears",
                             "exCouponDate", "fixingConvention", "pricer", "cap", "floor"},
                         s.type);
            else
                onlyKeys(o, {"type", "paymentDate", "nominal", "startDate", "endDate", "fixingDays", "index",
                             "gearing", "spread", "refPeriodStart", "refPeriodEnd", "dayCounter", "isInArrears",
                             "exCouponDate", "fixingConvention", "pricer"},
                         s.type);
            s.paymentDate = date(key(o, "paymentDate", s.type));
            s.nominal = real(key(o, "nominal", s.type));
            s.startDate = date(key(o, "startDate", s.type));
            s.endDate = date(key(o, "endDate", s.type));
            s.fixingDays = natural(key(o, "fixingDays", s.type));
            s.index = indexes::iborIndex(key(o, "index", s.type));
            s.gearing = real(key(o, "gearing", s.type));
            s.spread = real(key(o, "spread", s.type));
            s.refPeriodStart = date(key(o, "refPeriodStart", s.type));
            s.refPeriodEnd = date(key(o, "refPeriodEnd", s.type));
            optionalDayCounter(key(o, "dayCounter", s.type), s.emptyDayCounter, s.dayCounter);
            s.isInArrears = boolean(key(o, "isInArrears", s.type));
            s.exCouponDate = date(key(o, "exCouponDate", s.type));
            s.fixingConvention = enumeration<BusinessDayConvention>(key(o, "fixingConvention", s.type));
        } else {
            throw InvalidParams("unknown coupon " + s.type);
        }
        if (s.type == "CappedFlooredCoupon" || s.type == "CappedFlooredIborCoupon") {
            s.cap = real(key(o, "cap", s.type));
            s.floor = real(key(o, "floor", s.type));
        }
        s.pricer = pricer(key(o, "pricer", s.type));
        return s;
    }

    std::vector<CouponSpec> coupons(const json::value& v) {
        std::vector<CouponSpec> out;
        for (const json::value& c : array(v, "a list of coupons"))
            out.push_back(coupon(c));
        return out;
    }

    ext::shared_ptr<FloatingRateCoupon> makeCoupon(const CouponSpec& s) {
        ext::shared_ptr<FloatingRateCoupon> c;
        if (s.type == "CappedFlooredCoupon") {
            c = ext::make_shared<CappedFlooredCoupon>(makeCoupon(*s.underlying), s.cap, s.floor);
        } else {
            const ext::shared_ptr<IborIndex> index = indexes::makeIborIndex(s.index);
            const DayCounter dc = makeOptionalDayCounter(s.emptyDayCounter, s.dayCounter);
            if (s.type == "IborCoupon")
                c = ext::make_shared<IborCoupon>(makeDate(s.paymentDate), s.nominal, makeDate(s.startDate),
                                                 makeDate(s.endDate), s.fixingDays, index, s.gearing, s.spread,
                                                 makeDate(s.refPeriodStart), makeDate(s.refPeriodEnd), dc,
                                                 s.isInArrears, makeDate(s.exCouponDate), s.fixingConvention);
            else if (s.type == "FloatingRateCoupon")
                c = ext::make_shared<FloatingRateCoupon>(makeDate(s.paymentDate), s.nominal, makeDate(s.startDate),
                                                         makeDate(s.endDate), s.fixingDays, index, s.gearing,
                                                         s.spread, makeDate(s.refPeriodStart),
                                                         makeDate(s.refPeriodEnd), dc, s.isInArrears,
                                                         makeDate(s.exCouponDate), s.fixingConvention);
            else
                c = ext::make_shared<CappedFlooredIborCoupon>(
                    makeDate(s.paymentDate), s.nominal, makeDate(s.startDate), makeDate(s.endDate), s.fixingDays,
                    index, s.gearing, s.spread, s.cap, s.floor, makeDate(s.refPeriodStart),
                    makeDate(s.refPeriodEnd), dc, s.isInArrears, makeDate(s.exCouponDate), s.fixingConvention);
        }
        const ext::shared_ptr<FloatingRateCouponPricer> p = makePricer(s.pricer);
        if (p)
            c->setPricer(p);
        return c;
    }

    Leg makeCoupons(const std::vector<CouponSpec>& s) {
        Leg out;
        for (const CouponSpec& c : s)
            out.push_back(makeCoupon(c));
        return out;
    }

    ScheduleSpec schedule(const json::value& v) {
        const std::string what = "a schedule";
        const json::object& o = object(v, what);
        ScheduleSpec s;
        if (o.contains("dates")) {
            onlyKeys(o, {"dates"}, what);
            s.fromDates = true;
            for (const json::value& d : array(key(o, "dates", what), "a list of dates"))
                s.dates.push_back(date(d));
            return s;
        }
        onlyKeys(o, {"effectiveDate", "terminationDate", "tenor", "calendar", "convention", "terminationDateConvention",
                     "rule", "endOfMonth", "firstDate", "nextToLastDate"},
                 what);
        s.effectiveDate = date(key(o, "effectiveDate", what));
        s.terminationDate = date(key(o, "terminationDate", what));
        const json::array& tenor = array(key(o, "tenor", what), "a tenor as [length, units]");
        if (tenor.size() != 2)
            throw InvalidParams("expected a tenor as [length, units]");
        s.tenorLength = integer(tenor[0]);
        s.tenorUnits = enumeration<TimeUnit>(tenor[1]);
        s.calendar = indexes::calendar(key(o, "calendar", what));
        s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
        s.terminationDateConvention = enumeration<BusinessDayConvention>(key(o, "terminationDateConvention", what));
        s.rule = enumeration<DateGeneration::Rule>(key(o, "rule", what));
        s.endOfMonth = boolean(key(o, "endOfMonth", what));
        s.firstDate = date(key(o, "firstDate", what));
        s.nextToLastDate = date(key(o, "nextToLastDate", what));
        return s;
    }

    Schedule makeSchedule(const ScheduleSpec& s) {
        if (s.fromDates) {
            std::vector<Date> dates;
            for (const DateSpec& d : s.dates)
                dates.push_back(makeDate(d));
            return Schedule(dates);
        }
        return {makeDate(s.effectiveDate),
                makeDate(s.terminationDate),
                Period(s.tenorLength, s.tenorUnits),
                indexes::makeCalendar(s.calendar),
                s.convention,
                s.terminationDateConvention,
                s.rule,
                s.endOfMonth,
                makeDate(s.firstDate),
                makeDate(s.nextToLastDate)};
    }

    SelectionSpec selection(const json::value& v) {
        SelectionSpec s;
        if (v.is_null())
            return s;
        const std::string what = "a stub selection";
        const json::object& o = object(v, what);
        onlyKeys(o, {"convention", "indices"}, what);
        s.empty = false;
        s.convention = enumeration<StubIndexSelection::Convention>(key(o, "convention", what));
        for (const json::value& i : array(key(o, "indices", what), "a list of indices"))
            s.indices.push_back(indexes::iborIndex(i));
        return s;
    }

    StubIndexSelection makeSelection(const SelectionSpec& s) {
        if (s.empty)
            return {};
        std::vector<ext::shared_ptr<IborIndex>> indices;
        for (const indexes::IndexSpec& i : s.indices)
            indices.push_back(indexes::makeIborIndex(i));
        return {s.convention, indices};
    }

    LegSpec leg(const json::value& v) {
        const std::string what = "an IborLeg";
        const json::object& o = object(v, what);
        onlyKeys(o, {"schedule", "index", "notionals", "paymentDayCounter", "paymentConvention", "fixingDays",
                     "gearings", "spreads", "caps", "floors", "isInArrears", "exCouponPeriod", "paymentCalendar",
                     "paymentLag", "withIndexedCoupons", "stubIndexSelection", "pricer", "zeroPayments",
                     "fixingConvention", "atParCoupons"},
                 what);
        LegSpec s;
        s.schedule = schedule(key(o, "schedule", what));
        s.index = indexes::iborIndex(key(o, "index", what));
        s.notionals = reals(key(o, "notionals", what));
        optionalDayCounter(key(o, "paymentDayCounter", what), s.emptyPaymentDayCounter, s.paymentDayCounter);
        s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", what));
        for (const json::value& n : array(key(o, "fixingDays", what), "a list of fixing days"))
            s.fixingDays.push_back(natural(n));
        s.gearings = reals(key(o, "gearings", what));
        s.spreads = reals(key(o, "spreads", what));
        s.caps = reals(key(o, "caps", what));
        s.floors = reals(key(o, "floors", what));
        s.isInArrears = boolean(key(o, "isInArrears", what));
        const json::value& ex = key(o, "exCouponPeriod", what);
        if (!ex.is_null()) {
            const json::object& e = object(ex, "an ex-coupon period");
            onlyKeys(e, {"period", "calendar", "convention", "endOfMonth"}, "an ex-coupon period");
            const json::array& p = array(key(e, "period", "an ex-coupon period"), "a period as [length, units]");
            if (p.size() != 2)
                throw InvalidParams("expected a period as [length, units]");
            s.hasExCoupon = true;
            s.exCouponLength = integer(p[0]);
            s.exCouponUnits = enumeration<TimeUnit>(p[1]);
            s.exCouponCalendar = indexes::calendar(key(e, "calendar", "an ex-coupon period"));
            s.exCouponConvention = enumeration<BusinessDayConvention>(key(e, "convention", "an ex-coupon period"));
            s.exCouponEndOfMonth = boolean(key(e, "endOfMonth", "an ex-coupon period"));
        }
        const json::value& cal = key(o, "paymentCalendar", what);
        if (!cal.is_null()) {
            s.hasPaymentCalendar = true;
            s.paymentCalendar = indexes::calendar(cal);
        }
        s.paymentLag = integer(key(o, "paymentLag", what));
        s.withIndexedCoupons = optionalBoolean(key(o, "withIndexedCoupons", what));
        s.stubIndexSelection = selection(key(o, "stubIndexSelection", what));
        if (const json::value* z = o.if_contains("zeroPayments"))
            s.zeroPayments = boolean(*z);
        if (const json::value* f = o.if_contains("fixingConvention"))
            s.fixingConvention = enumeration<BusinessDayConvention>(*f);
        if (const json::value* p = o.if_contains("atParCoupons"))
            s.atParCoupons = boolean(*p);
        s.pricer = pricer(key(o, "pricer", what));
        return s;
    }

    Leg makeLeg(const LegSpec& s) {
        IborLeg builder(makeSchedule(s.schedule), indexes::makeIborIndex(s.index));
        builder.withNotionals(s.notionals)
            .withPaymentAdjustment(s.paymentConvention)
            .withFixingDays(s.fixingDays)
            .withGearings(s.gearings)
            .withSpreads(s.spreads)
            .withCaps(s.caps)
            .withFloors(s.floors)
            .inArrears(s.isInArrears)
            .withPaymentLag(s.paymentLag)
            .withIndexedCoupons(s.withIndexedCoupons)
            .withStubIndexSelection(makeSelection(s.stubIndexSelection));
        if (!s.emptyPaymentDayCounter)
            builder.withPaymentDayCounter(makeDayCounter(s.paymentDayCounter));
        if (s.hasPaymentCalendar)
            builder.withPaymentCalendar(indexes::makeCalendar(s.paymentCalendar));
        if (s.hasExCoupon)
            builder.withExCouponPeriod(Period(s.exCouponLength, s.exCouponUnits),
                                       indexes::makeCalendar(s.exCouponCalendar), s.exCouponConvention,
                                       s.exCouponEndOfMonth);
        if (s.zeroPayments)
            builder.withZeroPayments(*s.zeroPayments);
        if (s.fixingConvention)
            builder.withFixingConvention(*s.fixingConvention);
        if (s.atParCoupons)
            builder.withAtParCoupons(*s.atParCoupons);
        Leg out = builder;
        const ext::shared_ptr<FloatingRateCouponPricer> p = makePricer(s.pricer);
        if (p)
            setCouponPricer(out, p);
        return out;
    }

    json::value inspectors(const FloatingRateCoupon& c) {
        return json::object{{"date", result(c.date())},
                            {"nominal", c.nominal()},
                            {"accrualStartDate", result(c.accrualStartDate())},
                            {"accrualEndDate", result(c.accrualEndDate())},
                            {"referencePeriodStart", result(c.referencePeriodStart())},
                            {"referencePeriodEnd", result(c.referencePeriodEnd())},
                            {"exCouponDate", result(c.exCouponDate())},
                            {"accrualPeriod", c.accrualPeriod()},
                            {"fixingDate", result(c.fixingDate())},
                            {"fixingDays", c.fixingDays()},
                            {"gearing", c.gearing()},
                            {"spread", c.spread()},
                            {"isInArrears", c.isInArrears()},
                            {"fixingConvention", static_cast<int>(c.fixingConvention())},
                            {"dayCounter", c.dayCounter().name()},
                            {"index", c.index()->name()},
                            {"hasPricer", c.pricer() != nullptr}};
    }

    json::value positions(const Leg& leg, const std::vector<ext::shared_ptr<FloatingRateCouponPricer>>& p) {
        json::array out;
        for (const auto& cf : leg) {
            auto c = ext::dynamic_pointer_cast<FloatingRateCoupon>(cf);
            json::value position = nullptr;
            if (c && c->pricer()) {
                for (std::size_t k = 0; k < p.size(); ++k) {
                    if (c->pricer() == p[k]) {
                        position = static_cast<std::int64_t>(k);
                        break;
                    }
                }
            }
            out.push_back(position);
        }
        return out;
    }

}
