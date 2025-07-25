# 1.2.0
- Added TweakScale compatibility
- Changed from single (float) to double precision for internal computation
- Fixed a couple of bugs surrounding `ModuleIgnitionTankController` added mass and cost
# 1.1.4
- Restricted `ModuleIgnitionTankController` to only add its configured resources to its host part in the editor
  - Fixes various issues which keep arising with resource amounts not being properly maintained
- Added removal of zero-volume resources from part when `OnStart` is called
# 1.1.3.1
- Fixed removal of previous resource when `ModuleIgnitionTankController` is reloaded which was causing a massive time complexity issue for reasons I do not fully understand
# 1.1.3
- Fixed multiple `ModuleIgnitionTankController`s controlling volumes of the same resource
# 1.1.2
- Fixed `ModuleIgnitionTankController` added mass and cost computation
- Fixed loading of `drawStackGauge` field on `PROPELLANT` nodes in a `PropellantCombinationConfig`
# 1.1.1
- Normalised auto-computed bipropellant ratios so they play nicer with additional externally configured propellants
- Fixed pass-through of existing propellant nodes
- Minor improvements to avoid null propellant config exceptions
# 1.1.0
- Fixed isp key querying
- Fixed "none" tank type mass
- Changed behaviour of existing `PROPELLANT` nodes on `ModuleEngines` and `ModuleRCS` so that they are no longer removed automatically and now follow through into the new configuration
- Changed setup of `MaxThrustOriginal`, `IspVacuumOriginal` and `IspSeaLevelOriginal` so that they can now be overridden via patch if required
# 1.0.1
- Fixed single-propellant engines undergoing ignition simulation
  - A single propellant cannot "ignite" in the strict sense, so the engine should light without ignition simulation
- Fixed part info recompilation for multi-mode engines
- Changed litre symbol from "l" to "L" for clarity
# 1.0.0
- Release
- Added part tool tip info recompilation in the VAB, so engines, RCS and tanks now properly show their default configuration
- Added ability to ignite unthrottlable engines
- Added `ModuleIgnitionEngineController`, `ModuleIgnitionRCSController`, `ModuleIgnitionTankController`
  - These handle all the actual computation, leaving `ModuleIgnitionPropellant` as a lightweight proxy module for allowing e.g. B9 part switching of individual propellants
- Removed `ModuleIgnitor` as ignition simulation is now handled by `ModuleIgnitionEngineController`
- Fixed retention of resource amount when switching subtypes and reloading a scene
- Fixed ignition resource display in part action window
- Fixed various small bugs in ignition simulation logic
- Fixed stack gauge display
# 0.3.0
- Added engine thrust and isp to part action window
- Added ModuleIgnitor and IgnitorResource
  - The basic logic for engine ignition was loosely based on the mod EngineIgnitor, so many thanks to HoneyFox, Riocrokite, DennyTX and linuxgurugamer for their work on that plugin!
# 0.2.0
- Added support for ModuleEngines which use velocity curve rather than atmosphere curve, i.e. jet engines
- Added retention of `ModuleFuelMixerModulePropellant` resource amount when switching another module with a `maxAmount` greater than or equal to the current amount
- Fixed `ModuleFuelMixerModulePropellant` mass and cost computation
- Added proper support for explicit mass and cost definition via `addedVolume`, `addedMass` and `addedCost` parameters
# 0.1.0
- Pre-release