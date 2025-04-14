using System.Collections.Generic;

namespace FuelMixer
{
	public class EngineWrapper
	{
		public bool isModuleEngineFX = false;
		private ModuleEngines engine = null;
		private ModuleEnginesFX engineFX = null;
       
		public EngineWrapper(ModuleEngines engine)
		{
			isModuleEngineFX = false;
			this.engine = engine;
		}

		public EngineWrapper(ModuleEnginesFX engineFX)
		{
			isModuleEngineFX = true;
			this.engineFX = engineFX;
		}

		public Vessel vessel
		{
			get
			{
				if(isModuleEngineFX == false) return engine.vessel;
    			return engineFX.vessel;
			}
		}

		public void SetRunningGroupsActive(bool active)
		{
			if (isModuleEngineFX == false)
				engine.SetRunningGroupsActive(active);
			// Do not need to worry about ModuleEnginesFX.
		}

        //DennyTX
        public bool EngineIgnited
        {
            get
            {
                if (isModuleEngineFX == false) return engine.EngineIgnited;
                return engineFX.EngineIgnited;
            }
        }

        public Vector3d ForwardTransform
        {
            get
            {
                if (isModuleEngineFX == false) return engine.thrustTransforms[0].forward;
                return engineFX.thrustTransforms[0].forward;
                
            }
        }
        public string status
        {
            get
            {
                if (isModuleEngineFX == false) return engine.status;
                return engineFX.status;
            }
        }
 

        public float requestedThrottle
		{
			get
			{
				if (isModuleEngineFX == false) return engine.requestedThrottle;
				return engineFX.requestedThrottle;
			}
		}

		public bool throttleLocked
		{
			get
			{
				if (isModuleEngineFX == false) return engine.throttleLocked;
				return engineFX.throttleLocked;
			}
		}

		public List<Propellant> propellants
		{
			get
			{
				if (isModuleEngineFX == false) return engine.propellants;
				return engineFX.propellants;
			}
		}

		public Part part
		{
			get
			{
				if (isModuleEngineFX == false) return engine.part;
				return engineFX.part;
			}
		}

		public BaseEventList Events
		{
			get
			{
				if (isModuleEngineFX == false) return engine.Events;
				return engineFX.Events;
			}
		}

		public void BurstFlameoutGroups()
		{
			if (isModuleEngineFX == false)
				engine.BurstFlameoutGroups();
			else
				engineFX.part.Effects.Event(engineFX.flameoutEffectName,engineFX.transform.hierarchyCount);
		}

		public bool allowShutdown
		{
			get
			{
				if (isModuleEngineFX == false) return engine.allowShutdown;
				return engineFX.allowShutdown;
			}
		}

		public bool flameout
		{ 
			get
			{
				if (isModuleEngineFX == false) return engine.flameout;
				return engineFX.getFlameoutState;
			}
        }

        public float MassRate
        {
            get
            {
				float isp;
                if (isModuleEngineFX == false)
				{
					isp = engine.atmosphereCurve.Curve.keys[0].value;
                    if (!engine.useVelCurve) isp = engine.atmosphereCurve.Curve.keys[1].value;
					return 1000 * engine.maxThrust / (engine.g * isp);
                }

                isp = engineFX.atmosphereCurve.Curve.keys[0].value;
                if (!engineFX.useVelCurve) isp = engineFX.atmosphereCurve.Curve.keys[1].value;
                return 1000 * engineFX.maxThrust / (engineFX.g * isp);
            }
        }
    }
}
