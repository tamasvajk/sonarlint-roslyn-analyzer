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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class RedundantCastCodeFixProvider : SonarCodeFixProvider
    {
        internal const string Title = "Remove redundant cast";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(RedundantCast.DiagnosticId);
            }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return DocumentBasedFixAllProvider.Instance;
        }

        protected sealed override async Task RegisterCodeFixesAsync(SyntaxNode root, CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var syntaxNode = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true);

            var castExpression = syntaxNode.Parent as CastExpressionSyntax;
            if (castExpression != null)
            {
                //this is handled by IDE0004 code fix.
                return;
            }

            var castInvocation = syntaxNode as InvocationExpressionSyntax;
            var memberAccess = syntaxNode as MemberAccessExpressionSyntax;
            if (castInvocation != null ||
                memberAccess != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        c =>
                        {
                            var newRoot = RemoveCall(root, castInvocation, memberAccess);
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        }),
                    context.Diagnostics);
            }

            var asExpression = syntaxNode as BinaryExpressionSyntax;
            if (asExpression != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        c =>
                        {
                            var newRoot = root.ReplaceNode(
                                asExpression,
                                asExpression.Left.WithTriviaFrom(asExpression));
                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        }),
                    context.Diagnostics);
            }
        }

        private static SyntaxNode RemoveCall(SyntaxNode root,
            InvocationExpressionSyntax castInvocation, MemberAccessExpressionSyntax memberAccess)
        {
            return castInvocation != null
                ? RemoveExtensionMethodCall(root, castInvocation)
                : RemoveStaticMemberCall(root, memberAccess);
        }

        private static SyntaxNode RemoveStaticMemberCall(SyntaxNode root,
            MemberAccessExpressionSyntax memberAccess)
        {
            var invocation = (InvocationExpressionSyntax)memberAccess.Parent;
            return root.ReplaceNode(invocation, invocation.ArgumentList.Arguments.First().Expression);
        }

        private static SyntaxNode RemoveExtensionMethodCall(SyntaxNode root, InvocationExpressionSyntax invocation)
        {
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
            return root.ReplaceNode(invocation, memberAccess.Expression);
        }
    }
}

