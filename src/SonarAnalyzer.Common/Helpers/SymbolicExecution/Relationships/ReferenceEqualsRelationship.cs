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
using System.Collections.Immutable;
using System.Linq;

namespace SonarAnalyzer.Helpers.FlowAnalysis.Common
{
    public sealed class ReferenceEqualsRelationship : EqualsRelationship
    {
        public ReferenceEqualsRelationship(SymbolicValue leftOperand, SymbolicValue rightOperand)
            : base(leftOperand, rightOperand)
        {
        }

        internal override bool IsContradicting(IEnumerable<BinaryRelationship> relationships)
        {
            return relationships
                .OfType<NotEqualsRelationship>()
                .Any(rel => AreOperandsMatching(rel));
        }

        public override BinaryRelationship Negate()
        {
            return new ReferenceNotEqualsRelationship(LeftOperand, RightOperand);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return Equals(obj as ReferenceEqualsRelationship);
        }

        public override string ToString()
        {
            return $"RefEq({LeftOperand}, {RightOperand})";
        }

        internal override IEnumerable<BinaryRelationship> GetTransitiveRelationships(ImmutableHashSet<BinaryRelationship> relationships)
        {
            foreach (var other in relationships)
            {
                var transitive = GetTransitiveRelationship(other, other);
                if (transitive != null)
                {
                    yield return transitive;
                }
            }
        }

        internal override BinaryRelationship CreateNewWithOperands(SymbolicValue leftOperand, SymbolicValue rightOperand)
        {
            return new ReferenceEqualsRelationship(leftOperand, rightOperand);
        }
    }
}
