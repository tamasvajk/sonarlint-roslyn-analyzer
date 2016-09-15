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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using Microsoft.CodeAnalysis.CSharp;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, Severity.Minor, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class MemberOverrideCallsBaseMember : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1185";
        internal const string Title = "Overriding members should do more than simply call the same member in the super class";
        internal const string Description =
            "Overriding a member just to call the same member from the base class without performing any other actions is useless " +
            "and misleading. The only time this is justified is in \"sealed\" overriding methods, where the effect is to lock in the " +
            "parent class behavior.";
        internal const string MessageFormat = "Remove this {1} \"{0}\" to simply inherit its behavior.";
        internal const string Category = SonarAnalyzer.Common.Category.Maintainability;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                DiagnosticSeverity.Info, IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: IdeVisibility.Hidden.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly string[] IgnoredMethodNames = { "Equals", "GetHashCode" };

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var method = (MethodDeclarationSyntax)c.Node;
                    if (IsMethodCandidate(method, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, method.GetLocation(), method.Identifier.ValueText, "method"));
                    }
                },
                SyntaxKind.MethodDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var property = (PropertyDeclarationSyntax)c.Node;
                    if (IsPropertyCandidate(property, c.SemanticModel))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, property.GetLocation(), property.Identifier.ValueText, "property"));
                    }
                },
                SyntaxKind.PropertyDeclaration);
        }

        private static bool IsPropertyCandidate(PropertyDeclarationSyntax propertySyntax, SemanticModel semanticModel)
        {
            if (HasDocumentationComment(propertySyntax))
            {
                return false;
            }

            var propertySymbol = semanticModel.GetDeclaredSymbol(propertySyntax);

            if (propertySymbol == null ||
                !propertySymbol.IsOverride ||
                propertySymbol.IsSealed ||
                propertySymbol.OverriddenProperty == null)
            {
                return false;
            }

            if (propertySymbol.GetMethod != null && propertySymbol.OverriddenProperty.GetMethod == null)
            {
                return false;
            }

            if (propertySymbol.SetMethod != null && propertySymbol.OverriddenProperty.SetMethod == null)
            {
                return false;
            }

            return CheckGetAccessorIfAny(propertySyntax, propertySymbol, semanticModel) &&
                CheckSetAccessorIfAny(propertySyntax, propertySymbol, semanticModel);
        }

        private static bool CheckGetAccessorIfAny(PropertyDeclarationSyntax propertySyntax, IPropertySymbol propertySymbol,
            SemanticModel semanticModel)
        {
            var getAccessor = propertySyntax.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));

            if (getAccessor == null &&
                propertySyntax.ExpressionBody == null)
            {
                // no getter
                return true;
            }

            var memberAccess = GetExpressionToCheck(getAccessor?.Body, propertySyntax.ExpressionBody, true)
                as MemberAccessExpressionSyntax;

            if (memberAccess == null ||
                !(memberAccess.Expression is BaseExpressionSyntax))
            {
                return false;
            }

            return IsBaseProperty(propertySymbol, semanticModel, memberAccess);
        }

        private static bool IsBaseProperty(IPropertySymbol propertySymbol, SemanticModel semanticModel,
            MemberAccessExpressionSyntax memberAccess)
        {
            var invokedPropertySymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol as IPropertySymbol;
            return invokedPropertySymbol != null &&
                invokedPropertySymbol.Equals(propertySymbol.OverriddenProperty);
        }

        private static bool CheckSetAccessorIfAny(PropertyDeclarationSyntax propertySyntax, IPropertySymbol propertySymbol,
            SemanticModel semanticModel)
        {
            var setAccessor = propertySyntax.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
            if (setAccessor == null)
            {
                return true;
            }

            var expressionToCheck = GetExpressionToCheck(setAccessor.Body, null, false) as AssignmentExpressionSyntax;
            if (expressionToCheck == null ||
                !expressionToCheck.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                return false;
            }

            //check right:
            var valueParameter = semanticModel.GetSymbolInfo(expressionToCheck.Right).Symbol as IParameterSymbol;
            if (valueParameter == null || valueParameter.Name != "value" || !valueParameter.IsImplicitlyDeclared)
            {
                return false;
            }

            //check left:
            var memberAccess = expressionToCheck.Left as MemberAccessExpressionSyntax;

            if (memberAccess == null ||
                !(memberAccess.Expression is BaseExpressionSyntax))
            {
                return false;
            }

            return IsBaseProperty(propertySymbol, semanticModel, memberAccess);
        }

        private static bool IsMethodCandidate(MethodDeclarationSyntax methodSyntax, SemanticModel semanticModel)
        {
            if (HasDocumentationComment(methodSyntax))
            {
                return false;
            }

            var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);

            if (methodSymbol == null ||
                !methodSymbol.IsOverride ||
                methodSymbol.IsSealed ||
                methodSymbol.OverriddenMethod == null ||
                IgnoredMethodNames.Contains(methodSymbol.Name) ||
                methodSymbol.Parameters.Any(p => p.HasExplicitDefaultValue) ||
                methodSymbol.OverriddenMethod.Parameters.Any(p => p.HasExplicitDefaultValue))
            {
                return false;
            }

            var expressionToCheck = GetExpressionToCheck(methodSyntax.Body, methodSyntax.ExpressionBody, !methodSymbol.ReturnsVoid)
                as InvocationExpressionSyntax;

            var memberAccess = expressionToCheck?.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null ||
                !(memberAccess.Expression is BaseExpressionSyntax))
            {
                return false;
            }

            var invokedMethod = semanticModel.GetSymbolInfo(expressionToCheck).Symbol as IMethodSymbol;
            if (invokedMethod == null ||
                !invokedMethod.Equals(methodSymbol.OverriddenMethod))
            {
                return false;
            }

            return AreArgumentsMatchParameters(methodSymbol, semanticModel, expressionToCheck, invokedMethod);
        }

        private static bool HasDocumentationComment(SyntaxNode node)
        {
            return node.GetLeadingTrivia()
                .Any(t =>
                    t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                    t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        }

        private static bool AreArgumentsMatchParameters(IMethodSymbol methodSymbol, SemanticModel semanticModel,
            InvocationExpressionSyntax expressionToCheck, IMethodSymbol invokedMethod)
        {
            if (!invokedMethod.Parameters.Any())
            {
                return true;
            }

            if (expressionToCheck.ArgumentList == null ||
                invokedMethod.Parameters.Length != expressionToCheck.ArgumentList.Arguments.Count)
            {
                return false;
            }

            var argumentExpressions = expressionToCheck.ArgumentList.Arguments
                .Select(a => a.Expression as IdentifierNameSyntax)
                .ToList();

            if (argumentExpressions.Any(identifier => identifier == null))
            {
                return false;
            }

            for (int i = 0; i < argumentExpressions.Count; i++)
            {
                var parameterSymbol = semanticModel.GetSymbolInfo(argumentExpressions[i]).Symbol as IParameterSymbol;
                if (parameterSymbol == null ||
                    !parameterSymbol.Equals(methodSymbol.Parameters[i]) ||
                    parameterSymbol.Name != methodSymbol.OverriddenMethod.Parameters[i].Name)
                {
                    return false;
                }
            }

            return true;
        }

        private static ExpressionSyntax GetExpressionToCheck(BlockSyntax blockSyntax, ArrowExpressionClauseSyntax expressionBodySyntax,
            bool hasReturnValue)
        {
            return hasReturnValue
                ? GetExpressionFromBodyOptions(blockSyntax, expressionBodySyntax)
                : (GetSingleStatement(blockSyntax) as ExpressionStatementSyntax)?.Expression;
        }

        private static ExpressionSyntax GetExpressionFromBodyOptions(BlockSyntax blockSyntax, ArrowExpressionClauseSyntax expressionBodySyntax)
        {
            return blockSyntax == null
                ? expressionBodySyntax?.Expression
                : (GetSingleStatement(blockSyntax) as ReturnStatementSyntax)?.Expression;
        }

        private static StatementSyntax GetSingleStatement(BlockSyntax block)
        {
            if (block == null ||
                block.Statements.Count != 1)
            {
                return null;
            }

            return block.Statements.First();
        }
    }
}
