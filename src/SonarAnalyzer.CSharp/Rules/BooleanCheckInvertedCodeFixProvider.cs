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
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Generic;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class BooleanCheckInvertedCodeFixProvider : SonarCodeFixProvider
    {
        internal const string Title = "Invert \"Boolean\" check";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(BooleanCheckInverted.DiagnosticId);
            }
        }
        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        protected sealed override async Task RegisterCodeFixesAsync(SyntaxNode root, CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var syntaxNode = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true) as PrefixUnaryExpressionSyntax;

            if (syntaxNode == null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var expression = syntaxNode.Operand.RemoveParentheses();
                        var newBinary = ChangeOperator((BinaryExpressionSyntax)expression);

                        if (syntaxNode.Parent is ExpressionSyntax &&
                            !ExpressionTypesWithNoParens.Any(type => type.IsInstanceOfType(syntaxNode.Parent)))
                        {
                            newBinary = SyntaxFactory.ParenthesizedExpression(newBinary);
                        }

                        var newRoot = root.ReplaceNode(
                            syntaxNode,
                            newBinary.WithAdditionalAnnotations(Formatter.Annotation));

                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }

        private static readonly Type[] ExpressionTypesWithNoParens = { typeof(AssignmentExpressionSyntax) };

        private static ExpressionSyntax ChangeOperator(BinaryExpressionSyntax binary)
        {
            return
                SyntaxFactory.BinaryExpression(
                    OppositeExpressionKinds[binary.Kind()],
                    binary.Left,
                    binary.Right)
                .WithTriviaFrom(binary);
        }

        private static readonly Dictionary<SyntaxKind, SyntaxKind> OppositeExpressionKinds =
            new Dictionary<SyntaxKind, SyntaxKind>
            {
                {SyntaxKind.GreaterThanExpression, SyntaxKind.LessThanOrEqualExpression},
                {SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.LessThanExpression},
                {SyntaxKind.LessThanExpression, SyntaxKind.GreaterThanOrEqualExpression},
                {SyntaxKind.LessThanOrEqualExpression, SyntaxKind.GreaterThanExpression},
                {SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression},
                {SyntaxKind.NotEqualsExpression, SyntaxKind.EqualsExpression}
            };
    }
}

