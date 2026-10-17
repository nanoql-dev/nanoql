// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "math/optimization/specs.hpp"

#include <ql/errors.hpp>

#include <cmath>
#include <set>

using namespace QuantLib;

namespace harness::optimization {

    namespace {

        std::string shown(const json::value& v) { return json::serialize(v); }

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, const std::set<std::string>& keys, const std::string& what) {
            for (const auto& kv : o) {
                if (keys.count(std::string(kv.key())) == 0)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        // The harness's cost functions: values() only, so every other member is CostFunction's default.

        class Rosenbrock : public CostFunction {
          public:
            Array values(const Array& x) const override {
                Array v(2);
                v[0] = 10.0 * (x[1] - x[0] * x[0]);
                v[1] = 1.0 - x[0];
                return v;
            }
        };

        class PolynomialFit : public CostFunction {
          public:
            Array values(const Array& x) const override {
                static const Real t[] = {-1.0, 0.0, 1.0, 2.0};
                static const Real y[] = {2.5, 1.0, 1.5, 3.5};
                Array v(4);
                for (Size i = 0; i < 4; ++i)
                    v[i] = x[0] + x[1] * t[i] + x[2] * t[i] * t[i] - y[i];
                return v;
            }
        };

        class Beale : public CostFunction {
          public:
            Array values(const Array& x) const override {
                Array v(3);
                v[0] = 1.5 - x[0] * (1.0 - x[1]);
                v[1] = 2.25 - x[0] * (1.0 - x[1] * x[1]);
                v[2] = 2.625 - x[0] * (1.0 - x[1] * x[1] * x[1]);
                return v;
            }
        };

        class Linear : public CostFunction {
          public:
            Array values(const Array& x) const override {
                Real s = 0.0;
                for (Size i = 0; i < x.size(); ++i)
                    s += x[i] * (static_cast<Real>(i) + 1.0);
                return Array(1, s - 1.0);
            }
        };

        // Values that depend on x0 + x1 only, so the Jacobian's two columns are equal: rank 1 everywhere.
        class RankDeficient : public CostFunction {
          public:
            Array values(const Array& x) const override {
                const Real s = x[0] + x[1];
                Array v(2);
                v[0] = 10.0 * (s * s * s - 2.0);
                v[1] = s - 3.0;
                return v;
            }
        };

        // Scalar cost functions, as tools/vectors/gen/math/optimization/simplex.py's: value() only, and values() not
        // implemented, as QuantLib-SWIG's PyCostFunction's isn't.
        class Quadratic2 : public CostFunction {
          public:
            Real value(const Array& x) const override {
                return (x[0] - 1.0) * (x[0] - 1.0) + (x[1] + 0.5) * (x[1] + 0.5);
            }
            Array values(const Array&) const override { QL_FAIL("Not implemented"); }
        };

        class Booth : public CostFunction {
          public:
            Real value(const Array& x) const override {
                const Real a = x[0] + 2.0 * x[1] - 7.0;
                const Real b = 2.0 * x[0] + x[1] - 5.0;
                return a * a + b * b;
            }
            Array values(const Array&) const override { QL_FAIL("Not implemented"); }
        };

        // The named values functions of SimpleCostFunction.
        Array circle(const Array& x) {
            Array v(2);
            v[0] = x[0] * x[0] + x[1] * x[1] - 1.0;
            v[1] = x[0] - x[1];
            return v;
        }

        const std::set<std::string>& constraintTypes() {
            static const std::set<std::string> types = {"NoConstraint", "PositiveConstraint", "BoundaryConstraint",
                                                        "CompositeConstraint", "NonhomogeneousBoundaryConstraint"};
            return types;
        }

    }

    CostFunctionSpec costFunction(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a cost function spec, got " + shown(v));
        const json::object& o = v.get_object();
        const std::string type = text(key(o, "type", "a cost function spec"));
        if (type == "SimpleCostFunction") {
            onlyKeys(o, {"type", "values"}, type);
            const std::string values = text(key(o, "values", type));
            if (values != "circle")
                throw InvalidParams("unknown values function " + values);
            return {type, values};
        }
        if (type != "rosenbrock" && type != "polynomialFit" && type != "beale" && type != "linear" &&
            type != "rankDeficient" && type != "quadratic2" && type != "booth")
            throw InvalidParams("unknown cost function " + type);
        onlyKeys(o, {"type"}, type);
        return {type, ""};
    }

    std::optional<Size> variables(const CostFunctionSpec& s) {
        if (s.type == "rosenbrock" || s.type == "beale" || s.type == "rankDeficient" || s.type == "quadratic2" ||
            s.type == "booth" || s.type == "SimpleCostFunction")
            return 2;
        if (s.type == "polynomialFit")
            return 3;
        return std::nullopt;
    }

    void checkPoint(const CostFunctionSpec& s, const std::vector<Real>& x) {
        std::optional<Size> n = variables(s);
        if (n && x.size() != *n)
            throw InvalidParams(s.type + " takes " + std::to_string(*n) + " variables, got " +
                                std::to_string(x.size()));
    }

    std::unique_ptr<CostFunction> makeCostFunction(const CostFunctionSpec& s) {
        if (s.type == "rosenbrock")
            return std::make_unique<Rosenbrock>();
        if (s.type == "polynomialFit")
            return std::make_unique<PolynomialFit>();
        if (s.type == "beale")
            return std::make_unique<Beale>();
        if (s.type == "linear")
            return std::make_unique<Linear>();
        if (s.type == "rankDeficient")
            return std::make_unique<RankDeficient>();
        if (s.type == "quadratic2")
            return std::make_unique<Quadratic2>();
        if (s.type == "booth")
            return std::make_unique<Booth>();
        if (s.type == "SimpleCostFunction" && s.values == "circle")
            return std::make_unique<SimpleCostFunction<Array (*)(const Array&)>>(&circle);
        throw InvalidParams("the cost function " + s.type + " was not checked");
    }

    ConstraintSpec constraint(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a constraint spec, got " + shown(v));
        const json::object& o = v.get_object();
        ConstraintSpec s;
        s.type = text(key(o, "type", "a constraint spec"));
        if (constraintTypes().count(s.type) == 0)
            throw InvalidParams("unknown constraint " + s.type);
        if (s.type == "NoConstraint" || s.type == "PositiveConstraint") {
            onlyKeys(o, {"type"}, s.type);
        } else if (s.type == "BoundaryConstraint") {
            onlyKeys(o, {"type", "low", "high"}, s.type);
            s.low = real(key(o, "low", s.type));
            s.high = real(key(o, "high", s.type));
        } else if (s.type == "CompositeConstraint") {
            onlyKeys(o, {"type", "c1", "c2"}, s.type);
            s.c1 = std::make_shared<ConstraintSpec>(constraint(key(o, "c1", s.type)));
            s.c2 = std::make_shared<ConstraintSpec>(constraint(key(o, "c2", s.type)));
        } else {
            onlyKeys(o, {"type", "low", "high"}, s.type);
            s.lows = reals(key(o, "low", s.type));
            s.highs = reals(key(o, "high", s.type));
        }
        return s;
    }

    Constraint makeConstraint(const ConstraintSpec& s) {
        if (s.type == "NoConstraint")
            return NoConstraint();
        if (s.type == "PositiveConstraint")
            return PositiveConstraint();
        if (s.type == "BoundaryConstraint")
            return BoundaryConstraint(s.low, s.high);
        if (s.type == "CompositeConstraint")
            return CompositeConstraint(makeConstraint(*s.c1), makeConstraint(*s.c2));
        if (s.type == "NonhomogeneousBoundaryConstraint")
            return NonhomogeneousBoundaryConstraint(makeArray(s.lows), makeArray(s.highs));
        throw InvalidParams("the constraint " + s.type + " was not checked");
    }

    EndCriteriaSpec endCriteria(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected an end criteria spec, got " + shown(v));
        const json::object& o = v.get_object();
        const std::string what = "an end criteria spec";
        onlyKeys(o,
                 {"maxIterations", "maxStationaryStateIterations", "rootEpsilon", "functionEpsilon",
                  "gradientNormEpsilon"},
                 what);
        const json::value& stationary = key(o, "maxStationaryStateIterations", what);
        const json::value& root = key(o, "rootEpsilon", what);
        const json::value& function = key(o, "functionEpsilon", what);
        if (root.is_null() || function.is_null())
            throw InvalidParams("an end criteria spec's root and function epsilons are reals, not null");
        return {size(key(o, "maxIterations", what)), stationary.is_null() ? Null<Size>() : size(stationary),
                real(root), real(function), real(key(o, "gradientNormEpsilon", what))};
    }

    EndCriteria makeEndCriteria(const EndCriteriaSpec& s) {
        return EndCriteria(s.maxIterations, s.maxStationaryStateIterations, s.rootEpsilon, s.functionEpsilon,
                           s.gradientNormEpsilon);
    }

    Size size(const json::value& v) {
        if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
            throw InvalidParams("expected a non-negative integer, got " + shown(v));
        return static_cast<Size>(v.get_int64());
    }

    Array makeArray(const std::vector<Real>& x) { return Array(x.begin(), x.end()); }

    json::value arrayResult(const Array& a) {
        json::array out;
        for (Real x : a)
            out.emplace_back(x);
        return json::value(std::move(out));
    }

    json::value matrixResult(const Matrix& m) {
        json::array out;
        for (Size i = 0; i < m.rows(); ++i) {
            json::array row;
            for (Size j = 0; j < m.columns(); ++j)
                row.emplace_back(m[i][j]);
            out.emplace_back(std::move(row));
        }
        return json::value(std::move(out));
    }

    json::value realOrNull(Real x) {
        if (x == Null<Real>())
            return nullptr;
        return json::value(x);
    }

    json::value integerResult(Integer n) { return json::value(static_cast<std::int64_t>(n)); }

    json::object problemResult(const Problem& p) {
        return {{"currentValue", arrayResult(p.currentValue())},
                {"functionValue", realOrNull(p.functionValue())},
                {"gradientNormValue", realOrNull(p.gradientNormValue())},
                {"functionEvaluation", integerResult(p.functionEvaluation())},
                {"gradientEvaluation", integerResult(p.gradientEvaluation())}};
    }

    json::value minimizationResult(const Problem& p, EndCriteria::Type type) {
        json::object out = problemResult(p);
        out["endCriteriaType"] = static_cast<std::int64_t>(type);
        return json::value(std::move(out));
    }

}
