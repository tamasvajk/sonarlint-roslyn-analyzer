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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System;
using System.Collections.Generic;
using SonarAnalyzer.Helpers.FlowAnalysis.CSharp;
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using SonarAnalyzer.Helpers.FlowAnalysis;

namespace SonarAnalyzer.Rules.CSharp
{
    using SymbolWithInitializer = KeyValuePair<ISymbol, EqualsValueClauseSyntax>;
    using CtorDeclarationTuple = SyntaxNodeSymbolSemanticModelTuple<ConstructorDeclarationSyntax, IMethodSymbol>;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("1min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, Severity.Minor, Title, false)]
    [Tags(Tag.Finding)]
    public class MemberInitializerRedundant : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3604";
        internal const string Title = "Member initializer values should not be redundant";
        internal const string Description =
            "Fields, properties and events can be initialized either inline or in the constructor. Initializing them inline and in the constructor at the " +
            "same time is redundant; the inline initialization will be overridden.";
        internal const string MessageFormat = "Remove the member initializer, all constructors set an initial value for the member.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                DiagnosticSeverity.Info, true,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: IdeVisibility.Hidden.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (TypeDeclarationSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(declaration);

                    var constructorSymbols = symbol?.GetMembers()
                        .OfType<IMethodSymbol>()
                        .Where(IsExplicitlyDefinedConstructor)
                        .ToList();

                    if (constructorSymbols == null ||
                        !constructorSymbols.Any())
                    {
                        return;
                    }

                    var ctorDeclarations = GetConstructorTuples(c, constructorSymbols);

                    var candidateFields = GetInitializedFieldLikeDeclarations<FieldDeclarationSyntax, IFieldSymbol>(declaration, c.SemanticModel, f => f.Type);
                    var candidateEvents = GetInitializedFieldLikeDeclarations<EventFieldDeclarationSyntax, IEventSymbol>(declaration, c.SemanticModel, f => f.Type);
                    var candidateProperties = GetInitializedPropertyDeclarations(declaration, c.SemanticModel);

                    var symbolInitializerPairs = candidateFields.Select(t => new SymbolWithInitializer(t.Symbol, t.Initializer))
                        .Concat(candidateEvents.Select(t => new SymbolWithInitializer(t.Symbol, t.Initializer)))
                        .Concat(candidateProperties.Select(t => new SymbolWithInitializer(t.Symbol, t.Initializer)))
                        .ToDictionary(t => t.Key, t => t.Value);

                    if (!symbolInitializerPairs.Any())
                    {
                        return;
                    }

                    foreach (var declaredSymbol in symbolInitializerPairs.Keys)
                    {
                        var setInAllCtors = ctorDeclarations
                            .All(ctor => IsSymbolFirstSetInCtor(declaredSymbol, ctor));

                        if (setInAllCtors)
                        {
                            c.ReportDiagnostic(Diagnostic.Create(Rule, symbolInitializerPairs[declaredSymbol].GetLocation()));
                        }
                    }
                },
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration);
        }

        private static List<CtorDeclarationTuple> GetConstructorTuples(
            SyntaxNodeAnalysisContext context, List<IMethodSymbol> constructorSymbols)
        {
            return constructorSymbols
                .Select(ctor => new SyntaxNodeSymbolSemanticModelTuple<ConstructorDeclarationSyntax, IMethodSymbol>
                {
                    SyntaxNode = ctor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as ConstructorDeclarationSyntax,
                    Symbol = ctor
                })
                .Where(ctor => ctor.SyntaxNode != null)
                .Select(ctor => new SyntaxNodeSymbolSemanticModelTuple<ConstructorDeclarationSyntax, IMethodSymbol>
                {
                    SyntaxNode = ctor.SyntaxNode,
                    Symbol = ctor.Symbol,
                    SemanticModel = context.SemanticModel.Compilation.GetSemanticModel(ctor.SyntaxNode.SyntaxTree)
                })
                .Where(ctor => ctor.SemanticModel != null)
                .ToList();
        }

        private static bool IsExplicitlyDefinedConstructor(ISymbol member)
        {
            var method = member as IMethodSymbol;
            return method != null &&
                method.MethodKind == MethodKind.Constructor &&
                !method.IsImplicitlyDeclared;
        }

        private static bool IsSymbolFirstSetInCtor(ISymbol declaredSymbol, CtorDeclarationTuple ctor)
        {
            if (ctor.SyntaxNode.Initializer != null &&
                ctor.SyntaxNode.Initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword))
            {
                // Calls another ctor, which is also checked.
                return true;
            }

            IControlFlowGraph cfg;
            if (!ControlFlowGraph.TryGet(ctor.SyntaxNode.Body, ctor.SemanticModel, out cfg))
            {
                return false;
            }

            var checker = new MemberInitializerRedundancyChecker(cfg, declaredSymbol, ctor.SemanticModel);
            return checker.CheckAllPaths();
        }

        private static IEnumerable<DeclarationTuple<IPropertySymbol>> GetInitializedPropertyDeclarations(TypeDeclarationSyntax declaration,
            SemanticModel semanticModel)
        {
            return declaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => !p.Modifiers.Any(IsStaticOrConst) &&
                    p.Initializer != null &&
                    MemberInitializedToDefault.IsAutoProperty(p))
                .Select(p =>
                    new DeclarationTuple<IPropertySymbol>
                    {
                        Initializer = p.Initializer,
                        SemanticModel = semanticModel,
                        Symbol = semanticModel.GetDeclaredSymbol(p)
                    })
                .Where(t =>
                    t.Symbol != null &&
                    !MemberInitializedToDefault.IsDefaultValueInitializer(t.Initializer, t.Symbol.Type));
        }

        private static IEnumerable<DeclarationTuple<TSymbol>> GetInitializedFieldLikeDeclarations<TDeclarationType, TSymbol>(TypeDeclarationSyntax declaration,
            SemanticModel semanticModel, Func<TSymbol, ITypeSymbol> typeSelector)
            where TDeclarationType : BaseFieldDeclarationSyntax
            where TSymbol : class, ISymbol
        {
            return declaration.Members
                .OfType<TDeclarationType>()
                .Where(fd => !fd.Modifiers.Any(IsStaticOrConst))
                .SelectMany(fd => fd.Declaration.Variables
                    .Where(v => v.Initializer != null)
                    .Select(v =>
                        new DeclarationTuple<TSymbol>
                        {
                            Initializer = v.Initializer,
                            SemanticModel = semanticModel,
                            Symbol = semanticModel.GetDeclaredSymbol(v) as TSymbol
                        }))
                .Where(t =>
                    t.Symbol != null &&
                    !MemberInitializedToDefault.IsDefaultValueInitializer(t.Initializer, typeSelector(t.Symbol)));
        }

        private static bool IsStaticOrConst(SyntaxToken token)
        {
            return token.IsKind(SyntaxKind.StaticKeyword) || token.IsKind(SyntaxKind.ConstKeyword);
        }

        private class DeclarationTuple<TSymbol>
            where TSymbol : ISymbol
        {
            public EqualsValueClauseSyntax Initializer { get; set; }
            public SemanticModel SemanticModel { get; set; }
            public TSymbol Symbol { get; set; }
        }

        private class MemberInitializerRedundancyChecker : CfgAllPathValidator
        {
            private readonly ISymbol memberToCheck;
            private readonly SemanticModel semanticModel;

            public MemberInitializerRedundancyChecker(IControlFlowGraph cfg, ISymbol memberToCheck, SemanticModel semanticModel)
                : base(cfg)
            {
                this.memberToCheck = memberToCheck;
                this.semanticModel = semanticModel;
            }

            protected override bool IsBlockValid(Block block)
            {
                // Contains assignment before access

                foreach (var instruction in block.Instructions)
                {
                    switch (instruction.Kind())
                    {
                        case SyntaxKind.IdentifierName:
                        case SyntaxKind.SimpleMemberAccessExpression:
                            {
                                var memberAccess = GetPossibleMemberAccessParent(instruction);

                                bool isRead;
                                if (memberAccess != null &&
                                    TryGetReadWriteFromMemberAccess(memberAccess, out isRead))
                                {
                                    return !isRead;
                                }
                            }
                            break;
                        case SyntaxKind.SimpleAssignmentExpression:
                            {
                                var assignment = (AssignmentExpressionSyntax)instruction;
                                if (IsMatchingMember(assignment.Left))
                                {
                                    return true;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }

                return false;
            }

            protected override bool IsBlockInvalid(Block block)
            {
                // Contains access before assignment

                foreach (var instruction in block.Instructions)
                {
                    switch (instruction.Kind())
                    {
                        case SyntaxKind.IdentifierName:
                        case SyntaxKind.SimpleMemberAccessExpression:
                            {
                                var memberAccess = GetPossibleMemberAccessParent(instruction);

                                bool isRead;
                                if (memberAccess != null &&
                                    TryGetReadWriteFromMemberAccess(memberAccess, out isRead))
                                {
                                    return isRead;
                                }
                            }
                            break;
                        case SyntaxKind.SimpleAssignmentExpression:
                            {
                                var assignment = (AssignmentExpressionSyntax)instruction;
                                if (IsMatchingMember(assignment.Left))
                                {
                                    return false;
                                }
                            }
                            break;

                        case SyntaxKind.AnonymousMethodExpression:
                        case SyntaxKind.ParenthesizedLambdaExpression:
                        case SyntaxKind.SimpleLambdaExpression:
                        case SyntaxKind.QueryExpression:
                            {
                                if (IsMemberUsedInsideLambda(instruction))
                                {
                                    return true;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }

                return false;
            }

            private bool TryGetReadWriteFromMemberAccess(ExpressionSyntax expression, out bool isRead)
            {
                isRead = false;

                var parenthesized = expression.GetSelfOrTopParenthesizedExpression();

                if (!IsMatchingMember(expression))
                {
                    return false;
                }

                if (IsOutArgument(parenthesized))
                {
                    isRead = false;
                    return true;
                }

                if (IsReadAccess(parenthesized, semanticModel))
                {
                    isRead = true;
                    return true;
                }

                return false;
            }

            private static bool IsOutArgument(ExpressionSyntax parenthesized)
            {
                var argument = parenthesized.Parent as ArgumentSyntax;
                return argument != null && argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword);
            }

            private static bool IsReadAccess(ExpressionSyntax parenthesized, SemanticModel semanticModel)
            {
                return !IsBeingAssigned(parenthesized) &&
                    !parenthesized.IsInNameofCall(semanticModel);
            }

            private bool IsMemberUsedInsideLambda(SyntaxNode instruction)
            {
                return instruction.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Select(i => GetPossibleMemberAccessParent(i))
                    .Any(i => IsMatchingMember(i));
            }

            private static ExpressionSyntax GetPossibleMemberAccessParent(SyntaxNode node)
            {
                var memberAccess = node as MemberAccessExpressionSyntax;
                if (memberAccess != null)
                {
                    return memberAccess;
                }

                var identifier = node as IdentifierNameSyntax;
                if (identifier != null)
                {
                    return GetPossibleMemberAccessParent(identifier);
                }

                return null;
            }

            private static ExpressionSyntax GetPossibleMemberAccessParent(IdentifierNameSyntax identifier)
            {
                var memberAccess = identifier.Parent as MemberAccessExpressionSyntax;
                if (memberAccess != null)
                {
                    return memberAccess;
                }

                var memberBinding = identifier.Parent as MemberBindingExpressionSyntax;
                if (memberBinding != null)
                {
                    return (ExpressionSyntax)memberBinding.Parent;
                }

                return identifier;
            }

            private static bool IsBeingAssigned(ExpressionSyntax expression)
            {
                var assignment = expression.Parent as AssignmentExpressionSyntax;

                return assignment != null &&
                    assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                    assignment.Left == expression;
            }

            private bool IsMatchingMember(ExpressionSyntax expression)
            {
                IdentifierNameSyntax identifier = null;

                if (expression.IsKind(SyntaxKind.IdentifierName))
                {
                    identifier = (IdentifierNameSyntax)expression;
                }

                var memberAccess = expression as MemberAccessExpressionSyntax;
                if (memberAccess != null &&
                    memberAccess.Expression.IsKind(SyntaxKind.ThisExpression))
                {
                    identifier = memberAccess.Name as IdentifierNameSyntax;
                }

                var conditionalAccess = expression as ConditionalAccessExpressionSyntax;
                if (conditionalAccess != null &&
                    conditionalAccess.Expression.IsKind(SyntaxKind.ThisExpression))
                {
                    identifier = (conditionalAccess.WhenNotNull as MemberBindingExpressionSyntax)?.Name as IdentifierNameSyntax;
                }

                if (identifier == null)
                {
                    return false;
                }

                var assignedSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;

                return memberToCheck.Equals(assignedSymbol);
            }
        }
    }
}
