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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Rules.Common;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Tags(Tag.Pitfall)]
    public class OptionalParameter : OptionalParameterBase<SyntaxKind, BaseMethodDeclarationSyntax, ParameterSyntax>
    {
        private static readonly ImmutableArray<SyntaxKind> kindsOfInterest = ImmutableArray.Create(SyntaxKind.MethodDeclaration, SyntaxKind.ConstructorDeclaration);
        public override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => kindsOfInterest;

        protected override IEnumerable<ParameterSyntax> GetParameters(BaseMethodDeclarationSyntax method) =>
            method.ParameterList?.Parameters ?? Enumerable.Empty<ParameterSyntax>();

        protected override bool IsOptional(ParameterSyntax parameter) =>
            parameter.Default != null && parameter.Default.Value != null;

        protected override Location GetReportLocation(ParameterSyntax parameter) =>
            parameter.Default.GetLocation();

        protected sealed override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.CSharp.GeneratedCodeRecognizer.Instance;
    }
}
