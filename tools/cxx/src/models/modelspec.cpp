// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "models/modelspec.hpp"

#include <ql/handle.hpp>
#include <ql/models/shortrate/onefactormodels/hullwhite.hpp>
#include <ql/termstructures/yield/discountcurve.hpp>
#include <ql/termstructures/yield/flatforward.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness::models {

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

        std::optional<std::vector<Real>> optionalReals(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            return reals(v);
        }

    }

    CurveSpec curve(const json::value& v) {
        const json::object& o = object(v, "a curve");
        CurveSpec s;
        s.type = text(key(o, "type", "a curve"));
        if (s.type == "FlatForward") {
            onlyKeys(o, {"type", "referenceDate", "forward", "dayCounter", "compounding", "frequency"}, s.type);
            s.referenceDate = date(key(o, "referenceDate", s.type));
            s.forward = real(key(o, "forward", s.type));
            s.dayCounter = dayCounter(key(o, "dayCounter", s.type));
            s.compounding = enumeration<Compounding>(key(o, "compounding", s.type));
            s.frequency = enumeration<Frequency>(key(o, "frequency", s.type));
        } else if (s.type == "DiscountCurve") {
            onlyKeys(o, {"type", "dates", "discounts", "dayCounter"}, s.type);
            const json::value& dates = key(o, "dates", s.type);
            if (!dates.is_array())
                throw InvalidParams("expected a list of dates, got " + json::serialize(dates));
            for (const json::value& d : dates.get_array())
                s.dates.push_back(date(d));
            s.discounts = reals(key(o, "discounts", s.type));
            s.dayCounter = dayCounter(key(o, "dayCounter", s.type));
        } else {
            throw InvalidParams("unknown curve " + s.type);
        }
        return s;
    }

    ext::shared_ptr<YieldTermStructure> makeCurve(const CurveSpec& s) {
        if (s.type == "FlatForward")
            return ext::make_shared<FlatForward>(makeDate(s.referenceDate), s.forward, makeDayCounter(s.dayCounter),
                                                 s.compounding, s.frequency);
        std::vector<Date> dates;
        for (const DateSpec& d : s.dates)
            dates.push_back(makeDate(d));
        return ext::make_shared<DiscountCurve>(dates, s.discounts, makeDayCounter(s.dayCounter));
    }

    ModelSpec model(const json::value& v) {
        const json::object& o = object(v, "a model");
        ModelSpec s;
        s.type = text(key(o, "type", "a model"));
        if (s.type == "Vasicek") {
            onlyKeys(o, {"type", "r0", "a", "b", "sigma", "lambda", "params"}, s.type);
            s.r0 = real(key(o, "r0", s.type));
            s.b = real(key(o, "b", s.type));
            s.lambda = real(key(o, "lambda", s.type));
        } else if (s.type == "HullWhite") {
            onlyKeys(o, {"type", "curve", "a", "sigma", "params"}, s.type);
            s.curve = curve(key(o, "curve", s.type));
        } else {
            throw InvalidParams("unknown model " + s.type);
        }
        s.a = real(key(o, "a", s.type));
        s.sigma = real(key(o, "sigma", s.type));
        s.params = optionalReals(key(o, "params", s.type));
        return s;
    }

    ext::shared_ptr<Vasicek> makeModel(const ModelSpec& s) {
        ext::shared_ptr<Vasicek> m;
        if (s.type == "Vasicek")
            m = ext::make_shared<Vasicek>(s.r0, s.a, s.b, s.sigma, s.lambda);
        else
            m = ext::make_shared<HullWhite>(Handle<YieldTermStructure>(makeCurve(s.curve)), s.a, s.sigma);
        if (s.params)
            m->setParams(makeArray(*s.params));
        return m;
    }

    LatticeSpec lattice(const json::value& v) {
        const json::object& o = object(v, "a lattice");
        onlyKeys(o, {"model", "timeGrid", "spread"}, "a lattice");
        LatticeSpec s;
        s.model = model(key(o, "model", "a lattice"));
        s.grid = timeGrid(key(o, "timeGrid", "a lattice"));
        const json::value& spread = key(o, "spread", "a lattice");
        if (!spread.is_null())
            s.spread = real(spread);
        return s;
    }

    ext::shared_ptr<OneFactorModel::ShortRateTree> makeLattice(const LatticeSpec& s) {
        ext::shared_ptr<Lattice> lattice = makeModel(s.model)->tree(makeTimeGrid(s.grid));
        auto tree = ext::dynamic_pointer_cast<OneFactorModel::ShortRateTree>(lattice);
        if (!tree)
            throw InvalidParams("the model's tree is no OneFactorModel::ShortRateTree");
        if (s.spread)
            tree->setSpread(*s.spread);
        return tree;
    }

    Size index(const json::value& v) {
        if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
            throw InvalidParams("expected an index, got " + json::serialize(v));
        return static_cast<Size>(v.get_int64());
    }

    Array makeArray(const std::vector<Real>& x) { return Array(x.begin(), x.end()); }

    json::value arrayResult(const Array& a) {
        json::array out;
        for (Real x : a)
            out.emplace_back(x);
        return json::value(std::move(out));
    }

}
