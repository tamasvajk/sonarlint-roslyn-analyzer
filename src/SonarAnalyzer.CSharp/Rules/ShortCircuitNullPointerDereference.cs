/*
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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System;
using SonarAnalyzer.Helpers.CSharp;

namespace SonarAnalyzer.Rules.CSharp
{
    [Obsolete("This rule is superceded by S2259.")]
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("2min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class ShortCircuitNullPointerDereference : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1697";
        internal const string Title =
            "Short-circuit logic should be used to prevent null pointer dereferences in conditionals";
        internal const string Description =
            "When either the equality operator in a null test or the logical operator that follows it is reversed, " +
            "the code has the appearance of safely null-testing the object before dereferencing it. Unfortunately " +
            "the effect is just the opposite - the object is null-tested and then dereferenced only if it is null, " +
            "leading to a guaranteed null pointer dereference.";
        internal const string MessageFormat =
            "Either reverse the equality operator in the \"{0}\" null test, or reverse the logical operator that follows it.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Blocker;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binaryExpression = (BinaryExpressionSyntax)c.Node;

                    var comparisonOperator = SyntaxKind.ExclamationEqualsToken;

                    if (binaryExpression.OperatorToken.IsKind(SyntaxKind.AmpersandAmpersandToken))
                    {
                        comparisonOperator = SyntaxKind.EqualsEqualsToken;
                    }

                    ReportDereference(binaryExpression, comparisonOperator, c);
                },
                SyntaxKind.LogicalOrExpression, SyntaxKind.LogicalAndExpression);
        }

        private static void ReportDereference(BinaryExpressionSyntax binaryExpression, SyntaxKind comparisonOperator,
            SyntaxNodeAnalysisContext context)
        {
            if (IsMidLevelExpression(binaryExpression))
            {
                return;
            }

            var expressionsInChain = GetExpressionsInChain(binaryExpression).ToList();

            for (var i = 0; i < expressionsInChain.Count; i++)
            {
                var currentExpression = expressionsInChain[i];

                var comparisonToNull = currentExpression as BinaryExpressionSyntax;

                if (comparisonToNull == null || !comparisonToNull.OperatorToken.IsKind(comparisonOperator))
                {
                    continue;
                }

                var leftNull = SyntaxFactory.AreEquivalent(comparisonToNull.Left, SyntaxHelper.NullLiteralExpression);
                var rightNull = SyntaxFactory.AreEquivalent(comparisonToNull.Right, SyntaxHelper.NullLiteralExpression);

                if (leftNull && rightNull)
                {
                    continue;
                }

                if (!leftNull && !rightNull)
                {
                    continue;
                }

                var expressionComparedToNull = leftNull ? comparisonToNull.Right : comparisonToNull.Left;
                CheckFollowingExpressions(context, i, expressionsInChain, expressionComparedToNull, comparisonToNull);
            }
        }

        private static bool IsMidLevelExpression(BinaryExpressionSyntax binaryExpression)
        {
            var binaryParent = binaryExpression.Parent as BinaryExpressionSyntax;
            return binaryParent != null &&
                   SyntaxFactory.AreEquivalent(binaryExpression.OperatorToken, binaryParent.OperatorToken);
        }

        private static void CheckFollowingExpressions(SyntaxNodeAnalysisContext context, int currentExpressionIndex,
            IList<ExpressionSyntax> expressionsInChain,
            ExpressionSyntax expressionComparedToNull, BinaryExpressionSyntax comparisonToNull)
        {
            for (var j = currentExpressionIndex + 1; j < expressionsInChain.Count; j++)
            {
                var descendantNodes = expressionsInChain[j].DescendantNodes()
                    .Where(descendant =>
                        descendant.IsKind(expressionComparedToNull.Kind()) &&
                        EquivalenceChecker.AreEquivalent(expressionComparedToNull, descendant))
                        .Where(descendant =>
                    (descendant.Parent is MemberAccessExpressionSyntax &&
                        EquivalenceChecker.AreEquivalent(expressionComparedToNull,
                            ((MemberAccessExpressionSyntax) descendant.Parent).Expression)) ||
                    (descendant.Parent is ElementAccessExpressionSyntax &&
                        EquivalenceChecker.AreEquivalent(expressionComparedToNull,
                            ((ElementAccessExpressionSyntax) descendant.Parent).Expression)))
                    .ToList();

                if (descendantNodes.Any())
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, comparisonToNull.GetLocation(),
                        expressionComparedToNull.ToString()));
                }
            }
        }

        private static IEnumerable<ExpressionSyntax> GetExpressionsInChain(BinaryExpressionSyntax binaryExpression)
        {
            var expressionList = new List<ExpressionSyntax>();

            var currentBinary = binaryExpression;
            while (currentBinary != null)
            {
                expressionList.Add(currentBinary.Right);

                var leftBinary = currentBinary.Left as BinaryExpressionSyntax;
                if (leftBinary == null ||
                    !SyntaxFactory.AreEquivalent(leftBinary.OperatorToken, binaryExpression.OperatorToken))
                {
                    expressionList.Add(currentBinary.Left);
                    break;
                }

                currentBinary = leftBinary;
            }

            expressionList.Reverse();
            return expressionList;
        }
    }
}