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

using System.Collections.Generic;
using System.Linq;

namespace SonarAnalyzer.Helpers.FlowAnalysis.Common
{
    public sealed class ValueEqualsRelationship : EqualsRelationship
    {
        public ValueEqualsRelationship(SymbolicValue leftOperand, SymbolicValue rightOperand)
            : base(leftOperand, rightOperand)
        {
        }

        internal override bool IsContradicting(IEnumerable<BinaryRelationship> relationships)
        {
            var isNotEqContradicting = relationships
                .OfType<ValueNotEqualsRelationship>()
                .Any(rel => AreOperandsMatching(rel));

            if (isNotEqContradicting)
            {
                return true;
            }

            var isComparisonContradicting = relationships
                .OfType<ComparisonRelationship>()
                .Where(c => c.ComparisonKind == ComparisonKind.Less)
                .Any(c => AreOperandsMatching(c));

            return isComparisonContradicting;
        }

        public override BinaryRelationship Negate()
        {
            return new ValueNotEqualsRelationship(LeftOperand, RightOperand);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return Equals(obj as ValueEqualsRelationship);
        }

        public override string ToString()
        {
            return $"Eq({LeftOperand}, {RightOperand})";
        }
    }
}
