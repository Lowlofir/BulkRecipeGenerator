using Harmony;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;


namespace Ad2mod
{
    [HarmonyPatch(typeof(ThingDef))]
    [HarmonyPatch("AllRecipes", MethodType.Getter)]
    public class ThingDef_AllRecipes_Getter_Patch
    {
        public static void Prefix(ref bool __state, List<RecipeDef> ___allRecipesCached)
        {
            __state = ___allRecipesCached == null;
        }

        public static List<RecipeDef> Postfix(List<RecipeDef> __result, ref List<RecipeDef> ___allRecipesCached, bool __state)
        {
            if (!__state)
                return __result;

            List<RecipeDef> res = new List<RecipeDef>();
            foreach (var r in __result)
            {
                if (!Ad2.IsNewRecipe(r))
                {
                    res.Add(r);
                    if (Ad2.IsSrcRecipe(r))
                        res.Add(Ad2.dict[r]);
                }
            }

            ___allRecipesCached = res;
            //Log.Message("___allRecipesCached = res");

            return res;
        }
    }


    [HarmonyPatch(typeof(RecipeDef))]
    [HarmonyPatch("AvailableNow", MethodType.Getter)]
    public class RecipeDef_AvailableNow_Getter_Patch
    {
        public static bool Postfix(bool __result, RecipeDef __instance)
        {
            if (__result == false)
                return __result;
            var dictNO = Ad2.dictReversed;
            if (Ad2.IsNewRecipe(__instance) && dictNO[__instance].WorkAmountTotal(null) > Ad2WorldComp.instance.threshold * 60)
            {
                Log.Message(__instance.label + " rejected with src workAmount " + dictNO[__instance].WorkAmountTotal(null)/60);
                return false;
            }
            return __result;
        }
    }
}

