﻿using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    class Person
    {
        static int maxAge = 42;
        int age = 42 /*init*/; // Noncompliant {{Remove the member initializer, all constructors set an initial value for the member.}}
//              ^^^^
        int Age { get; } = 42; // Noncompliant
//                       ^^^^
        int Age2 { get; } = 0; // we already report on this with S3052
        event EventHandler myEvent = (a, b) => { }; // Noncompliant
        public Person(int age)
        {
            var x = nameof(this.Age); // Nameof doesn't matter

            if (true)
            {
                maxAge = 43;
                this.age = age;
                this.Age = this.age;
                Age2 = 44;
                myEvent = (a, b) => { };
            }
            else
            {
                maxAge = 43;
                this.age = age;
                this.Age = this.age;
                Age2 = 44;
                myEvent = (a, b) => { };
            }

            Console.WriteLine(this.Age);
        }
    }

    class Person2
    {
        int age = 42;
        public Person2(int age)
        {
            this.age = age;
        }

        public Person2(int age, int other)
        {
        }
    }

    class Person2b
    {
        int age = 42; // Noncompliant
        public Person2b(int age)
        {
            this.age = age;
        }

        public Person2b(int age, int other)
        {
            this.age = age;
        }
    }

    class Person3
    {
        int age = 42; // Noncompliant
        public Person3(int age)
        {
            this.age = age;
        }

        public Person3(int age, int other)
            : this(age)
        {
        }
    }

    class Person4
    {
        int age = 42;
        public Person4()
        {
            Console.WriteLine(this?.age);
            this.age = 40;
        }
    }

    class Person4b
    {
        int age = 42;
        public Person4b()
        {
            Console.WriteLine(this.age);
            this.age = 40;
        }
    }

    class Person5
    {
        int age = 42;
        public Person5()
        {
            Delegate d = new Action(() => { this.age = 40; });
        }
    }

    class Person6
    {
        int age = 42, // Noncompliant
            year = 1980; // Noncompliant
        public Person6(bool c)
        {
            if (c)
            {
                this.age = 40;
            }
            else
            {
                this.age = 42;
            }
            this.year = 400;
            Console.WriteLine(this.year);
        }
    }

    class Person7
    {
        int age = 42,
            year = 1980; // Noncompliant
        public Person7(bool c)
        {
            if (c)
            {
                this.age = 40;
            }
            this.year = 400;
            Console.WriteLine(this.year);
        }
    }

    class Person8
    {
        int year = 1980; // Compliant, lambda uses it in contructor
        public Person8()
        {
            Action a = () => Console.WriteLine(this?.year);
            a();
            this.year = 1980;
        }
    }

    class Person9
    {
        int year = 1980; // Compliant, try statements are not handled
        public Person9()
        {
            try
            {
                year = 1980;
            }
            catch (Exception)
            {

                throw;
            }
        }
    }

    class Person10
    {
        int year = 1980; // Noncompliant
        public Person10()
        {
            M(out ((this.year)));
        }

        void M(out int x) { x = 42; }
    }
}
