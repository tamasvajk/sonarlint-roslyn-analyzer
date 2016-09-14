/*
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

using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SonarLint.Helpers
{
    public static class DiagnosticReportHelper
    {
        #region Line Number

        public static int GetLineNumberToReport(this SyntaxNode self)
        {
            return self.GetLocation().GetLineNumberToReport();
        }

        public static int GetLineNumberToReport(this Diagnostic self)
        {
            return self.Location.GetLineNumberToReport();
        }

        private static int GetLineNumberToReport(this Location self)
        {
            return self.GetLineSpan().StartLinePosition.GetLineNumberToReport();
        }

        public static int GetLineNumberToReport(this LinePosition self)
        {
            return self.Line + 1;
        }

        #endregion

        #region Help link

        private const string HelpLinkPattern = "http://vs.sonarlint.org/rules/index.html#version={0}&ruleId={1}";
        public static string GetHelpLink(this string ruleId)
        {
            var productVersion = FileVersionInfo.GetVersionInfo(typeof(DiagnosticReportHelper).Assembly.Location).FileVersion;
            return string.Format(HelpLinkPattern, productVersion, ruleId);
        }

        #endregion
    }
}