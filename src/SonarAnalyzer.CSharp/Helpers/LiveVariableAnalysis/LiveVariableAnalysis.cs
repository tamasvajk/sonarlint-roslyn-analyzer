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
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SonarAnalyzer.Helpers.FlowAnalysis.CSharp
{
    public sealed class LiveVariableAnalysis : Common.LiveVariableAnalysis
    {
        private readonly ISymbol declaration;
        private readonly SemanticModel semanticModel;

        private LiveVariableAnalysis(IControlFlowGraph controlFlowGraph, ISymbol declaration, SemanticModel semanticModel)
            : base(controlFlowGraph)
        {
            this.declaration = declaration;
            this.semanticModel = semanticModel;
        }

        public static Common.LiveVariableAnalysis Analyze(IControlFlowGraph controlFlowGraph, ISymbol declaration, SemanticModel semanticModel)
        {
            var lva = new LiveVariableAnalysis(controlFlowGraph, declaration, semanticModel);
            lva.PerformAnalysis();
            return lva;
        }

        protected override void ProcessBlock(Block block, out HashSet<ISymbol> assignedInBlock,
            out HashSet<ISymbol> usedBeforeAssigned)
        {
            assignedInBlock = new HashSet<ISymbol>(); // Kill (The set of variables that are assigned a value.)
            usedBeforeAssigned = new HashSet<ISymbol>(); // Gen (The set of variables that are used before any assignment.)

            var assignmentLhs = new HashSet<SyntaxNode>();

            foreach (var instruction in block.Instructions.Reverse())
            {
                switch (instruction.Kind())
                {
                    case SyntaxKind.IdentifierName:
                        ProcessIdentifier(instruction, assignedInBlock, usedBeforeAssigned, assignmentLhs);
                        break;

                    case SyntaxKind.SimpleAssignmentExpression:
                        ProcessSimpleAssignment(instruction, assignedInBlock, usedBeforeAssigned, assignmentLhs);
                        break;

                    case SyntaxKind.VariableDeclarator:
                        ProcessVariableDeclarator(instruction, assignedInBlock, usedBeforeAssigned);
                        break;

                    case SyntaxKind.AnonymousMethodExpression:
                    case SyntaxKind.ParenthesizedLambdaExpression:
                    case SyntaxKind.SimpleLambdaExpression:
                    case SyntaxKind.QueryExpression:
                        CollectAllCapturedLocal(instruction);
                        break;

                    default:
                        break;
                }
            }

            if (block.Instructions.Any())
            {
                return;
            }

            // Variable declaration in a foreach statement is not a VariableDeclarator, so handling it separately:
            var foreachBlock = block as BinaryBranchBlock;
            if (foreachBlock != null &&
                foreachBlock.BranchingNode.IsKind(SyntaxKind.ForEachStatement))
            {
                var foreachNode = (ForEachStatementSyntax)foreachBlock.BranchingNode;
                ProcessVariableInForeach(foreachNode, assignedInBlock, usedBeforeAssigned);
            }
        }

        private void ProcessVariableInForeach(ForEachStatementSyntax foreachNode, HashSet<ISymbol> assignedInBlock, HashSet<ISymbol> usedBeforeAssigned)
        {
            var symbol = semanticModel.GetDeclaredSymbol(foreachNode);
            if (symbol == null)
            {
                return;
            }

            assignedInBlock.Add(symbol);
            usedBeforeAssigned.Remove(symbol);
        }

        private void ProcessVariableDeclarator(SyntaxNode instruction, HashSet<ISymbol> assignedInBlock,
            HashSet<ISymbol> usedBeforeAssigned)
        {
            var symbol = semanticModel.GetDeclaredSymbol(instruction);
            if (symbol == null)
            {
                return;
            }

            assignedInBlock.Add(symbol);
            usedBeforeAssigned.Remove(symbol);
        }

        private void ProcessSimpleAssignment(SyntaxNode instruction, HashSet<ISymbol> assignedInBlock, HashSet<ISymbol> usedBeforeAssigned, HashSet<SyntaxNode> assignmentLhs)
        {
            var assignment = (AssignmentExpressionSyntax)instruction;
            var left = assignment.Left.RemoveParentheses();
            if (left.IsKind(SyntaxKind.IdentifierName))
            {
                var symbol = semanticModel.GetSymbolInfo(left).Symbol;
                if (symbol == null)
                {
                    return;
                }

                if (IsLocalScoped(symbol))
                {
                    assignmentLhs.Add(left);
                    assignedInBlock.Add(symbol);
                    usedBeforeAssigned.Remove(symbol);
                }
            }
        }

        private void ProcessIdentifier(SyntaxNode instruction, HashSet<ISymbol> assignedInBlock, HashSet<ISymbol> usedBeforeAssigned,
            HashSet<SyntaxNode> assignmentLhs)
        {
            var identifier = (IdentifierNameSyntax)instruction;
            var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol == null)
            {
                return;
            }

            if (!identifier.GetSelfOrTopParenthesizedExpression().IsInNameofCall(semanticModel) &&
                IsLocalScoped(symbol))
            {
                if (IsOutArgument(identifier))
                {
                    assignedInBlock.Add(symbol);
                    usedBeforeAssigned.Remove(symbol);
                }
                else
                {
                    if (!assignmentLhs.Contains(instruction))
                    {
                        usedBeforeAssigned.Add(symbol);
                    }
                }
            }
        }

        private void CollectAllCapturedLocal(SyntaxNode instruction)
        {
            var allCapturedSymbols = instruction.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(i => semanticModel.GetSymbolInfo(i).Symbol)
                .Where(s => s != null && IsLocalScoped(s));

            // Collect captured locals
            // Read and write both affects liveness
            capturedVariables.UnionWith(allCapturedSymbols);
        }

        internal static bool IsOutArgument(IdentifierNameSyntax identifier)
        {
            var argument = identifier.GetSelfOrTopParenthesizedExpression().Parent as ArgumentSyntax;

            return argument != null &&
                argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword);
        }

        private bool IsLocalScoped(ISymbol symbol)
        {
            return IsLocalScoped(symbol, declaration);
        }

        internal static bool IsLocalScoped(ISymbol symbol, ISymbol declaration)
        {
            var local = symbol as ILocalSymbol;
            if (local == null)
            {
                var parameter = symbol as IParameterSymbol;
                if (parameter == null ||
                    parameter.RefKind != RefKind.None)
                {
                    return false;
                }
            }

            return symbol.ContainingSymbol != null &&
                symbol.ContainingSymbol.Equals(declaration);
        }
    }
}
