﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deveroom.VisualStudio.SpecFlowConnector.Discovery;
using Deveroom.VisualStudio.SpecFlowConnector.Models;
using Deveroom.VisualStudio.SpecFlowConnector.SourceDiscovery;
using FluentAssertions;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Bindings;
using TechTalk.SpecFlow.Bindings.Reflection;
using Xunit;

namespace Deveroom.VisualStudio.SpecFlowConnector.V1.Tests
{
    public class BaseDiscovererTests
    {
        private readonly BindingRegistry _bindingRegistry = new BindingRegistry();

        class StubDiscoverer : RemotingBaseDiscoverer
        {
            public IBindingRegistry BindingRegistry { get; set; }

            protected override IBindingRegistry GetBindingRegistry(Assembly testAssembly, string configFilePath)
            {
                return BindingRegistry;
            }

            protected override IEnumerable<IStepDefinitionBinding> GetStepDefinitions(IBindingRegistry bindingRegistry)
            {
                return bindingRegistry.GetStepDefinitions();
            }

            protected override IDeveroomSymbolReader CreateSymbolReader(string assemblyFilePath, WarningCollector warningCollector)
            {
                return new StubSymbolReader();
            }

            public DiscoveryResult DiscoverInternal(string testAssemblyPath, string configFilePath)
            {
                var testAssembly = Assembly.LoadFrom(testAssemblyPath);
                return DiscoverInternal(testAssembly, testAssemblyPath, configFilePath);
            }
        }

        class StubSymbolReader : IDeveroomSymbolReader
        {
            public static readonly List<MethodSymbol> NextSymbols = new List<MethodSymbol>();

            public static void SetNextSymbol(string fileName, int line, int column)
            {
                NextSymbols.Add(
                    new MethodSymbol(1, new []{ new SequencePoint(0, fileName, line, line, column, column) }));
            }

            public MethodSymbol ReadMethodSymbol(int token)
            {
                var symbol = NextSymbols.FirstOrDefault();
                if (NextSymbols.Any())
                    NextSymbols.RemoveAt(0);
                return symbol;
            }

            public void Dispose()
            {
            }
        }
        class CustomParamType { }

        class StubBindingClass
        {
            public void GivenACalculator() { }
            public void WhenIPressAdd() { }
            public void WhenIPressMultiply() { }
            public void ThenTheReasultShouldBeCalculated() { }
            public void WithCustomParam(CustomParamType p1) { }
            public void WithParams(string p0, CustomParamType p1, int p2, Table p3) { }
            public void WithStdParams(string p0, string p1, int p2, Table p3) { }
        }

        private StubDiscoverer CreateSut()
        {
            var stubDiscoverer = new StubDiscoverer();
            stubDiscoverer.BindingRegistry = _bindingRegistry;
            return stubDiscoverer;
        }

        private string GetTestAssemblyPath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }

        private void RegisterStepDefinitionBinding(string regex = "I press add", StepDefinitionType type = StepDefinitionType.When, string method = nameof(StubBindingClass.WhenIPressAdd), BindingScope scope = null)
        {
            var methodInfo = GetMethodInfo(method);
            _bindingRegistry.RegisterStepDefinitionBinding(new StepDefinitionBinding(type, regex, new RuntimeBindingMethod(methodInfo), scope));
        }

        private static MethodInfo GetMethodInfo(string method)
        {
            var methodInfo = typeof(StubBindingClass).GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
            methodInfo.Should().NotBeNull();
            return methodInfo;
        }

        [Fact]
        public void Discovers_step_definitions()
        {
            RegisterStepDefinitionBinding(method: nameof(StubBindingClass.WhenIPressAdd));
            RegisterStepDefinitionBinding(method: nameof(StubBindingClass.WhenIPressMultiply));

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().NotBeNullOrEmpty();
            result.StepDefinitions.Should().HaveCount(2);
        }

