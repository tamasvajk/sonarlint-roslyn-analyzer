﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Diagnostics
{
    public class Params
    {
        public void method(int i, int k = 5, params int[] rest)
        {
        }

        public void call()
        {
            int i = 0, j = 5, k = 6, l=7;
            method(i, j, k, l);
        }
        public void call2()
        {
            int i = 0, j = 5, rest = 6, l = 7;
            var k = new[] { i, l };
            method(i, k : rest, rest : k); //Noncompliant
//          ^^^^^^
        }
    }

    public static class Extensions
    {
        public static void Ex(this string self, string v1, string v2)
        {
        }
        public static void Ex(this string self, string v1, string v2, int x)
        {
            Ex(self, v1, v2);
            self.Ex(v1, v2);
            Extensions.Ex(self, v1, v2);
            Tests.Diagnostics.Extensions.Ex(self, v1, v2);
        }
    }

    public partial class ParametersCorrectOrder
    {
        partial void divide(int divisor, int someOther, int dividend, int p = 10, int some = 5, int other2 = 7);
    }

    public partial class ParametersCorrectOrder
    {
        partial void divide(int a, int b, int c, int p, int other, int other2)
        {
            var x = a / b;
        }

        public void m(int a, int b)
        {
        }

        public void doTheThing()
        {
            int divisor = 15;
            int dividend = 5;
            var something = 6;
            var someOther = 6;
            var other2 = 6;
            var some = 6;

            divide(dividend, 1 + 1, divisor, other2: 6);  // Noncompliant; operation succeeds, but result is unexpected

            divide(divisor, other2, dividend);
            divide(divisor, other2, dividend, other2: someOther); // Noncompliant {{Parameters to "divide" have the same names but not the same order as the method arguments.}}

            divide(divisor, someOther, dividend, other2: some, some: other2); // Noncompliant;

            divide(1, 1, 1, other2: some, some: other2); // Noncompliant;
            divide(1, 1, 1, other2: 1, some: other2);

            int a=5, b=6;

            m(1, a); // Compliant
            m(1, b);
            m(b, b);
            m(divisor, dividend);

            m(a, b);
            m(b, b); // Compliant
            m(b, a); // Noncompliant

            var v1 = "";
            var v2 = "";

            "aaaaa".Ex(v1, v2);
            "aaaaa".Ex(v2, v1); // Noncompliant
        }
    }
}
