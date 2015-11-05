﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;
using System.Linq;

namespace SonarLint.Rules.Common
{
    public abstract class StringConcatenationInLoopBase : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1643";
        internal const string Title = "Strings should not be concatenated using \"+\" in a loop";
        internal const string Description =
            "\"StringBuilder\" is more efficient than string concatenation, especially when the operator is repeated over and over as in loops.";
        internal const string MessageFormat = "Use a StringBuilder instead.";
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }
    }

    public abstract class StringConcatenationInLoopBase<TLanguageKindEnum, TAssignmentExpression, TBinaryExpression>
            : StringConcatenationInLoopBase
        where TLanguageKindEnum : struct
        where TAssignmentExpression : SyntaxNode
        where TBinaryExpression : SyntaxNode
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var addAssignment = (TAssignmentExpression)c.Node;
                    if (!IsString(GetLeft(addAssignment), c.SemanticModel))
                    {
                        return;
                    }

                    SyntaxNode nearestLoop;
                    if (!TryGetNearestLoop(addAssignment, out nearestLoop))
                    {
                        return;
                    }

                    var symbol = c.SemanticModel.GetSymbolInfo(GetLeft(addAssignment)).Symbol as ILocalSymbol;
                    if (symbol != null &&
                        IsDefinedInLoop(GetLeft(addAssignment), nearestLoop, c.SemanticModel))
                    {
                        return;
                    }

                    c.ReportDiagnostic(Diagnostic.Create(Rule, addAssignment.GetLocation()));
                },
                CompoundAssignmentKinds.ToArray());

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var assignment = (TAssignmentExpression)c.Node;
                    if (!IsString(GetLeft(assignment), c.SemanticModel))
                    {
                        return;
                    }

                    var addExpression = GetRight(assignment) as TBinaryExpression;
                    if (addExpression == null ||
                        !ExpressionIsConcatenation(addExpression) ||
                        !EquivalenceChecker.AreEquivalent(GetLeft(assignment), GetLeft(addExpression)))
                    {
                        return;
                    }

                    SyntaxNode nearestLoop;
                    if (!TryGetNearestLoop(assignment, out nearestLoop))
                    {
                        return;
                    }

                    if (IsDefinedInLoop(GetLeft(assignment), nearestLoop, c.SemanticModel))
                    {
                        return;
                    }

                    c.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation()));
                },
                SimpleAssignmentKinds.ToArray());
        }

        protected abstract bool ExpressionIsConcatenation(TBinaryExpression addExpression);
        protected abstract SyntaxNode GetLeft(TAssignmentExpression assignment);
        protected abstract SyntaxNode GetRight(TAssignmentExpression assignment);
        protected abstract SyntaxNode GetLeft(TBinaryExpression binary);

        protected abstract ImmutableArray<TLanguageKindEnum> SimpleAssignmentKinds { get; }
        protected abstract ImmutableArray<TLanguageKindEnum> CompoundAssignmentKinds { get; }

        private static bool IsString(SyntaxNode node, SemanticModel semanticModel)
        {
            var type = semanticModel.GetTypeInfo(node).Type;
            return type != null && type.SpecialType == SpecialType.System_String;
        }

        private bool TryGetNearestLoop(SyntaxNode node, out SyntaxNode nearestLoop)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                if (IsInLoop(parent))
                {
                    nearestLoop = parent;
                    return true;
                }
                parent = parent.Parent;
            }
            nearestLoop = null;
            return false;
        }

        protected abstract bool IsInLoop(SyntaxNode node);

        private bool IsDefinedInLoop(SyntaxNode expression, SyntaxNode nearestLoopForConcatenation,
                SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetSymbolInfo(expression).Symbol as ILocalSymbol;
            if (symbol == null)
            {
                return false;
            }

            var declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (declaration == null)
            {
                return false;
            }

            SyntaxNode nearestLoop;
            if (!TryGetNearestLoop(declaration, out nearestLoop))
            {
                return false;
            }

            return nearestLoop == nearestLoopForConcatenation;
        }
    }
}
