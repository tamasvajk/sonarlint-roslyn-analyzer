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
using System.Collections.Generic;
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.ArchitectureReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Pitfall)]
    public class DisposeNotImplementingDispose : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2953";
        internal const string Title = "Methods named \"Dispose\" should implement \"IDisposable.Dispose\"";
        internal const string Description =
            "\"Dispose\" as a method name should be used exclusively to implement \"IDisposable.Dispose\" to prevent any " +
            "confusion. It may be tempting to create a \"Dispose\" method for other purposes, but doing so will result in " +
            "confusion and likely lead to problems in production.";
        internal const string MessageFormat = "Either implement \"IDisposable.Dispose\", or totally rename this method to prevent confusion.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const string DisposeMethodName = "Dispose";

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var declaredSymbol = (INamedTypeSymbol)c.Symbol;

                    if (declaredSymbol.DeclaringSyntaxReferences.Count() > 1)
                    {
                        // Partial classes are not processed.
                        // See https://github.com/dotnet/roslyn/issues/3748
                        return;
                    }

                    var disposeMethod = GetDisposeMethod(c.Compilation);
                    if (disposeMethod == null)
                    {
                        return;
                    }

                    var mightImplementDispose = new HashSet<IMethodSymbol>();
                    var namedDispose = new HashSet<IMethodSymbol>();

                    var methods = declaredSymbol.GetMembers(DisposeMethodName).OfType<IMethodSymbol>();
                    foreach (var method in methods)
                    {
                        CollectMethodsNamedAndImplementingDispose(method, disposeMethod, namedDispose, mightImplementDispose);
                    }

                    var disposeMethodsCalledFromDispose = new HashSet<IMethodSymbol>();
                    CollectInvocationsFromDisposeImplementation(disposeMethod, c.Compilation, mightImplementDispose, disposeMethodsCalledFromDispose);

                    ReportDisposeMethods(
                        namedDispose.Except(mightImplementDispose).Where(m => !disposeMethodsCalledFromDispose.Contains(m)),
                        c);
                },
                SymbolKind.NamedType);
        }

        private static void CollectInvocationsFromDisposeImplementation(IMethodSymbol disposeMethod, Compilation compilation,
            HashSet<IMethodSymbol> mightImplementDispose,
            HashSet<IMethodSymbol> disposeMethodsCalledFromDispose)
        {
            foreach (var method in mightImplementDispose
                .Where(method => MethodIsDisposeImplementation(method, disposeMethod)))
            {
                var methodDeclarations = method.DeclaringSyntaxReferences
                    .Select(r => new SyntaxNodeSemanticModelTuple<MethodDeclarationSyntax>
                    {
                        SyntaxNode = r.GetSyntax() as MethodDeclarationSyntax,
                        SemanticModel = compilation.GetSemanticModel(r.SyntaxTree)
                    })
                    .Where(m => m.SyntaxNode != null);

                var methodDeclaration = methodDeclarations
                    .FirstOrDefault(m =>
                        m.SyntaxNode.Body != null ||
                        m.SyntaxNode.ExpressionBody != null);

                if (methodDeclaration == null)
                {
                    continue;
                }

                var invocations = methodDeclaration.SyntaxNode.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>();

                foreach (var invocation in invocations)
                {
                    CollectDisposeMethodsCalledFromDispose(invocation, methodDeclaration.SemanticModel,
                        disposeMethodsCalledFromDispose);
                }
            }
        }

        private static void CollectDisposeMethodsCalledFromDispose(InvocationExpressionSyntax invocationExpression,
            SemanticModel semanticModel,
            HashSet<IMethodSymbol> disposeMethodsCalledFromDispose)
        {
            if (!IsCallOnThis(invocationExpression))
            {
                return;
            }

            var invokedMethod = semanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;
            if (invokedMethod == null ||
                invokedMethod.Name != DisposeMethodName)
            {
                return;
            }

            disposeMethodsCalledFromDispose.Add(invokedMethod);
        }

        private static bool IsCallOnThis(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is NameSyntax)
            {
                return true;
            }

            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess != null && memberAccess.Expression.IsKind(SyntaxKind.ThisExpression))
            {
                return true;
            }

            return false;
        }

        private static void ReportDisposeMethods(IEnumerable<IMethodSymbol> disposeMethods,
            SymbolAnalysisContext context)
        {
            foreach (var location in disposeMethods.SelectMany(m => m.Locations))
            {
                context.ReportDiagnosticIfNonGenerated(Diagnostic.Create(
                    Rule, location));
            }
        }

        private static void CollectMethodsNamedAndImplementingDispose(IMethodSymbol methodSymbol, IMethodSymbol disposeMethod,
            HashSet<IMethodSymbol> namedDispose, HashSet<IMethodSymbol> mightImplementDispose)
        {
            if (methodSymbol.Name != DisposeMethodName)
            {
                return;
            }

            namedDispose.Add(methodSymbol);

            if (methodSymbol.IsOverride ||
                MethodIsDisposeImplementation(methodSymbol, disposeMethod) ||
                MethodMightImplementDispose(methodSymbol))
            {
                mightImplementDispose.Add(methodSymbol);
            }
        }

        private static bool MethodIsDisposeImplementation(IMethodSymbol methodSymbol, IMethodSymbol disposeMethod)
        {
            return methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(disposeMethod));
        }

        private static bool MethodMightImplementDispose(IMethodSymbol declaredMethodSymbol)
        {
            var containingType = declaredMethodSymbol.ContainingType;

            if (containingType.BaseType != null && containingType.BaseType.Kind == SymbolKind.ErrorType)
            {
                return true;
            }

            var interfaces = containingType.AllInterfaces;
            foreach (var @interface in interfaces)
            {
                if (@interface.Kind == SymbolKind.ErrorType)
                {
                    return true;
                }

                var interfaceMethods = @interface.GetMembers().OfType<IMethodSymbol>();
                if (interfaceMethods.Any(interfaceMethod => declaredMethodSymbol.Equals(containingType.FindImplementationForInterfaceMember(interfaceMethod))))
                {
                    return true;
                }
            }
            return false;
        }

        internal static IMethodSymbol GetDisposeMethod(Compilation compilation)
        {
            return (IMethodSymbol)compilation.GetSpecialType(SpecialType.System_IDisposable)
                .GetMembers("Dispose")
                .SingleOrDefault();
        }
    }
}
