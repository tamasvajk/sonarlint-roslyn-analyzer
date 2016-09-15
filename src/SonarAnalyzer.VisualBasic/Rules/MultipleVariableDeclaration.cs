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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Rules.Common;

namespace SonarAnalyzer.Rules.VisualBasic
{
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis.VisualBasic;
    using Microsoft.CodeAnalysis.VisualBasic.Syntax;
    using Helpers;

    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("2min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Tags(Tag.Convention)]
    public class MultipleVariableDeclaration : MultipleVariableDeclarationBase<SyntaxKind,
        FieldDeclarationSyntax, LocalDeclarationStatementSyntax>
    {
        public override SyntaxKind FieldDeclarationKind => SyntaxKind.FieldDeclaration;
        public override SyntaxKind LocalDeclarationKind => SyntaxKind.LocalDeclarationStatement;


        protected override IEnumerable<SyntaxToken> GetIdentifiers(FieldDeclarationSyntax node) =>
            node.Declarators.SelectMany(d => d.Names.Select(n => n.Identifier));

        protected override IEnumerable<SyntaxToken> GetIdentifiers(LocalDeclarationStatementSyntax node) =>
            node.Declarators.SelectMany(d => d.Names.Select(n => n.Identifier));

        protected sealed override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.VisualBasic.GeneratedCodeRecognizer.Instance;
    }
}