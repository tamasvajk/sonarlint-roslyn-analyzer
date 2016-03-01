﻿/*
 * SonarLint for Visual Studio
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
using Microsoft.CodeAnalysis.Diagnostics;
using System;

namespace SonarLint.Helpers
{
    public static class DiagnosticAnalyzerContextHelper
    {
        #region Register*ActionInNonGenerated

        public static void RegisterSyntaxNodeActionInNonGenerated<TLanguageKindEnum>(
            this AnalysisContext context,
            Action<SyntaxNodeAnalysisContext> action,
            params TLanguageKindEnum[] syntaxKinds) where TLanguageKindEnum : struct
        {
            context.RegisterSyntaxNodeActionInNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, action, syntaxKinds);
        }

        public static void RegisterSyntaxNodeActionInNonGenerated<TLanguageKindEnum>(
            this ParameterLoaderAnalysisContext context,
            Action<SyntaxNodeAnalysisContext> action,
            params TLanguageKindEnum[] syntaxKinds) where TLanguageKindEnum : struct
        {
            context.RegisterSyntaxNodeActionInNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, action, syntaxKinds);
        }

        public static void RegisterSyntaxNodeActionInNonGenerated<TLanguageKindEnum>(
            this CompilationStartAnalysisContext context,
            Action<SyntaxNodeAnalysisContext> action,
            params TLanguageKindEnum[] syntaxKinds) where TLanguageKindEnum : struct
        {
            context.RegisterSyntaxNodeActionInNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, action, syntaxKinds);
        }

        public static void RegisterSyntaxTreeActionInNonGenerated(
            this AnalysisContext context,
            Action<SyntaxTreeAnalysisContext> action)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, action);
        }

        public static void RegisterSyntaxTreeActionInNonGenerated(
            this ParameterLoaderAnalysisContext context,
            Action<SyntaxTreeAnalysisContext> action)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, action);
        }

        public static void RegisterCodeBlockStartActionInNonGenerated<TLanguageKindEnum>(
            this AnalysisContext context,
            Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action) where TLanguageKindEnum : struct
        {
            context.RegisterCodeBlockStartActionInNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, action);
        }

        public static void RegisterCodeBlockStartActionInNonGenerated<TLanguageKindEnum>(
            this ParameterLoaderAnalysisContext context,
            Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action) where TLanguageKindEnum : struct
        {
            context.RegisterCodeBlockStartActionInNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, action);
        }

        #endregion

        #region ReportDiagnosticIfNonGenerated

        public static void ReportDiagnosticIfNonGenerated(
            this CompilationAnalysisContext context,
            Diagnostic diagnostic,
            Compilation compilation)
        {
            context.ReportDiagnosticIfNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, diagnostic, compilation);
        }

        public static void ReportDiagnosticIfNonGenerated(
            this SymbolAnalysisContext context,
            Diagnostic diagnostic,
            Compilation compilation)
        {
            context.ReportDiagnosticIfNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, diagnostic, compilation);
        }

        public static void ReportDiagnosticIfNonGenerated(
            this SymbolAnalysisContext context,
            Diagnostic diagnostic)
        {
            context.ReportDiagnosticIfNonGenerated(CSharp.GeneratedCodeRecognizer.Instance, diagnostic, context.Compilation);
        }

        #endregion
    }
}
