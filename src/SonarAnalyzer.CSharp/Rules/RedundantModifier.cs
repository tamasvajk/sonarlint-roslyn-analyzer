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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [SqaleConstantRemediation("2min")]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    [Tags(Tag.Unused, Tag.Finding)]
    public class RedundantModifier : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2333";
        internal const string Title = "Redundant modifiers should be removed";
        internal const string Description =
            "Unnecessary keywords simply clutter the code and should be removed.";
        internal const string MessageFormat = "\"{0}\" is {1} in this context.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), true,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckSealedMemberInSealedClass(c),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.PropertyDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckTypeDeclarationForRedundantPartial(c),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.StructDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckForUnnecessaryUnsafeBlocks(c),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.InterfaceDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    if (CheckedWalker.IsTopLevel(c.Node))
                    {
                        new CheckedWalker(c).Visit(c.Node);
                    }
                },
                SyntaxKind.CheckedStatement,
                SyntaxKind.UncheckedStatement,
                SyntaxKind.CheckedExpression,
                SyntaxKind.UncheckedExpression);
        }

        private static void CheckForUnnecessaryUnsafeBlocks(SyntaxNodeAnalysisContext context)
        {
            var typeDeclaration = (TypeDeclarationSyntax)context.Node;
            if (typeDeclaration.Parent is TypeDeclarationSyntax)
            {
                // only process top level type declarations
                return;
            }

            CheckForUnnecessaryUnsafeBlocksBelow(typeDeclaration, context);
        }

        private static void CheckForUnnecessaryUnsafeBlocksBelow(TypeDeclarationSyntax typeDeclaration, SyntaxNodeAnalysisContext context)
        {
            SyntaxToken unsafeKeyword;
            if (TryGetUnsafeKeyword(typeDeclaration, out unsafeKeyword))
            {
                MarkAllUnsafeBlockInside(typeDeclaration, context);
                if (!HasUnsafeConstructInside(typeDeclaration, context.SemanticModel))
                {
                    ReportOnUnsafeBlock(context, unsafeKeyword.GetLocation());
                }
                return;
            }

            foreach (var member in typeDeclaration.Members)
            {
                if (TryGetUnsafeKeyword(member, out unsafeKeyword))
                {
                    MarkAllUnsafeBlockInside(member, context);
                    if (!HasUnsafeConstructInside(member, context.SemanticModel))
                    {
                        ReportOnUnsafeBlock(context, unsafeKeyword.GetLocation());
                    }
                    continue;
                }

                var nestedTypeDeclaration = member as TypeDeclarationSyntax;
                if (nestedTypeDeclaration != null)
                {
                    CheckForUnnecessaryUnsafeBlocksBelow(nestedTypeDeclaration, context);
                    continue;
                }

                var topLevelUnsafeBlocks = member.DescendantNodes(n => !n.IsKind(SyntaxKind.UnsafeStatement)).OfType<UnsafeStatementSyntax>();
                foreach (var topLevelUnsafeBlock in topLevelUnsafeBlocks)
                {
                    MarkAllUnsafeBlockInside(topLevelUnsafeBlock, context);
                    if (!HasUnsafeConstructInside(member, context.SemanticModel))
                    {
                        ReportOnUnsafeBlock(context, topLevelUnsafeBlock.UnsafeKeyword.GetLocation());
                    }
                }
            }
        }

        private static bool HasUnsafeConstructInside(SyntaxNode container, SemanticModel semanticModel)
        {
            return ContainsUnsafeConstruct(container) ||
                ContainsFixedDeclaration(container) ||
                ContainsUnsafeTypedIdentifier(container, semanticModel) ||
                ContainsUnsafeInvocationReturnValue(container, semanticModel) ||
                ContainsUnsafeParameter(container, semanticModel);
        }

        private static bool ContainsUnsafeParameter(SyntaxNode container, SemanticModel semanticModel)
        {
            return container.DescendantNodes()
                .OfType<ParameterSyntax>()
                .Select(p => semanticModel.GetDeclaredSymbol(p))
                .Any(p => IsUnsafe(p?.Type));
        }

        private static bool ContainsUnsafeInvocationReturnValue(SyntaxNode container, SemanticModel semanticModel)
        {
            return container.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Select(i => semanticModel.GetSymbolInfo(i).Symbol as IMethodSymbol)
                .Any(m => IsUnsafe(m?.ReturnType));
        }

        private static bool ContainsUnsafeTypedIdentifier(SyntaxNode container, SemanticModel semanticModel)
        {
            return container.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(i => semanticModel.GetTypeInfo(i).Type)
                .Any(t => IsUnsafe(t));
        }

        private static bool ContainsFixedDeclaration(SyntaxNode container)
        {
            return container.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Any(fd => fd.Modifiers.Any(m => m.IsKind(SyntaxKind.FixedKeyword)));
        }

        private static bool ContainsUnsafeConstruct(SyntaxNode container)
        {
            return container.DescendantNodes().Any(node => UnsafeConstructKinds.Contains(node.Kind()));
        }

        private static bool IsUnsafe(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.TypeKind == TypeKind.Pointer)
            {
                return true;
            }

            return type.TypeKind == TypeKind.Array &&
                IsUnsafe(((IArrayTypeSymbol)type).ElementType);
        }

        private static readonly ISet<SyntaxKind> UnsafeConstructKinds = new HashSet<SyntaxKind>(new[]
        {
            SyntaxKind.AddressOfExpression,
            SyntaxKind.PointerIndirectionExpression,
            SyntaxKind.SizeOfExpression,
            SyntaxKind.PointerType,
            SyntaxKind.FixedStatement,
            SyntaxKind.StackAllocArrayCreationExpression
        });

        private static void MarkAllUnsafeBlockInside(SyntaxNode container, SyntaxNodeAnalysisContext context)
        {
            foreach (var @unsafe in container.DescendantNodes()
                .SelectMany(node => node.ChildTokens())
                .Where(token => token.IsKind(SyntaxKind.UnsafeKeyword)))
            {
                ReportOnUnsafeBlock(context, @unsafe.GetLocation());
            }
        }

        private static void ReportOnUnsafeBlock(SyntaxNodeAnalysisContext context, Location issueLocation)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, issueLocation, "unsafe", "redundant"));
        }

        private static bool TryGetUnsafeKeyword(MemberDeclarationSyntax memberDeclaration, out SyntaxToken unsafeKeyword)
        {
            var delegateDeclaration = memberDeclaration as DelegateDeclarationSyntax;
            if (delegateDeclaration != null)
            {
                unsafeKeyword = delegateDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.UnsafeKeyword));
                return unsafeKeyword != default(SyntaxToken);
            }

            var propertyDeclaration = memberDeclaration as BasePropertyDeclarationSyntax;
            if (propertyDeclaration != null)
            {
                unsafeKeyword = propertyDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.UnsafeKeyword));
                return unsafeKeyword != default(SyntaxToken);
            }

            var methodDeclaration = memberDeclaration as BaseMethodDeclarationSyntax;
            if (methodDeclaration != null)
            {
                unsafeKeyword = methodDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.UnsafeKeyword));
                return unsafeKeyword != default(SyntaxToken);
            }

            var fieldDeclaration = memberDeclaration as BaseFieldDeclarationSyntax;
            if (fieldDeclaration != null)
            {
                unsafeKeyword = fieldDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.UnsafeKeyword));
                return unsafeKeyword != default(SyntaxToken);
            }

            var typeDeclaration = memberDeclaration as BaseTypeDeclarationSyntax;
            if (typeDeclaration != null)
            {
                unsafeKeyword = typeDeclaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.UnsafeKeyword));
                return unsafeKeyword != default(SyntaxToken);
            }

            unsafeKeyword = default(SyntaxToken);
            return false;
        }

        private static void CheckTypeDeclarationForRedundantPartial(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (TypeDeclarationSyntax)context.Node;
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

            if (classSymbol == null ||
                !classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) ||
                classSymbol.DeclaringSyntaxReferences.Count() > 1)
            {
                return;
            }

            var keyword = classDeclaration.Modifiers.First(m => m.IsKind(SyntaxKind.PartialKeyword));
            context.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation(), "partial", "gratuitous"));
        }

        private static SyntaxTokenList GetModifiers(MemberDeclarationSyntax memberDeclaration)
        {
            var method = memberDeclaration as MethodDeclarationSyntax;
            if (method != null)
            {
                return method.Modifiers;
            }

            var property = memberDeclaration as PropertyDeclarationSyntax;
            return property?.Modifiers ?? default(SyntaxTokenList);
        }

        private static void CheckSealedMemberInSealedClass(SyntaxNodeAnalysisContext context)
        {
            var memberDeclaration = (MemberDeclarationSyntax)context.Node;
            var memberSymbol = context.SemanticModel.GetDeclaredSymbol(memberDeclaration);
            if (memberSymbol == null)
            {
                return;
            }

            if (!memberSymbol.IsSealed ||
                !memberSymbol.ContainingType.IsSealed)
            {
                return;
            }

            var modifiers = GetModifiers(memberDeclaration);
            if (modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword)))
            {
                var keyword = modifiers.First(m => m.IsKind(SyntaxKind.SealedKeyword));
                context.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation(), "sealed", "redundant"));
            }
        }


        private class CheckedWalker : CSharpSyntaxWalker
        {
            private readonly SyntaxNodeAnalysisContext context;

            private bool isCurrentContextChecked;
            private bool currentContextHasIntegralOperation = false;

            public CheckedWalker(SyntaxNodeAnalysisContext context)
            {
                this.context = context;

                var statement = context.Node as CheckedStatementSyntax;
                if (statement != null)
                {
                    isCurrentContextChecked = statement.IsKind(SyntaxKind.CheckedStatement);
                    return;
                }

                var expression = context.Node as CheckedExpressionSyntax;
                if (expression != null)
                {
                    isCurrentContextChecked = expression.IsKind(SyntaxKind.CheckedExpression);
                    return;
                }

                throw new NotSupportedException("Only checked expressions and statements are supported");
            }

            public override void VisitCheckedExpression(CheckedExpressionSyntax node)
            {
                VisitChecked(node, SyntaxKind.CheckedExpression, node.Keyword, base.VisitCheckedExpression);
            }

            public override void VisitCheckedStatement(CheckedStatementSyntax node)
            {
                VisitChecked(node, SyntaxKind.CheckedStatement, node.Keyword, base.VisitCheckedStatement);
            }

            public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
            {
                base.VisitAssignmentExpression(node);

                if (AssignmentsForChecked.Contains(node.Kind()))
                {
                    SetHasIntegralOperation(node);
                }
            }

            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                base.VisitBinaryExpression(node);

                if (BinaryOperationsForChecked.Contains(node.Kind()))
                {
                    SetHasIntegralOperation(node);
                }
            }

            public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
            {
                base.VisitPrefixUnaryExpression(node);

                if (UnaryOperationsForChecked.Contains(node.Kind()))
                {
                    SetHasIntegralOperation(node);
                }
            }

            public override void VisitCastExpression(CastExpressionSyntax node)
            {
                base.VisitCastExpression(node);

                SetHasIntegralOperation(node);
            }

            private void VisitChecked<T>(T node, SyntaxKind checkedKind, SyntaxToken tokenToReport, Action<T> baseCall)
                where T: SyntaxNode
            {
                var isThisNodeChecked = node.IsKind(checkedKind);

                var originalIsCurrentContextChecked = isCurrentContextChecked;
                var originalContextHasIntegralOperation = currentContextHasIntegralOperation;

                isCurrentContextChecked = isThisNodeChecked;
                currentContextHasIntegralOperation = false;

                baseCall(node);

                var isSimplyRendundant = IsCurrentNodeEmbeddedInsideSameChecked(node, isThisNodeChecked, originalIsCurrentContextChecked);

                if (isSimplyRendundant || !currentContextHasIntegralOperation)
                {
                    var keywordToReport = isThisNodeChecked ? "checked" : "unchecked";
                    context.ReportDiagnostic(Diagnostic.Create(Rule, tokenToReport.GetLocation(), keywordToReport, "redundant"));
                }

                isCurrentContextChecked = originalIsCurrentContextChecked;
                currentContextHasIntegralOperation = originalContextHasIntegralOperation ||
                    currentContextHasIntegralOperation && isSimplyRendundant;
            }

            private bool IsCurrentNodeEmbeddedInsideSameChecked(SyntaxNode node, bool isThisNodeChecked, bool isCurrentContextChecked)
            {
                return node != context.Node &&
                    isThisNodeChecked == isCurrentContextChecked;
            }

            private void SetHasIntegralOperation(CastExpressionSyntax node)
            {
                var expressionType = context.SemanticModel.GetTypeInfo(node.Expression).Type;
                var castedToType = context.SemanticModel.GetTypeInfo(node.Type).Type;
                currentContextHasIntegralOperation |= castedToType != null && expressionType != null && castedToType.IsAny(KnownType.IntegralNumbers);
            }

            private void SetHasIntegralOperation(ExpressionSyntax node)
            {
                var methodSymbol = context.SemanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
                currentContextHasIntegralOperation |= methodSymbol != null && methodSymbol.ReceiverType.IsAny(KnownType.IntegralNumbers);
            }

            private static readonly ISet<SyntaxKind> BinaryOperationsForChecked = ImmutableHashSet.Create(
                SyntaxKind.AddExpression,
                SyntaxKind.SubtractExpression,
                SyntaxKind.MultiplyExpression,
                SyntaxKind.DivideExpression);

            private static readonly ISet<SyntaxKind> AssignmentsForChecked = ImmutableHashSet.Create(
                SyntaxKind.AddAssignmentExpression,
                SyntaxKind.SubtractAssignmentExpression,
                SyntaxKind.MultiplyAssignmentExpression,
                SyntaxKind.DivideAssignmentExpression);

            private static readonly ISet<SyntaxKind> UnaryOperationsForChecked = ImmutableHashSet.Create(
                SyntaxKind.UnaryMinusExpression,
                SyntaxKind.PostDecrementExpression,
                SyntaxKind.PostIncrementExpression,
                SyntaxKind.PreDecrementExpression,
                SyntaxKind.PreIncrementExpression);

            public static bool IsTopLevel(SyntaxNode node)
            {
                return !node.Ancestors().Any(a =>
                    a is CheckedStatementSyntax ||
                    a is CheckedExpressionSyntax);
            }
        }
    }
}
