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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using SonarAnalyzer.Helpers.FlowAnalysis.CSharp;

namespace SonarAnalyzer.Rules.CSharp
{
    using ExplodedGraph = Helpers.FlowAnalysis.CSharp.ExplodedGraph;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class EmptyNullableValueAccess : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3655";
        internal const string Title = "Empty nullable value should not be accessed";
        internal const string Description =
            "Nullable value types can hold either a value or \"null\". The value held in the nullable type can be accessed with " +
            "the \"Value\" property, but \".Value\" throws an \"InvalidOperationException\" when if the nullable type's value is " +
            "\"null\". To avoid the exception, a nullable type should always be tested before \".Value\" is accessed.";
        internal const string MessageFormat = "\"{0}\" is null on at least one execution path.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Blocker;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private const string ValueLiteral = "Value";
        private const string HasValueLiteral = "HasValue";

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterExplodedGraphBasedAnalysis((e, c) => CheckEmptyNullableAccess(e, c));
        }

        private static void CheckEmptyNullableAccess(ExplodedGraph explodedGraph, SyntaxNodeAnalysisContext context)
        {
            var nullPointerCheck = new NullValueAccessedCheck(explodedGraph);
            explodedGraph.AddExplodedGraphCheck(nullPointerCheck);

            var nullIdentifiers = new HashSet<IdentifierNameSyntax>();

            EventHandler<MemberAccessedEventArgs> nullValueAccessedHandler =
                (sender, args) => nullIdentifiers.Add(args.Identifier);

            nullPointerCheck.ValuePropertyAccessed += nullValueAccessedHandler;

            try
            {
                explodedGraph.Walk();
            }
            finally
            {
                nullPointerCheck.ValuePropertyAccessed -= nullValueAccessedHandler;
            }

            foreach (var nullIdentifier in nullIdentifiers)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, nullIdentifier.Parent.GetLocation(), nullIdentifier.Identifier.ValueText));
            }
        }

        internal sealed class NullValueAccessedCheck : ExplodedGraphCheck
        {
            public event EventHandler<MemberAccessedEventArgs> ValuePropertyAccessed;

            public NullValueAccessedCheck(ExplodedGraph explodedGraph)
                : base(explodedGraph)
            {
            }

            private void OnValuePropertyAccessed(IdentifierNameSyntax identifier)
            {
                ValuePropertyAccessed?.Invoke(this, new MemberAccessedEventArgs
                {
                    Identifier = identifier
                });
            }

            public override ProgramState PreProcessInstruction(ProgramPoint programPoint, ProgramState programState)
            {
                var instruction = programPoint.Block.Instructions[programPoint.Offset];

                return instruction.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                    ? ProcessMemberAccess(programState, (MemberAccessExpressionSyntax)instruction)
                    : programState;
            }

            private ProgramState ProcessMemberAccess(ProgramState programState, MemberAccessExpressionSyntax memberAccess)
            {
                var identifier = memberAccess.Expression.RemoveParentheses() as IdentifierNameSyntax;
                if (identifier == null ||
                    memberAccess.Name.Identifier.ValueText != ValueLiteral)
                {
                    return programState;
                }

                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                if (!IsNullableLocalScoped(symbol))
                {
                    return programState;
                }

                if (symbol.HasConstraint(ObjectConstraint.Null, programState))
                {
                    OnValuePropertyAccessed(identifier);
                    return null;
                }

                return programState;
            }

            private bool IsNullableLocalScoped(ISymbol symbol)
            {
                var type = symbol.GetSymbolType();
                return type != null &&
                    type.OriginalDefinition.Is(KnownType.System_Nullable_T) &&
                    explodedGraph.IsLocalScoped(symbol);
            }

            private bool IsHasValueAccess(MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Name.Identifier.ValueText == HasValueLiteral &&
                    semanticModel.GetTypeInfo(memberAccess.Expression).Type.OriginalDefinition.Is(KnownType.System_Nullable_T);
            }

            internal bool TryProcessInstruction(MemberAccessExpressionSyntax instruction, ProgramState programState, out ProgramState newProgramState)
            {
                if (IsHasValueAccess(instruction))
                {
                    SymbolicValue nullable;
                    newProgramState = programState.PopValue(out nullable);
                    newProgramState = newProgramState.PushValue(new HasValueAccessSymbolicValue(nullable));
                    return true;
                }

                newProgramState = programState;
                return false;
            }
        }

        internal sealed class HasValueAccessSymbolicValue : MemberAccessSymbolicValue
        {
            public HasValueAccessSymbolicValue(SymbolicValue nullable)
                : base(nullable, HasValueLiteral)
            {
            }

            public override IEnumerable<ProgramState> TrySetConstraint(SymbolicValueConstraint constraint, ProgramState currentProgramState)
            {
                var boolConstraint = constraint as BoolConstraint;
                if (boolConstraint == null)
                {
                    return new[] { currentProgramState };
                }

                var nullabilityConstraint = boolConstraint == BoolConstraint.True
                    ? ObjectConstraint.NotNull
                    : ObjectConstraint.Null;

                return MemberExpression.TrySetConstraint(nullabilityConstraint, currentProgramState);
            }
        }
    }
}