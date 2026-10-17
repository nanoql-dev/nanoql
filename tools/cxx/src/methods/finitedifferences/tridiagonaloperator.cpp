// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/tridiagonaloperator.hpp: the members of TridiagonalOperator the wheel doesn't expose.
// An operator argument is the spec {"low", "mid", "high"}, its three diagonals, from which the call builds it with the
// three-diagonal constructor, and optionally "timeSetter": "scaleByTime", for which it builds the harness's
// time-dependent operator below; or {"size": 0}, the operator TridiagonalOperator(0) builds, which the three-diagonal
// constructor can't (another size would leave the diagonals uninitialized). An operator result is {"low", "mid",
// "high", "timeDependent"}; an Array argument or result is a list of reals.
//
// QuantLib defines no TimeSetter, so the harness defines one, which tests/NanoQL.Tests defines too: ScaleByTime
// multiplies every element of the three diagonals by the time, through the accessors and the row setters, so each
// setTime(t) scales the operator's current coefficients by t.

#include "convert.hpp"

#include <ql/methods/finitedifferences/tridiagonaloperator.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        class ScaleByTime : public TridiagonalOperator::TimeSetter {
          public:
            void setTime(Time t, TridiagonalOperator& L) const override {
                const Array low = L.lowerDiagonal(), mid = L.diagonal(), high = L.upperDiagonal();
                const Size n = L.size();
                L.setFirstRow(mid[0] * t, high[0] * t);
                for (Size i = 1; i <= n - 2; ++i)
                    L.setMidRow(i, low[i - 1] * t, mid[i] * t, high[i] * t);
                L.setLastRow(low[n - 2] * t, mid[n - 1] * t);
            }
        };

        // An operator whose time setter is ScaleByTime: a derived class sets the protected timeSetter_.
        class TimeDependentOperator : public TridiagonalOperator {
          public:
            TimeDependentOperator(const Array& low, const Array& mid, const Array& high)
            : TridiagonalOperator(low, mid, high) {
                timeSetter_ = ext::make_shared<ScaleByTime>();
            }
        };

        struct OperatorSpec {
            std::vector<Real> low, mid, high;
            bool timeDependent;
            bool sizeZero;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a TridiagonalOperator spec needs ") + name);
            return *v;
        }

        OperatorSpec tridiagonal(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a TridiagonalOperator spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            if (const json::value* n = o.if_contains("size")) {
                if (o.size() != 1 || !n->is_int64() || n->get_int64() != 0)
                    throw InvalidParams("a TridiagonalOperator spec by size is {\"size\": 0}, got " + json::serialize(v));
                return {{}, {}, {}, false, true};
            }
            for (const auto& kv : o) {
                if (kv.key() != "low" && kv.key() != "mid" && kv.key() != "high" && kv.key() != "timeSetter")
                    throw InvalidParams("TridiagonalOperator takes no key " + std::string(kv.key()));
            }
            bool timeDependent = false;
            if (const json::value* setter = o.if_contains("timeSetter")) {
                if (text(*setter) != "scaleByTime")
                    throw InvalidParams("unknown time setter " + json::serialize(*setter));
                timeDependent = true;
            }
            return {reals(key(o, "low")), reals(key(o, "mid")), reals(key(o, "high")), timeDependent, false};
        }

        Array makeArray(const std::vector<Real>& x) { return Array(x.begin(), x.end()); }

        TridiagonalOperator make(const OperatorSpec& s) {
            if (s.sizeZero)
                return TridiagonalOperator(0);
            if (s.timeDependent)
                return TimeDependentOperator(makeArray(s.low), makeArray(s.mid), makeArray(s.high));
            return TridiagonalOperator(makeArray(s.low), makeArray(s.mid), makeArray(s.high));
        }

        json::value arrayResult(const Array& a) {
            json::array out;
            for (Real x : a)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        json::value operatorResult(const TridiagonalOperator& L) {
            return json::object{{"low", arrayResult(L.lowerDiagonal())},
                                {"mid", arrayResult(L.diagonal())},
                                {"high", arrayResult(L.upperDiagonal())},
                                {"timeDependent", L.isTimeDependent()}};
        }

        Size size(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected a size, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        // A function of one operator.
        Call unary(const Args& a, json::value (*f)(const TridiagonalOperator&)) {
            arity(a, 1);
            OperatorSpec s = tridiagonal(a[0]);
            return [=] { return f(make(s)); };
        }

        // A function of one operator and a Real.
        Call withScalar(const Args& a, json::value (*f)(const TridiagonalOperator&, Real), bool scalarFirst) {
            arity(a, 2);
            OperatorSpec s = tridiagonal(a[scalarFirst ? 1 : 0]);
            Real x = real(a[scalarFirst ? 0 : 1]);
            return [=] { return f(make(s), x); };
        }

        const Register unit({
            {"TridiagonalOperator(Size)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Size n = size(a[0]);
                 // The diagonals' elements are left uninitialized, so only the sizes are read.
                 return [=] {
                     TridiagonalOperator L(n);
                     return json::value(json::array{
                         static_cast<std::int64_t>(L.size()), static_cast<std::int64_t>(L.lowerDiagonal().size()),
                         static_cast<std::int64_t>(L.diagonal().size()),
                         static_cast<std::int64_t>(L.upperDiagonal().size())});
                 };
             }},
            {"TridiagonalOperator::identity",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Size n = size(a[0]);
                 return [=] { return operatorResult(TridiagonalOperator::identity(n)); };
             }},
            {"TridiagonalOperator::isTimeDependent",
             [](const Args& a) {
                 return unary(a, [](const TridiagonalOperator& L) { return json::value(L.isTimeDependent()); });
             }},
            {"TridiagonalOperator::lowerDiagonal",
             [](const Args& a) {
                 return unary(a, [](const TridiagonalOperator& L) { return arrayResult(L.lowerDiagonal()); });
             }},
            {"TridiagonalOperator::diagonal",
             [](const Args& a) {
                 return unary(a, [](const TridiagonalOperator& L) { return arrayResult(L.diagonal()); });
             }},
            {"TridiagonalOperator::upperDiagonal",
             [](const Args& a) {
                 return unary(a, [](const TridiagonalOperator& L) { return arrayResult(L.upperDiagonal()); });
             }},
            {"TridiagonalOperator::solveFor(const Array&)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 OperatorSpec s = tridiagonal(a[0]);
                 std::vector<Real> rhs = reals(a[1]);
                 return [=] { return arrayResult(make(s).solveFor(makeArray(rhs))); };
             }},
            {"TridiagonalOperator::solveFor(const Array&,Array&)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 OperatorSpec s = tridiagonal(a[0]);
                 std::vector<Real> rhs = reals(a[1]);
                 bool same = boolean(a[2]);
                 return [=] {
                     const TridiagonalOperator L = make(s);
                     Array x = makeArray(rhs);
                     if (same) {
                         // rhs and result the same Array, which the solution overwrites
                         L.solveFor(x, x);
                         return arrayResult(x);
                     }
                     Array result(x.size(), 0.0);
                     L.solveFor(x, result);
                     return arrayResult(result);
                 };
             }},
            {"TridiagonalOperator::SOR",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 OperatorSpec s = tridiagonal(a[0]);
                 std::vector<Real> rhs = reals(a[1]);
                 Real tol = real(a[2]);
                 return [=] { return arrayResult(make(s).SOR(makeArray(rhs), tol)); };
             }},
            {"TridiagonalOperator::setTime",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 OperatorSpec s = tridiagonal(a[0]);
                 std::vector<Real> times = reals(a[1]);
                 return [=] {
                     TridiagonalOperator L = make(s);
                     json::array out;
                     for (Real t : times) {
                         L.setTime(t);
                         out.emplace_back(operatorResult(L));
                     }
                     return json::value(std::move(out));
                 };
             }},
            {"TridiagonalOperator::swap",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 OperatorSpec s1 = tridiagonal(a[0]), s2 = tridiagonal(a[1]);
                 return [=] {
                     TridiagonalOperator L1 = make(s1), L2 = make(s2);
                     L1.swap(L2);
                     return json::value(json::array{
                         json::object{{"size", static_cast<std::int64_t>(L1.size())}, {"operator", operatorResult(L1)}},
                         json::object{{"size", static_cast<std::int64_t>(L2.size())}, {"operator", operatorResult(L2)}}});
                 };
             }},
            {"operator+(const TridiagonalOperator&)",
             [](const Args& a) { return unary(a, [](const TridiagonalOperator& L) { return operatorResult(+L); }); }},
            {"operator-(const TridiagonalOperator&)",
             [](const Args& a) { return unary(a, [](const TridiagonalOperator& L) { return operatorResult(-L); }); }},
            {"operator*(Real,const TridiagonalOperator&)",
             [](const Args& a) {
                 return withScalar(
                     a, [](const TridiagonalOperator& L, Real x) { return operatorResult(x * L); }, true);
             }},
            {"operator/(const TridiagonalOperator&,Real)",
             [](const Args& a) {
                 return withScalar(
                     a, [](const TridiagonalOperator& L, Real x) { return operatorResult(L / x); }, false);
             }},
        });

    }

}
