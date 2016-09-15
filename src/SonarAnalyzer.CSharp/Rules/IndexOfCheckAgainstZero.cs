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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class IndexOfCheckAgainstZero : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2692";
        internal const string Title = "\"IndexOf\" checks should not be for positive numbers";
        internal const string Description =
            "Most checks against an \"IndexOf\" value compare it with \"-1\" because \"0\" is a valid index. Any checks which " +
            "look for values \">0\" ignore the first element, which is likely a bug. If the intent is merely to check inclusion " +
            "of a value in a \"string\", \"List\", or an array, consider using the \"Contains\" method instead.";
        internal const string MessageFormat = "0 is a valid index, but this check ignores it.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Critical;
        internal const bool IsActivatedByDefault = true;

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
                    var lessThan = (BinaryExpressionSyntax) c.Node;
                    int constValue;
                    if (ExpressionNumericConverter.TryGetConstantIntValue(lessThan.Left, out constValue) &&
                        constValue == 0 &&
                        IsIndexOfCall(lessThan.Right, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(lessThan.SyntaxTree,
                            TextSpan.FromBounds(lessThan.Left.SpanStart, lessThan.OperatorToken.Span.End))));
                    }
                },
                SyntaxKind.LessThanExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var greaterThan = (BinaryExpressionSyntax)c.Node;
                    int constValue;
                    if (ExpressionNumericConverter.TryGetConstantIntValue(greaterThan.Right, out constValue) &&
                        constValue == 0 &&
                        IsIndexOfCall(greaterThan.Left, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(greaterThan.SyntaxTree,
                            TextSpan.FromBounds(greaterThan.OperatorToken.SpanStart, greaterThan.Right.Span.End))));
                    }
                },
                SyntaxKind.GreaterThanExpression);
        }

        private static bool IsIndexOfCall(ExpressionSyntax call, SemanticModel semanticModel)
        {
            var indexOfSymbol = semanticModel.GetSymbolInfo(call).Symbol as IMethodSymbol;
            if (indexOfSymbol == null ||
                indexOfSymbol.Name != "IndexOf")
            {
                return false;
            }

            var possibleTypes = new[]
            {
                KnownType.System_Array,
                KnownType.System_Collections_Generic_IList_T,
                KnownType.System_String,
                KnownType.System_Collections_IList
            }.ToImmutableHashSet();

            return indexOfSymbol.ContainingType.DerivesOrImplementsAny(possibleTypes);
        }
    }
}
