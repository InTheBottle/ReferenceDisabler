using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins;
using Noggog;
using Mutagen.Bethesda.Plugins.Records;
using Microsoft.VisualBasic;

namespace SRExteriorCitiesPatcher
{
    public class Program
    {
        // Vanilla ModKeys
        private static readonly HashSet<ModKey> vanillaModKeys = new()
        {
            ModKey.FromNameAndExtension("Skyrim.esm"),
            ModKey.FromNameAndExtension("Update.esm"),
            ModKey.FromNameAndExtension("Dawnguard.esm"),
            ModKey.FromNameAndExtension("HearthFires.esm"),
            ModKey.FromNameAndExtension("Dragonborn.esm"),
            ModKey.FromNameAndExtension("ccbgssse001-fish.esm"),
            ModKey.FromNameAndExtension("ccqdrsse001-survivalmode.esl"),
            ModKey.FromNameAndExtension("ccbgssse037-curios.esl"),
            ModKey.FromNameAndExtension("ccbgssse025-advdsgs.esm")
        };

        // Counters
        internal static int nbTotal = 0;
        internal static int nbObjects = 0;
        internal static int nbNPCs = 0;
        internal static int nbHazards = 0;


        /**
         * Moves placed objects/NPCs/hazards from the city worldspace to the corresponding Tamriel cell
         */
        internal static void DisableReference(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, IModContext<ISkyrimMod, ISkyrimModGetter, IPlaced, IPlacedGetter> placed)
        {

            // Ignore null
            if (placed is null || placed.Record.Placement is null) return;

            // Ignore vanilla
            if (vanillaModKeys.Contains(placed.ModKey)) return;


            bool editRecord = false;
            // If the record has the initially disabled flag
            if(placed.Record.SkyrimMajorRecordFlags.HasFlag(SkyrimMajorRecord.SkyrimMajorRecordFlag.InitiallyDisabled))
            {
                // If the record is missing an XESP record
                if (placed.Record.EnableParent is null /*|| !placed.Record.EnableParent.Reference.FormKey.Equals(Skyrim.Npc.Player.FormKey)*/ )
                {
                    editRecord = true;
                }

                // If the Z position is not at -30000
                if(placed.Record.Placement.Position.Z != -30000)
                {
                    editRecord = true;
                }
            }

            // Fix Deleted records too
            if(Settings.fixDeleted && placed.Record.SkyrimMajorRecordFlags.HasFlag(SkyrimMajorRecord.SkyrimMajorRecordFlag.Deleted))
            {
                if (Settings.debug)
                    System.Console.WriteLine("     Found deleted item: " + placed.ToString());

                editRecord = true;
            }
            
            // If there are objects that are not properly disabled
            if(editRecord)
            {
                // Open/copy the PlacedObject in the patch mod
                var placedState = placed.GetOrAddAsOverride(state.PatchMod);


                // Ignore PlacedObjects with linked references
                var p = placedState.ToLink().TryResolve<IPlacedObject>(state.LinkCache);
                if (p is not null && p.LinkedReferences is not null && p.LinkedReferences.Count > 0)
                {
                    if(Settings.debug)
                        System.Console.WriteLine("     Found Linked Ref > 0 : " + p.FormKey.ToString());

                    state.PatchMod.Remove(placed.Record.FormKey, p.GetType());
                    return;
                }

                // Ignore PlacedObjects with scripts
                if (p is not null && p.VirtualMachineAdapter is not null && p.VirtualMachineAdapter.Scripts.Count > 0)
                {
                    if (Settings.debug)
                        System.Console.WriteLine("     Found Scripts > 0 : " + p.FormKey.ToString());

                    state.PatchMod.Remove(placed.Record.FormKey, p.GetType());
                    return; 
                }

                // Ignore PlacedObjects with LocRefTypes
                /*if (p is not null && p.LocationRefTypes is not null && p.LocationRefTypes.Count > 0)
                {
                    if(Settings.debug)
                        System.Console.WriteLine("locreftypes > 0 : " + p.FormKey.ToString());
                    state.PatchMod.Remove(placed.Record.FormKey, p.GetType());
                    return;
                }*/


                // Remove the Deleted record flag
                if(Settings.fixDeleted)
                    placedState.SkyrimMajorRecordFlags = placedState.SkyrimMajorRecordFlags.SetFlag(SkyrimMajorRecord.SkyrimMajorRecordFlag.Deleted, false);

                // Fix the record flag (not necessary at all)
                placedState.SkyrimMajorRecordFlags = placedState.SkyrimMajorRecordFlags.SetFlag(SkyrimMajorRecord.SkyrimMajorRecordFlag.InitiallyDisabled, true);

                // XESP
                if (placedState.EnableParent is null)
                {
                    EnableParent ep = new()
                    {
                        Reference = Skyrim.PlayerRef,
                        Flags = EnableParent.Flag.SetEnableStateToOppositeOfParent
                    };

                    placedState.EnableParent = ep;
                }
                placedState.EnableParent.Flags.SetFlag(EnableParent.Flag.SetEnableStateToOppositeOfParent, true);

                // Position Z
                if (placedState.Placement is null)
                {
                    return;
                }

                placedState.Placement.Position = new P3Float(placedState.Placement.Position.X, placedState.Placement.Position.Y,-30000);

                // Increment the counter
                nbTotal++;

                // Show progress every 1000 records moved
                if (nbTotal % 50 == 0 && nbTotal > 0)
                    System.Console.WriteLine("Properly disabled " + nbTotal + " total placed referenced...");
            }

        }

