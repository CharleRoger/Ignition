# Ignition
Ignition is a plugin for Kerbal Space Program which provides a simple framework for requiring engine ignitor resources and maintaining consistent propellant mixtures across fuel tanks, engines and RCS thrusters. This plugin was developed specifically for the mod [Chemical Propulsion](https://github.com/CharleRoger/ChemicalPropulsion), but is designed in a generic way and may find uses elsewhere. The engine ignitor logic was based on the mod EngineIgnitor, so many thanks to HoneyFox, Riocrokite, DennyTX and linuxgurugamer for their work on that plugin!

This is an experimental plugin still very much in development, use at your own risk!

## Features
### Supported
- Configurable engine ignitors which can require a resource
- Automated computation of bipropellant ignition potential, mixture ratio, thrust multiplier and Isp multiplier
- User-definable override propellant mixtures
- Monopropellant fuel tanks, engines and RCS thrusters
- Bipropellant fuel tanks, engines and RCS thrusters
- Jet engines
### Planned
- Tripropellant+ fuel tanks, engines and RCS thrusters
- Relevant stats displayed in the PAW

## Usage
### IgnitionPropellantConfig
Any resource used as a propellant by Ignition requires a `IgnitionPropellantConfig` defined in any .cfg file anywhere in your GameData directory, which takes the following fields:
- `name`: Name of the resource to use.
- `IsOxidizer`: Whether this resource acts as the oxidizer in a fuel-oxidizer bipropellant. `false` (fuel) by default.
- `MixtureConstant`: A dimensionless quantity used to determine mixture ratios of bipropellants.
- `ThrustMultiplier`: Vacuum thrust multiplier, scaled by proportion in a bipropellant mixture.
- `IspMultiplier`: Vacuum Isp multiplier, scaled by proportion in a bipropellant mixture.
- `IgnitionPotential`: A dimensionless quantity representing how readily the propellant will ignite with another, scaled by proportion in a bipropellant mixture.

For example, consider the following set of three propellants:
```
IgnitionPropellantConfig
{
	name = Hydrazine
	MixtureConstant = 5
	ThrustMultiplier = 1.05
	IspMultiplier = 1.12
	IgnitionPotential = 1.2
}
IgnitionPropellantConfig
{
	name = NTO
	IsOxidizer = true
	MixtureConstant = 6
	ThrustMultiplier = 1.05
	IspMultiplier = 0.92
	IgnitionPotential = 1.2
}
IgnitionPropellantConfig
{
	name = LqdOxygen
	IsOxidizer = true
	MixtureConstant = 5
	ThrustMultiplier = 1
	IspMultiplier = 1
	IgnitionPotential = 0.6
}
```
Any engine set up using `Hydrazine` and `NTO` or `Hydrazine` and `LqdOxygen` will identify the presence of both a fuel and an oxidizer and create a bipropellant engine. If both `NTO` and `LqdOxygen` are present, only the first propellant will be used, since the use of multiple propellants of the same kind in a single engine is not currently supported.

All final computed parameters for the engine will be rounded to sensible levels of precision. Bipropellant mixture ratios in particular are computed by rounding the ratio of the two `MixtureConstant`s to the nearest multiple of 0.0625 or 0.1, whichever is closer. In this case, `Hydrazine`+`NTO` have a 5:6 ratio which is rounded to 7:9 = 0.4375:0.5625, while `Hydrazine`+`LqdOxygen` yields 5:5 = 1:1. The thrust multiplier, isp multiplier and ignition potential in the NTO case are calculated as follows:
- Thrust: 1.05^0.4375 * 1.05^0.5625 = 1.05
- Isp: 1.12^0.4375 * 0.92^0.5625 = ~1.003
- Ignition potential: 1.2^0.4375 * 1.2^0.5625 = 1.2
And in the LqdOxygen case:
- Thrust: 1.05^0.5 * 1^0.5 = ~1.025
- Isp: 1.12^0.5 * 1^0.5 = ~1.058
- Ignition potential: 1.2^0.5 * 0.6^0.5 = ~0.849

The thrust and isp multipliers are applied to the base stats of the engine, while the ignition potential is used to determine whether the engine ignites. `Hydrazine`+`NTO` yield an ignition potential of 1.2, which is greater than 1, so this combination ignites hypergolically. `Hydrazine`+`LqdOxygen` only reaches ~0.849, so cannot ignite alone and require additional ignition potential from a `ModuleIgnitor` (see below).

### IgnitionPropellantCombinationConfig
The automatic computation of propellant combinations described above can be overridden by defining a `IgnitionPropellantCombinationConfig` with the following fields:
- Any number of `PROPELLANT` nodes, though currently only monopropellants and bipropellants are supported.
- `ThrustMultiplier`: Total vacuum thrust multiplier.
- `IspMultiplier`: Total Isp thrust multiplier.
- `IgnitionPotential`: Total ignition potential.

```
IgnitionPropellantCombinationConfig
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
	IgnitionPotential = 0.7
}
```

### ModuleIgnitor
`ModuleIgnitor` is used to add ignition constraints or provide ignition potential to an engine. It can be configured with any number of `IGNITOR_RESOURCE`s, with the following fields:
- `name` = Name of the resource to use.
- `Amount` = Absolute amount of resource to use.
- `ScaledAmount` = Approximate amount of resource scaled to the max mass flow rate of the engine, used if `Amount` is not defined.
- `AddedIgnitionPotential` = Ignition potential added to that computed from the propellant combination, used if the engine is configured with Ignition propellants.
- `AlwaysRequired` = Whether the ignitor resource is required regardless of the total ignition potential (default is `false`).

In the `Hydrazine`+`LqdOxygen` case above, the following ignitor would provide enough additional ignition potential to bring the total above 1 and achieve ignition.
```
MODULE
{
    name = ModuleIgnitor
    IGNITOR_RESOURCE
    {
        name = ElectricCharge
        ScaledAmount = 0.1
        AddedIgnitionPotential = 0.5
	}
}
```

In a simplified case using a traditional engine without Ignition configured propellants, a required ignitor can be added like so:
```
MODULE
{
    name = ModuleIgnitor
    IGNITOR_RESOURCE
    {
        name = ElectricCharge
        Amount = 10
		AlwaysRequired = true
	}
}
```

### ModuleIgnitionPropellant
`ModuleIgnitionPropellant` is used to inject propellants into an engine module and take advantage of all of Ignition's automatic performance computation. It has the following fields:
- `moduleID`: Optional arbitrary string identifier.
- `resourceName`: Name of the resource to use, used to look up the corresponding `IgnitionPropellantConfig` or `IgnitionPropellantCombinationConfig` where applicable.
- `removeResource`: Name of a resource whose `RESOURCE` node will be removed from the part, for replacing existing tanks.
- `ratio`: Fixed ratio to override auto-computed mixture ratios. Ratio should be specified on either all the propellants or none of them. Useful for e.g. jet engines with a switchable fuel but a fixed fuel:IntakeAir ratio.
- `ignoreForIsp`: Whether this propellant should be ignored in thrust and isp computations, e.g. for the IntakeAir above.
- `addedVolume`: Volume added to the total volume considered by the set of all active `ModuleIgnitionPropellant`s.
- `addedMass`: Mass added to the part.
- `addedCost`: Cost added to the part.
- `engineID`: The `engineID` of a `ModuleEngines*` to act upon. If unspecified, the module will target the first `ModuleEngines*` it finds.

All instances of this module work together and act on stored resources, engine modules and RCS modules simultaneously.

For example, with the example propellant configs defined above, the following modules added to the LFB KR-1x2 "Twin-Boar" would convert the engine to run on 3 Kerosene : 5 LqdOxygen and change the in-built tanks to contain a total volume of 6,400 units (32,000 litres) of propellant split into 12,000 Kerosene and 20,000 LqdOxygen:
```
MODULE
{
	name = ModuleIgnitionPropellant
	moduleID = fuel
	resourceName = Kerosene
	addedVolume = 2880
}
MODULE
{
	name = ModuleIgnitionPropellant
	moduleID = oxidizer
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
				name = ModuleIgnitionPropellant
				moduleID = fuel
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
				name = ModuleIgnitionPropellant
				moduleID = fuel
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
