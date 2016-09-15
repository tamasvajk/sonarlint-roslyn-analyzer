﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace Tests.Diagnostics
{
    public class StringOperationWithoutCulture
    {
        void Test()
        {
            var s = "";
            s = s.ToLower(); // Noncompliant
//                ^^^^^^^
            s = s.ToUpper(); // Noncompliant {{Define the locale to be used in this string operation.}}

            s = s.ToUpperInvariant();
            s = s.ToUpper(CultureInfo.InvariantCulture);
            var b = s.StartsWith("", StringComparison.CurrentCulture);
            b = s.StartsWith(""); // Compliant, although culture specific
            b = s.EndsWith(""); // Compliant, although culture specific
            b = s.StartsWith("", true, CultureInfo.InvariantCulture);
            b = s.Equals(""); // Compliant, ordinal compare
            b = s.Equals(new object());
            b = s.Equals("", StringComparison.CurrentCulture);
            var i = string.Compare("", "", true); // Noncompliant
            i = string.Compare("", 1, "", 2, 3, true); // Noncompliant
            i = string.Compare("", 1, "", 2, 3, true, CultureInfo.InstalledUICulture);
            i = string.Compare("", "", StringComparison.CurrentCulture);

            s = 1.8.ToString(); //Noncompliant
            s = 1.8m.ToString(); //Noncompliant
            s = 1.8f.ToString("d");
            s = new DateTime().ToString(); //Noncompliant
            s = 1.8.ToString(CultureInfo.InstalledUICulture);

            i = "".CompareTo(""); // Noncompliant {{Use "CompareOrdinal" or "Compare" with the locale specified instead of "CompareTo".}}
            object o = "";
            i = "".CompareTo(o); // Noncompliant

            i = "".IndexOf(""); // Noncompliant
            i = "".IndexOf('');
            i = "".LastIndexOf(""); // Noncompliant
            i = "".LastIndexOf("", StringComparison.CurrentCulture);
        }
    }
}
