// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/array.hpp: the members and functions of Array the wheel doesn't expose. An Array argument is a list of
// reals, from which the call builds the Array through its iterator-range constructor; an Array result is the list of
// its elements.

#include "convert.hpp"

#include <ql/math/array.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        Array makeArray(const std::vector<Real>& x) { return Array(x.begin(), x.end()); }

        json::value arrayResult(const Array& a) {
            json::array out;
            for (Real x : a)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        Size size(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected a size, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        // A function of one Array.
        Call unary(const Args& a, json::value (*f)(const Array&)) {
            arity(a, 1);
            std::vector<Real> x = reals(a[0]);
            return [=] { return f(makeArray(x)); };
        }

        // A function of two Arrays, the first changed in place and returned.
        Call inPlace(const Args& a, void (*f)(Array&, const Array&)) {
            arity(a, 2);
            std::vector<Real> x = reals(a[0]), y = reals(a[1]);
            return [=] {
                Array v = makeArray(x);
                f(v, makeArray(y));
                return arrayResult(v);
            };
        }

        // A function of an Array and a Real, the Array changed in place and returned.
        Call inPlaceScalar(const Args& a, void (*f)(Array&, Real)) {
            arity(a, 2);
            std::vector<Real> x = reals(a[0]);
            Real s = real(a[1]);
            return [=] {
                Array v = makeArray(x);
                f(v, s);
                return arrayResult(v);
            };
        }

        // A function of a Real and an Array.
        Call scalarFirst(const Args& a, Array (*f)(Real, const Array&)) {
            arity(a, 2);
            Real s = real(a[0]);
            std::vector<Real> x = reals(a[1]);
            return [=] { return arrayResult(f(s, makeArray(x))); };
        }

        const Register unit({
            {"Array(Size)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Size n = size(a[0]);
                 // The elements are left uninitialized, so only the size is read.
                 return [=] { return json::value(static_cast<std::int64_t>(Array(n).size())); };
             }},
            {"Array(Size,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Size n = size(a[0]);
                 Real value = real(a[1]), increment = real(a[2]);
                 return [=] { return arrayResult(Array(n, value, increment)); };
             }},
            {"Norm2",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 std::vector<Real> x = reals(a[0]);
                 return [=] { return json::value(Norm2(makeArray(x))); };
             }},
            {"Abs", [](const Args& a) { return unary(a, [](const Array& v) { return arrayResult(Abs(v)); }); }},
            {"Sqrt", [](const Args& a) { return unary(a, [](const Array& v) { return arrayResult(Sqrt(v)); }); }},
            {"Log", [](const Args& a) { return unary(a, [](const Array& v) { return arrayResult(Log(v)); }); }},
            {"Exp", [](const Args& a) { return unary(a, [](const Array& v) { return arrayResult(Exp(v)); }); }},
            {"Pow",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> x = reals(a[0]);
                 Real alpha = real(a[1]);
                 return [=] { return arrayResult(Pow(makeArray(x), alpha)); };
             }},
            {"Array::resize",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> x = reals(a[0]);
                 Size n = size(a[1]);
                 return [=] {
                     Array v = makeArray(x);
                     v.resize(n);
                     // A grown array's new elements are uninitialized: only those kept are read.
                     json::array kept;
                     for (Size i = 0; i < std::min(n, static_cast<Size>(x.size())); ++i)
                         kept.emplace_back(v[i]);
                     return json::value(json::object{{"size", static_cast<std::int64_t>(v.size())},
                                                     {"kept", std::move(kept)}});
                 };
             }},
            {"Array::front",
             [](const Args& a) {
                 return unary(a, [](const Array& v) { return json::value(v.front()); });
             }},
            {"Array::back",
             [](const Args& a) {
                 return unary(a, [](const Array& v) { return json::value(v.back()); });
             }},
            {"Array::empty",
             [](const Args& a) {
                 return unary(a, [](const Array& v) { return json::value(v.empty()); });
             }},
            {"Array::at",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> x = reals(a[0]);
                 Size i = size(a[1]);
                 return [=] { return json::value(makeArray(x).at(i)); };
             }},
            {"Array::operator+=(const Array&)",
             [](const Args& a) { return inPlace(a, [](Array& v, const Array& w) { v += w; }); }},
            {"Array::operator+=(Real)",
             [](const Args& a) { return inPlaceScalar(a, [](Array& v, Real x) { v += x; }); }},
            {"Array::operator-=(const Array&)",
             [](const Args& a) { return inPlace(a, [](Array& v, const Array& w) { v -= w; }); }},
            {"Array::operator-=(Real)",
             [](const Args& a) { return inPlaceScalar(a, [](Array& v, Real x) { v -= x; }); }},
            {"Array::operator*=(const Array&)",
             [](const Args& a) { return inPlace(a, [](Array& v, const Array& w) { v *= w; }); }},
            {"Array::operator*=(Real)",
             [](const Args& a) { return inPlaceScalar(a, [](Array& v, Real x) { v *= x; }); }},
            {"Array::operator/=(const Array&)",
             [](const Args& a) { return inPlace(a, [](Array& v, const Array& w) { v /= w; }); }},
            {"Array::operator/=(Real)",
             [](const Args& a) { return inPlaceScalar(a, [](Array& v, Real x) { v /= x; }); }},
            {"operator+(Real,const Array&)",
             [](const Args& a) { return scalarFirst(a, [](Real s, const Array& v) { return s + v; }); }},
            {"operator-(Real,const Array&)",
             [](const Args& a) { return scalarFirst(a, [](Real s, const Array& v) { return s - v; }); }},
            {"operator*(Real,const Array&)",
             [](const Args& a) { return scalarFirst(a, [](Real s, const Array& v) { return s * v; }); }},
            {"operator/(Real,const Array&)",
             [](const Args& a) { return scalarFirst(a, [](Real s, const Array& v) { return s / v; }); }},
            {"operator+(const Array&)",
             [](const Args& a) { return unary(a, [](const Array& v) { return arrayResult(+v); }); }},
            {"Array::swap",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]);
                 return [=] {
                     Array v = makeArray(x), w = makeArray(y);
                     v.swap(w);
                     return json::value(json::array{arrayResult(v), arrayResult(w)});
                 };
             }},
        });

    }

}
