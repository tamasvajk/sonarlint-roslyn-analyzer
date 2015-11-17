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
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [NoSqaleRemediation]
    [Rule(TemplateDiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    public class CommentRegularExpression : DiagnosticAnalyzer, IRuleTemplate<CommentRegularExpression.CommentRegularExpressionRule>
    {
        public sealed class CommentRegularExpressionRule : IRuleTemplateInstance
        {
            public CommentRegularExpressionRule(string diagnosticId, string regularExpression, string message)
            {
                DiagnosticId = diagnosticId;
                RegularExpression = regularExpression;
                Message = message;

                descriptor = new DiagnosticDescriptor(DiagnosticId, Title, Message, Category,
                    RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault);
            }
            public CommentRegularExpressionRule(string diagnosticId, string regularExpression)
                : this(diagnosticId, regularExpression, DefaultMessage)
            {
            }

            private readonly DiagnosticDescriptor descriptor;
            public DiagnosticDescriptor Descriptor => descriptor;

            public string DiagnosticId { get; private set; }

            [RuleParameter("regularExpression", PropertyType.String, "The regular expression")]
            public string RegularExpression { get; private set; }

            [RuleParameter("message", PropertyType.String, "The issue message", DefaultMessage)]
            public string Message { get; private set; }

            private const string DefaultMessage = "The regular expression matches this comment.";
        }


        public const string TemplateDiagnosticId = "S124";
        internal const string Title = "Comments matching a regular expression should be handled";
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        public IImmutableList<CommentRegularExpressionRule> RuleInstances { get; set; }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return RuleInstances == null
                    ? ImmutableArray<DiagnosticDescriptor>.Empty
                    : RuleInstances.Select(r => r.Descriptor).ToImmutableArray();
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    if (RuleInstances == null ||
                        !RuleInstances.Any())
                    {
                        return;
                    }

                    var comments = c.Tree.GetCompilationUnitRoot().DescendantTrivia()
                        .Where(trivia => IsComment(trivia));

                    foreach (var comment in comments)
                    {
                        var text = comment.ToString();
                        foreach (var rule in RuleInstances.Where(rule => Regex.IsMatch(text, rule.RegularExpression)))
                        {
                            c.ReportDiagnostic(Diagnostic.Create(rule.Descriptor, comment.GetLocation()));
                        }
                    }
                });
        }

        private static bool IsComment(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
        }
    }
}
