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
using Microsoft.CodeAnalysis.Text;
using System;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, false)]
    [Tags(Tag.Clumsy, Tag.Finding)]
    public class RedundantToStringCall : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1858";
        internal const string Title = "\"ToString()\" calls should not be redundant";
        internal const string Description =
            "Invoking a method designed to return a string representation of an object which is already a string is a waste of " +
            "keystrokes. Similarly, explicitly invoking \"ToString()\" when the compiler would do it implicitly is also needless " +
            "code-bloat.";
        internal const string MessageFormat = "There's no need to call \"ToString()\"{0}.";
        internal const string MessageCallOnString = " on a string";
        internal const string MessageCompiler = ", the compiler will do it for you";
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

        private const string additionOperatorName = "op_Addition";

        protected override void Initialize(SonarAnalysisContext context)
        {
            CheckToStringInvocationsOnStringAndInStringFormat(context);
            CheckSidesOfAddExpressionsForToStringCall(context);
            CheckRightSideOfAddAssignmentsForToStringCall(context);
        }

        private static void CheckRightSideOfAddAssignmentsForToStringCall(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var assignment = (AssignmentExpressionSyntax)c.Node;
                    var operation = c.SemanticModel.GetSymbolInfo(assignment).Symbol as IMethodSymbol;
                    if (!IsOperationAddOnString(operation))
                    {
                        return;
                    }

                    CheckRightExpressionForRemovableToStringCall(c, assignment);
                },
                SyntaxKind.AddAssignmentExpression);
        }

        private static void CheckSidesOfAddExpressionsForToStringCall(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var binary = (BinaryExpressionSyntax)c.Node;
                    var operation = c.SemanticModel.GetSymbolInfo(binary).Symbol as IMethodSymbol;
                    if (!IsOperationAddOnString(operation))
                    {
                        return;
                    }

                    CheckLeftExpressionForRemovableToStringCall(c, binary);
                    CheckRightExpressionForRemovableToStringCall(c, binary);
                },
                SyntaxKind.AddExpression);
        }

        private static void CheckToStringInvocationsOnStringAndInStringFormat(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var invocation = (InvocationExpressionSyntax)c.Node;

                    Location location;
                    IMethodSymbol methodSymbol;
                    if (!IsArgumentlessToStringCallNotOnBaseExpression(invocation, c.SemanticModel, out location, out methodSymbol))
                    {
                        return;
                    }

                    if (methodSymbol.IsInType(KnownType.System_String))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, location, MessageCallOnString));
                        return;
                    }

                    ITypeSymbol subExpressionType;
                    if (!TryGetExpressionTypeOfOwner(invocation, c.SemanticModel, out subExpressionType) ||
                        subExpressionType.IsValueType)
                    {
                        return;
                    }

                    var stringFormatArgument = invocation?.Parent as ArgumentSyntax;
                    var stringFormatInvocation = stringFormatArgument?.Parent?.Parent as InvocationExpressionSyntax;
                    if (stringFormatInvocation == null ||
                        !IsStringFormatCall(c.SemanticModel.GetSymbolInfo(stringFormatInvocation).Symbol as IMethodSymbol))
                    {
                        return;
                    }

                    var parameterLookup = new MethodParameterLookup(stringFormatInvocation, c.SemanticModel);
                    IParameterSymbol argParameter;
                    if (parameterLookup.TryGetParameterSymbol(stringFormatArgument, out argParameter) &&
                        argParameter.Name.StartsWith("arg", StringComparison.Ordinal))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, location, MessageCompiler));
                    }
                },
                SyntaxKind.InvocationExpression);
        }

        private static void CheckLeftExpressionForRemovableToStringCall(SyntaxNodeAnalysisContext context,
            BinaryExpressionSyntax binary)
        {
            CheckExpressionForRemovableToStringCall(context, binary.Left, binary.Right, 0);
        }
        private static void CheckRightExpressionForRemovableToStringCall(SyntaxNodeAnalysisContext context,
            BinaryExpressionSyntax binary)
        {
            CheckExpressionForRemovableToStringCall(context, binary.Right, binary.Left, 1);
        }
        private static void CheckRightExpressionForRemovableToStringCall(SyntaxNodeAnalysisContext context,
            AssignmentExpressionSyntax assignment)
        {
            CheckExpressionForRemovableToStringCall(context, assignment.Right, assignment.Left, 1);
        }

        private static void CheckExpressionForRemovableToStringCall(SyntaxNodeAnalysisContext context,
            ExpressionSyntax expressionWithToStringCall, ExpressionSyntax otherOperandOfAddition, int checkedSideIndex)
        {
            Location location;
            IMethodSymbol methodSymbol;
            if (!IsArgumentlessToStringCallNotOnBaseExpression(expressionWithToStringCall, context.SemanticModel, out location, out methodSymbol) ||
                methodSymbol.IsInType(KnownType.System_String))
            {
                return;
            }

            var sideBType = context.SemanticModel.GetTypeInfo(otherOperandOfAddition).Type;
            if (!sideBType.Is(KnownType.System_String))
            {
                return;
            }

            ITypeSymbol subExpressionType;
            if (!TryGetExpressionTypeOfOwner((InvocationExpressionSyntax)expressionWithToStringCall, context.SemanticModel, out subExpressionType) ||
                subExpressionType.IsValueType)
            {
                return;
            }

            var stringParameterIndex = (checkedSideIndex + 1) % 2;
            if (!DoesCollidingAdditionExist(subExpressionType, stringParameterIndex))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, MessageCompiler));
            }
        }

        private static bool TryGetExpressionTypeOfOwner(InvocationExpressionSyntax invocation, SemanticModel semanticModel,
            out ITypeSymbol subExpressionType)
        {
            subExpressionType = null;

            var subExpression = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;
            if (subExpression == null)
            {
                return false;
            }

            subExpressionType = semanticModel.GetTypeInfo(subExpression).Type;
            return subExpressionType != null;
        }

        private static bool DoesCollidingAdditionExist(ITypeSymbol subExpressionType, int stringParameterIndex)
        {
            return subExpressionType.GetMembers(additionOperatorName)
                .OfType<IMethodSymbol>()
                .Where(method =>
                    method.MethodKind == MethodKind.BuiltinOperator ||
                    method.MethodKind == MethodKind.UserDefinedOperator)
                .Any(method =>
                    method.Parameters.Length == 2 &&
                    method.Parameters[stringParameterIndex].IsType(KnownType.System_String));
        }

        private static bool IsStringFormatCall(IMethodSymbol stringFormatSymbol)
        {
            return stringFormatSymbol != null &&
                stringFormatSymbol.Name == "Format" &&
                (stringFormatSymbol.ContainingType == null || stringFormatSymbol.IsInType(KnownType.System_String));
        }

        private static bool IsOperationAddOnString(IMethodSymbol operation)
        {
            return operation != null &&
                operation.Name == additionOperatorName &&
                operation.IsInType(KnownType.System_String);
        }

        private static bool IsArgumentlessToStringCallNotOnBaseExpression(ExpressionSyntax expression, SemanticModel semanticModel,
            out Location location, out IMethodSymbol methodSymbol)
        {
            location = null;
            methodSymbol = null;
            var invocation = expression as InvocationExpressionSyntax;
            if (invocation == null ||
                invocation.ArgumentList.CloseParenToken.IsMissing)
            {
                return false;
            }

            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null ||
                memberAccess.Expression is BaseExpressionSyntax)
            {
                return false;
            }

            methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (!IsParameterlessToString(methodSymbol))
            {
                return false;
            }

            location = Location.Create(invocation.SyntaxTree,
                TextSpan.FromBounds(
                    memberAccess.OperatorToken.SpanStart,
                    invocation.Span.End));
            return true;
        }

        private static bool IsParameterlessToString(IMethodSymbol methodSymbol)
        {
            return methodSymbol != null &&
                methodSymbol.Name == "ToString" &&
                !methodSymbol.Parameters.Any();
        }
    }
}
