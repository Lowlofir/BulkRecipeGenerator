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
                    RecipeDef newR = Ad2.GetNewRecipe(r);
                    if (newR != null)
                        res.Add(newR);
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
            RecipeDef srcRecipe = Ad2.GetSrcRecipe(__instance);
            if (srcRecipe != null && srcRecipe.WorkAmountTotal(null) > Ad2WorldComp.instance.threshold * 60)
            {
               // Log.Message(__instance.label + " rejected with src workAmount " + srcRecipe.WorkAmountTotal(null)/60);
                return false;
            }
            return __result;
        }
    }
}

