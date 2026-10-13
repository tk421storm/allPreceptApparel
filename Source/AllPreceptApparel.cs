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

            Log.Message("TKS_AllPreceptApparel: AllPossibleRequirements prefix beginning for "+ideo.name);

            bool tribal;

            //get culture and use to filter items
            if (ideo.culture.allowedPlaceTags.Contains("OriginTribal"))
            {
                Log.Message("TKS_AllPreceptApparel: found tribal ideo, limiting items to neolithic/tribal ");
                tribal = true;
            } else
            {
                Log.Message("TKS_AllPreceptApparel: found non-tribal ideo ("+ideo.culture.allowedPlaceTags[0]+")");
                tribal = false;
            }
            List<PreceptApparelRequirement> temp = new List<PreceptApparelRequirement>();

            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (thingDef != null && thingDef.IsApparel) {

#if v1_4
                    //skip armor, utility items
                    if (thingDef.thingCategories != null && (thingDef.thingCategories.Contains(ThingCategoryDefOf.ApparelUtility) || thingDef.thingCategories.Contains(ThingCategoryDefOf.ArmorHeadgear)
                        || thingDef.thingCategories.Contains(ThingCategoryDefOf.ApparelArmor)))
#elif v1_5
                    //skip armor, utility items
                    if (thingDef.thingCategories != null && (thingDef.thingCategories.Contains(ThingCategoryDefOf.ArmorHeadgear) || thingDef.thingCategories.Contains(ThingCategoryDefOf.ApparelArmor)))
#endif
                    {
                        continue;
                    }
                    
                    //skip slave stuff, mechanator stuff
                    if (thingDef.label.ToLower().Contains("slave") || thingDef.apparel.mechanitorApparel)
                    {
                        continue;
                    }

                    //skip kids clothes
                    if (!thingDef.apparel.developmentalStageFilter.Adult())
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
                        //Log.Message("TKS_AllPreceptApparel: adding " + thingDef.defName);
                        PreceptApparelRequirement requirement = new PreceptApparelRequirement();
                        requirement.allowedFactionCategoryTags = null;
                        requirement.disallowedFactionCategoryTags = null;
                        requirement.anyMemeRequired = null;


                        ApparelRequirement apparelRequirement = new ApparelRequirement();

                        apparelRequirement.requiredDefs = new List<ThingDef>() { thingDef, };
                        apparelRequirement.bodyPartGroupsMatchAny = thingDef.apparel.bodyPartGroups;
                        apparelRequirement.requiredTags = requirement.allowedFactionCategoryTags;
                        apparelRequirement.allowedTags = requirement.allowedFactionCategoryTags;

                        requirement.requirement = apparelRequirement;

                        temp.Add(requirement);
                    }

                }
            }
            Log.Message("TKS_AllPreceptApparel: AllPossibleRequirements prefix complete");
            temp.Sort((x, y) => x.requirement.requiredDefs[0].label.CompareTo(y.requirement.requiredDefs[0].label));
            __result = temp;
            return false;
        }
    }
}
