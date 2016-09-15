﻿/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Helpers.FlowAnalysis.Common;

namespace SonarAnalyzer.UnitTest.Helpers
{
    [TestClass]
    public class ProgramPointTests
    {
        public class TestBlock : Block { }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ProgramPoint_Equivalence()
        {
            var block = new TestBlock();
            var pp1 = new ProgramPoint(block, 1);
            var pp2 = new ProgramPoint(block, 1);

            Assert.AreEqual(pp1, pp2);
            Assert.AreEqual(pp1.GetHashCode(), pp2.GetHashCode());
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ProgramPoint_Diff_Offset()
        {
            var block = new TestBlock();
            var pp1 = new ProgramPoint(block, 1);
            var pp2 = new ProgramPoint(block, 2);

            Assert.AreNotEqual(pp1, pp2);
            Assert.AreNotEqual(pp1.GetHashCode(), pp2.GetHashCode());
        }

        [TestMethod]
        [TestCategory("Symbolic execution")]
        public void ProgramPoint_Diff_Block()
        {
            var pp1 = new ProgramPoint(new TestBlock(), 1);
            var pp2 = new ProgramPoint(new TestBlock(), 1);

            Assert.AreNotEqual(pp1, pp2);
            Assert.AreNotEqual(pp1.GetHashCode(), pp2.GetHashCode());
        }
    }
}
