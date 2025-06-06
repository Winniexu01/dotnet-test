# Developer Guide

The following document describes the setup and workflow that is recommended for working on the Windows Forms project. It assumes that you have read the [Contributing Guide](../CONTRIBUTING.md).

The [Issue Guide](issue-guide.md) describes our approach to using GitHub issues.

## Machine Setup

Windows Forms development requires the following workloads and components be selected when installing Visual Studio 2022 (17.0.0):

* Required Workloads:
  * .NET Desktop Development
  * Desktop development with C++
* [Required Individual Components][required-individual-components]:
  * Windows 10 SDK
  * C++ CMake tools for Windows
* Required Windows Features
  * 'Media Features' including 'Windows Media Player Legacy (App)' (Windows 11) or 'Windows Media Player' (Windows 10) 
* Useful Visual Studio extensions:  
  * [Editor guidelines](https://marketplace.dev.azure.com/items?itemName=PaulHarrington.EditorGuidelines), see https://github.com/dotnet/winforms/pull/4836 for more information
  * [VS Color Output](https://marketplace.dev.azure.com/items?itemName=MikeWard-AnnArbor.VSColorOutput64)
  * [Productivity Power Tools](https://marketplace.dev.azure.com/items?itemName=VisualStudioPlatformTeam.ProductivityPowerPack2022)
 
Optional: To get the latest C# features in Visual Studio or to experiment with the latest runtime when not using the `start-vs.cmd`, install the [latest SDK build](https://github.com/dotnet/dotnet/blob/main/docs/builds-table.md).

## Workflow

We use the following workflow for building as well as testing features and fixes.

You first need to [fork][fork] then [clone][clone] this Windows Forms repository. This is a one-time task.

1. [Build](building.md) the repository.
2. [Debug](debugging.md) the change, as needed.
3. [Test](testing.md) the change, to validate quality.

## More Information

* [How to write good docs for .NET](https://review.docs.microsoft.com/help/contribute-ref/how-to-write-net-docs?branch=main)
* [.NET Docs and Guidelines][net-runtime-instructions]
* ["help wanted" issues][help wanted]

[comment]: <> (URI Links)

[net-runtime-instructions]: https://github.com/dotnet/runtime/tree/master/docs
[fork]: https://guides.github.com/activities/forking/
[clone]: https://www.git-scm.com/docs/git-clone
[help wanted]: https://github.com/dotnet/winforms/issues?q=is%3Aopen+is%3Aissue+label%3A"help%20wanted"
[chocolatey]: https://chocolatey.org/
[cmake-download]: https://cmake.org/download/
[required-individual-components]: ../WinForms.vsconfig
