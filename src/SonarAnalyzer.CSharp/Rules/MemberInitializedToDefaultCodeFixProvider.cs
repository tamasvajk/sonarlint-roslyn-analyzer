﻿/*
 * SonarAnalyzer for .NET
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
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarAnalyzer.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class MemberInitializedToDefaultCodeFixProvider : SonarCodeFixProvider
    {
        private const string Title = "Remove redundant initializer";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(MemberInitializedToDefault.DiagnosticId, MemberInitializerRedundant.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => DocumentBasedFixAllProvider.Instance;

        protected sealed override async Task RegisterCodeFixesAsync(SyntaxNode root, CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var initializer = root.FindNode(diagnosticSpan) as EqualsValueClauseSyntax;
            if (initializer == null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var parent = initializer.Parent;
                        var propDecl = parent as PropertyDeclarationSyntax;

                        SyntaxNode newParent;

                        if (propDecl == null)
                        {
                            newParent = parent.RemoveNode(initializer, SyntaxRemoveOptions.KeepNoTrivia);
                        }
                        else
                        {
                            var newPropDecl = propDecl
                                .WithInitializer(null)
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
                                .WithTriviaFrom(propDecl);

                            newParent = newPropDecl;
                        }

                        var newRoot = root.ReplaceNode(
                            parent,
                            newParent.WithAdditionalAnnotations(Formatter.Annotation));
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }
    }
}
