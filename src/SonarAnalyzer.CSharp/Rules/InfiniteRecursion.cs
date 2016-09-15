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
using SonarAnalyzer.Helpers.FlowAnalysis.CSharp;
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using System;
using SonarAnalyzer.Helpers.FlowAnalysis;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("30min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class InfiniteRecursion : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2190";
        internal const string Title = "Recursion should not be infinite";
        internal const string Description =
            "Recursion happens when control enters a loop that has no exit. This can happen a method invokes itself, when a pair of " +
            "methods invoke each other, or when \"goto\"s are used to move between two segments of code. It can be a useful tool, but " +
            "unless the method includes a provision to break out of the recursion and return, the recursion will continue until the " +
            "stack overflows and the program crashes.";
        internal const string MessageFormat = "Add a way to break out of this {0}.";
        internal const string Category = SonarAnalyzer.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Blocker;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c=> CheckForNoExitMethod(c),
                SyntaxKind.MethodDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckForNoExitProperty(c),
                SyntaxKind.PropertyDeclaration);
        }

        private static void CheckForNoExitProperty(SyntaxNodeAnalysisContext c)
        {
            var property = (PropertyDeclarationSyntax)c.Node;
            var propertySymbol = c.SemanticModel.GetDeclaredSymbol(property);
            if (propertySymbol == null)
            {
                return;
            }

            IControlFlowGraph cfg;
            if (property.ExpressionBody?.Expression != null)
            {
                if (ControlFlowGraph.TryGet(property.ExpressionBody.Expression, c.SemanticModel, out cfg))
                {
                    var walker = new CfgWalkerForProperty(
                         new RecursionAnalysisContext(cfg, propertySymbol, property.Identifier.GetLocation(), c),
                        "property's recursion",
                        isSetAccessor: false);
                    walker.CheckPaths();
                }
                return;
            }

            var accessors = property.AccessorList?.Accessors.Where(a => a.Body != null);
            if (accessors != null)
            {
                foreach (var accessor in accessors)
                {
                    if (ControlFlowGraph.TryGet(accessor.Body, c.SemanticModel, out cfg))
                    {
                        var walker = new CfgWalkerForProperty(
                            new RecursionAnalysisContext(cfg, propertySymbol, accessor.Keyword.GetLocation(), c),
                            "property accessor's recursion",
                            isSetAccessor: accessor.Keyword.IsKind(SyntaxKind.SetKeyword));
                        walker.CheckPaths();

                        CheckInfiniteJumpLoop(accessor.Body, cfg, "property accessor", c);
                    }
                }
            }
        }

        private static void CheckForNoExitMethod(SyntaxNodeAnalysisContext c)
        {
            var method = (MethodDeclarationSyntax)c.Node;
            var methodSymbol = c.SemanticModel.GetDeclaredSymbol(method);

            if (methodSymbol == null)
            {
                return;
            }

            IControlFlowGraph cfg;
            if (ControlFlowGraph.TryGet(method.Body, c.SemanticModel, out cfg) ||
                ControlFlowGraph.TryGet(method.ExpressionBody?.Expression, c.SemanticModel, out cfg))
            {
                var walker = new CfgWalkerForMethod(
                    new RecursionAnalysisContext(cfg, methodSymbol, method.Identifier.GetLocation(), c));
                walker.CheckPaths();

                CheckInfiniteJumpLoop(method.Body, cfg, "method", c);
            }
        }

        private static void CheckInfiniteJumpLoop(BlockSyntax body, IControlFlowGraph cfg, string declarationType,
            SyntaxNodeAnalysisContext analysisContext)
        {
            if (body == null)
            {
                return;
            }

            var reachableFromBlock = cfg.Blocks.Except(new[] { cfg.ExitBlock }).ToDictionary(
                b => b,
                b => b.AllSuccessorBlocks);

            var alreadyProcessed = new HashSet<Block>();

            foreach (var reachable in reachableFromBlock)
            {
                if (!reachable.Key.AllPredecessorBlocks.Contains(cfg.EntryBlock) ||
                    alreadyProcessed.Contains(reachable.Key) ||
                    reachable.Value.Contains(cfg.ExitBlock))
                {
                    continue;
                }

                alreadyProcessed.UnionWith(reachable.Value);
                alreadyProcessed.Add(reachable.Key);

                var reportOnOptions = reachable.Value.OfType<JumpBlock>()
                    .Where(jb => jb.JumpNode is GotoStatementSyntax)
                    .ToList();

                if (!reportOnOptions.Any())
                {
                    continue;
                }

                // Calculate stable report location:
                var lastJumpLocation = reportOnOptions.Max(b => b.JumpNode.SpanStart);
                var reportOn = reportOnOptions.First(b => b.JumpNode.SpanStart == lastJumpLocation);

                analysisContext.ReportDiagnostic(Diagnostic.Create(Rule, reportOn.JumpNode.GetLocation(), declarationType));
            }
        }

        #region CFG walkers for call recursion

        private class RecursionAnalysisContext
        {
            public IControlFlowGraph ControlFlowGraph { get; }
            public ISymbol AnalyzedSymbol { get; }
            public SemanticModel SemanticModel { get; }
            public Location IssueLocation { get; }
            public SyntaxNodeAnalysisContext AnalysisContext { get; }

            public RecursionAnalysisContext(IControlFlowGraph controlFlowGraph, ISymbol analyzedSymbol, Location issueLocation,
                SyntaxNodeAnalysisContext analysisContext)
            {
                ControlFlowGraph = controlFlowGraph;
                AnalyzedSymbol = analyzedSymbol;
                IssueLocation = issueLocation;
                AnalysisContext = analysisContext;

                SemanticModel = analysisContext.SemanticModel;
            }
        }

        private class CfgWalkerForMethod : CfgRecursionSearcher
        {
            public CfgWalkerForMethod(RecursionAnalysisContext context)
                : base(context.ControlFlowGraph, context.AnalyzedSymbol, context.SemanticModel,
                      () => context.AnalysisContext.ReportDiagnostic(Diagnostic.Create(Rule, context.IssueLocation, "method's recursion")))
            {
            }

            protected override bool BlockHasReferenceToDeclaringSymbol(Block block)
            {
                return block.Instructions.Any(i =>
                {
                    var invocation = i as InvocationExpressionSyntax;
                    if (invocation == null)
                    {
                        return false;
                    }

                    return IsInstructionOnThisAndMatchesDeclaringSymbol(invocation.Expression);
                });
            }
        }

        private class CfgWalkerForProperty : CfgRecursionSearcher
        {
            private readonly bool isSet;

            public CfgWalkerForProperty(RecursionAnalysisContext context, string reportOn, bool isSetAccessor)
                : base(context.ControlFlowGraph, context.AnalyzedSymbol, context.SemanticModel,
                      () => context.AnalysisContext.ReportDiagnostic(Diagnostic.Create(Rule, context.IssueLocation, reportOn)))
            {
                isSet = isSetAccessor;
            }

            private static readonly ISet<Type> TypesForReference = ImmutableHashSet.Create(
                typeof(IdentifierNameSyntax),
                typeof(MemberAccessExpressionSyntax));

            protected override bool BlockHasReferenceToDeclaringSymbol(Block block)
            {
                return block.Instructions.Any(i =>
                    TypesForReference.Contains(i.GetType()) &&
                    MatchesAccessor(i) &&
                    IsInstructionOnThisAndMatchesDeclaringSymbol(i));
            }

            private bool MatchesAccessor(SyntaxNode node)
            {
                var expr = node as ExpressionSyntax;
                if (expr == null)
                {
                    return false;
                }

                var propertyAccess = expr.GetSelfOrTopParenthesizedExpression();
                if (propertyAccess.IsInNameofCall(semanticModel))
                {
                    return false;
                }

                var assignment = propertyAccess.Parent as AssignmentExpressionSyntax;
                var isNodeASet = assignment != null && assignment.Left == propertyAccess;
                return isNodeASet == isSet;
            }
        }

        private abstract class CfgRecursionSearcher : CfgAllPathValidator
        {
            protected readonly ISymbol declaringSymbol;
            protected readonly SemanticModel semanticModel;
            protected readonly Action reportIssue;

            protected CfgRecursionSearcher(IControlFlowGraph cfg, ISymbol declaringSymbol, SemanticModel semanticModel, Action reportIssue)
                : base(cfg)
            {
                this.declaringSymbol = declaringSymbol;
                this.semanticModel = semanticModel;
                this.reportIssue = reportIssue;
            }

            public void CheckPaths()
            {
                if (CheckAllPaths())
                {
                    reportIssue();
                }
            }

            protected override bool IsBlockValid(Block block)
            {
                return BlockHasReferenceToDeclaringSymbol(block);
            }

            protected abstract bool BlockHasReferenceToDeclaringSymbol(Block block);

            protected bool IsInstructionOnThisAndMatchesDeclaringSymbol(SyntaxNode node)
            {
                var expression = node as ExpressionSyntax;
                if (expression == null)
                {
                    return false;
                }

                NameSyntax name = expression as NameSyntax;

                var memberAccess = expression as MemberAccessExpressionSyntax;
                if (memberAccess != null &&
                    memberAccess.Expression.IsKind(SyntaxKind.ThisExpression))
                {
                    name = memberAccess.Name as IdentifierNameSyntax;
                }

                var conditionalAccess = expression as ConditionalAccessExpressionSyntax;
                if (conditionalAccess != null &&
                    conditionalAccess.Expression.IsKind(SyntaxKind.ThisExpression))
                {
                    name = (conditionalAccess.WhenNotNull as MemberBindingExpressionSyntax)?.Name as IdentifierNameSyntax;
                }

                if (name == null)
                {
                    return false;
                }

                var assignedSymbol = semanticModel.GetSymbolInfo(name).Symbol;

                return declaringSymbol.Equals(assignedSymbol);
            }
        }

        #endregion
    }
}
