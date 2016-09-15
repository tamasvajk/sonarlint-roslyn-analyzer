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

using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using SonarAnalyzer.Common;

namespace SonarAnalyzer.Runner
{
    public static class CompilationHelper
    {
        private static readonly MetadataReference SystemMetadataReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        public static Solution GetSolutionFromFiles(string filePath, AnalyzerLanguage language)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var file = new FileInfo(filePath);
                var lang = language == AnalyzerLanguage.CSharp ? LanguageNames.CSharp : LanguageNames.VisualBasic;
                var project = workspace.CurrentSolution.AddProject("foo", "foo.dll", lang)
                    .AddMetadataReference(SystemMetadataReference);

                var document = project.AddDocument(file.Name, File.ReadAllText(file.FullName, Encoding.UTF8));
                project = document.Project;

                return project.Solution;
            }
        }

        public static Solution GetSolutionWithEmptyFile(AnalyzerLanguage language)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var lang = language == AnalyzerLanguage.CSharp ? LanguageNames.CSharp : LanguageNames.VisualBasic;
                return workspace.CurrentSolution.AddProject("foo", "foo.dll", lang)
                    .AddMetadataReference(SystemMetadataReference)
                    .AddDocument($"foo.{language.GetFileExtension()}", string.Empty)
                    .Project
                    .Solution;
            }
        }
    }
}
