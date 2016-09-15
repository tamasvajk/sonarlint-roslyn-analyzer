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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace SonarAnalyzer.Helpers
{
    // todo: this should come from the Roslyn API (https://github.com/dotnet/roslyn/issues/9)
    internal class MethodParameterLookup
    {
        private readonly InvocationExpressionSyntax invocation;
        public IMethodSymbol MethodSymbol { get; }

        public MethodParameterLookup(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            this.invocation = invocation;
            MethodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        }

        public static bool TryGetParameterSymbol(ArgumentSyntax argument, ArgumentListSyntax argumentList,
            IMethodSymbol method, out IParameterSymbol parameter)
        {
            parameter = null;
            if (!argumentList.Arguments.Contains(argument) ||
                method == null ||
                method.IsVararg)
            {
                return false;
            }

            if (argument.NameColon != null)
            {
                parameter = method.Parameters
                    .FirstOrDefault(symbol => symbol.Name == argument.NameColon.Name.Identifier.ValueText);
                return parameter != null;
            }

            var argumentIndex = argumentList.Arguments.IndexOf(argument);
            var parameterIndex = argumentIndex;

            if (parameterIndex >= method.Parameters.Length)
            {
                var lastParameter = method.Parameters.Last();
                parameter = lastParameter.IsParams ? lastParameter : null;
                return parameter != null;
            }
            parameter = method.Parameters[parameterIndex];
            return true;
        }

        public bool TryGetParameterSymbol(ArgumentSyntax argument, out IParameterSymbol parameter)
        {
            return TryGetParameterSymbol(argument, invocation.ArgumentList, MethodSymbol, out parameter);
        }

        internal IEnumerable<ArgumentParameterMapping> GetAllArgumentParameterMappings()
        {
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                IParameterSymbol parameter;
                if (TryGetParameterSymbol(argument, out parameter))
                {
                    yield return new ArgumentParameterMapping(argument, parameter);
                }
            }
        }

        public class ArgumentParameterMapping
        {
            public ArgumentSyntax Argument { get; set; }
            public IParameterSymbol Parameter { get; set; }

            public ArgumentParameterMapping(ArgumentSyntax argument, IParameterSymbol parameter)
            {
                Argument = argument;
                Parameter = parameter;
            }
        }
    }
}
