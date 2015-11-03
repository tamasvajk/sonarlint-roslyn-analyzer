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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using System;

namespace SonarLint.Rules.Common
{
    public abstract class SingleStatementPerLineBase : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S122";
        internal const string Title = "Statements should be on separate lines";
        internal const string Description =
            "For better readability, do not put more than one statement on a single line.";
        internal const string MessageFormat = "Reformat the code to have only one statement per line.";
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }
    }

    public abstract class SingleStatementPerLineBase<TStatementSyntax> : SingleStatementPerLineBase
        where TStatementSyntax : SyntaxNode
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    var statements = GetStatements(c.Tree);

                    var statementsByLines = new Dictionary<int, List<TStatementSyntax>>();
                    foreach (var statement in statements)
                    {
                        AddStatementToLineCache(statement, statementsByLines);
                    }

                    var lines = c.Tree.GetText().Lines;
                    foreach (var statementsByLine in statementsByLines.Where(pair => pair.Value.Count > 1))
                    {
                        var location = CalculateLocationForLine(lines, c.Tree, statementsByLine);
                        c.ReportDiagnostic(Diagnostic.Create(Rule, location));
                    }
                });
        }

        private IEnumerable<TStatementSyntax> GetStatements(SyntaxTree tree)
        {
            var statements = tree.GetRoot()
                    .DescendantNodesAndSelf()
                    .OfType<TStatementSyntax>()
                    .Where(st => !StatementShouldBeExcluded(st));

            return statements;
        }

        protected abstract bool StatementShouldBeExcluded(TStatementSyntax statement);
        private TStatementSyntax GetContainingStatement(SyntaxToken token)
        {
            var node = token.Parent;
            var statement = node as TStatementSyntax;
            while (node != null &&
                (statement == null || !StatementShouldBeExcluded(statement)))
            {
                node = node.Parent;
                statement = node as TStatementSyntax;
            }
            return statement;
        }

        private static Location CalculateLocationForLine(TextLineCollection lines, SyntaxTree tree,
            KeyValuePair<int, List<TStatementSyntax>> statementsByLine)
        {
            var line = statementsByLine.Key;
            var lineSpan = lines[line].Span;

            var min = statementsByLine.Value.Min(st => lineSpan.Intersection(st.Span).Value.Start);
            var max = statementsByLine.Value.Max(st => lineSpan.Intersection(st.Span).Value.End);

            return Location.Create(tree, TextSpan.FromBounds(min, max));
        }

        private void AddStatementToLineCache(TStatementSyntax statement, Dictionary<int, List<TStatementSyntax>> statementsByLines)
        {
            var startLine = statement.GetLocation().GetLineSpan().StartLinePosition.Line;
            AddStatementWithLine(statement, startLine, statementsByLines);

            var lastToken = statement.GetLastToken();
            var tokenBelonsTo = GetContainingStatement(lastToken);
            if (tokenBelonsTo == statement)
            {
                var endLine = statement.GetLocation().GetLineSpan().EndLinePosition.Line;
                AddStatementWithLine(statement, endLine, statementsByLines);
            }
        }

        private static void AddStatementWithLine(TStatementSyntax statement, int line, Dictionary<int, List<TStatementSyntax>> statementsByLines)
        {
            if (!statementsByLines.ContainsKey(line))
            {
                statementsByLines.Add(line, new List<TStatementSyntax>());
            }

            if (!statementsByLines[line].Contains(statement))
            {
                statementsByLines[line].Add(statement);
            }
        }
    }
}
