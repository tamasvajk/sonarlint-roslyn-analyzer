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
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class ObjectCreatedDropped : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1848";
        internal const string Title = "Objects should not be created to be dropped immediately without being used";
        internal const string Description =
            "There is no good reason to create a new object to not do anything with it. Most of the time, " +
            "this is due to a missing piece of code and so could lead to an unexpected behavior in production.";
        internal const string MessageFormat = "Either remove this useless object instantiation of class \"{0}\" or use it.";
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
                    if (c.SemanticModel.Compilation.IsTest())
                    {
                        return;
                    }

                    var objectCreation = (ObjectCreationExpressionSyntax)c.Node;
                    var parent = objectCreation.Parent as ExpressionStatementSyntax;
                    if (parent != null)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.GetLocation(), objectCreation.Type));
                    }
                },
                SyntaxKind.ObjectCreationExpression);
        }
    }
}
