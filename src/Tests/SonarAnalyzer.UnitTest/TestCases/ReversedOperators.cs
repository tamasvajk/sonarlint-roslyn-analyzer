﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class ReversedOperators
    {
        public ReversedOperators()
        {
            int target = -5;
            int num = 3;

            target =- num;  // Noncompliant {{Was "-=" meant instead?}}
//                  ^
            target =+ num;  // Noncompliant; target = 3

            target = -num;  // Compliant; intent to assign inverse value of num is clear
            target += num;

            target += -num;
            target =
                +num;

            bool a, b;
            if (a =! b) ; // Noncompliant
            if (a != b) ;

            if (a = !b) ;
        }
    }
}
