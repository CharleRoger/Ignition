# 1.0.1
- Fixed single-propellant engines undergoing ignition simulation
  - A single propellant cannot "ignite" in the strict sense, so the engine should light without ignition simulation
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