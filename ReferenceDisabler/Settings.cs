using Mutagen.Bethesda.Plugins;
using System.Collections.Generic;

namespace LuxCSreferenceDisabler
{
    public class Settings
    {
        public bool debug { get; set; } = false;

        // List of base objects (FormKeys) to disable
        public HashSet<FormKey> TargetBaseObjects { get; set; } = new();
    }
}
