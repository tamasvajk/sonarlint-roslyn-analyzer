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
using System.Linq;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Cert, Tag.Cwe)]
    public class ReferenceEqualityCheckWhenEqualsExists : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1698";
        internal const string Title = "\"==\" should not be used when \"Equals\" is overridden";
        internal const string Description =
            "Using the equality \"==\" and inequality \"!=\" operators to compare two objects generally works. The operators can be " +
            "overloaded, and therefore the comparison can resolve to the appropriate method. However, when the operators are used on " +
            "interface instances, then \"==\" resolves to reference equality, which may result in unexpected behavior  if implementing " +
            "classes override \"Equals\". Similarly, when a class overrides \"Equals\", but instances are compared with non-overloaded " +
            "\"==\", there is a high chance that value comparison was meant instead of the reference one.";
        internal const string MessageFormat = "Consider using \"Equals\" if value comparison was intended.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        private const string EqualsName = "Equals";
        private static readonly ISet<KnownType> AllowedTypes = ImmutableHashSet.Create(
            KnownType.System_Type,
            KnownType.System_Reflection_Assembly,
            KnownType.System_Reflection_MemberInfo,
            KnownType.System_Reflection_Module,
            KnownType.System_Data_Common_CommandTrees_DbExpression);

        private static readonly ISet<KnownType> AllowedTypesWithAllDerived = ImmutableHashSet.Create(new[]
        {
            KnownType.System_Windows_DependencyObject
        });

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterCompilationStartAction(
                compilationStartContext =>
                {
                    var allNamedTypeSymbols = compilationStartContext.Compilation.GlobalNamespace.GetAllNamedTypes();
                    var allInterfacesWithImplementationsOverridenEquals =
                        allNamedTypeSymbols
                            .Where(t => t.AllInterfaces.Any() && HasEqualsOverride(t))
                            .SelectMany(t => t.AllInterfaces)
                            .ToImmutableHashSet();

                    compilationStartContext.RegisterSyntaxNodeActionInNonGenerated(
                        c =>
                        {
                            var binary = (BinaryExpressionSyntax)c.Node;
                            if (!IsBinaryCandidateForReporting(binary, c.SemanticModel))
                            {
                                return;
                            }

                            var typeLeft = c.SemanticModel.GetTypeInfo(binary.Left).Type;
                            var typeRight = c.SemanticModel.GetTypeInfo(binary.Right).Type;
                            if (typeLeft == null ||
                                typeRight == null ||
                                IsAllowedType(typeLeft) ||
                                IsAllowedType(typeRight))
                            {
                                return;
                            }

                            if (MightOverrideEquals(typeLeft, allInterfacesWithImplementationsOverridenEquals) ||
                                MightOverrideEquals(typeRight, allInterfacesWithImplementationsOverridenEquals))
                            {
                                c.ReportDiagnostic(Diagnostic.Create(Rule, binary.OperatorToken.GetLocation()));
                            }
                        },
                        SyntaxKind.EqualsExpression,
                        SyntaxKind.NotEqualsExpression);
                });
        }

        private static bool MightOverrideEquals(ITypeSymbol type, ISet<INamedTypeSymbol> allInterfacesWithImplementationsOverridenEquals)
        {
            return HasEqualsOverride(type) ||
                allInterfacesWithImplementationsOverridenEquals.Contains(type) ||
                HasTypeConstraintsWhichMightOverrideEquals(type, allInterfacesWithImplementationsOverridenEquals);
        }

        private static bool HasTypeConstraintsWhichMightOverrideEquals(ITypeSymbol type, ISet<INamedTypeSymbol> allInterfacesWithImplementationsOverridenEquals)
        {
            if (type.TypeKind != TypeKind.TypeParameter)
            {
                return false;
            }

            var typeParameter = (ITypeParameterSymbol)type;
            return typeParameter.ConstraintTypes.Any(t => MightOverrideEquals(t, allInterfacesWithImplementationsOverridenEquals));
        }

        private static bool IsAllowedType(ITypeSymbol type)
        {
            return type.IsAny(AllowedTypes) || HasAllowedBaseType(type);
        }

        private static bool HasAllowedBaseType(ITypeSymbol type)
        {
            var currentType = type;
            while (currentType != null)
            {
                if (currentType.IsAny(AllowedTypesWithAllDerived))
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }
            return false;
        }

        private static bool IsBinaryCandidateForReporting(BinaryExpressionSyntax binary, SemanticModel semanticModel)
        {
            var equalitySymbol = semanticModel.GetSymbolInfo(binary).Symbol as IMethodSymbol;

            return equalitySymbol.IsInType(KnownType.System_Object) &&
                !IsInEqualsOverride(semanticModel.GetEnclosingSymbol(binary.SpanStart) as IMethodSymbol);
        }

        private static bool HasEqualsOverride(ITypeSymbol type)
        {
            return GetEqualsOverrides(type).Any(m => m.OverriddenMethod.IsInType(KnownType.System_Object));
        }

        private static IEnumerable<IMethodSymbol> GetEqualsOverrides(ITypeSymbol type)
        {
            if (type == null)
            {
                return Enumerable.Empty<IMethodSymbol>();
            }

            var candidateEqualsMethods = new HashSet<IMethodSymbol>();

            var currentType = type;
            while (currentType != null &&
                !currentType.Is(KnownType.System_Object))
            {
                candidateEqualsMethods.UnionWith(currentType.GetMembers(EqualsName)
                    .OfType<IMethodSymbol>()
                    .Where(method => method.IsOverride && method.OverriddenMethod != null));

                currentType = currentType.BaseType;
            }

            return candidateEqualsMethods;
        }

        private static bool IsInEqualsOverride(IMethodSymbol method)
        {
            if (method == null)
            {
                return false;
            }

            var currentMethod = method;
            while (currentMethod != null)
            {
                if (currentMethod.Name == EqualsName &&
                    currentMethod.IsInType(KnownType.System_Object))
                {
                    return true;
                }

                currentMethod = currentMethod.OverriddenMethod;
            }
            return false;
        }
    }
}