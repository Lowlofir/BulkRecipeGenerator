using Harmony;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;
using System;
using System.Collections.Generic;


namespace Ad2mod
{
    class Util
    {
        public static int Clamp(int val, int a, int b)
        {
            return (val < a) ? a : ((val > b) ? b : val);
        }
    }


    public class Ad2Settings : ModSettings
    {
        public int defaultThreshold = 60;
        public bool limitToX5 = true;
        public bool useRightClickMenu = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref defaultThreshold, "defaultThreshold", 60);
            Scribe_Values.Look(ref limitToX5, "limitToX5", true);
            //Scribe_Values.Look(ref useRightClickMenu, "useRightClickMenu", true);
            base.ExposeData();
        }
    }

    class Ad2Mod : Mod
    {
        public static Ad2Settings settings;
        public static Ad2Mod instance;

        NumField defaultThresholdField = new NumField();
        NumField thresholdField = new NumField();
        Game lastGame;

        class NumField
        {
            string buffer;
            int min, max;

            public NumField(int min = 0, int max = 120)
            {
                this.min = min;
                this.max = max;
            }
            public void Reset()
            {
                buffer = null;
            }
            public bool DoField(float y, string label, ref int val)
            {
                float LH = Text.LineHeight;
                if (buffer == null)
                    buffer = val.ToString();
                float x = 0;
                TextAnchor anchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(x, y, 200, LH), label);
                Text.Anchor = anchor;
                x += 200;
                buffer = Widgets.TextField(new Rect(x, y, 60, LH), buffer);
                x += 60;
                if (Widgets.ButtonText(new Rect(x, y, 100, LH), "Apply"))
                {
                    int resInt;
                    if (int.TryParse(buffer, out resInt))
                    {
                        val = Util.Clamp(resInt, min, max);
                        buffer = val.ToString();
                        return true;
                    }
                    buffer = val.ToString();
                }
                return false;
            }
        }

        public Ad2Mod(ModContentPack content) : base(content)
        {
            instance = this;
            settings = GetSettings<Ad2Settings>();
        }

        public override string SettingsCategory() => "Bulk recipe generator";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float x = inRect.x;
            float y = inRect.y;
            float LH = Text.LineHeight;
            y += LH;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(x, y, 200, Text.LineHeight), "Global settings");
            y += Text.LineHeight + 2;
            Text.Font = GameFont.Small;
            if (defaultThresholdField.DoField(y, "Default target time", ref settings.defaultThreshold))
                Messages.Message("Default target time changed to " + settings.defaultThreshold, MessageTypeDefOf.NeutralEvent);
            
            y += LH;
            Widgets.CheckboxLabeled( new Rect(x, y, 360, LH), "Stop on x5 recipes", ref settings.limitToX5);

            y += 2*LH+2;

            if (Current.Game == null)
            {
                lastGame = null;
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(x, y, 200, Text.LineHeight), "World settings");
            y += Text.LineHeight + 2;
            Text.Font = GameFont.Small;

            if (lastGame != Current.Game)
                thresholdField.Reset();
            var wc = Ad2WorldComp.instance;
            if (thresholdField.DoField(y, "Current game target time", ref wc.threshold))
                Messages.Message("Current game target time changed to " + wc.threshold, MessageTypeDefOf.NeutralEvent);
            lastGame = Current.Game;

            y += 2*LH+2;

            List<Bill> bills = Ad2.FindRecipesUses();
            string s = $"Remove modded recipes from save ({bills.Count} found)";
            var w = Text.CalcSize(s).x + 64;
            if (Widgets.ButtonText( new Rect(x, y, w, 32), s))
            {
                foreach (Bill bill in bills)
                    bill.billStack.Delete(bill);
                Messages.Message(bills.Count.ToString() + " bills removed", MessageTypeDefOf.NeutralEvent);
            }
        }
    }


    [StaticConstructorOnStartup]
    public class Ad2
    {
        const int thresholdLimit = 120;
        static readonly int[] mulFactors = { 5, 10, 25 };

        //  old:new
        static Dictionary<RecipeDef, List<RecipeDef>> dictON = new Dictionary<RecipeDef, List<RecipeDef>>();
        //  new:old
        static Dictionary<RecipeDef, RecipeDef> dictNO = new Dictionary<RecipeDef, RecipeDef>();
        //  recipe.LabelCap : RecipeDef
        static Dictionary<string, RecipeDef> dictLR = new Dictionary<string, RecipeDef>();

        static string TransformRecipeLabel(string s)
        {
            if (s.NullOrEmpty())
            {
                s = "(missing label)";
            }
            return s.TrimEnd(new char[0]);
        }

        static void RememberRecipeLabel(RecipeDef r)
        {
            string label = TransformRecipeLabel(r.LabelCap);
            if (!dictLR.ContainsKey(label))
                dictLR.Add(label, r);
            else if (dictLR[label] != r)
            {
                Log.Warning($"Ambiguous recipe label: {label}. Right click menu will be disabled for this one.");
                dictLR[label] = null;
            }
        }
        static void RememberNewRecipe(RecipeDef src, RecipeDef n)
        {
            if (!dictON.ContainsKey(src))
                dictON.Add(src, new List<RecipeDef>());
            dictON[src].Add(n);

            if (dictNO.ContainsKey(n))
                Log.Error($"dictNO already contains {n.defName} ({n.label})");
            dictNO.Add(n, src);

            RememberRecipeLabel(src);
            RememberRecipeLabel(n);
        }

        public static bool IsSrcRecipe(RecipeDef recipe) => dictON.ContainsKey(recipe);
        public static bool IsNewRecipe(RecipeDef recipe) => dictNO.ContainsKey(recipe);

        public static RecipeDef GetSrcRecipe(RecipeDef recipe)
        {
            RecipeDef res;
            dictNO.TryGetValue(recipe, out res);
            return res;
        }
        public static List<RecipeDef> GetNewRecipesList(RecipeDef recipe)
        {
            List<RecipeDef> res;
            dictON.TryGetValue(recipe, out res);
            return res;
        }
        public static RecipeDef GetRecipeByLabel(string label)
        {
            RecipeDef res;
            dictLR.TryGetValue(label, out res);
            return res;
        }

        public static List<Bill> FindRecipesUses()
        {
            if (Find.Maps == null)
                throw new Exception("Find.Maps == null");
            var res = new List<Bill>();
            foreach (Map map in Find.Maps)
            {
                foreach (Building_WorkTable wt in map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>())
                {
                    foreach (Bill bill in wt.BillStack)
                    {
                        if (IsNewRecipe(bill.recipe))
                            res.Add(bill);
                    }
                }
            }
            return res;
        }

        static void RecipeIconsCompatibility(HarmonyInstance harmony)
        {
            try
            {
                ((Action)(() =>
                {
                    if (LoadedModManager.RunningModsListForReading.Any(x => x.Name == "Recipe icons"))
                    {
                        harmony.Patch(AccessTools.Method("RecipeIcons.FloatMenuOptionLeft:DoGUI"),
                            postfix: new HarmonyMethod(typeof(FloatMenuOption_DoGUI_Patch), "Postfix"));
                    }
                }))();
            }
            catch (TypeLoadException ex) { }
        }

        static Ad2()
        {
            var harmony = HarmonyInstance.Create("com.local.anon.ad2");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            RecipeIconsCompatibility(harmony);
            GenRecipes();
        }

        static RecipeDef MkNewRecipe(RecipeDef rd, int factor)
        {
            if (rd.ingredients.Count == 0 || rd.products.Count != 1)
                return null;

            RecipeDef r = new RecipeDef
            {
                defName = rd.defName + $"_{factor}x",
                label = rd.label + $" x{factor}",
                description = rd.description + $" (x{factor})",
                jobString = rd.jobString,
                modContentPack = rd.modContentPack,
                workSpeedStat = rd.workSpeedStat,
                efficiencyStat = rd.efficiencyStat,
                fixedIngredientFilter = rd.fixedIngredientFilter,
                productHasIngredientStuff = rd.productHasIngredientStuff,
                workSkill = rd.workSkill,
                workSkillLearnFactor = rd.workSkillLearnFactor,
                skillRequirements = rd.skillRequirements.ListFullCopyOrNull(),
                recipeUsers = rd.recipeUsers.ListFullCopyOrNull(),
                unfinishedThingDef = null, //rd.unfinishedThingDef,
                effectWorking = rd.effectWorking,
                soundWorking = rd.soundWorking,
                allowMixingIngredients = rd.allowMixingIngredients,
                defaultIngredientFilter = rd.defaultIngredientFilter,
            };
            r.products.Add(new ThingDefCountClass(rd.products[0].thingDef, rd.products[0].count * factor));
            List<IngredientCount> new_ingredients = new List<IngredientCount>();
            foreach (var oic in rd.ingredients)
            {
                var nic = new IngredientCount();
                nic.SetBaseCount(oic.GetBaseCount() * factor);
                nic.filter = oic.filter;
                new_ingredients.Add(nic);
            }
            r.ingredients = new_ingredients;
            r.workAmount = rd.WorkAmountTotal(null) * factor;

            Type IVGClass;
            IVGClass = (Type)Traverse.Create(rd).Field("ingredientValueGetterClass").GetValue();
            Traverse.Create(r).Field("ingredientValueGetterClass").SetValue(IVGClass);

            //if (rd.unfinishedThingDef != null)
            //    Log.Message(rd.label + " uses unfinishedThingDef " + rd.unfinishedThingDef.label+"  an it is removed");
            return r;
        }

        static void GenRecipes()
        {
            var allRecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            var srcs = new List<RecipeDef>();
            foreach (var recipe in allRecipes)
            {
                if (recipe.products.Count != 1) continue;
                if (recipe.ingredients.Count == 0) continue;
                if (recipe.WorkAmountTotal(null) > thresholdLimit * 60) continue;
                srcs.Add(recipe);
                //Log.Message(recipe.label + "\t" + recipe.defName + "\t" + recipe.WorkAmountTotal(null)/60);
            }
            List<ThingDef> RecipesUsers = new List<ThingDef>();
            foreach (var recipe in srcs)
            {
                bool lastOne = false;
                foreach (int factor in mulFactors)
                {
                    if (factor * recipe.WorkAmountTotal(null) > thresholdLimit * 60)
                        lastOne = true;
                    var newRecipe = MkNewRecipe(recipe, factor);
                    if (newRecipe == null)
                    {
                        Log.Error("newRecipe == null  on " + recipe.label);
                        continue;
                    }
                    newRecipe.ResolveReferences();
                    DefDatabase<RecipeDef>.Add(def: newRecipe);

                    RememberNewRecipe(recipe, newRecipe);

                    foreach (var ru in recipe.AllRecipeUsers)
                    {
                        if (!RecipesUsers.Contains(ru))
                            RecipesUsers.Add(ru);
                        if (newRecipe.recipeUsers == null)
                            ru.recipes.Add(newRecipe);
                    }
                    if (lastOne) break;
                }
            }
            foreach (var ru in RecipesUsers)
                Traverse.Create(ru).Field("allRecipesCached").SetValue(null);
        }
    }

}
