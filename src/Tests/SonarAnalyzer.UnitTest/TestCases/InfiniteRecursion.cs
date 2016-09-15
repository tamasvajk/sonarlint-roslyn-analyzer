﻿using System;

namespace Tests.Diagnostics
{
    class InfiniteRecursion
    {
        int Pow(int num, int exponent)   // Noncompliant; no condition under which pow isn't re-called
//          ^^^
        {
            num = num * Pow(num, exponent - 1);
            return num;  // this is never reached
        }

        void Test1() // Noncompliant {{Add a way to break out of this method's recursion.}}
        {
            var i = 10;
            if (i == 10)
            {
                Test1();
            }
            else
            {
                switch (i)
                {
                    case 1:
                        Test1();
                        break;
                    default:
                        Test1();
                        break;
                }
            }
        }

        void Test2()
        {
            var i = 10;
            switch (i)
            {
                case 1:
                    goto default;
                default:
                    goto case 1; // Noncompliant {{Add a way to break out of this method.}}
            }
        }

        void Test3()
        {
            var i = 10;
            switch (i)
            {
                case 1:
                    goto default;
                case 2:
                    break;
                default:
                    goto case 1; // Noncompliant
            }

            switch (i)
            {
                case 1:
                    goto default;
                case 2:
                    break;
                default:
                    goto case 1; // Noncompliant
            }
        }

        int Prop
        {
            get // Noncompliant {{Add a way to break out of this property accessor's recursion.}}
            {
                return Prop;
            }
        }

        string Prop0
        {
            get
            {
                return nameof(Prop0);
            }
        }

        object Prop1
        {
            get
            {
                return (new InfiniteRecursion())?.Prop1;
            }
        }

        int Prop2
        {
            get // Not recognized, but the accessors are cirularly infinitely recursive
            {
                (Prop2) = 10;
                return 10;
            }
            set
            {
                var x = Prop2;
            }
        }

        void InternalRecursion(int i)
        {
            start:
            goto end;
            end:
            goto start; // Noncompliant

            switch (i)
            {
                case 1:
                    goto default;
                case 2:
                    break;
                default:
                    goto case 1; // Compliant, already not reachable
            }
        }

        int Pow2(int num, int exponent)
        {
            if (exponent > 1)
            {
                num = num * Pow2(num, exponent - 1);
            }
            return num;
        }

        void Generic<T>() // Noncompliant
        {
            Generic<T>();
        }

        void Generic2<T>() // Compliant
        {
            Generic2<int>();
        }
    }
}