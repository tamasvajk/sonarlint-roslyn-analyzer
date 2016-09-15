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
using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SonarAnalyzer.Helpers.FlowAnalysis.Common;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SonarAnalyzer.Helpers.FlowAnalysis.CSharp
{
    internal static class FlowAnalysisExtensions
    {
        public static void RegisterExplodedGraphBasedAnalysis(this SonarAnalysisContext context,
            Action<ExplodedGraph, SyntaxNodeAnalysisContext> analyze)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (BaseMethodDeclarationSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(declaration);
                    if (symbol == null)
                    {
                        return;
                    }

                    Analyze(declaration.Body, symbol, analyze, c);
                },
                SyntaxKind.ConstructorDeclaration,
                SyntaxKind.DestructorDeclaration,
                SyntaxKind.ConversionOperatorDeclaration,
                SyntaxKind.OperatorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (MethodDeclarationSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(declaration);
                    if (symbol == null)
                    {
                        return;
                    }

                    Analyze(declaration.Body, symbol, analyze, c);
                    Analyze(declaration.ExpressionBody?.Expression, symbol, analyze, c);
                },
                SyntaxKind.MethodDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (PropertyDeclarationSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(declaration);
                    if (symbol == null)
                    {
                        return;
                    }

                    Analyze(declaration.ExpressionBody?.Expression, symbol, analyze, c);
                },
                SyntaxKind.PropertyDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (AccessorDeclarationSyntax)c.Node;
                    var symbol = c.SemanticModel.GetDeclaredSymbol(declaration);
                    if (symbol == null)
                    {
                        return;
                    }

                    Analyze(declaration.Body, symbol, analyze, c);
                },
                SyntaxKind.GetAccessorDeclaration,
                SyntaxKind.SetAccessorDeclaration,
                SyntaxKind.AddAccessorDeclaration,
                SyntaxKind.RemoveAccessorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var declaration = (AnonymousFunctionExpressionSyntax)c.Node;
                    var symbol = c.SemanticModel.GetSymbolInfo(declaration).Symbol;
                    if (symbol == null)
                    {
                        return;
                    }

                    Analyze(declaration.Body, symbol, analyze, c);
                },
                SyntaxKind.AnonymousMethodExpression,
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression);
        }

        private static void Analyze(CSharpSyntaxNode declarationBody, ISymbol symbol,
            Action<ExplodedGraph, SyntaxNodeAnalysisContext> analyze, SyntaxNodeAnalysisContext context)
        {
            if (declarationBody == null ||
                declarationBody.ContainsDiagnostics)
            {
                return;
            }

            IControlFlowGraph cfg;
            if (!ControlFlowGraph.TryGet(declarationBody, context.SemanticModel, out cfg))
            {
                return;
            }

            var lva = LiveVariableAnalysis.Analyze(cfg, symbol, context.SemanticModel);

            var explodedGraph = new ExplodedGraph(cfg, symbol, context.SemanticModel, lva);
            analyze(explodedGraph, context);
        }

        public static bool HasConstraint(this ISymbol symbol, SymbolicValueConstraint constraint, ProgramState programState)
        {
            var symbolicValue = programState.GetSymbolValue(symbol);
            if (symbolicValue == null)
            {
                return false;
            }

            return symbolicValue.HasConstraint(constraint, programState);
        }

        public static ProgramState SetConstraint(this ISymbol symbol, SymbolicValueConstraint constraint, ProgramState programState)
        {
            var symbolicValue = programState.GetSymbolValue(symbol);
            if (symbolicValue == null ||
                symbolicValue.HasConstraint(constraint, programState))
            {
                return programState;
            }

            return symbolicValue.SetConstraint(constraint, programState);
        }
    }
}
