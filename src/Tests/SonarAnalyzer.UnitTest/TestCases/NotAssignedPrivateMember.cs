﻿using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    struct ValueType1
    {
        public int field;
    }

    class RefType
    {
        public int field2;
    }

    struct ValueType2
    {
        public RefType field1;
    }

    class MyClass
    {
        class Nested
        {
            private int field; // Noncompliant, shouldn't it be initialized? This way the value is always default(int), 0.
//                      ^^^^^
            private int field2;
            private static int field3; // Noncompliant {{Remove unassigned field "field3", or set its value.}}
            private static int field4;

            private static int field5; //reported by unused member rule

            private static int field6 = 42;
            private readonly int field7; // Noncompliant
            private int Property { get; }  // Noncompliant {{Remove unassigned auto-property "Property", or set its value.}}
            private int Property2 { get; }
            private int Property3 { get; } = 42; // Unused, S1144 reports on it

            private int Property4 { get; set; }  // Noncompliant
            private int Property5 { get; set; } = 42;
            private int Property6 { get { return 42; } set { } }

            private ValueType1 v1; // Compliant, a member is assigned
            private ValueType1 v2; // Compliant, a member is assigned
            private ValueType1 v3; // Noncompliant

            private ValueType2 v4; // Compliant, a member is assigned
            private ValueType2 v5; // Compliant, a member is assigned
            private ValueType2 v6; // Noncompliant

            public Nested()
            {
                Property2 = 42;
                v1.field++;
                ((((v2).field))) = 42;
                Console.WriteLine(v3.field);

                this.v4.field1.field2++;
                ((((this.v5).field1)).field2) = 42;
                Console.WriteLine(v6.field1?.field2);
            }

            public void Print()
            {
                Console.WriteLine((field)); //Will always print 0
                Console.WriteLine((field6));
                Console.WriteLine((field7));
                Console.WriteLine((Property));
                Console.WriteLine((Property4));
                Console.WriteLine((Property6));

                Console.WriteLine(this.field); //Will always print 0
                Console.WriteLine(MyClass.Nested.field3); //Will always print 0
                new MyClass().M(ref MyClass.Nested.field4);

                this.field2 = 10;
                field2 = 10;
            }
        }

        public void M(ref int f) { }
    }

    unsafe struct FixedArray
    {
        private fixed int a[42]; // Compliant, because of the fixed modifier

        private int[] b; // Noncompliant

        void M()
        {
            a[0] = 42;
            b[0] = 42;
        }
    }
}
