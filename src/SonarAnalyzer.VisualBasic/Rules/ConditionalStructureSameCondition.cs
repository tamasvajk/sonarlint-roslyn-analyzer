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

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.Helpers.VisualBasic;
using System.Collections.Generic;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    public class ConditionalStructureSameCondition : ConditionalStructureSameConditionBase
    {
        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var ifBlock = (MultiLineIfBlockSyntax)c.Node;

                    var conditions = new[] { ifBlock.IfStatement?.Condition }
                        .Concat(ifBlock.ElseIfBlocks.Select(elseIf => elseIf.ElseIfStatement?.Condition))
                        .Where(cond => cond != null)
                        .Select(cond => cond.RemoveParentheses())
                        .ToList();

                    for (int i = 1; i < conditions.Count; i++)
                    {
                        CheckConditionAt(i, conditions, c);
                    }
                },
                SyntaxKind.MultiLineIfBlock);
        }

        private static void CheckConditionAt(int currentIndex, List<ExpressionSyntax> conditions, SyntaxNodeAnalysisContext context)
        {
            for (int j = 0; j < currentIndex; j++)
            {
                if (EquivalenceChecker.AreEquivalent(conditions[currentIndex], conditions[j]))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, conditions[currentIndex].GetLocation(),
                        conditions[j].GetLineNumberToReport()));
                    return;
                }
            }
        }
    }
}
