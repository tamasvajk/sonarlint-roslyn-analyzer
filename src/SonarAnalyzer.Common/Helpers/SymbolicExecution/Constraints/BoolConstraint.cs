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

namespace SonarAnalyzer.Helpers.FlowAnalysis.Common
{
    internal sealed class BoolConstraint : SymbolicValueConstraint
    {
        public static readonly BoolConstraint True = new BoolConstraint();
        public static readonly BoolConstraint False = new BoolConstraint();

        internal override bool Implies(SymbolicValueConstraint constraint)
        {
            return base.Implies(constraint) ||
                constraint == ObjectConstraint.NotNull;
        }

        public override SymbolicValueConstraint OppositeForLogicalNot =>
            this == True ? False : True;

        public override string ToString()
        {
            return this == True
                ? "True"
                : "False";
        }
    }
}
