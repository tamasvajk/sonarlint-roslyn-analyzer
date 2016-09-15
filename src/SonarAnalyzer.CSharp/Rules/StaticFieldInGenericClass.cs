/*
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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    public class StaticFieldInGenericClass : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2743";
        internal const string Title = "Static fields should not be used in generic types";
        internal const string Description =
            "A static field in a generic type is not shared among instances of different closed constructed types, " +
            "If you need to have a static field shared among instances with different generic arguments, define a " +
            "non-generic base class to store your static members, then set your generic type to inherit from the " +
            "base class.";
        internal const string MessageFormat =
            "A static field in a generic type is not shared among instances of different close constructed types.";
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
                    var classDeclaration = (ClassDeclarationSyntax)c.Node;

                    if (classDeclaration.TypeParameterList == null ||
                        classDeclaration.TypeParameterList.Parameters.Count < 1)
                    {
                        return;
                    }

                    var typeParameterNames =
                        classDeclaration.TypeParameterList.Parameters.Select(p => p.Identifier.ToString()).ToList();

                    var fields = classDeclaration.Members
                        .OfType<FieldDeclarationSyntax>()
                        .Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)));

                    foreach (var field in fields.Where(field => !HasGenericType(field.Declaration.Type, typeParameterNames, c)))
                    {
                        field.Declaration.Variables.ToList().ForEach(variable =>
                        {
                            CheckMember(variable, variable.Identifier.GetLocation(), typeParameterNames, c);
                        });
                    }

                    var properties = classDeclaration.Members
                        .OfType<PropertyDeclarationSyntax>()
                        .Where(p => p.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
                        .ToList();

                    properties.ForEach(property =>
                    {
                        CheckMember(property, property.Identifier.GetLocation(), typeParameterNames, c);
                    });

                },
                SyntaxKind.ClassDeclaration);
        }

        private static void CheckMember(SyntaxNode root, Location location, IEnumerable<string> typeParameterNames,
            SyntaxNodeAnalysisContext context)
        {
            if (HasGenericType(root, typeParameterNames, context))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
        }

        private static bool HasGenericType(SyntaxNode root, IEnumerable<string> typeParameterNames,
            SyntaxNodeAnalysisContext context)
        {
            var typeParameters = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(identifier => context.SemanticModel.GetSymbolInfo(identifier).Symbol)
                .Where(symbol => symbol != null && symbol.Kind == SymbolKind.TypeParameter)
                .Select(symbol => symbol.Name)
                .ToList();

            return typeParameters.Intersect(typeParameterNames).Any();
        }
    }
}