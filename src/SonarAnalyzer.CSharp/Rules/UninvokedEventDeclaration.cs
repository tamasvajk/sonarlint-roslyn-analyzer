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
using System;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("10min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Unused)]
    public class UninvokedEventDeclaration : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3264";
        internal const string Title = "Events should be invoked";
        internal const string Description =
            "Events that are not invoked anywhere are dead code, and there's no good reason to keep them in the source.";
        internal const string MessageFormat = "Remove this unused event or invoke it.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        private static readonly Accessibility maxAccessibility = Accessibility.Public;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var namedType = (INamedTypeSymbol)c.Symbol;
                    if (!namedType.IsClassOrStruct() ||
                        namedType.ContainingType != null)
                    {
                        return;
                    }

                    var removableDeclarationCollector = new RemovableDeclarationCollector(namedType, c.Compilation);

                    var removableEvents = removableDeclarationCollector.GetRemovableDeclarations(
                        ImmutableHashSet.Create(SyntaxKind.EventDeclaration), maxAccessibility);
                    var removableEventFields = removableDeclarationCollector.GetRemovableFieldLikeDeclarations(
                        ImmutableHashSet.Create(SyntaxKind.EventFieldDeclaration), maxAccessibility);

                    var allRemovableEvents = removableEvents.Concat(removableEventFields).ToList();
                    if (!allRemovableEvents.Any())
                    {
                        return;
                    }

                    var symbolNames = allRemovableEvents.Select(t => t.Symbol.Name).ToImmutableHashSet();
                    var usedSymbols = GetReferencedSymbolsWithMatchingNames(removableDeclarationCollector, symbolNames);
                    var invokedSymbols = GetInvokedEventSymbols(removableDeclarationCollector);
                    var possiblyCopiedSymbols = GetPossiblyCopiedSymbols(removableDeclarationCollector);

                    foreach (var removableEvent in allRemovableEvents)
                    {
                        if (!usedSymbols.Contains(removableEvent.Symbol))
                        {
                            /// reported by <see cref="UnusedPrivateMember"/>
                            continue;
                        }

                        if (!invokedSymbols.Contains(removableEvent.Symbol) &&
                            !possiblyCopiedSymbols.Contains(removableEvent.Symbol))
                        {
                            var eventField = removableEvent.SyntaxNode as VariableDeclaratorSyntax;
                            var location = eventField != null
                                ? eventField.Identifier.GetLocation()
                                : ((EventDeclarationSyntax)removableEvent.SyntaxNode).Identifier.GetLocation();

                            c.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule, location));
                        }
                    }
                },
                SymbolKind.NamedType);
        }

        private static ISet<ISymbol> GetReferencedSymbolsWithMatchingNames(RemovableDeclarationCollector removableDeclarationCollector,
            ISet<string> symbolNames)
        {
            var usedSymbols = new HashSet<ISymbol>();

            var identifiers = removableDeclarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .Where(node =>
                        node.IsKind(SyntaxKind.IdentifierName))
                    .Cast<IdentifierNameSyntax>()
                    .Where(node => symbolNames.Contains(node.Identifier.ValueText))
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }));

            var generic = removableDeclarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .Where(node =>
                        node.IsKind(SyntaxKind.GenericName))
                    .Cast<GenericNameSyntax>()
                    .Where(node => symbolNames.Contains(node.Identifier.ValueText))
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<SyntaxNode>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel
                        }));

            var allNodes = identifiers.Concat(generic);

            foreach (var node in allNodes)
            {
                var symbol = node.SemanticModel.GetSymbolInfo(node.SyntaxNode).Symbol;

                if (symbol != null)
                {
                    usedSymbols.Add(symbol.OriginalDefinition);
                }
            }

            return usedSymbols;
        }

        private static ISet<ISymbol> GetInvokedEventSymbols(RemovableDeclarationCollector removableDeclarationCollector)
        {
            var delegateInvocations = removableDeclarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .Where(node => node.IsKind(SyntaxKind.InvocationExpression))
                    .Cast<InvocationExpressionSyntax>()
                    .Select(node =>
                        new SyntaxNodeSymbolSemanticModelTuple<InvocationExpressionSyntax, IMethodSymbol>
                        {
                            SyntaxNode = node,
                            SemanticModel = container.SemanticModel,
                            Symbol = container.SemanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol
                        }))
                 .Where(tuple => tuple.Symbol != null &&
                    tuple.Symbol.MethodKind == MethodKind.DelegateInvoke);

            var invokedEventSymbols = delegateInvocations
                .Select(tuple =>
                    new SyntaxNodeSemanticModelTuple<ExpressionSyntax>
                    {
                        SyntaxNode = GetEventExpressionFromInvocation(tuple.SyntaxNode, tuple.Symbol),
                        SemanticModel = tuple.SemanticModel
                    })
                .Select(tuple =>
                    new SyntaxNodeSymbolSemanticModelTuple<ExpressionSyntax, IEventSymbol>
                    {
                        SyntaxNode = tuple.SyntaxNode,
                        SemanticModel = tuple.SemanticModel,
                        Symbol = tuple.SemanticModel.GetSymbolInfo(tuple.SyntaxNode).Symbol as IEventSymbol
                    })
                .Where(tuple => tuple.Symbol != null)
                .Select(tuple => tuple.Symbol.OriginalDefinition);

            return new HashSet<ISymbol>(invokedEventSymbols);
        }

        private static ISet<ISymbol> GetPossiblyCopiedSymbols(RemovableDeclarationCollector removableDeclarationCollector)
        {
            var usedSymbols = new HashSet<ISymbol>();

            var arguments = removableDeclarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .Where(node =>
                        node.IsKind(SyntaxKind.Argument))
                    .Cast<ArgumentSyntax>()
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<SyntaxNode>
                        {
                            SyntaxNode = node.Expression,
                            SemanticModel = container.SemanticModel
                        }));

            var equalsValue = removableDeclarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .OfType<EqualsValueClauseSyntax>()
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<SyntaxNode>
                        {
                            SyntaxNode = node.Value,
                            SemanticModel = container.SemanticModel
                        }));

            var assignment = removableDeclarationCollector.TypeDeclarations
                .SelectMany(container => container.SyntaxNode.DescendantNodes()
                    .Where(node =>
                        node.IsKind(SyntaxKind.SimpleAssignmentExpression))
                    .Cast<AssignmentExpressionSyntax>()
                    .Select(node =>
                        new SyntaxNodeSemanticModelTuple<SyntaxNode>
                        {
                            SyntaxNode = node.Right,
                            SemanticModel = container.SemanticModel
                        }));

            var allNodes = arguments
                .Concat(equalsValue)
                .Concat(assignment);

            foreach (var node in allNodes)
            {
                var symbol = node.SemanticModel.GetSymbolInfo(node.SyntaxNode).Symbol as IEventSymbol;

                if (symbol != null)
                {
                    usedSymbols.Add(symbol.OriginalDefinition);
                }
            }

            return usedSymbols;
        }

        private static ExpressionSyntax GetEventExpressionFromInvocation(InvocationExpressionSyntax invocation, IMethodSymbol symbol)
        {
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            var memberBinding = invocation.Expression as MemberBindingExpressionSyntax;

            var expression = memberAccess?.Expression;
            var invokedMethodName = memberAccess?.Name;
            if (memberBinding != null)
            {
                expression = (invocation.Parent as ConditionalAccessExpressionSyntax)?.Expression;
                invokedMethodName = memberBinding.Name;
            }

            if ((memberAccess != null || memberBinding  != null) &&
                expression != null &&
                IsExplicitDelegateInvocation(symbol, invokedMethodName))
            {
                return expression;
            }

            return invocation.Expression;
        }

        private static bool IsExplicitDelegateInvocation(IMethodSymbol symbol, SimpleNameSyntax invokedMethodName)
        {
            var isDynamicInvocation = symbol.MethodKind == MethodKind.Ordinary &&
                symbol.Name == "DynamicInvoke" &&
                symbol.ReceiverType.OriginalDefinition.Is(KnownType.System_Delegate);

            if (isDynamicInvocation)
            {
                return true;
            }

            return symbol.MethodKind == MethodKind.DelegateInvoke && invokedMethodName.Identifier.ValueText == "Invoke";
        }
    }
}
