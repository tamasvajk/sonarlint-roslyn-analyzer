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

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Rules.Common;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.MemoryEfficiency)]
    [Tags(Tag.Performance)]
    public class StringConcatenationInLoop
        : StringConcatenationInLoopBase<SyntaxKind, AssignmentStatementSyntax, BinaryExpressionSyntax>
    {
        protected override bool IsExpressionConcatenation(BinaryExpressionSyntax addExpression)
        {
            return addExpression.IsKind(SyntaxKind.AddExpression) ||
                addExpression.IsKind(SyntaxKind.ConcatenateExpression);
        }

        protected override SyntaxNode GetLeft(AssignmentStatementSyntax assignment) => assignment.Left;

        protected override SyntaxNode GetRight(AssignmentStatementSyntax assignment) => assignment.Right;

        protected override SyntaxNode GetLeft(BinaryExpressionSyntax binary) => binary.Left;

        protected override bool IsInLoop(SyntaxNode node) => LoopKinds.Any(loopKind => node.IsKind(loopKind));

        protected override bool AreEquivalent(SyntaxNode node1, SyntaxNode node2) =>
            SyntaxFactory.AreEquivalent(node1, node2);

        private static readonly SyntaxKind[] LoopKinds =
        {
            SyntaxKind.WhileBlock,
            SyntaxKind.SimpleDoLoopBlock,
            SyntaxKind.ForBlock,
            SyntaxKind.ForEachBlock,
            SyntaxKind.DoUntilLoopBlock,
            SyntaxKind.DoWhileLoopBlock,
            SyntaxKind.DoLoopUntilBlock,
            SyntaxKind.DoLoopWhileBlock
        };

        private static readonly ImmutableArray<SyntaxKind> simpleAssignmentKinds =
            ImmutableArray.Create(SyntaxKind.SimpleAssignmentStatement);

        private static readonly ImmutableArray<SyntaxKind> compoundAssignmentKinds =
            ImmutableArray.Create(SyntaxKind.AddAssignmentStatement, SyntaxKind.ConcatenateAssignmentStatement);

        protected override ImmutableArray<SyntaxKind> SimpleAssignmentKinds => simpleAssignmentKinds;

        protected override ImmutableArray<SyntaxKind> CompoundAssignmentKinds => compoundAssignmentKinds;

        protected sealed override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.VisualBasic.GeneratedCodeRecognizer.Instance;
    }
}