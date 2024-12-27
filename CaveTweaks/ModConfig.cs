using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaveTweaks
{
    public class ModConfig
    {
        public int CaveAmountDivisor { get; set; } = 64;
        public float TunnelHorizontalSizeMultiplier { get; set; } = 1.2f;
        public float TunnelVerticalSizeMultiplier { get; set; } = 1.1f;
        public float TunnelCurvinessMultiplier { get; set; } = 1.25f;
        public bool CreateShafts { get; set; } = false;
        public string ModVersion { get; set; } = null;

        public ModConfig()
        {
            // Initialize default settings...
            CaveAmountDivisor = 64;
            TunnelHorizontalSizeMultiplier = 1.2f;
            TunnelVerticalSizeMultiplier = 1.1f;
            TunnelCurvinessMultiplier = 1.25f;
            CreateShafts = false;
            ModVersion = InitializeMod.ModInfo.Version;
        }

        public void FixMissingOrInvalidProperties(ModConfig defaultConfig)
        {
            System.Reflection.PropertyInfo[] properties = typeof(ModConfig).GetProperties();

            foreach(System.Reflection.PropertyInfo prop in properties)
            {
                object currentValue = prop.GetValue(this);
                object defaultValue = prop.GetValue(defaultConfig);

                // If current value is null or the same as default, replace it with the default value...
                if(currentValue == null || currentValue.Equals(defaultValue))
                {
                    prop.SetValue(this, defaultValue);
                }
            }
        }
    }
}
