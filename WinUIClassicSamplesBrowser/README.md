# WinClassicSamplesCS
A duplication in C# of the [Windows-classic-samples](https://github.com/Microsoft/Windows-classic-samples) using [Vanara](https://github.com/dahall/Vanara) libraries, this time using latest WinUI technology.

## Project Intent
* Test and validate that the structures, methods and interfaces in Vanara using known code and outcomes.
* Demonstrate the use of the Vanara libraries in a side-by-side model with the native C++ code.
* Exposed gaps in the Vanara libraries for future development.

## [Work in Progress]: Add WinUI 3 based Samples.

13/03/2016 Working on, and trying to integrate WinUI3 Samples. Including Browser.

- https://github.com/electrifier/vamara-demo-browser/


# WinUI Classic Samples Browser

A Sample Application for browsing the [WinClassicSamplesCS](https://github.com/dahall/WinClassicSamplesCS) Gallery, built with Template Studio for WinUI.

## Table of Contents
- [Prerequisites](#prerequisites)
- [Getting Started with Template Studio for WinUI](#getting-started-with-template-studio-for-winui)

## Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the following workloads:
  - Universal Windows Platform development
  - .NET desktop development
  - Desktop development with C++
- [Windows App SDK](https://docs.microsoft.com/windows/apps/windows-app-sdk/) version 1.4 or later

The following libraries and tools are used in this project:
- [Vanara](https://github.com/dahall/Vanara) A set of .NET libraries for Windows implementing PInvoke calls to many native Windows APIs with supporting wrappers.
- [WinUI 3](https://docs.microsoft.com/windows/apps/winui/) for building the user interface, including the following Extensions:
  - [Template Studio for WinUI](https://marketplace.visualstudio.com/items?itemName=VisualStudioClient.MicrosoftTemplateStudio) Visual Studio extension
  - [WinUIEx](https://dotmorten.github.io/WinUIEx/) WinUI Extensions library


## Getting Started with Template Studio for WinUI

Browse and address `TODO:` comments in `View -> Task List` to learn the codebase and understand next steps for turning the generated code into production code.

Explore the [WinUI Gallery](https://www.microsoft.com/store/productId/9P3JFPWWDZRC) to learn about available controls and design patterns.

Relaunch Template Studio to modify the project by right-clicking on the project in `View -> Solution Explorer` then selecting `Add -> New Item (Template Studio)`.

## Publishing

For projects with MSIX packaging, right-click on the application project and select `Package and Publish -> Create App Packages...` to create an MSIX package.

For projects without MSIX packaging, follow the [deployment guide](https://docs.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps) or add the `Self-Contained` Feature to enable xcopy deployment.

## CI Pipelines

See [README.md](https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/pipelines/README.md) for guidance on building and testing projects in CI pipelines.

## Changelog

See [releases](https://github.com/microsoft/TemplateStudio/releases) and [milestones](https://github.com/microsoft/TemplateStudio/milestones).

## Feedback

Bugs and feature requests should be filed at https://aka.ms/templatestudio.
