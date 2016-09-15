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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using Microsoft.CodeAnalysis;

namespace SonarAnalyzer.Utilities
{
    public class RuleFinder
    {
        private readonly List<Type> diagnosticAnalyzers;

        public static IEnumerable<Assembly> PackagedRuleAssemblies => new[]
            {
                Assembly.LoadFrom(typeof(Rules.CSharp.FlagsEnumZeroMember).Assembly.Location),
                Assembly.LoadFrom(typeof(Rules.VisualBasic.FlagsEnumZeroMember).Assembly.Location),
                Assembly.LoadFrom(typeof(Rules.Common.FlagsEnumZeroMemberBase).Assembly.Location)
            };

        public RuleFinder()
        {
            diagnosticAnalyzers = PackagedRuleAssemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => t.IsSubclassOf(typeof (DiagnosticAnalyzer)))
                .Where(t => t.GetCustomAttributes<RuleAttribute>().Any())
                .ToList();
        }

        public IEnumerable<Type> GetParameterlessAnalyzerTypes(AnalyzerLanguage language)
        {
            return diagnosticAnalyzers
                .Where(analyzerType =>
                    !analyzerType.GetProperties()
                        .Any(p => p.GetCustomAttributes<RuleParameterAttribute>().Any()))
                .Where(type => GetTargetLanguages(type).IsAlso(language));
        }

        public static bool IsParameterized(Type analyzerType)
        {
            return analyzerType.GetProperties()
                .Any(p => p.GetCustomAttributes<RuleParameterAttribute>().Any());
        }

        public IEnumerable<Type> GetAllAnalyzerTypes()
        {
            return diagnosticAnalyzers;
        }
        public IEnumerable<Type> GetAnalyzerTypes(AnalyzerLanguage language)
        {
            return diagnosticAnalyzers
                .Where(type => GetTargetLanguages(type).IsAlso(language));
        }

        public static AnalyzerLanguage GetTargetLanguages(Type analyzerType)
        {
            var attribute = analyzerType.GetCustomAttributes<DiagnosticAnalyzerAttribute>().FirstOrDefault();
            if (attribute == null)
            {
                return null;
            }

            var language = AnalyzerLanguage.None;
            foreach (var lang in attribute.Languages)
            {
                switch (lang)
                {
                    case LanguageNames.CSharp:
                        language = language.AddLanguage(AnalyzerLanguage.CSharp);
                        break;
                    case LanguageNames.VisualBasic:
                        language = language.AddLanguage(AnalyzerLanguage.VisualBasic);
                        break;
                    default:
                        break;
                }
            }

            return language;
        }
    }
}