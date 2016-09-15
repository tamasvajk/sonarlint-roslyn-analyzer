﻿using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class BooleanCheckInverted
    {
        public void Test()
        {
            var a = 2;
            if (a != 2) // Fixed
            {

            }
            bool b = a >= 10;  // Fixed
            b = a > 10;  // Fixed
            b = a <= 10;  // Fixed
            b = a < 10;  // Fixed
            b = a != 10;  // Fixed
            b = a == 10;  // Fixed


            if (a != 2)
            {
            }
            b = (a >= 10);

            var c = true && (new int[0].Length != 0); // Fixed

            int[] args = { };
            var a = !(args.Length != 0); // Fixed

            SomeFunc(a < 10); // Fixed
        }

        public static void SomeFunc(bool x) { }

        public static bool operator ==     (BooleanCheckInverted a, BooleanCheckInverted b)
        {
            return false;
        }

        public static bool operator !=(BooleanCheckInverted a, BooleanCheckInverted b)
        {
            return !(a==b); // Compliant
        }
    }
}
