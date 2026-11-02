param(
    [string]$EditorData = 'C:/Program Files/Unity/Hub/Editor/6000.3.12f1/Editor/Data',
    [string]$UnityProject = ''
)
$ErrorActionPreference = 'Stop'
$bundle = Split-Path -Parent $PSScriptRoot
$output = Join-Path $bundle '.verification'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$dotnet = Join-Path $EditorData 'NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $EditorData 'DotNetSdkRoslyn/csc.dll'
$references = @(Get-ChildItem -LiteralPath (Join-Path $EditorData 'NetStandard/ref/2.1.0') -Filter '*.dll') +
              @(Get-ChildItem -LiteralPath (Join-Path $EditorData 'Managed/UnityEngine') -Filter 'UnityEngine*.dll')
$referenceArgs = @($references | ForEach-Object { '-r:"' + $_.FullName + '"' })
$sourceArgs = @(Get-ChildItem -LiteralPath (Join-Path $bundle 'Assets/VRComfortStudy/Scripts') -Recurse -Filter '*.cs' |
    ForEach-Object { '"' + $_.FullName + '"' })
$library = Join-Path $output 'VRComfortStudy.Runtime.dll'
$runtimeRsp = Join-Path $output 'runtime.rsp'
@('-nologo','-target:library','-langversion:9.0','-nostdlib+',('-out:"' + $library + '"')) + $referenceArgs + $sourceArgs |
    Set-Content -LiteralPath $runtimeRsp -Encoding utf8
& $dotnet $compiler ('@' + $runtimeRsp)
if ($LASTEXITCODE -ne 0) { throw 'Runtime compile failed.' }

# Also compile Editor helpers and the actual development-overlay branch against Unity's real editor assemblies.
$editorRefs = @(Get-ChildItem -LiteralPath (Join-Path $EditorData 'Managed') -Filter 'UnityEditor*.dll') +
              @(Get-ChildItem -LiteralPath (Join-Path $EditorData 'Managed/UnityEngine') -Filter 'UnityEditor*.dll')
$editorRsp = Join-Path $output 'editor.rsp'
@('-nologo','-target:library','-langversion:9.0','-nostdlib+','-define:UNITY_EDITOR',
    ('-out:"' + (Join-Path $output 'VRComfortStudy.EditorCheck.dll') + '"')) + $referenceArgs +
    @($editorRefs | ForEach-Object { '-r:"' + $_.FullName + '"' }) + $sourceArgs +
    @('"' + (Join-Path $bundle 'Assets/VRComfortStudy/Editor/StudySetupMenu.cs') + '"') |
    Set-Content -LiteralPath $editorRsp -Encoding utf8
& $dotnet $compiler ('@' + $editorRsp)
if ($LASTEXITCODE -ne 0) { throw 'Editor compile failed.' }

if ($UnityProject) {
    $xriAssembly = Join-Path $UnityProject 'Library/ScriptAssemblies/Unity.XR.Interaction.Toolkit.dll'
    if (-not (Test-Path -LiteralPath $xriAssembly)) { throw 'Open the Unity project once to compile XRI assemblies.' }
    $xriRsp = Join-Path $output 'xri.rsp'
    @('-nologo','-target:library','-langversion:9.0','-nostdlib+','-define:VR_COMFORT_XRI3',
        ('-out:"' + (Join-Path $output 'VRComfortStudy.XRI.dll') + '"'),
        ('-r:"' + $library + '"'), ('-r:"' + $xriAssembly + '"')) + $referenceArgs +
        @('"' + (Join-Path $bundle 'Assets/VRComfortStudy/Scripts/Integrations/XriVignetteOutput.cs') + '"') |
        Set-Content -LiteralPath $xriRsp -Encoding utf8
    & $dotnet $compiler ('@' + $xriRsp)
    if ($LASTEXITCODE -ne 0) { throw 'Actual installed XRI bridge compile failed.' }
    $inputAssembly = Join-Path $UnityProject 'Library/ScriptAssemblies/Unity.InputSystem.dll'
    $inputRsp = Join-Path $output 'input.rsp'
    @('-nologo','-target:library','-langversion:9.0','-nostdlib+','-define:VR_COMFORT_INPUT',
        ('-out:"' + (Join-Path $output 'VRComfortStudy.Input.dll') + '"'),
        ('-r:"' + $library + '"'), ('-r:"' + $inputAssembly + '"')) + $referenceArgs +
        @('"' + (Join-Path $bundle 'Assets/VRComfortStudy/Scripts/InputIntegration/StudyInputActions.cs') + '"') |
        Set-Content -LiteralPath $inputRsp -Encoding utf8
    & $dotnet $compiler ('@' + $inputRsp)
    if ($LASTEXITCODE -ne 0) { throw 'Actual installed Input System compile failed.' }
}

$testDll = Join-Path $output 'NumericLogicTests.dll'
$testRsp = Join-Path $output 'tests.rsp'
@('-nologo','-target:exe','-langversion:9.0','-nostdlib+',('-out:"' + $testDll + '"'),
    ('-r:"' + $library + '"')) + $referenceArgs +
    @('"' + (Join-Path $PSScriptRoot 'NumericLogicTests.cs') + '"') |
    Set-Content -LiteralPath $testRsp -Encoding utf8
& $dotnet $compiler ('@' + $testRsp)
if ($LASTEXITCODE -ne 0) { throw 'Test compile failed.' }
'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.21"}}}' |
    Set-Content -LiteralPath (Join-Path $output 'NumericLogicTests.runtimeconfig.json') -Encoding utf8
& $dotnet $testDll
if ($LASTEXITCODE -ne 0) { throw 'Numeric tests failed.' }
Write-Output 'Compile and numeric checks passed. Actual scene/HMD behavior requires validation in Unity.'
