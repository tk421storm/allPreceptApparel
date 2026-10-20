using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;


namespace TKS_AllPreceptApparel
{
    [StaticConstructorOnStartup]
    public static class InsertHarmony
    {
        static InsertHarmony()
        {
            Harmony harmony = new Harmony("TKS_AllPreceptApparel");
            //Harmony.DEBUG = true;
            harmony.PatchAll();
            //Harmony.DEBUG = false;
            Log.Message($"TKS_AllPreceptApparel: Patching finished");
        }
    }

    [HarmonyPatch(typeof(Precept_Role))]
    static class Precept_Role_Patches
    {
        //apparel allowed as role apparel, filtered and sorted by label. defs don't change after loading, so each list is
        //built the first time it's needed instead of scanning every ThingDef on every call (tribal ideos only get neolithic apparel)
        private static List<ThingDef> allowedApparel = null;
        private static List<ThingDef> allowedApparelTribal = null;

        private static List<ThingDef> GetAllowedApparel(bool tribal)
        {
            if (tribal)
            {
                if (allowedApparelTribal == null)
                {
                    allowedApparelTribal = BuildAllowedApparel(true);
                }
                return allowedApparelTribal;
            }

            if (allowedApparel == null)
            {
                allowedApparel = BuildAllowedApparel(false);
            }
            return allowedApparel;
        }

        private static List<ThingDef> BuildAllowedApparel(bool tribal)
        {
            List<ThingDef> result = new List<ThingDef>();

            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (thingDef != null && thingDef.IsApparel) {

                    if (NeverSelect(thingDef))
                    {
                        continue;
                    }

                    //check for tribal
                    bool addThis = true;
                    if (tribal && (thingDef.techLevel!=TechLevel.Neolithic))
                    {
                        addThis = false;
                    }
                    if (addThis)
                    {
                        result.Add(thingDef);
                    }

                }
            }

            result.Sort((x, y) => x.label.CompareTo(y.label));
            return result;
        }

        //single-use items that happen to be apparel - a role can't be expected to keep one on
        private static readonly HashSet<string> neverSelectDefNames = new HashSet<string>
        {
            "OrbitalTargeterBombardment",
            "OrbitalTargeterPowerBeam",
            "OrbitalTargeterMechCluster",
        };

        //apparel that should never be offered as role apparel - either it's out of place on a role, or a colonist could never
        //satisfy it (vanilla wouldn't count it as worn, so the role holder would carry the unmet apparel mood penalty for good)
        private static bool NeverSelect(ThingDef thingDef)
        {
#if v1_4
            //skip armor, utility items
            if (thingDef.thingCategories != null && (thingDef.thingCategories.Contains(ThingCategoryDefOf.ApparelUtility) || thingDef.thingCategories.Contains(ThingCategoryDefOf.ArmorHeadgear)
                || thingDef.thingCategories.Contains(ThingCategoryDefOf.ApparelArmor)))
#else
            //skip armor, utility items
            if (thingDef.thingCategories != null && (thingDef.thingCategories.Contains(ThingCategoryDefOf.ArmorHeadgear) || thingDef.thingCategories.Contains(ThingCategoryDefOf.ApparelArmor)))
#endif
            {
                return true;
            }

            //skip slave stuff, mechanator stuff
            if (thingDef.label.ToLower().Contains("slave") || thingDef.apparel.mechanitorApparel)
            {
                return true;
            }

            //skip kids clothes
            if (!thingDef.apparel.developmentalStageFilter.Adult())
            {
                return true;
            }

            //the def itself says it's not for ideoligions (vanilla's own flag, ie apparel modders keep out of ideo choices)
            if (!thingDef.apparel.canBeDesiredForIdeo)
            {
                return true;
            }

            //vanilla ignores apparel without a tags list when checking role apparel, so it could never count as worn
            if (thingDef.apparel.tags == null)
            {
                return true;
            }

            //animal gear (Animal Gear framework and similar mods tag it "Animal") - it uses human body part groups, so nothing else gives it away
            if (thingDef.apparel.tags.Contains("Animal"))
            {
                return true;
            }

            if (neverSelectDefNames.Contains(thingDef.defName))
            {
                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(Precept_Role), "AllPossibleRequirements")]
        [HarmonyPrefix]
        public static bool AllPossibleRequirements_Prefix(Ideo ideo, PreceptDef def, bool desperate, ref List<PreceptApparelRequirement> __result)
        {
            // No player faction exists at this point (we could be walking through before it's been assigned to the player)
            /*
            Faction player = Faction.OfPlayerSilentFail;
            if (player != null && player.ideos.PrimaryIdeo != ideo)
            {
                return true;
            }
            */

            //get culture and use to filter items
            bool tribal = ideo.culture.allowedPlaceTags.Contains("OriginTribal");

            List<PreceptApparelRequirement> temp = new List<PreceptApparelRequirement>();

            //requirements are created fresh on every call, as roles keep and save the ones they're given
            foreach (ThingDef thingDef in GetAllowedApparel(tribal))
            {
                PreceptApparelRequirement requirement = new PreceptApparelRequirement();
                requirement.allowedFactionCategoryTags = null;
                requirement.disallowedFactionCategoryTags = null;
                requirement.anyMemeRequired = null;


                ApparelRequirement apparelRequirement = new ApparelRequirement();

                apparelRequirement.requiredDefs = new List<ThingDef>() { thingDef, };
                apparelRequirement.bodyPartGroupsMatchAny = thingDef.apparel.bodyPartGroups;
                apparelRequirement.requiredTags = requirement.allowedFactionCategoryTags;
                apparelRequirement.allowedTags = requirement.allowedFactionCategoryTags;

                //vanilla labels a requirement with no group label by the first matching apparel a male could wear - there is none for
                //female-only apparel, which threw. our requirements are a single item, so its label is the same text vanilla would show
                apparelRequirement.groupLabel = thingDef.LabelCap;

                requirement.requirement = apparelRequirement;

                temp.Add(requirement);
            }

            __result = temp;
            return false;
        }

        //roles save their requirements, so ones made before the group label was set (above) still throw in existing saves -
        //give any single-item requirement without a label the item's label. once set it's saved, so this only does work once per requirement
        [HarmonyPatch(typeof(Precept_Role), "AllApparelRequirementLabels")]
        [HarmonyPrefix]
        public static void AllApparelRequirementLabels_Prefix(Precept_Role __instance)
        {
            if (__instance.apparelRequirements == null) { return; }

            foreach (PreceptApparelRequirement preceptRequirement in __instance.apparelRequirements)
            {
                ApparelRequirement requirement = preceptRequirement?.requirement;
                //single item and no tags is how ours are made - vanilla's tag-based requirements are left alone
                if (requirement != null && requirement.groupLabel.NullOrEmpty() && requirement.requiredTags.NullOrEmpty() && requirement.requiredDefs != null && requirement.requiredDefs.Count == 1 && requirement.requiredDefs[0] != null)
                {
                    requirement.groupLabel = requirement.requiredDefs[0].LabelCap;
                }
            }
        }
    }
}
