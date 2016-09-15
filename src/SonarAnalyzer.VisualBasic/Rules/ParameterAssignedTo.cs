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
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Misra, Tag.Pitfall)]
    public class ParameterAssignedTo : ParameterAssignedToBase<SyntaxKind, AssignmentStatementSyntax>
    {
        private static readonly ImmutableArray<SyntaxKind> kindsOfInterest = ImmutableArray.Create(
            SyntaxKind.AddAssignmentStatement,
            SyntaxKind.SimpleAssignmentStatement,
            SyntaxKind.SubtractAssignmentStatement,
            SyntaxKind.MultiplyAssignmentStatement,
            SyntaxKind.DivideAssignmentStatement,
            SyntaxKind.MidAssignmentStatement,
            SyntaxKind.ConcatenateAssignmentStatement,
            SyntaxKind.ExponentiateAssignmentStatement,
            SyntaxKind.IntegerDivideAssignmentStatement,
            SyntaxKind.LeftShiftAssignmentStatement,
            SyntaxKind.RightShiftAssignmentStatement
            );
        public override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => kindsOfInterest;

        protected override bool IsAssignmentToCatchVariable(ISymbol symbol, SyntaxNode node)
        {
            var localSymbol = symbol as ILocalSymbol;

            // this could mimic the C# variant too, but that doesn't work:
            // https://github.com/dotnet/roslyn/issues/6209
            // so:
            var location = localSymbol?.Locations.FirstOrDefault();
            if (location == null)
            {
                return false;
            }

            var declarationName = node.SyntaxTree.GetRoot().FindNode(location.SourceSpan, getInnermostNodeForTie: true) as IdentifierNameSyntax;
            if (declarationName == null)
            {
                return false;
            }

            var catchStatement = declarationName.Parent as CatchStatementSyntax;
            return catchStatement != null && catchStatement.IdentifierName == declarationName;
        }

        protected override bool IsAssignmentToParameter(ISymbol symbol)
        {
            var parameterSymbol = symbol as IParameterSymbol;

            return parameterSymbol?.RefKind == RefKind.None;
        }

        protected override SyntaxNode GetAssignedNode(AssignmentStatementSyntax assignment) => assignment.Left;

        protected sealed override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.VisualBasic.GeneratedCodeRecognizer.Instance;
    }
}