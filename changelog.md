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