# FuelMixer
FuelMixer is a plugin for Kerbal Space Program which provides a simple framework for maintaining consistent propellant mixtures across fuel tanks, engines and RCS thrusters.

This is an experimental plugin still very much in development, use at your own risk!

## Features
### Supported
- Automated computation of mixture ratio, thrust multiplier and Isp multiplier
- User-definable override propellant mixtures
- Monopropellant fuel tanks, engines and RCS thrusters
- Bipropellant fuel tanks, engines and RCS thrusters
### Planned
- Jet engines
- Tripropellant+ fuel tanks, engines and RCS thrusters

## Usage
### FuelMixerPropellantConfig
Any resource used as a propellant by FuelMixer requires a `FuelMixerPropellantConfig` defined in any .cfg file anywhere in your GameData directory, which takes the following fields:
- `name`: Name of the resource to use.
- `IsOxidizer`: Whether this resource acts as the oxidizer in a fuel-oxidizer bipropellant. `false` (fuel) by default.
- `MixtureConstant`: A dimensionless quantity used to determine mixture ratios of bipropellants.
- `ThrustMultiplier`: Vacuum thrust multiplier, scaled by proportion in a bipropellant mixture.
- `IspMultiplier`: Vacuum Isp multiplier, scaled by proportion in a bipropellant mixture.

For example, consider the following set of three propellants:
```
FuelMixerPropellantConfig
{
	name = Kerosene
	BipropellantComponent = fuel
	MixtureConstant = 3
	ThrustMultiplier = 1
	IspMultiplier = 1
}
FuelMixerPropellantConfig
{
	name = LqdHydrogen
	BipropellantComponent = fuel
	MixtureConstant = 15
	ThrustMultiplier = 0.7
	IspMultiplier = 1.5
}
FuelMixerPropellantConfig
{
	name = LqdOxygen
	BipropellantComponent = oxidizer
	MixtureConstant = 5
	ThrustMultiplier = 1
	IspMultiplier = 1
}
```
Any engine set up using `Kerosene` and `LqdOxygen` or `LqdHydrogen` and `LqdOxygen` will identify the presence of both a fuel and an oxidizer and create a bipropellant engine. If both `Kerosene` and `LqdHydrogen` are present, only the first propellant will be used, since multiple propellants of the same kind in a single FuelMixer is not currently supported.

All final computed parameters for the engine will be rounded to sensible levels of precision. Bipropellant mixture ratios in particular are computed by rounding the ratio of the two `MixtureConstant`s to the nearest multiple of 0.0625 or 0.1, whichever is closer. In this case, `Kerosene`+`LqdOxygen` yield a 3:5 = 0.375:0.625 ratio, while `LqdHydrogen`+`LqdOxygen` yields 15:5 = 0.75:0.25. The thrust and isp multipliers in the kerolox case are both 1, while in the hydrolox case are then computed as 0.7^0.75 * 1^0.25 = ~0.765 and 1.5^0.75 * 1^0.25 = ~1.355 respectively.

### FuelMixerPropellantCombinationConfig
The automatic computation of propellant combinations described above can be overridden by defining a `FuelMixerPropellantCombinationConfig` with the following fields:
- Any number of `PROPELLANT` nodes, though currently only monopropellants and bipropellants are supported.
- `ThrustMultiplier`: Total vacuum thrust multiplier.
- `IspMultiplier`: Total Isp thrust multiplier.

```
FuelMixerPropellantCombinationConfig
{
	PROPELLANT
	{
		name = LqdHydrogen
		ratio = 3
		drawStackGauge = True
	}
	PROPELLANT
	{
		name = LqdOxygen
		ratio = 1
	}
	IspMultiplier = 1.4
	ThrustMultiplier = 0.8
}
```

### ModuleFuelMixerPropellant
FuelMixer is implemented on a part using `ModuleFuelMixerPropellant` with the following fields:
- `moduleID`: Optional arbitrary string identifier.
- `resourceName`: Name of the resource to use, used to look up the corresponding `FuelMixerPropellantConfig` or `FuelMixerPropellantCombinationConfig` where applicable.
- `removeResource`: Name of a resource whose `RESOURCE` node will be removed from the part, for replacing existing tanks.
- `addedVolume`: Volume added to the total volume considered by the set of all active `ModuleFuelMixerPropellant`s.
- `engineID`: The `engineID` of a `ModuleEngines*` to act upon. If unspecified, the module will target the first `ModuleEngines*` it finds.

All instances of this module work together and act on stored resources, engine modules and RCS modules simultaneously.

For example, with the example propellant configs defined above, the following modules added to the LFB KR-1x2 "Twin-Boar" would convert the engine to run on 3 Kerosene : 5 LqdOxygen and change the in-built tanks to contain a total volume of 6,400 units (32,000 litres) of propellant split into 12,000 Kerosene and 20,000 LqdOxygen:
```
MODULE
{
	name = ModuleFuelMixerPropellant
	moduleID = fuelMixerFuel
	resourceName = Kerosene
	addedVolume = 2880
}
MODULE
{
	name = ModuleFuelMixerPropellant
	moduleID = fuelMixerOxidizer
	resourceName = LqdOxygen
	addedVolume = 3520
}
```

This design allows for flexibility in the control of the tank volume and resource via ModuleManager patches and switches, for example B9PartSwitch could be used to change the fuel like so:
```
MODULE
{
	name = ModuleB9PartSwitch
	SUBTYPE
	{
		name = Kerosene
		title = Kerosene
		MODULE
		{
			IDENTIFIER
			{
				name = ModuleFuelMixerPropellant
				moduleID = fuelMixerFuel
			}
			DATA
			{
				resourceName = Kerosene
			}
		}
	}
	SUBTYPE
	{
		name = LH2
		title = Liquid Hydrogen
		MODULE
		{
			IDENTIFIER
			{
				name = ModuleFuelMixerPropellant
				moduleID = fuelMixerFuel
			}
			DATA
			{
				resourceName = LqdHydrogen
			}
		}
	}
}
```

## Dependencies
- [ModuleManager (4.2.3)](https://github.com/sarbian/ModuleManager)

## License
Distributed under the GNU General Public License.