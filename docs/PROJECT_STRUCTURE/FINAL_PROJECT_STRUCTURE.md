# Final Project Structure After Runtime-Prefab Refactor

- Repository: `de-vis-proj-group8-main-audit`
- Revision: `working tree on rework/runtime-prefab-conv`
- File count: 619
- Generated: 2026-08-11 07:40:07 +02:00

Unity caches, local logs, builds, and other ignored machine output are intentionally excluded.

```text
.gitignore
.vsconfig
AGENTS.md
Assets/absorber column.fbx
Assets/absorber column.fbx.meta
Assets/AbsorberController.cs
Assets/AbsorberController.cs.meta
Assets/CameraController.cs
Assets/CameraController.cs.meta
Assets/co2 tank.fbx
Assets/co2 tank.fbx.meta
Assets/compressor_block.fbx
Assets/compressor_block.fbx.meta
Assets/condenser.fbx
Assets/condenser.fbx.meta
Assets/deabsorber column.fbx
Assets/deabsorber column.fbx.meta
Assets/distillation column.fbx
Assets/distillation column.fbx.meta
Assets/Editor.meta
Assets/Editor/ProjectInventory.cs
Assets/Editor/ProjectInventory.cs.meta
Assets/Editor/ReleaseValidation.cs
Assets/Editor/ReleaseValidation.cs.meta
Assets/Editor/RuntimePrefabConversion.cs
Assets/Editor/RuntimePrefabConversion.cs.meta
Assets/Editor/WindowsBuild.cs
Assets/Editor/WindowsBuild.cs.meta
Assets/electrolyzer box.fbx
Assets/electrolyzer box.fbx.meta
Assets/flash sep skirt.fbx
Assets/flash sep skirt.fbx.meta
Assets/flash separator.fbx
Assets/flash separator.fbx.meta
Assets/gear.png
Assets/gear.png.meta
Assets/Green.mat
Assets/Green.mat.meta
Assets/Ground.mat
Assets/Ground.mat.meta
Assets/h2 tank.fbx
Assets/h2 tank.fbx.meta
Assets/Icons.meta
Assets/Icons/gear.png
Assets/Icons/gear.png.meta
Assets/InputSystem_Actions.inputactions
Assets/InputSystem_Actions.inputactions.meta
Assets/Mat_AmineLean.mat
Assets/Mat_AmineLean.mat.meta
Assets/Mat_AmineRich.mat
Assets/Mat_AmineRich.mat.meta
Assets/Mat_CO2Flow.mat
Assets/Mat_CO2Flow.mat.meta
Assets/Mat_H2Flow.mat
Assets/Mat_H2Flow.mat.meta
Assets/Mat_MeOHLiquid.mat
Assets/Mat_MeOHLiquid.mat.meta
Assets/Mat_ReactorEffluent.mat
Assets/Mat_ReactorEffluent.mat.meta
Assets/Mat_RecycleVapor.mat
Assets/Mat_RecycleVapor.mat.meta
Assets/Mat_Syngas.mat
Assets/Mat_Syngas.mat.meta
Assets/Materials_N.meta
Assets/Materials_N/Environment.meta
Assets/Materials_N/Environment/Generated Asphalt.mat
Assets/Materials_N/Environment/Generated Asphalt.mat.meta
Assets/Materials_N/Environment/Generated Brushed Steel.mat
Assets/Materials_N/Environment/Generated Brushed Steel.mat.meta
Assets/Materials_N/Environment/Generated CO2 Pipe Blue.mat
Assets/Materials_N/Environment/Generated CO2 Pipe Blue.mat.meta
Assets/Materials_N/Environment/Generated Concrete.mat
Assets/Materials_N/Environment/Generated Concrete.mat.meta
Assets/Materials_N/Environment/Generated Control Room Blue Grey.mat
Assets/Materials_N/Environment/Generated Control Room Blue Grey.mat.meta
Assets/Materials_N/Environment/Generated Distant Ground.mat
Assets/Materials_N/Environment/Generated Distant Ground.mat.meta
Assets/Materials_N/Environment/Generated Fence Dark Steel.mat
Assets/Materials_N/Environment/Generated Fence Dark Steel.mat.meta
Assets/Materials_N/Environment/Generated H2 Pipe Green.mat
Assets/Materials_N/Environment/Generated H2 Pipe Green.mat.meta
Assets/Materials_N/Environment/Generated Hazard Marking.mat
Assets/Materials_N/Environment/Generated Hazard Marking.mat.meta
Assets/Materials_N/Environment/Generated Methanol Pipe Purple.mat
Assets/Materials_N/Environment/Generated Methanol Pipe Purple.mat.meta
Assets/Materials_N/Environment/Generated Safety Yellow.mat
Assets/Materials_N/Environment/Generated Safety Yellow.mat.meta
Assets/methanol tank.fbx
Assets/methanol tank.fbx.meta
Assets/orange.mat
Assets/orange.mat.meta
Assets/OrbitCameraController.cs
Assets/OrbitCameraController.cs.meta
Assets/OverviewPanelController.cs
Assets/OverviewPanelController.cs.meta
Assets/pipe bend.fbx
Assets/pipe bend.fbx.meta
Assets/PipeFlow.shader
Assets/PipeFlow.shader.meta
Assets/PipeFlow_Dashes.png
Assets/PipeFlow_Dashes.png.meta
Assets/PipeFlowAnimator.cs
Assets/PipeFlowAnimator.cs.meta
Assets/PlantPipeManager.cs
Assets/PlantPipeManager.cs.meta
Assets/Plinth.mat
Assets/Plinth.mat.meta
Assets/Prefabs_N.meta
Assets/Prefabs_N/Environment.meta
Assets/Prefabs_N/Environment/IndustrialPlantEnvironment.prefab
Assets/Prefabs_N/Environment/IndustrialPlantEnvironment.prefab.meta
Assets/Prefabs_N/Flow.meta
Assets/Prefabs_N/Flow/H2Particle.prefab
Assets/Prefabs_N/Flow/H2Particle.prefab.meta
Assets/Prefabs_N/Runtime.meta
Assets/Prefabs_N/Runtime/Systems.meta
Assets/Prefabs_N/Runtime/Systems/PlantRuntimeServices.prefab
Assets/Prefabs_N/Runtime/Systems/PlantRuntimeServices.prefab.meta
Assets/Prefabs_N/Runtime/UI.meta
Assets/Prefabs_N/Runtime/UI/SafetyWarningOverlay.prefab
Assets/Prefabs_N/Runtime/UI/SafetyWarningOverlay.prefab.meta
Assets/Reactor base model 1.fbx
Assets/Reactor base model 1.fbx.meta
Assets/Reactor base model.fbx
Assets/Reactor base model.fbx.meta
Assets/reactor steel skirt.fbx
Assets/reactor steel skirt.fbx.meta
Assets/ReactorController.cs
Assets/ReactorController.cs.meta
Assets/ReactorPanelToggle.cs
Assets/ReactorPanelToggle.cs.meta
Assets/Readme.asset
Assets/Readme.asset.meta
Assets/Resources.meta
Assets/Resources/PtMeOHParticleUnlit.shader
Assets/Resources/PtMeOHParticleUnlit.shader.meta
Assets/saddle support.fbx
Assets/saddle support.fbx.meta
Assets/Scenes.meta
Assets/Scenes/SampleScene.unity
Assets/Scenes/SampleScene.unity.meta
Assets/Scripts_N.meta
Assets/Scripts_N/AutoFlow.meta
Assets/Scripts_N/FinalFlowSystem.meta
Assets/Scripts_N/FinalFlowSystem/CatalystBedColorAnimator.cs
Assets/Scripts_N/FinalFlowSystem/CatalystBedColorAnimator.cs.meta
Assets/Scripts_N/FinalFlowSystem/FinalPlantFlowRuntime.cs
Assets/Scripts_N/FinalFlowSystem/FinalPlantFlowRuntime.cs.meta
Assets/Scripts_N/FinalFlowSystem/LightweightReactorVisual.cs
Assets/Scripts_N/FinalFlowSystem/LightweightReactorVisual.cs.meta
Assets/Scripts_N/FinalFlowSystem/PlantFlowKind.cs
Assets/Scripts_N/FinalFlowSystem/PlantFlowKind.cs.meta
Assets/Scripts_N/FlowFollower.cs
Assets/Scripts_N/FlowFollower.cs.meta
Assets/Scripts_N/FlowPath.cs
Assets/Scripts_N/FlowPath.cs.meta
Assets/Scripts_N/IcodosDashboardRuntime.cs
Assets/Scripts_N/IcodosDashboardRuntime.cs.meta
Assets/Scripts_N/InteractiveModulePanelRuntime.cs
Assets/Scripts_N/InteractiveModulePanelRuntime.cs.meta
Assets/Scripts_N/PipeWaypointGenerator.cs
Assets/Scripts_N/PipeWaypointGenerator.cs.meta
Assets/Scripts_N/PlantEnvironmentBuilder.cs
Assets/Scripts_N/PlantEnvironmentBuilder.cs.meta
Assets/Scripts_N/PlantProcessSimulator.cs
Assets/Scripts_N/PlantProcessSimulator.cs.meta
Assets/Scripts_N/RuntimeValidationCapture.cs
Assets/Scripts_N/RuntimeValidationCapture.cs.meta
Assets/Scripts_N/SafetyWarningRuntime.cs
Assets/Scripts_N/SafetyWarningRuntime.cs.meta
Assets/Settings.meta
Assets/Settings/DefaultVolumeProfile.asset
Assets/Settings/DefaultVolumeProfile.asset.meta
Assets/Settings/Mobile_Renderer.asset
Assets/Settings/Mobile_Renderer.asset.meta
Assets/Settings/Mobile_RPAsset.asset
Assets/Settings/Mobile_RPAsset.asset.meta
Assets/Settings/PC_Renderer.asset
Assets/Settings/PC_Renderer.asset.meta
Assets/Settings/PC_RPAsset.asset
Assets/Settings/PC_RPAsset.asset.meta
Assets/Settings/SampleSceneProfile.asset
Assets/Settings/SampleSceneProfile.asset.meta
Assets/Settings/UniversalRenderPipelineGlobalSettings.asset
Assets/Settings/UniversalRenderPipelineGlobalSettings.asset.meta
Assets/steel skirt.fbx
Assets/steel skirt.fbx.meta
Assets/t junction.fbx
Assets/t junction.fbx.meta
Assets/tank saddle.fbx
Assets/tank saddle.fbx.meta
Assets/TextMesh Pro.meta
Assets/TextMesh Pro/Examples & Extras.meta
Assets/TextMesh Pro/Examples & Extras/Fonts.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Anton OFL.txt
Assets/TextMesh Pro/Examples & Extras/Fonts/Anton OFL.txt.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Anton.ttf
Assets/TextMesh Pro/Examples & Extras/Fonts/Anton.ttf.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers - OFL.txt
Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers - OFL.txt.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf
Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Electronic Highway Sign.TTF
Assets/TextMesh Pro/Examples & Extras/Fonts/Electronic Highway Sign.TTF.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Oswald-Bold - OFL.txt
Assets/TextMesh Pro/Examples & Extras/Fonts/Oswald-Bold - OFL.txt.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Oswald-Bold.ttf
Assets/TextMesh Pro/Examples & Extras/Fonts/Oswald-Bold.ttf.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold - AFL.txt
Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold - AFL.txt.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold - License.txt
Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold - License.txt.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf
Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Unity - OFL.txt
Assets/TextMesh Pro/Examples & Extras/Fonts/Unity - OFL.txt.meta
Assets/TextMesh Pro/Examples & Extras/Fonts/Unity.ttf
Assets/TextMesh Pro/Examples & Extras/Fonts/Unity.ttf.meta
Assets/TextMesh Pro/Examples & Extras/Materials.meta
Assets/TextMesh Pro/Examples & Extras/Materials/Crate - Surface Shader Scene.mat
Assets/TextMesh Pro/Examples & Extras/Materials/Crate - Surface Shader Scene.mat.meta
Assets/TextMesh Pro/Examples & Extras/Materials/Crate - URP.mat
Assets/TextMesh Pro/Examples & Extras/Materials/Crate - URP.mat.meta
Assets/TextMesh Pro/Examples & Extras/Materials/Ground - Logo Scene.mat
Assets/TextMesh Pro/Examples & Extras/Materials/Ground - Logo Scene.mat.meta
Assets/TextMesh Pro/Examples & Extras/Materials/Ground - Surface Shader Scene.mat
Assets/TextMesh Pro/Examples & Extras/Materials/Ground - Surface Shader Scene.mat.meta
Assets/TextMesh Pro/Examples & Extras/Materials/Ground - URP.mat
Assets/TextMesh Pro/Examples & Extras/Materials/Ground - URP.mat.meta
Assets/TextMesh Pro/Examples & Extras/Materials/Small Crate_diffuse.mat
Assets/TextMesh Pro/Examples & Extras/Materials/Small Crate_diffuse.mat.meta
Assets/TextMesh Pro/Examples & Extras/Prefabs.meta
Assets/TextMesh Pro/Examples & Extras/Prefabs/Text Popup.prefab
Assets/TextMesh Pro/Examples & Extras/Prefabs/Text Popup.prefab.meta
Assets/TextMesh Pro/Examples & Extras/Prefabs/TextMeshPro - Prefab 1.prefab
Assets/TextMesh Pro/Examples & Extras/Prefabs/TextMeshPro - Prefab 1.prefab.meta
Assets/TextMesh Pro/Examples & Extras/Prefabs/TextMeshPro - Prefab 2.prefab
Assets/TextMesh Pro/Examples & Extras/Prefabs/TextMeshPro - Prefab 2.prefab.meta
Assets/TextMesh Pro/Examples & Extras/Resources.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Color Gradient Presets.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Color Gradient Presets/Blue to Purple - Vertical.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Color Gradient Presets/Blue to Purple - Vertical.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Color Gradient Presets/Dark to Light Green - Vertical.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Color Gradient Presets/Dark to Light Green - Vertical.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Color Gradient Presets/Light to Dark Green - Vertical.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Color Gradient Presets/Light to Dark Green - Vertical.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Color Gradient Presets/Yellow to Orange - Vertical.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Color Gradient Presets/Yellow to Orange - Vertical.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF - Drop Shadow.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF - Drop Shadow.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF - Outline.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF - Outline.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF - Sunny Days.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF - Sunny Days.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF - Drop Shadow - 2 Pass.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF - Drop Shadow - 2 Pass.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF - Drop Shadow.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF - Drop Shadow.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF - Outline.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF - Outline.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF Glow.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF Glow.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF Logo - URP.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF Logo - URP.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF Logo.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF Logo.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/LiberationSans SDF - Metalic Green.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/LiberationSans SDF - Metalic Green.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/LiberationSans SDF - Overlay.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/LiberationSans SDF - Overlay.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/LiberationSans SDF - Soft Mask.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/LiberationSans SDF - Soft Mask.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Oswald Bold SDF.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Oswald Bold SDF.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF - Drop Shadow.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF - Drop Shadow.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF - HDRP Unlit.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF - HDRP Unlit.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF - Surface.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF - Surface.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF - URP.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF - URP.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Unity SDF - HDRP LIT - Bloom.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Unity SDF - HDRP LIT - Bloom.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Unity SDF - HDRP LIT - Outline.mat
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Unity SDF - HDRP LIT - Outline.mat.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Unity SDF.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Unity SDF.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Sprite Assets.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Sprite Assets/Default Sprite Asset.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Sprite Assets/Default Sprite Asset.asset.meta
Assets/TextMesh Pro/Examples & Extras/Resources/Sprite Assets/DropCap Numbers.asset
Assets/TextMesh Pro/Examples & Extras/Resources/Sprite Assets/DropCap Numbers.asset.meta
Assets/TextMesh Pro/Examples & Extras/Scenes.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/01-  Single Line TextMesh Pro.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/01-  Single Line TextMesh Pro.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/02 - Multi-line TextMesh Pro.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/02 - Multi-line TextMesh Pro.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/03 - Line Justification.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/03 - Line Justification.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/04 - Word Wrapping.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/04 - Word Wrapping.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/05 - Style Tags.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/05 - Style Tags.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/06 - Extra Rich Text Examples.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/06 - Extra Rich Text Examples.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/07 - Superscript & Subscript Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/07 - Superscript & Subscript Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/08 - Improved Text Alignment.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/08 - Improved Text Alignment.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/09 - Margin Tag Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/09 - Margin Tag Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/10 - Bullets & Numbered List Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/10 - Bullets & Numbered List Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/11 - The Style Tag.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/11 - The Style Tag.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/12 - Link Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/12 - Link Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/12a - Text Interactions.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/12a - Text Interactions.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/13 - Soft Hyphenation.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/13 - Soft Hyphenation.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/14 - Multi Font & Sprites.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/14 - Multi Font & Sprites.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/15 - Inline Graphics & Sprites.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/15 - Inline Graphics & Sprites.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/16 - Linked text overflow mode example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/16 - Linked text overflow mode example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/17 - Old Computer Terminal.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/17 - Old Computer Terminal.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/18 - ScrollRect & Masking & Layout.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/18 - ScrollRect & Masking & Layout.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/19 - Masking Texture & Soft Mask.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/19 - Masking Texture & Soft Mask.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/20 - Input Field with Scrollbar.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/20 - Input Field with Scrollbar.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/21 - Script Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/21 - Script Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/22 - Basic Scripting Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/22 - Basic Scripting Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/23 - Animating Vertex Attributes.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/23 - Animating Vertex Attributes.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/24 - Surface Shader Example URP.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/24 - Surface Shader Example URP.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/24 - Surface Shader Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/24 - Surface Shader Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/25 - Sunny Days Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/25 - Sunny Days Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/26 - Dropdown Placeholder Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/26 - Dropdown Placeholder Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/27 - Double Pass Shader Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/27 - Double Pass Shader Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/28 - HDRP Shader Example.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/28 - HDRP Shader Example.unity
Assets/TextMesh Pro/Examples & Extras/Scenes/28 - HDRP Shader Example.unity.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/28 - HDRP Shader Example/Sky and Fog Volume Profile.asset
Assets/TextMesh Pro/Examples & Extras/Scenes/28 - HDRP Shader Example/Sky and Fog Volume Profile.asset.meta
Assets/TextMesh Pro/Examples & Extras/Scenes/Benchmark (Floating Text).unity
Assets/TextMesh Pro/Examples & Extras/Scenes/Benchmark (Floating Text).unity.meta
Assets/TextMesh Pro/Examples & Extras/Scripts.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark01.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark01.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark01_UGUI.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark01_UGUI.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark02.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark02.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark03.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark03.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark04.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/Benchmark04.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/ChatController.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/ChatController.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/DropdownSample.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/DropdownSample.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/EnvMapAnimator.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/EnvMapAnimator.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/ObjectSpin.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/ObjectSpin.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/ShaderPropAnimator.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/ShaderPropAnimator.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/SimpleScript.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/SimpleScript.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/SkewTextExample.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/SkewTextExample.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TeleType.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TeleType.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TextConsoleSimulator.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TextConsoleSimulator.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TextMeshProFloatingText.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TextMeshProFloatingText.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TextMeshSpawner.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TextMeshSpawner.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_DigitValidator.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_DigitValidator.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_ExampleScript_01.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_ExampleScript_01.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_FrameRateCounter.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_FrameRateCounter.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_PhoneNumberValidator.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_PhoneNumberValidator.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextEventCheck.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextEventCheck.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextEventHandler.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextEventHandler.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextInfoDebugTool.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextInfoDebugTool.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextSelector_A.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextSelector_A.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextSelector_B.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextSelector_B.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_UiFrameRateCounter.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_UiFrameRateCounter.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/TMPro_InstructionOverlay.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/TMPro_InstructionOverlay.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexColorCycler.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexColorCycler.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexJitter.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexJitter.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexShakeA.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexShakeA.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexShakeB.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexShakeB.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexZoom.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/VertexZoom.cs.meta
Assets/TextMesh Pro/Examples & Extras/Scripts/WarpTextExample.cs
Assets/TextMesh Pro/Examples & Extras/Scripts/WarpTextExample.cs.meta
Assets/TextMesh Pro/Examples & Extras/Sprites.meta
Assets/TextMesh Pro/Examples & Extras/Sprites/Default Sprites.png
Assets/TextMesh Pro/Examples & Extras/Sprites/Default Sprites.png.meta
Assets/TextMesh Pro/Examples & Extras/Sprites/DropCap Numbers.psd
Assets/TextMesh Pro/Examples & Extras/Sprites/DropCap Numbers.psd.meta
Assets/TextMesh Pro/Examples & Extras/Textures.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Brushed Metal 3.jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Brushed Metal 3.jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Floor Cement.jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Floor Cement.jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Floor Tiles 1 - diffuse.jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Floor Tiles 1 - diffuse.jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Fruit Jelly (B&W).jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Fruit Jelly (B&W).jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Gradient Diagonal (Color).jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Gradient Diagonal (Color).jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Gradient Horizontal (Color).jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Gradient Horizontal (Color).jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Gradient Vertical (Color).jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Gradient Vertical (Color).jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Mask Zig-n-Zag.psd
Assets/TextMesh Pro/Examples & Extras/Textures/Mask Zig-n-Zag.psd.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Small Crate_diffuse.jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Small Crate_diffuse.jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Small Crate_normal.jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Small Crate_normal.jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Sunny Days - Seamless.jpg
Assets/TextMesh Pro/Examples & Extras/Textures/Sunny Days - Seamless.jpg.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Text Overflow - Linked Text Image 1.png
Assets/TextMesh Pro/Examples & Extras/Textures/Text Overflow - Linked Text Image 1.png.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Text Overflow - Linked Text UI Screenshot.png
Assets/TextMesh Pro/Examples & Extras/Textures/Text Overflow - Linked Text UI Screenshot.png.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Circle.psd
Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Circle.psd.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Diagonal.psd
Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Diagonal.psd.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Radial Double.psd
Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Radial Double.psd.meta
Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Radial Quad.psd
Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Radial Quad.psd.meta
Assets/TextMesh Pro/Fonts.meta
Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt
Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt.meta
Assets/TextMesh Pro/Fonts/LiberationSans.ttf
Assets/TextMesh Pro/Fonts/LiberationSans.ttf.meta
Assets/TextMesh Pro/Resources.meta
Assets/TextMesh Pro/Resources/Fonts & Materials.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/AbsorberOutputMaterial.mat
Assets/TextMesh Pro/Resources/Fonts & Materials/AbsorberOutputMaterial.mat.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/LEDFont1.mat
Assets/TextMesh Pro/Resources/Fonts & Materials/LEDFont1.mat.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/LEDFont2.mat
Assets/TextMesh Pro/Resources/Fonts & Materials/LEDFont2.mat.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/LEDFOnt3.mat
Assets/TextMesh Pro/Resources/Fonts & Materials/LEDFOnt3.mat.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/LEDFont4.mat
Assets/TextMesh Pro/Resources/Fonts & Materials/LEDFont4.mat.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset.meta
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.mat
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.mat.meta
Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt
Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt.meta
Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt
Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt.meta
Assets/TextMesh Pro/Resources/Style Sheets.meta
Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset
Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset.meta
Assets/TextMesh Pro/Resources/TMP Settings.asset
Assets/TextMesh Pro/Resources/TMP Settings.asset.meta
Assets/TextMesh Pro/Shaders.meta
Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl
Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl.meta
Assets/TextMesh Pro/Shaders/TMP_Bitmap.shader
Assets/TextMesh Pro/Shaders/TMP_Bitmap.shader.meta
Assets/TextMesh Pro/Shaders/TMP_Bitmap-Custom-Atlas.shader
Assets/TextMesh Pro/Shaders/TMP_Bitmap-Custom-Atlas.shader.meta
Assets/TextMesh Pro/Shaders/TMP_Bitmap-Mobile.shader
Assets/TextMesh Pro/Shaders/TMP_Bitmap-Mobile.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader
Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader
Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF.shader
Assets/TextMesh Pro/Shaders/TMP_SDF.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP LIT.shadergraph
Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP LIT.shadergraph.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP UNLIT.shadergraph
Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP UNLIT.shadergraph.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD.shader
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile-2-Pass.shader
Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile-2-Pass.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-Surface.shader
Assets/TextMesh Pro/Shaders/TMP_SDF-Surface.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-Surface-Mobile.shader
Assets/TextMesh Pro/Shaders/TMP_SDF-Surface-Mobile.shader.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-URP Lit.shadergraph
Assets/TextMesh Pro/Shaders/TMP_SDF-URP Lit.shadergraph.meta
Assets/TextMesh Pro/Shaders/TMP_SDF-URP Unlit.shadergraph
Assets/TextMesh Pro/Shaders/TMP_SDF-URP Unlit.shadergraph.meta
Assets/TextMesh Pro/Shaders/TMP_Sprite.shader
Assets/TextMesh Pro/Shaders/TMP_Sprite.shader.meta
Assets/TextMesh Pro/Shaders/TMPro.cginc
Assets/TextMesh Pro/Shaders/TMPro.cginc.meta
Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc
Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc.meta
Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc
Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc.meta
Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc
Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc.meta
Assets/TutorialInfo.meta
Assets/TutorialInfo/Icons.meta
Assets/TutorialInfo/Icons/URP.png
Assets/TutorialInfo/Icons/URP.png.meta
Assets/TutorialInfo/Layout.wlt
Assets/TutorialInfo/Layout.wlt.meta
Assets/TutorialInfo/Scripts.meta
Assets/TutorialInfo/Scripts/Editor.meta
Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs
Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs.meta
Assets/TutorialInfo/Scripts/Readme.cs
Assets/TutorialInfo/Scripts/Readme.cs.meta
docs/ARCHITECTURE_AND_ASSET_POLICY.md
docs/DEVELOPMENT_NOTES/UnityProjectContext.md
docs/DEVELOPMENT_NOTES/UnityProjectHealth.md
docs/FINAL_PROJECT_REPORT.md
docs/images/equipment-detail-platforms.jpeg
docs/images/reactor-ui-prototype.png
docs/images/unity-full-plant-game-view.png
docs/images/unity-full-plant-scene-overview.jpeg
docs/IMPLEMENTATION_REFERENCE.md
docs/PROFESSOR_EVALUATION_GUIDE.md
docs/progress-screenshots.md
docs/PROJECT_STRUCTURE/FINAL_PROJECT_STRUCTURE.md
docs/PROJECT_STRUCTURE/README.md
docs/REFERENCES_AND_ASSET_PROVENANCE.md
docs/REPRODUCIBILITY_AND_BUILD.md
docs/RUNTIME_PREFAB_CONVERSION.md
docs/SUBMISSION_READINESS_CHECKLIST.md
docs/TOOLS_AND_ASSISTANCE.md
Packages/manifest.json
Packages/packages-lock.json
ProjectSettings/AudioManager.asset
ProjectSettings/ClusterInputManager.asset
ProjectSettings/DynamicsManager.asset
ProjectSettings/EditorBuildSettings.asset
ProjectSettings/EditorSettings.asset
ProjectSettings/GraphicsSettings.asset
ProjectSettings/InputManager.asset
ProjectSettings/MemorySettings.asset
ProjectSettings/MultiplayerManager.asset
ProjectSettings/NavMeshAreas.asset
ProjectSettings/PackageManagerSettings.asset
ProjectSettings/Physics2DSettings.asset
ProjectSettings/PresetManager.asset
ProjectSettings/ProjectSettings.asset
ProjectSettings/ProjectVersion.txt
ProjectSettings/QualitySettings.asset
ProjectSettings/SceneTemplateSettings.json
ProjectSettings/ShaderGraphSettings.asset
ProjectSettings/TagManager.asset
ProjectSettings/TimeManager.asset
ProjectSettings/UnityConnectSettings.asset
ProjectSettings/URPProjectSettings.asset
ProjectSettings/VersionControlSettings.asset
ProjectSettings/VFXManager.asset
ProjectSettings/XRSettings.asset
README.md
tools/Export-RepositoryStructure.ps1
```