        /* =================================================== \\
        || --------------------------------------------------- ||
        ||                                                     ||
        ||                     RUN PATCHER                     ||
        ||                                                     ||
        || --------------------------------------------------- ||
        \\ =================================================== */

        public static Lazy<Settings> _settings = null!;
        public static Settings Settings => _settings.Value;

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetAutogeneratedSettings("Settings", "settings.json", out _settings)
                .SetTypicalOpen(GameRelease.SkyrimSE, "SynthesisDisabler.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            /// Variables and initialisation
            // Create a link cache
            ILinkCache cache = state.LinkCache;


            var loadorder = state.LoadOrder.PriorityOrder.Where(x => !vanillaModKeys.Contains(x.ModKey));

            /* --------------------------------------------------- \
            |                  DISABLE OBJECTS                     |
            \ --------------------------------------------------- */

            System.Console.WriteLine("Disabling objects...");
            foreach (var placed in loadorder.PlacedObject().WinningContextOverrides(cache))
            {
                if (vanillaModKeys.Contains(placed.ModKey)) continue;

                DisableReference(state, placed);
            }
            nbObjects = nbTotal;
            System.Console.WriteLine("Properly disabled " + nbObjects + " objects");

            // NPCs
            System.Console.WriteLine("Disabling NPCs...");
            foreach (var placed in loadorder.PlacedNpc().WinningContextOverrides(cache))
            {
                if (vanillaModKeys.Contains(placed.ModKey)) continue;

                DisableReference(state, placed);
            }
            nbNPCs = nbTotal - nbObjects;
            System.Console.WriteLine("Properly disabled " + nbNPCs + " NPCs");

            // Hazards
            System.Console.WriteLine("Disabling Hazards...");
            foreach (var placed in loadorder.APlacedTrap().WinningContextOverrides(cache))
            {
                if (vanillaModKeys.Contains(placed.ModKey)) continue;

                DisableReference(state, placed);
            }
            nbHazards = nbTotal - nbObjects - nbNPCs;
            System.Console.WriteLine("Properly disabled " + nbHazards + " hazards");
            System.Console.WriteLine("Properly disabled " + nbTotal + " total placed referenced!");



            /* --------------------------------------------------- \
            |              REMOVE VANILLA DISABLED                 |
            \ --------------------------------------------------- */

            System.Console.WriteLine("Removing all vanilla initially disabled records from the patch...");
            loadorder = state.LoadOrder.PriorityOrder.Where(x => vanillaModKeys.Contains(x.ModKey));
            foreach (var placed in loadorder.PlacedObject().WinningOverrides())
            {
                // Only consider initially disabled files in Vanilla
                if (!placed.SkyrimMajorRecordFlags.HasFlag(SkyrimMajorRecord.SkyrimMajorRecordFlag.InitiallyDisabled)) continue;


                // Remove the placed object from the patchmod
                var p = placed.ToLink().TryResolve<IPlacedObject>(state.LinkCache);
                if (p != null)
                {
                    //System.Console.WriteLine("beep beep");
                    state.PatchMod.Remove(placed.FormKey, p.GetType());
                }
            }
            foreach (var placed in loadorder.PlacedNpc().WinningOverrides())
            {
                // Only consider initially disabled files in Vanilla
                if (!placed.SkyrimMajorRecordFlags.HasFlag(SkyrimMajorRecord.SkyrimMajorRecordFlag.InitiallyDisabled)) continue;


                // Remove the placed object from the patchmod
                var p = placed.ToLink().TryResolve<IPlacedNpc>(state.LinkCache);
                if (p != null)
                {
                    //System.Console.WriteLine("beep beep");
                    state.PatchMod.Remove(placed.FormKey, p.GetType());
                }
            }
            foreach (var placed in loadorder.APlacedTrap().WinningOverrides())
            {
                // Only consider initially disabled files in Vanilla
                if (!placed.SkyrimMajorRecordFlags.HasFlag(SkyrimMajorRecord.SkyrimMajorRecordFlag.InitiallyDisabled)) continue;


                // Remove the placed object from the patchmod
                var p = placed.ToLink().TryResolve<IAPlacedTrap>(state.LinkCache);
                if (p != null)
                {
                    //System.Console.WriteLine("beep beep");
                    state.PatchMod.Remove(placed.FormKey, p.GetType());
                }
            }
            System.Console.WriteLine("Done removing vanilla 'initially disabled' records from the patch!");

            System.Console.WriteLine("Final count of properly disabled records: " + state.PatchMod.ModHeader.Print() + "!");
            System.Console.WriteLine("Done!");
        }
    }
}
