Alibre Import STL as STEP (Alibre Add‑On)

Overview
- This repository contains an Alibre Design add‑on that converts an STL file to a STEP (.stp) file using an external converter (stltostp.exe) and then imports the result into Alibre.
- The add‑on exposes a menu/command inside Alibre Design and shows a progress dialog while the conversion runs. Users can cancel the operation.

Tech stack
- Language: C# (LangVersion 10)
- Framework: .NET Framework 4.8.1 (TargetFramework net481)
- Desktop UI: Windows Forms (OpenFileDialog, SaveFileDialog, Form, Button)
- Host: Alibre Design Add‑On API (interfaces like IADRoot, IAlibreAddOn, IADSession)
- External tool: stltostp.exe (bundled under AlibreImportStlAsStep/StlToStep)
- Build system: SDK‑style .csproj; can be built with dotnet SDK or MSBuild (Visual Studio)
- Package manager: Uses SDK‑style project with implicit NuGet restore; external Alibre DLLs are referenced by path

Key components and entry points
- AlibreImportStlAsStep/AlibreAddOn.cs
  - Static class used by Alibre to load the add‑on (AddOnLoad) and provide the add‑on interface (GetAddOnInterface).
- AlibreImportStlAsStep/AlibreImportOpen.cs
  - Implements the Alibre add‑on menu/command surface. Core flow:
    1) Prompt user to open an STL file.
    2) Prompt user where to save the STEP file.
    3) Run stltostp.exe with progress and cancel support.
    4) On success, proceeds with import (hosted by Alibre session).
- AlibreImportStlAsStep/Globals.cs
  - Provides application name and icon; reads an install path from the Windows registry under HKLM (see below).
- External binary
  - AlibreImportStlAsStep/StlToStep/stltostp.exe

Requirements
- Windows (required by Alibre and .NET Framework).
- .NET SDK with targeting pack for .NET Framework 4.8.1 (or Visual Studio 2022 configured to build net481).
- Alibre Design and Alibre Add‑On SDK/assemblies available locally.
  - The project references these external assemblies by relative HintPath:
    - AlibreAddOn.dll
    - AlibreX.dll
  - TODO: Document how to obtain and place these DLLs (current HintPath in the project points outside this repository: ..\..\..\..\AlibreLib\).
- Permissions to install/register add‑ons in Alibre (likely requires administrative rights for writing to Program Files or HKLM registry).

Setup (build)
Option A: Visual Studio
- Open AlibreImportStlAsStep.sln.
- Select configuration (Debug/Release) and build.

Option B: dotnet CLI
- Ensure the .NET SDK with net481 targeting pack is installed.
- Run from the repository root:
  - dotnet restore
  - dotnet build AlibreImportStlAsStep.sln -c Release

Build outputs
- Compiled DLL and content (icons, .adc, stltostp.exe) are copied to bin/<Config>/net481/.

Install and run inside Alibre
- This is an add‑on; it does not run as a standalone executable.
- Typical steps to install an Alibre add‑on:
  1) Copy the built files (from bin/<Config>/net481/) to the add‑ons installation folder recognized by Alibre.
  2) Ensure the .adc file (AlibreImportStlAsStep.adc) and required content (icons; StlToStep\stltostp.exe) are present.
  3) Restart Alibre Design; the add‑on should appear in the UI with its command/menu.
- TODO: Provide the exact add‑ons folder path(s) for the target Alibre version(s) (e.g., %ProgramData% vs %ProgramFiles%, per‑user vs machine‑wide).
- TODO: Confirm whether the add‑on uses a dedicated ribbon tab or existing menus for the final placement of the command.

Registry and icons
- Globals.cs expects to find an InstallPath under:
  - HKLM\SOFTWARE\Alibre Design Add‑Ons\{378829C4-F122-5217-92E0-E36ADD4F9AA8}
- It then loads the icon from <InstallPath>\\3DPrint.ico.
- Ensure your installer or manual installation writes this key and places the icon/file accordingly.
- TODO: Document how the GUID maps to this add‑on entry in Alibre and whether this is configurable per version.

Installer script
- AlibreImportStlAsStep/AlibreImportStlAsStepl.iss (Inno Setup)
  - TODO: Verify the filename ("AlibreImportStlAsStepl.iss" may be a typo of "...Step1.iss").
  - TODO: Provide packaging steps and required Inno Setup version if this installer is intended to be used.

Configuration and environment variables
- No required environment variables are defined in code.
- Optional environment considerations:
  - PATH is not required for stltostp.exe because it is distributed alongside the add‑on and invoked via a resolved path.
  - If you relocate AlibreAddOn.dll/AlibreX.dll, update the .csproj HintPath or provide a NuGet/package reference if available.

Scripts
- No custom build scripts are included.
- Writerside directory exists for documentation configuration:
  - Writerside/writerside.cfg, c.list, v.list
  - TODO: Add instructions for building/publishing docs with Writerside, if applicable.

Testing
- There are currently no automated tests in this repository.
- Manual test scenario:
  1) Build and install the add‑on as described above.
  2) Launch Alibre Design.
  3) Invoke the add‑on command.
  4) Choose an input .stl file.
  5) Confirm the save location for the .stp file.
  6) Verify conversion occurs and the resulting STEP imports without errors.
  7) Test cancellation behavior in the progress dialog.

Project structure (selected)
- AlibreImportStlAsStep.sln — solution file.
- AlibreImportStlAsStep/
  - AlibreImportStlAsStep.csproj — project file (net481, references AlibreAddOn.dll, AlibreX.dll, Windows Forms).
  - AlibreAddOn.cs — add‑on loader/entry for Alibre host.
  - AlibreImportOpen.cs — add‑on UI/command logic, calls stltostp.exe.
  - AlibreImportStlAsStep.adc — add‑on configuration file for Alibre (copied to output).
  - StlToStep/stltostp.exe — external converter (copied to output).
  - 3DPrint.ico, 3DPrint.svg, nexus.ico — icons/artifacts (copied to output as configured).
  - Globals.cs — icon/registry helpers.
  - AlibreImportStlAsStepl.iss — Inno Setup script (installer) [needs verification].
  - Copyright and License.txt — license and attributions.
- Writerside/ — documentation config (Writerside).

Usage notes
- The add‑on displays file dialogs for input (STL) and output (STEP), and a wait form with a Cancel button during conversion.
- If stltostp.exe cannot be found in the expected location, the add‑on shows an error message. Ensure the file exists under StlToStep in the installed add‑on directory.

Known limitations / TODO
- Document precise Alibre add‑on installation steps and default install paths for supported Alibre versions.
- Verify and document the installer script usage and any required registry entries.
- Provide guidance for distributing stltostp.exe in accordance with its license (see below).
- Consider adding automated tests (if feasible for parts that are outside the Alibre host).

License
- See "AlibreImportStlAsStep/Copyright and License.txt" for licensing terms of this project and third‑party acknowledgements.
- stltostp.exe is credited to slugdev; source: https://github.com/slugdev/stltostp (license terms included in the license file).