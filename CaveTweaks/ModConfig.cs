using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaveTweaks
{
    public class ModConfig
    {
        public float TestValue { get; set; } = 0.03f;
        public string ModVersion { get; set; } = null;

        public ModConfig()
        {
            // Initialize default settings...
            TestValue = 0.03f;
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
