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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace SonarAnalyzer.UnitTest.Helpers
{
    [TestClass]
    public class TypeHelperTest
    {
        private ClassDeclarationSyntax baseClassDeclaration;
        private ClassDeclarationSyntax derivedClassDeclaration1;
        private ClassDeclarationSyntax derivedClassDeclaration2;
        private SemanticModel semanticModel;

        [TestInitialize]
        public void Compile()
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = workspace.CurrentSolution.AddProject("foo", "foo.dll", LanguageNames.CSharp)
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                    .AddDocument("test", SymbolHelperTest.TestInput);
                var compilation = document.Project.GetCompilationAsync().Result;
                var tree = compilation.SyntaxTrees.First();
                semanticModel = compilation.GetSemanticModel(tree);

                baseClassDeclaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == "Base");
                derivedClassDeclaration1 = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == "Derived1");
                derivedClassDeclaration2 = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == "Derived2");
            }
        }

        [TestMethod]
        public void Type_DerivesOrImplementsAny()
        {
            var ctor = typeof(KnownType).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Single(m => m.GetParameters().Length == 1);

            var baseType = (KnownType)ctor.Invoke(new object[] { "NS.Base" });
            var interfaceType = (KnownType)ctor.Invoke(new object[] { "NS.IInterface" });

            var derived1Type = semanticModel.GetDeclaredSymbol(derivedClassDeclaration1) as INamedTypeSymbol;
            var derived2Type = semanticModel.GetDeclaredSymbol(derivedClassDeclaration2) as INamedTypeSymbol;

            Assert.IsTrue(derived2Type.DerivesOrImplements(interfaceType));
            Assert.IsFalse(derived1Type.DerivesOrImplements(interfaceType));

            var baseTypes = new HashSet<KnownType>(new[] { interfaceType, baseType });
            Assert.IsTrue(derived1Type.DerivesOrImplementsAny(baseTypes));
        }

        [TestMethod]
        public void Type_Is()
        {
            var ctor = typeof(KnownType).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Single(m => m.GetParameters().Length == 1);

            var baseKnownType = (KnownType)ctor.Invoke(new object[] { "NS.Base" });
            var baseKnownTypes = new HashSet<KnownType>(new[] { baseKnownType });

            var baseType = semanticModel.GetDeclaredSymbol(baseClassDeclaration) as INamedTypeSymbol;

            Assert.IsTrue(baseType.Is(baseKnownType));
            Assert.IsTrue(baseType.IsAny(baseKnownTypes));
        }
    }
}
