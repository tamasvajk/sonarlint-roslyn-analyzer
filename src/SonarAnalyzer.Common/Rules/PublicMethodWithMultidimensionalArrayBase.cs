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
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.Common
{
    public abstract class PublicMethodWithMultidimensionalArrayBase : SonarDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S2368";
        protected const string Title = "Public methods should not have multidimensional array parameters";
        protected const string Description =
            "Exposing methods with multidimensional array parameters require developers to have advanced knowledge about the language in " +
            "order to be able to use them. Moreover, what exactly to pass to such parameters is not intuitive. Therefore, such methods " +
            "should not be exposed, but can be used internally.";
        protected const string MessageFormat = "Make this method private or simplify its parameters to not use multidimensional arrays.";
        protected const string Category = SonarAnalyzer.Common.Category.Maintainability;
        protected const Severity RuleSeverity = Severity.Major;
        protected const bool IsActivatedByDefault = true;

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }
    }

    public abstract class PublicMethodWithMultidimensionalArrayBase<TLanguageKindEnum, TMethodSyntax> : PublicMethodWithMultidimensionalArrayBase
        where TLanguageKindEnum : struct
        where TMethodSyntax: SyntaxNode
    {
        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var method = (TMethodSyntax)c.Node;
                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

                    if (methodSymbol == null ||
                        methodSymbol.IsInterfaceImplementationOrMemberOverride() ||
                        !methodSymbol.IsPublicApi() ||
                        !MethodHasMultidimensionalArrayParameters(methodSymbol))
                    {
                        return;
                    }

                    var identifier = GetIdentifier(method);
                    c.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation()));
                },
                SyntaxKindsOfInterest.ToArray());
        }

        private static bool MethodHasMultidimensionalArrayParameters(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters
                .Select(param => param.Type as IArrayTypeSymbol)
                .Where(type => type != null)
                .Any(type => type.Rank > 1 || type.ElementType is IArrayTypeSymbol);
        }

        protected abstract SyntaxToken GetIdentifier(TMethodSyntax method);

        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
    }
}
