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
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [ExportCodeFixProvider(LanguageNames.VisualBasic)]
    public class ArrayDesignatorOnVariableCodeFixProvider : SonarCodeFixProvider
    {
        internal const string Title = "Move the array designator to the type";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(ArrayDesignatorOnVariable.DiagnosticId);
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
            var name = root.FindNode(diagnosticSpan) as ModifiedIdentifierSyntax;

            var variableDeclarator = name?.Parent as VariableDeclaratorSyntax;
            if (variableDeclarator == null ||
                variableDeclarator.Names.Count != 1)
            {
                return;
            }

            var simpleAsClause = variableDeclarator.AsClause as SimpleAsClauseSyntax;
            if (simpleAsClause == null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var type = simpleAsClause.Type.WithoutTrivia();
                        var typeAsArrayType = type as ArrayTypeSyntax;
                        var rankSpecifiers = name.ArrayRankSpecifiers.Select(rank => rank.WithoutTrivia());
                        var newType = typeAsArrayType == null
                            ? SyntaxFactory.ArrayType(
                                        type,
                                        SyntaxFactory.List(rankSpecifiers))
                            : typeAsArrayType.AddRankSpecifiers(rankSpecifiers.ToArray());

                        newType = newType.WithTriviaFrom(simpleAsClause.Type);

                        var newVariableDeclarator = variableDeclarator
                            .WithNames(SyntaxFactory.SeparatedList(new[] {
                                SyntaxFactory.ModifiedIdentifier(name.Identifier, name.ArrayBounds).WithTriviaFrom(name)
                            }))
                            .WithAsClause(simpleAsClause.WithType(newType));

                        var newRoot = root.ReplaceNode(
                            variableDeclarator,
                            newVariableDeclarator);

                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }
    }
}

