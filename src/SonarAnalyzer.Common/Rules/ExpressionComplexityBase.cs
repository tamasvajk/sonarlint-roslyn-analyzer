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

using System.Linq;
using Microsoft.CodeAnalysis;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules
{
    [SqaleConstantRemediation("30min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.UnitTestability)]
    [Tags(Tag.BrainOverload)]
    public abstract class ExpressionComplexityBase : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1067";
        internal const string Title = "Expressions should not be too complex";
        internal const string MessageFormat = "Reduce the number of conditional operators ({1}) used in the expression (maximum allowed {0}).";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;

        private const int DefaultValueMaximum = 3;

        [RuleParameter("max", PropertyType.Integer,
            "Maximum number of allowed conditional operators in an expression", DefaultValueMaximum)]
        public int Maximum { get; set; } = DefaultValueMaximum;
    }

    public abstract class ExpressionComplexityBase<TExpression> : ExpressionComplexityBase
        where TExpression : SyntaxNode
    {
        public abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var root = c.Tree.GetRoot();

                    var rootExpressions = root
                        .DescendantNodes(node => !(node is TExpression))
                        .OfType<TExpression>()
                        .Where(expression => !IsCompoundExpression(expression));

                    var compoundExpressionsDescendants = root
                        .DescendantNodes()
                        .Where(IsCompoundExpression)
                        .SelectMany(
                            compoundExpression => compoundExpression
                                .DescendantNodes(node => compoundExpression == node || !(node is TExpression))
                                .OfType<TExpression>()
                                .Where(expression => !IsCompoundExpression(expression)));

                    var expressionsToCheck = rootExpressions.Concat(compoundExpressionsDescendants);

                    var complexExpressions = expressionsToCheck
                        .Select(expression =>
                            new
                            {
                                Expression = expression,
                                Complexity = expression
                                    .DescendantNodesAndSelf(e2 => !IsCompoundExpression(e2))
                                    .Count(IsComplexityIncreasingKind)
                            })
                        .Where(e => e.Complexity > Maximum);

                    foreach (var complexExpression in complexExpressions)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(SupportedDiagnostics.First(),
                            complexExpression.Expression.GetLocation(),
                            Maximum,
                            complexExpression.Complexity));
                    }
                });
        }

        protected abstract bool IsComplexityIncreasingKind(SyntaxNode node);

        protected abstract bool IsCompoundExpression(SyntaxNode node);
    }
}
