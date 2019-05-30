﻿using Harmony;
using System;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
                    var newRList = Ad2.GetNewRecipesList(r);
                    if (newRList != null)
                        foreach (var nr in newRList)
                            res.Add(nr);
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
            if (Ad2Mod.settings.useRightClickMenu)
                return __result;
            if (__result == false)
                return false;
            RecipeDef srcRecipe = Ad2.GetSrcRecipe(__instance);
            if (srcRecipe == null)
                return true;

            if (Ad2Mod.settings.limitToX5 && __instance != Ad2.GetNewRecipesList(srcRecipe)[0])
                return false;
            if (__instance.workAmount > 1.5 * Ad2Mod.settings.defaultThreshold * 60)
            {
                //Log.Message(__instance.label + " hidden with src workAmount " + __instance.WorkAmountTotal(null)/60);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BillStack))]
    [HarmonyPatch("DoListing")]
    public class BillStack_DoListing_Patch
    {
        public static BillStack lastBillStack;
        public static void Prefix(ref Func<List<FloatMenuOption>> recipeOptionsMaker, BillStack __instance)
        {
            lastBillStack = __instance;
            if (!Ad2Mod.settings.useRightClickMenu)
                return;
            List<FloatMenuOption> list = recipeOptionsMaker();
            recipeOptionsMaker = delegate ()
            {
                List<FloatMenuOption> newList = new List<FloatMenuOption>();
                foreach (var opt in list)
                {
                    var recipe = Ad2.GetRecipeByLabel(opt.Label);
                    if (recipe == null || !Ad2.IsNewRecipe(recipe))
                        newList.Add(opt);
                }
                return newList;
            };
        }
    }



    [HarmonyPatch(typeof(FloatMenuOption))]
    [HarmonyPatch("DoGUI")]
    public class FloatMenuOption_DoGUI_Patch
    {
        static List<FloatMenuOption> recipeOptionsMaker(List<RecipeDef> recipesList)
        {
            var table = BillStack_DoListing_Patch.lastBillStack.billGiver as Building_WorkTable;
            List<FloatMenuOption> list = new List<FloatMenuOption>();
            if (table == null)
            {
                list.Add(new FloatMenuOption("table == null", delegate () { }));
                return list;
            }
            foreach (var recipe in recipesList)
            {
                if (!recipe.AvailableNow) continue;
                list.Add(new FloatMenuOption(recipe.LabelCap, delegate ()
                {
                    if (!table.Map.mapPawns.FreeColonists.Any((Pawn col) => recipe.PawnSatisfiesSkillRequirements(col)))
                    {
                        Bill.CreateNoPawnsWithSkillDialog(recipe);
                    }
                    Bill bill2 = recipe.MakeNewBill();
                    table.billStack.AddBill(bill2);
                }));
            }
            return list;
        }

        public static void Postfix(ref bool __result, FloatMenuOption __instance)
        {
            if (!Ad2Mod.settings.useRightClickMenu)
                return;

            if (__result == true && Event.current.button == 1)
            {
                __result = false;
                var recipe = Ad2.GetRecipeByLabel(__instance.Label);
                if (recipe==null || !Ad2.IsSrcRecipe(recipe)) return;
                List<RecipeDef> nlst = Ad2.GetNewRecipesList(recipe);
                if (nlst == null) return;

                Find.WindowStack.Add(new FloatMenu(recipeOptionsMaker(nlst)));
            }
        }
    }

    
    [HarmonyPatch(typeof(FloatMenuOption))]
    [HarmonyPatch("Chosen")]
    public class FloatMenuOption_Chosen_Patch
    {
        public static bool Prefix()
        {
            if (Ad2Mod.settings.useRightClickMenu && Event.current.button == 1)
                return false;
            return true;
        }
    }

}