        [Fact]
        public void Discovers_step_definition_type()
        {
            RegisterStepDefinitionBinding(type: StepDefinitionType.Given, method: nameof(StubBindingClass.GivenACalculator));
            RegisterStepDefinitionBinding(type: StepDefinitionType.When, method: nameof(StubBindingClass.WhenIPressAdd));
            RegisterStepDefinitionBinding(type: StepDefinitionType.Then, method: nameof(StubBindingClass.ThenTheReasultShouldBeCalculated));

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(3);
            result.StepDefinitions.Should().ContainSingle(sd => sd.Type == "Given");
            result.StepDefinitions.Should().ContainSingle(sd => sd.Type == "When");
            result.StepDefinitions.Should().ContainSingle(sd => sd.Type == "Then");
        }

        [Fact]
        public void Discovers_regex()
        {
            RegisterStepDefinitionBinding(regex: "this is? a (.*) regex");

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].Regex.Should().Be("^this is? a (.*) regex$");
        }

        [Fact]
        public void Discovers_tag_scope()
        {
            RegisterStepDefinitionBinding(scope: new BindingScope("foo", null, null));

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].Scope.Tag.Should().Be("@foo");
        }

        [Fact]
        public void Discovers_feature_scope()
        {
            RegisterStepDefinitionBinding(scope: new BindingScope(null, "foo", null));

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].Scope.FeatureTitle.Should().Be("foo");
        }

        [Fact]
        public void Discovers_scenario_scope()
        {
            RegisterStepDefinitionBinding(scope: new BindingScope(null, null, "foo"));

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].Scope.ScenarioTitle.Should().Be("foo");
        }

        [Fact]
        public void Scope_is_null_for_non_scoped()
        {
            RegisterStepDefinitionBinding();

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].Scope.Should().BeNull();
        }

        [Fact]
        public void Discovers_source_location()
        {
            RegisterStepDefinitionBinding();
            StubSymbolReader.SetNextSymbol(@"C:\Temp\MyFile.cs", 12, 3);

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].SourceLocation.Should().NotBeNullOrEmpty();
            result.StepDefinitions[0].SourceLocation.Should().Contain("|12|3");
        }

        [Fact]
        public void Collects_source_files()
        {
            RegisterStepDefinitionBinding();
            StubSymbolReader.SetNextSymbol(@"C:\Temp\MyFile.cs", 12, 3);
            RegisterStepDefinitionBinding();
            StubSymbolReader.SetNextSymbol(@"C:\Temp\MyFile.cs", 16, 5);
            RegisterStepDefinitionBinding();
            StubSymbolReader.SetNextSymbol(@"C:\Temp\OtherFile.cs", 12, 3);

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(3);
            result.StepDefinitions.Should().OnlyContain(sd => sd.SourceLocation.StartsWith("#0|") || sd.SourceLocation.StartsWith("#1|"));

            result.SourceFiles.Should().ContainKeys("0", "1");
            result.SourceFiles.Should().ContainValues(@"C:\Temp\MyFile.cs", @"C:\Temp\OtherFile.cs");
        }

        [Fact]
        public void Discovers_param_types()
        {
            RegisterStepDefinitionBinding(method: nameof(StubBindingClass.WithParams));

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].ParamTypes.Should().NotBeNullOrEmpty();
            result.StepDefinitions[0].ParamTypes.Should().Contain("|");
        }

        [Fact]
        public void ParamTypes_is_null_for_stepdef_without_param()
        {
            RegisterStepDefinitionBinding(method: nameof(StubBindingClass.WhenIPressAdd));

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].ParamTypes.Should().BeNull();
        }

        [Fact]
        public void Discovers_custom_param_types_with_type_names()
        {
            RegisterStepDefinitionBinding(method: nameof(StubBindingClass.WithCustomParam));

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].ParamTypes.Should().Be("#0");
            result.TypeNames.Should().ContainValue(typeof(CustomParamType).FullName);
        }

        [Fact]
        public void Discovers_usual_param_types_with_shortcuts()
        {
            RegisterStepDefinitionBinding(method: nameof(StubBindingClass.WithStdParams));

            var sut = CreateSut();

            var result = sut.DiscoverInternal(GetTestAssemblyPath(), null);

            result.StepDefinitions.Should().HaveCount(1);
            result.StepDefinitions[0].ParamTypes.Should().Be("s|s|i|st");
            result.TypeNames.Should().HaveCount(0);
        }
    }
}
