﻿using System.Collections.Generic;
using System.Linq;
using Deveroom.VisualStudio.Diagonostics;
using Deveroom.VisualStudio.Discovery;
using Deveroom.VisualStudio.Editor.Services.Parser;
using Deveroom.VisualStudio.SpecFlowConnector.Models;
using FluentAssertions;
using Xunit;

namespace Deveroom.VisualStudio.Tests.Discovery
{
    public class BindingImporterTests
    {
        private readonly Dictionary<string, string> _sourceFiles = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _typeNames = new Dictionary<string, string>();

        private BindingImporter CreateSut()
        {
            return new BindingImporter(_sourceFiles, _typeNames, new DeveroomNullLogger());
        }

        private StepDefinition CreateStepDefinition(string regex = null, string type = null, string sourceLocation = null, StepScope scope = null, string paramTypes = null, string method = null)
        {
            return new StepDefinition
            {
                Method = method ?? "M1",
                Type = type ?? "Given",
                Regex = regex ?? "regex",
                SourceLocation = sourceLocation,
                Scope = scope,
                ParamTypes = paramTypes
            };
        }

        [Fact]
        public void Parses_regex_with_full_match()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(regex: "^my step$"));

            result.Regex.Should().NotBeNull();
            result.Regex.ToString().Should().Be("^my step$");
        }

        [Fact]
        public void Parses_regex_with_partial_match()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(regex: "my step"));

            result.Regex.Should().NotBeNull();
            result.Regex.ToString().Should().Be("my step");
        }

        [Fact]
        public void Parses_type()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(type: "When"));

            result.StepDefinitionType.Should().Be(ScenarioBlock.When);
        }

        [Fact]
        public void Parses_source_location_start_only()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(sourceLocation: "MyClass.cs|2|5"));

            result.Implementation.SourceLocation.Should().NotBeNull();
            result.Implementation.SourceLocation.SourceFile.Should().Be("MyClass.cs");
            result.Implementation.SourceLocation.SourceFileLine.Should().Be(2);
            result.Implementation.SourceLocation.SourceFileColumn.Should().Be(5);
            result.Implementation.SourceLocation.HasEndPosition.Should().BeFalse();
        }

        [Fact]
        public void Parses_source_location()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(sourceLocation: "MyClass.cs|2|5|4|7"));

            result.Implementation.SourceLocation.Should().NotBeNull();
            result.Implementation.SourceLocation.SourceFile.Should().Be("MyClass.cs");
            result.Implementation.SourceLocation.SourceFileLine.Should().Be(2);
            result.Implementation.SourceLocation.SourceFileColumn.Should().Be(5);
            result.Implementation.SourceLocation.HasEndPosition.Should().BeTrue();
            result.Implementation.SourceLocation.SourceFileEndLine.Should().Be(4);
            result.Implementation.SourceLocation.SourceFileEndColumn.Should().Be(7);
        }

        [Fact]
        public void Parses_source_location_from_file_reference()
        {
            _sourceFiles.Add("1", "MyClass.cs");
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(sourceLocation: "#1|2|5"));

            result.Implementation.SourceLocation.Should().NotBeNull();
            result.Implementation.SourceLocation.SourceFile.Should().Be("MyClass.cs");
        }

        [Fact]
        public void Parses_source_location_without_column()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(sourceLocation: "MyClass.cs|2"));

            result.Implementation.SourceLocation.Should().NotBeNull();
            result.Implementation.SourceLocation.SourceFileLine.Should().Be(2);
            result.Implementation.SourceLocation.SourceFileColumn.Should().Be(1);
        }

        [Fact]
        public void Parses_source_location_without_line_and_column()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(sourceLocation: "MyClass.cs"));

            result.Implementation.SourceLocation.Should().NotBeNull();
            result.Implementation.SourceLocation.SourceFileLine.Should().Be(1);
            result.Implementation.SourceLocation.SourceFileColumn.Should().Be(1);
        }

        [Fact]
        public void Parses_step_definition_without_scope()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(scope: null));

            result.Scope.Should().BeNull();
        }

        [Fact]
        public void Parses_step_definition_tag_scope()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(scope: new StepScope { Tag = "@mytag" }));

            result.Scope.Should().NotBeNull();
            result.Scope.Tag.Should().NotBeNull();
            result.Scope.Tag.ToString().Should().Be("@mytag");
        }

        [Fact]
        public void Parses_single_parameter_type()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(paramTypes: "MyNamespace.MyType"));

            result.Implementation.ParameterTypes.Should().NotBeNull();
            result.Implementation.ParameterTypes.Should().HaveCount(1);
            result.Implementation.ParameterTypes[0].Should().Be("MyNamespace.MyType");
        }

        [Fact]
        public void Parses_type_name_with_assembly()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(paramTypes: "MyNamespace.MyType, MyAssembly"));

            result.Implementation.ParameterTypes.Should().NotBeNull();
            result.Implementation.ParameterTypes.Should().HaveCount(1);
            result.Implementation.ParameterTypes[0].Should().Be("MyNamespace.MyType, MyAssembly");
        }

        [Fact]
        public void Parses_parameter_shortcuts()
        {
            var sut = CreateSut();
            var shortcuts = TypeShortcuts.FromShortcut.ToArray();
            var result = sut.ImportStepDefinition(CreateStepDefinition(paramTypes: string.Join("|", shortcuts.Select(s => s.Key))));

            result.Implementation.ParameterTypes.Should().NotBeNull();
            result.Implementation.ParameterTypes.Should().Equal(shortcuts.Select(s => s.Value));
        }

        [Theory, 
            InlineData("s"),
            InlineData("i"),
            InlineData("s|s"),
            InlineData("st"),
        ]
        public void Parse_usual_parameters_optimized(string paramTypes)
        {
            var sut = CreateSut();
            var result1 = sut.ImportStepDefinition(CreateStepDefinition(paramTypes: paramTypes));
            var result2 = sut.ImportStepDefinition(CreateStepDefinition(paramTypes: paramTypes));

            result2.Implementation.ParameterTypes.Should().BeSameAs(result1.Implementation.ParameterTypes);
        }

        [Fact]
        public void Parses_parameter_types()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(paramTypes: "MyNamespace.MyType | MyNamespace.OtherType"));

            result.Implementation.ParameterTypes.Should().NotBeNull();
            result.Implementation.ParameterTypes.Should().HaveCount(2);
            result.Implementation.ParameterTypes[0].Should().Be("MyNamespace.MyType");
            result.Implementation.ParameterTypes[1].Should().Be("MyNamespace.OtherType");
        }

        [Fact]
        public void Parses_parameter_types_from_external_list()
        {
            _typeNames.Add("1", "MyNamespace.MyType");
            _typeNames.Add("2", "MyNamespace.OtherType");
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(paramTypes: "#1 | #2"));

            result.Implementation.ParameterTypes.Should().NotBeNull();
            result.Implementation.ParameterTypes.Should().HaveCount(2);
            result.Implementation.ParameterTypes[0].Should().Be("MyNamespace.MyType");
            result.Implementation.ParameterTypes[1].Should().Be("MyNamespace.OtherType");
        }

        [Fact]
        public void Parses_null_parameter_type_as_empty_array()
        {
            var sut = CreateSut();
            var result = sut.ImportStepDefinition(CreateStepDefinition(paramTypes: null));

            result.Implementation.ParameterTypes.Should().NotBeNull();
            result.Implementation.ParameterTypes.Should().HaveCount(0);
        }

        [Fact]
        public void Merges_implementations()
        {
            var sut = CreateSut();
            var result1 = sut.ImportStepDefinition(CreateStepDefinition(method: "MyMethod", regex: "R1"));
            var result2 = sut.ImportStepDefinition(CreateStepDefinition(method: "MyMethod", regex: "R2"));

            result1.Implementation.Should().BeSameAs(result2.Implementation);
        }
    }
}
