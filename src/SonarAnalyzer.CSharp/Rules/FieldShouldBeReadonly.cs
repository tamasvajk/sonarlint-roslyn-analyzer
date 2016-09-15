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
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.CSharp
{
    using FieldTuple = SyntaxNodeSymbolSemanticModelTuple<VariableDeclaratorSyntax, IFieldSymbol>;
    using TypeDeclarationTuple = SyntaxNodeSemanticModelTuple<TypeDeclarationSyntax>;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("2min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Confusing)]
    public class FieldShouldBeReadonly : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2933";
        internal const string Title = "Fields that are only assigned in the constructor should be \"readonly\"";
        internal const string Description =
            "\"readonly\" fields can only be assigned in a class constructor. If a class has " +
            "a field that's not marked \"readonly\" but is only set in the constructor, it " +
            "could cause confusion about the field's intended use. To avoid confusion, such " +
            "fields should be marked \"readonly\" to make their intended use explicit, and to " +
            "prevent future maintainers from inadvertently changing their use.";
        internal const string MessageFormat = "Make \"{0}\" \"readonly\".";
        internal const string Category = SonarAnalyzer.Common.Category.Design;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly ISet<SyntaxKind> assignmentKinds = ImmutableHashSet.Create(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.RightShiftAssignmentExpression);

        private static readonly ISet<SyntaxKind> prefixUnaryKinds = ImmutableHashSet.Create(
            SyntaxKind.PreDecrementExpression,
            SyntaxKind.PreIncrementExpression);

        private static readonly ISet<SyntaxKind> postfixUnaryKinds = ImmutableHashSet.Create(
            SyntaxKind.PostDecrementExpression,
            SyntaxKind.PostIncrementExpression);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSymbolAction(
                c =>
                {
                    var declaredSymbol = (INamedTypeSymbol)c.Symbol;
                    if (!declaredSymbol.IsClassOrStruct())
                    {
                        return;
                    }

                    if (declaredSymbol.DeclaringSyntaxReferences.Count() > 1)
                    {
                        // Partial classes are not processed.
                        // See https://github.com/dotnet/roslyn/issues/3748
                        return;
                    }

                    var partialDeclarations = declaredSymbol.DeclaringSyntaxReferences
                        .Select(reference => reference.GetSyntax())
                        .OfType<TypeDeclarationSyntax>()
                        .Select(node =>
                            new SyntaxNodeSemanticModelTuple<TypeDeclarationSyntax>
                            {
                                SyntaxNode = node,
                                SemanticModel = c.Compilation.GetSemanticModel(node.SyntaxTree)
                            })
                        .Where(n => n.SemanticModel != null);

                    var fieldCollector = new ReadonlyFieldCollector(partialDeclarations);

                    foreach (var field in fieldCollector.NonCompliantFields)
                    {
                        var identifier = field.SyntaxNode.Identifier;
                        c.ReportDiagnosticIfNonGenerated(Diagnostic.Create(Rule, identifier.GetLocation(), identifier.ValueText));
                    }
                },
                SymbolKind.NamedType);
        }

        private class ReadonlyFieldCollector
        {
            private readonly ISet<IFieldSymbol> assignedAsReadonly;
            private readonly ISet<IFieldSymbol> excludedFields;
            private readonly List<FieldTuple> allFields = new List<FieldTuple>();

            public IEnumerable<FieldTuple> NonCompliantFields
            {
                get
                {
                    var reportedFields = new HashSet<IFieldSymbol>(assignedAsReadonly.Except(excludedFields));
                    return allFields.Where(f => reportedFields.Contains(f.Symbol));
                }
            }

            public ReadonlyFieldCollector(IEnumerable<TypeDeclarationTuple> partialTypeDeclarations)
            {
                excludedFields = new HashSet<IFieldSymbol>();
                assignedAsReadonly = new HashSet<IFieldSymbol>();

                foreach (var partialTypeDeclaration in partialTypeDeclarations)
                {
                    var p = new PartialTypeDeclarationProcessor(partialTypeDeclaration, this);
                    p.CollectFields();
                    allFields.AddRange(p.AllFields);
                }
            }

            private class PartialTypeDeclarationProcessor
            {
                private readonly TypeDeclarationTuple partialTypeDeclaration;
                private readonly ReadonlyFieldCollector readonlyFieldCollector;
                private readonly IEnumerable<FieldTuple> allFields;

                public IEnumerable<FieldTuple> AllFields => allFields;

                public PartialTypeDeclarationProcessor(TypeDeclarationTuple partialTypeDeclaration, ReadonlyFieldCollector readonlyFieldCollector)
                {
                    this.partialTypeDeclaration = partialTypeDeclaration;
                    this.readonlyFieldCollector = readonlyFieldCollector;

                    allFields = partialTypeDeclaration.SyntaxNode.DescendantNodes()
                        .OfType<FieldDeclarationSyntax>()
                        .SelectMany(f => GetAllFields(f));
                }

                private IEnumerable<FieldTuple> GetAllFields(FieldDeclarationSyntax fieldDeclaration)
                {
                    return fieldDeclaration.Declaration.Variables
                        .Select(variableDeclaratorSyntax => new FieldTuple
                        {
                            SyntaxNode = variableDeclaratorSyntax,
                            Symbol = partialTypeDeclaration.SemanticModel.GetDeclaredSymbol(variableDeclaratorSyntax) as IFieldSymbol,
                            SemanticModel = partialTypeDeclaration.SemanticModel
                        });
                }

                public void CollectFields()
                {
                    CollectFieldsFromDeclarations();
                    CollectFieldsFromAssignments();
                    CollectFieldsFromPrefixUnaryExpressions();
                    CollectFieldsFromPostfixUnaryExpressions();
                    CollectFieldsFromArguments();
                }

                private void CollectFieldsFromDeclarations()
                {
                    var fieldDeclarations = allFields.Where(f =>
                        IsFieldRelevant(f.Symbol) &&
                        f.SyntaxNode.Initializer != null);

                    foreach (var field in fieldDeclarations)
                    {
                        readonlyFieldCollector.assignedAsReadonly.Add(field.Symbol);
                    }
                }

                private void CollectFieldsFromArguments()
                {
                    var arguments = partialTypeDeclaration.SyntaxNode.DescendantNodes()
                        .OfType<ArgumentSyntax>()
                        .Where(a => !a.RefOrOutKeyword.IsKind(SyntaxKind.None));

                    foreach (var argument in arguments)
                    {
                        // ref/out should be handled the same way as all other field assignments:
                        ProcessExpression(argument.Expression);
                    }
                }

                private void CollectFieldsFromPostfixUnaryExpressions()
                {
                    var postfixUnaries = partialTypeDeclaration.SyntaxNode.DescendantNodes()
                        .OfType<PostfixUnaryExpressionSyntax>()
                        .Where(a => postfixUnaryKinds.Contains(a.Kind()));

                    foreach (var postfixUnary in postfixUnaries)
                    {
                        ProcessExpression(postfixUnary.Operand);
                    }
                }

                private void CollectFieldsFromPrefixUnaryExpressions()
                {
                    var prefixUnaries = partialTypeDeclaration.SyntaxNode.DescendantNodes()
                        .OfType<PrefixUnaryExpressionSyntax>()
                        .Where(a => prefixUnaryKinds.Contains(a.Kind()));

                    foreach (var prefixUnary in prefixUnaries)
                    {
                        ProcessExpression(prefixUnary.Operand);
                    }
                }

                private void CollectFieldsFromAssignments()
                {
                    var assignments = partialTypeDeclaration.SyntaxNode.DescendantNodes()
                        .OfType<AssignmentExpressionSyntax>()
                        .Where(a => assignmentKinds.Contains(a.Kind()));

                    foreach (var assignment in assignments)
                    {
                        ProcessExpression(assignment.Left);
                    }
                }

                private void ProcessExpression(ExpressionSyntax expression)
                {
                    ProcessAssignedExpression(expression);
                    ProcessAssignedTopMemberAccessExpression(expression);
                }

                private void ProcessAssignedTopMemberAccessExpression(ExpressionSyntax expression)
                {
                    var topExpression = GetTopMemberAccessIfNested(expression);
                    if (topExpression == null)
                    {
                        return;
                    }

                    var fieldSymbol = partialTypeDeclaration.SemanticModel.GetSymbolInfo(topExpression).Symbol as IFieldSymbol;
                    if (fieldSymbol?.Type == null ||
                        !fieldSymbol.Type.IsValueType)
                    {
                        return;
                    }

                    ProcessExpressionOnField(topExpression, fieldSymbol);
                }

                private static ExpressionSyntax GetTopMemberAccessIfNested(ExpressionSyntax expression, bool isNestedMemberAccess = false)
                {
                    // If expression is (this.a.b).c, we need to return this.a

                    var noParens = expression.RemoveParentheses();

                    if (noParens is NameSyntax)
                    {
                        return isNestedMemberAccess ? noParens : null;
                    }

                    var memberAccess = noParens as MemberAccessExpressionSyntax;
                    if (memberAccess == null)
                    {
                        return null;
                    }

                    if (memberAccess.Expression.RemoveParentheses().IsKind(SyntaxKind.ThisExpression))
                    {
                        return isNestedMemberAccess ? memberAccess : null;
                    }

                    return GetTopMemberAccessIfNested(memberAccess.Expression, true);
                }

                private void ProcessAssignedExpression(ExpressionSyntax expression)
                {
                    var fieldSymbol = partialTypeDeclaration.SemanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
                    ProcessExpressionOnField(expression, fieldSymbol);
                }

                private void ProcessExpressionOnField(ExpressionSyntax expression, IFieldSymbol fieldSymbol)
                {
                    if (!IsFieldRelevant(fieldSymbol))
                    {
                        return;
                    }

                    if (!expression.RemoveParentheses().IsOnThis())
                    {
                        readonlyFieldCollector.excludedFields.Add(fieldSymbol);
                        return;
                    }

                    var constructorSymbol = partialTypeDeclaration.SemanticModel.GetEnclosingSymbol(expression.SpanStart) as IMethodSymbol;
                    if (constructorSymbol == null)
                    {
                        readonlyFieldCollector.excludedFields.Add(fieldSymbol);
                        return;
                    }

                    if (constructorSymbol.MethodKind == MethodKind.Constructor &&
                        constructorSymbol.ContainingType.Equals(fieldSymbol.ContainingType))
                    {
                        readonlyFieldCollector.assignedAsReadonly.Add(fieldSymbol);
                    }
                    else
                    {
                        readonlyFieldCollector.excludedFields.Add(fieldSymbol);
                    }
                }

                private static bool IsFieldRelevant(IFieldSymbol fieldSymbol)
                {
                    return fieldSymbol != null &&
                           !fieldSymbol.IsStatic &&
                           !fieldSymbol.IsConst &&
                           !fieldSymbol.IsReadOnly &&
                           fieldSymbol.DeclaredAccessibility == Accessibility.Private;
                }
            }
        }
    }
}
