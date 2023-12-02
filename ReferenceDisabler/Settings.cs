using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.WPF.Reflection.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRExteriorCitiesPatcher
{

    public class Settings
    {
        [SettingName("Fix deleted records?")]
        [Tooltip("Set to true to also properly disable deleted records")]
        public bool fixDeleted = false;

        [SettingName("Debug")]
        [Tooltip("Activate all the debug messages")]
        public bool debug = false;
    }
}