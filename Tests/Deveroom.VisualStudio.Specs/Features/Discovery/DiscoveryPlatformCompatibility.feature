Feature: Discovery - Platform Compatibility

Scenario Outline: Discover bindings from a SpecFlow v3 project in different .NET Core platforms
	Given there is a simple SpecFlow project for v3.*
	And the project uses the new project format
	And the target framework is <framework>
	And the project is built
	When the binding discovery performed
	Then the discovery succeeds with several step definitions
Examples: 
	| framework     |
	| netcoreapp3.1 |
	| net5.0        |
#@ignoreci
#Examples: Old .NET Core
#	| framework     |
#	| netcoreapp2.1 |

@ignoreci
Scenario Outline: Discover bindings from a SpecFlow v3 project in different .NET Core platforms (Old .NET Core)
	Given there is a simple SpecFlow project for v3.*
	And the project uses the new project format
	And the target framework is <framework>
	And the project is built
	When the binding discovery performed
	Then the discovery succeeds with several step definitions
Examples: Old .NET Core
	| framework     |
	| netcoreapp2.1 |
