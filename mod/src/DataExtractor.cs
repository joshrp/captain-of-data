/*
 * Data Extraction Mod
 * 
 * -------------------------------------
 * DESCRIPTION
 * -------------------------------------
 * 
 * This is a simple and somewhat messy mod that extracts in game data.
 * It was designed to export the game data in JSON with the majority of relevant fields.
 * 
 * The current version of the mod only covers buildings/machines that are part of the production chain recipes.
 * Thus, any buildings that are not part of production chaings (ramps, transports, retaining walls, etc) are not included.
 * The only exception is Vehicles and Ship upgrade parts which were specifically requested by someone.
 * 
 * Each object in the game has a Proto (prototype) that defines the object's properties and capabilities.
 * These prototypes are registerd with the game's Prototype Database (ProtoDB).
 * 
 * What this mod does is fetch the Protos for the ProtoDB and read the relevant properties and formats and exports them into JSON format.
 * 
 * Not All Objects share the same prototype, while most Machines do share the same prototype (MachineProto) many of the other specialized buildings
 * have their own unique protos.
 * 
 * There are two methods used to get the Protos from the DB, The first uses a list of Proto IDs to get the Protos from the DB, this requires having
 * a list of the Ids to lookup. These IDs can be found in Mafi.Base.Ids. With this I could then get each individual instance and lookup its Prototype.
 * 
 * The second methods is more simple.
 * 
 * After getting more familiar familiar with the code, I realized that you could just request all instances of a specific Proto from the DB, this
 * eliminates a few steps, mainly having the need of specifying a list of Ids.
 * 
 * The first part of the code uses the old method and I have not converted it to the new format.
 * 
 * -------------------------------------
 * JSON FORMATTING AND EXPORTING
 * -------------------------------------
 * 
 * I could not get the usual JSON serialization methods to work so I created a simple yet sloppy implementation to have proper JSON formatting.
 * There are different types of buildings/machines and thus there are several functions for formatting each kinds. Those are the first functions
 * you will see in the code below.
 * 
 * The resulting JSON files are exported to C:/temp but can be changed below, the folder might need to exists beforehand to prevent possible errors.
 * 
 * -------------------------------------
 * LIST OF MISSING ITEMS
 * -------------------------------------
 * Ids.Buildings.TradeDock
 * 
 * Ids.Buildings.MineTower
 * 
 * Ids.Buildings.HeadquartersT1
 * 
 * Ids.Machines.Flywheel
 * 
 * Ids.Buildings.Beacon
 * 
 * Ids.Buildings.Clinic
 * 
 * Ids.Buildings.SettlementPillar
 * Ids.Buildings.SettlementFountain
 * Ids.Buildings.SettlementSquare1
 * Ids.Buildings.SettlementSquare2
 * 
 * Ids.Buildings.Shipyard
 * Ids.Buildings.Shipyard2
 * 
 * Ids.Buildings.VehicleRamp
 * Ids.Buildings.VehicleRamp2
 * Ids.Buildings.VehicleRamp3
 * 
 * Ids.Buildings.RetainingWallStraight1
 * Ids.Buildings.RetainingWallStraight4
 * Ids.Buildings.RetainingWallCorner
 * Ids.Buildings.RetainingWallCross
 * Ids.Buildings.RetainingWallTee
 * 
 * Ids.Buildings.BarrierStraight1
 * Ids.Buildings.BarrierCorner
 * 
 * Ids.Buildings.BarrierCross
 * Ids.Buildings.BarrierTee
 * Ids.Buildings.BarrierEnding
 * 
 * Ids.Buildings.StatueOfMaintenance
 * Ids.Buildings.StatueOfMaintenanceGolden
 * 
 * Ids.Buildings.TombOfCaptainsStage1
 * Ids.Buildings.TombOfCaptainsStage2
 * Ids.Buildings.TombOfCaptainsStage3
 * Ids.Buildings.TombOfCaptainsStage4
 * Ids.Buildings.TombOfCaptainsStage5
 * Ids.Buildings.TombOfCaptainsStageFinal
 * 
 */

using System.IO;
using System.Collections.Generic;
using Mafi;
using Mafi.Base;
using Mafi.Core;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
using Mafi.Core.Products;
using Mafi.Core.Factory.Recipes;
using Mafi.Core.Factory.Machines;
using Mafi.Core.Factory.Datacenters;
using Mafi.Core.Factory.NuclearReactors;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Fleet;
using Mafi.Core.Vehicles.Excavators;
using Mafi.Core.Vehicles.TreeHarvesters;
using Mafi.Core.Vehicles.Trucks;
using Mafi.Core.Buildings.Farms;
using Mafi.Core.Buildings.Cargo.Modules;
using Mafi.Core.Buildings.Cargo;
using Mafi.Core.Buildings.ResearchLab;
using Mafi.Core.Buildings.VehicleDepots;
using Mafi.Core.Buildings.FuelStations;
using Mafi.Core.Buildings.Storages;
using Mafi.Core.Buildings.Settlements;
using Mafi.Core.Buildings.SpaceProgram;
using Mafi.Core.Buildings.Waste;
using Mafi.Core.Buildings.RainwaterHarvesters;
using Mafi.Collections.ImmutableCollections;
using Mafi.Base.Prototypes.Machines.PowerGenerators;
using System.Reflection;
using Mafi.Core.Buildings.Mine;
using Mafi.Core.World.Contracts;
using Mafi.Core.Game;
using Mafi.Base.Prototypes.Machines;
using Mafi.Core.Factory.Transports;
using System.Linq;
using Mafi.Core.Localization.Quantity;
using Mafi.Unity;
using System;
using System.Diagnostics;
using Mafi.Core.Economy;
using Mafi.Collections;
using Mafi.Core.Research;
using Mafi.Core.UnlockingTree;
using Mafi.Base.Prototypes.Research;
using Mafi.Base.Prototypes.Buildings.ThermalStorages;

namespace DataExtractorMod
{

    public sealed class DataExtractor : IMod
    {
        public string Name => "Data Extractor Mod By ItsDesm (modified by doubleaxe)";
        public AssetsDb assetsDb { get; private set; }
        public int Version => 1;

        public bool IsUiOnly => false;

        public Option<IConfig> ModConfig => Option<IConfig>.None;

        public ModManifest Manifest { get; set; }

        public ModJsonConfig JsonConfig { get; set; }

        public static readonly string MOD_ROOT_DIR_PATH = new FileSystemHelper().GetDirPath(FileType.Mod, false);
        public static readonly string MOD_DIR_PATH = Path.Combine(MOD_ROOT_DIR_PATH, "DataExtractor");
        public static readonly string MOD_OUTPUT_PATH = Path.Combine(MOD_DIR_PATH, "Outputs");
        public static readonly string PLUGIN_DIR_PATH = Path.Combine(MOD_DIR_PATH, "Plugins");

        public static readonly bool DEBUG = true;
        public struct spriteToExport { public string category; public string icon; };
        public Dictionary<string, spriteToExport> sprites = new Dictionary<string, spriteToExport>();
        public DataExtractor(ModManifest manifest, CoreModConfig config)
        {
            //AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Log.Info("Loaded Data Extractor Mod");

            this.Manifest = manifest;
            this.JsonConfig = new ModJsonConfig(this);
        }

        public void EarlyInit(DependencyResolver resolver)
        {
            Log.Info("Data Extractor Mod EarlyInit");
        }


        public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
        {
            Log.Info("Data Extractor Mod EarlyInit");
            //assetsDb = resolver.Resolve<AssetsDb>();
            //foreach (var sprite in this.sprites)
            //{
            //    var icon = assetsDb.GetSharedSprite(sprite.Value.icon);
            //    assetsDb.TryGetSharedAsset<Texture2D>(sprite.Value.icon, out var texture2d);
            //    Log.Info("Icon Export" + sprite.Key);
            //    var png = UnityEngine.ImageConversion.EncodeToPNG(texture2d);
            //    this.WriteFile(sprite.Value.category + "_" + sprite.Key + ".png", png);
            //}
        }

        private void EnsureOutputDir()
        {
            if (!Directory.Exists(MOD_OUTPUT_PATH))
                Directory.CreateDirectory(MOD_OUTPUT_PATH);
        }

        private void WriteOutput(string fileName, string content)
        {
            EnsureOutputDir();
            string filePath = Path.Combine(MOD_OUTPUT_PATH, fileName);
            File.WriteAllText(filePath, content);
            Log.Info($"Wrote {fileName} to {MOD_OUTPUT_PATH}");
        }

        public void WriteFile(string fileName, byte[] bytes)
        {
            EnsureOutputDir();
            string filePath = Path.Combine(MOD_OUTPUT_PATH, fileName);
            File.WriteAllBytes(filePath, bytes);
            Log.Info($"Wrote {fileName} to {MOD_OUTPUT_PATH}");
        }

        private static void LogError(string context, Exception ex = null)
        {
            Log.Info("###################################################");
            Log.Info("ERROR " + context);
            Log.Info("###################################################");
            if (ex != null) Log.Error(ex.ToString());
        }

        /*
         * -------------------------------------
         * JSON Helper Methods
         * -------------------------------------
        */

        /// <summary>Wraps a list of pre-formatted "key":value strings into a JSON object.</summary>
        private static string WrapJsonObject(List<string> props)
        {
            var obj = new System.Text.StringBuilder();
            obj.AppendLine("{");
            obj.AppendLine(props.JoinStrings(","));
            obj.AppendLine("}");
            return obj.ToString();
        }

        /// <summary>Formats a collection of ProductQuantity into a comma-separated JSON string of {product, quantity} objects.</summary>
        private static string FormatProductCosts(SmallImmutableArray<ProductQuantity> products)
        {
            var items = new List<string>();
            foreach (ProductQuantity cost in products)
            {
                items.Add(MakeVehicleProductJsonObject(
                    cost.Product.Strings.Name.ToString(),
                    cost.Quantity.ToString()
                ));
            }
            return items.JoinStrings(",");
        }

        /*
         * -------------------------------------
         * JSON Formatters For Specific Machine Types
         * -------------------------------------
        */


        public static string MakeRecipeIOJsonObject(
            string name,
            string quantity,
            bool optional = false
        )
        {
            var props = new List<string>
            {
                $"\"name\":\"{name}\"",
                $"\"quantity\":{quantity}"
            };
            if (optional)
                props.Add($"\"optional\":true");
            return WrapJsonObject(props);
        }

        public struct MachineCoolant
        {
            public ProductProto productIn;
            public ProductProto productOut;
            public int quantityIn;
            public int quantityOut;
            public bool optional;
        }

        public static string MakeMachineJsonObject(
            string id,
            string name,
            string category,
            string next_tier,
            string workers,
            string maintenance_cost_units,
            string maintenance_cost_quantity,
            string electricity_consumed,
            string electricity_generated,
            string computing_consumed,
            string computing_generated,
            string product_type,
            string capacity,
            string unity_cost,
            string research_speed,
            string icon,
            string build_costs,
            string recipes,
            RelTile3i footprint,
            MachineCoolant? coolant = null
        )
        {
            var props = new List<string>
            {
                $"\"id\":\"{id}\"",
                $"\"name\":\"{name}\"",
                $"\"category\":\"{category}\"",
                $"\"next_tier\":\"{next_tier}\"",
                $"\"workers\":{workers}",
                $"\"maintenance_cost_units\":\"{maintenance_cost_units}\"",
                $"\"maintenance_cost_quantity\":{maintenance_cost_quantity}",
                $"\"electricity_consumed\":{electricity_consumed}",
                $"\"electricity_generated\":{electricity_generated}",
                $"\"computing_consumed\":{computing_consumed}",
                $"\"computing_generated\":{computing_generated}",
                $"\"product_type\":\"{product_type}\"",
                $"\"storage_capacity\":{capacity}",
                $"\"unity_cost\":{unity_cost}",
                $"\"research_speed\":{research_speed}",
                $"\"icon_path\":\"{icon}\"",
                $"\"build_costs\":[{build_costs}]",
                $"\"recipes\":[{recipes}]"
            };

            if (coolant.HasValue)
            {
                MachineCoolant c = coolant.Value;
                props.Add($"\"coolant\":{{\"product_in\":\"{c.productIn.Strings.Name.ToString()}\"," +
                    $"\"product_out\":\"{c.productOut.Strings.Name.ToString()}\"," +
                    $"\"quantity_in\":{c.quantityIn}," +
                    $"\"quantity_out\":{c.quantityOut}," +
                    $"\"optional\":{c.optional.ToString().ToLower()} }}");
            }
            if (footprint.X > 0 || footprint.Y > 0)
                props.Add($"\"footprint\":[{footprint.X},{footprint.Y}]");

            return WrapJsonObject(props);
        }

        /// <summary>Overload without next_tier and product_type (defaults to "").</summary>
        public static string MakeMachineJsonObject(
            string id, string name, string category,
            string workers, string maintenance_cost_units, string maintenance_cost_quantity,
            string electricity_consumed, string electricity_generated,
            string computing_consumed, string computing_generated,
            string capacity, string unity_cost, string research_speed,
            string icon, string build_costs, string recipes,
            RelTile3i footprint
        )
        {
            return MakeMachineJsonObject(id, name, category, "", workers,
                maintenance_cost_units, maintenance_cost_quantity,
                electricity_consumed, electricity_generated,
                computing_consumed, computing_generated, "",
                capacity, unity_cost, research_speed,
                icon, build_costs, recipes, footprint);
        }

        public static string MakeTransportJsonObject(
            string id, string name, string category, string next_tier,
            string maintenance_cost_units, string maintenance_cost_quantity,
            string electricity_consumed, string throughput_per_second,
            string length_per_cost, string icon, string build_costs
        )
        {
            return WrapJsonObject(new List<string>
            {
                $"\"id\":\"{id}\"",
                $"\"name\":\"{name}\"",
                $"\"category\":\"{category}\"",
                $"\"next_tier\":\"{next_tier}\"",
                $"\"maintenance_cost_units\":\"{maintenance_cost_units}\"",
                $"\"maintenance_cost_quantity\":{maintenance_cost_quantity}",
                $"\"electricity_consumed\":{electricity_consumed}",
                $"\"throughput_per_second\":{throughput_per_second}",
                $"\"length_per_cost\":{length_per_cost}",
                $"\"icon_path\":\"{icon}\"",
                $"\"build_costs\":[{build_costs}]"
            });
        }

        public static string MakeRecipeJsonObject(
            string id, string name, string duration,
            string inputs, string outputs, float power_multiplier = 1.0f
        )
        {
            return WrapJsonObject(new List<string>
            {
                $"\"id\":\"{id}\"",
                $"\"name\":\"{name}\"",
                $"\"duration\":{duration}",
                $"\"power_multiplier\":{power_multiplier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}",
                $"\"inputs\":[{inputs}]",
                $"\"outputs\":[{outputs}]"
            });
        }

        public static string MakeVehicleJsonObject(string name, string costs)
        {
            return WrapJsonObject(new List<string>
            {
                $"\"name\":\"{name}\"",
                $"\"costs\":[{costs}]"
            });
        }

        public static string MakeVehicleProductJsonObject(string product, string quantity)
        {
            return WrapJsonObject(new List<string>
            {
                $"\"product\":\"{product}\"",
                $"\"quantity\":{quantity}"
            });
        }

        public static string MakeEngineJsonObject(string name, string capacity, string crew, string costs)
        {
            return WrapJsonObject(new List<string>
            {
                $"\"name\":\"{name}\"",
                $"\"fuel_capacity\":{capacity}",
                $"\"extra_crew_needed\":{crew}",
                $"\"costs\":[{costs}]"
            });
        }

        public static string MakeGunJsonObject(string name, string range, string damage, string crew, string costs)
        {
            return WrapJsonObject(new List<string>
            {
                $"\"name\":\"{name}\"",
                $"\"range\":{range}",
                $"\"damage\":{damage}",
                $"\"extra_crew_needed\":{crew}",
                $"\"costs\":[{costs}]"
            });
        }

        public static string MakeArmorJsonObject(string name, string hp, string armor, string costs)
        {
            return WrapJsonObject(new List<string>
            {
                $"\"name\":\"{name}\"",
                $"\"hp_upgrade\":{hp}",
                $"\"armor_upgrade\":{armor}",
                $"\"costs\":[{costs}]"
            });
        }

        public static string MakeBridgeJsonObject(string name, string hp, string radar, string crew, string costs)
        {
            return WrapJsonObject(new List<string>
            {
                $"\"name\":\"{name}\"",
                $"\"hp_upgrade\":{hp}",
                $"\"radar_upgrade\":{radar}",
                $"\"extra_crew_needed\":{crew}",
                $"\"costs\":[{costs}]"
            });
        }

        public static string MakeTankJsonObject(string name, string added_capacity, string costs)
        {
            return WrapJsonObject(new List<string>
            {
                $"\"name\":\"{name}\"",
                $"\"added_capacity\":{added_capacity}",
                $"\"costs\":[{costs}]"
            });
        }

        public static string MakeProductJsonObject(
            string id, string name, string type, string icon,
            ColorRgba color, FormatInfo format
        )
        {
            var props = new List<string>
            {
                $"\"id\":\"{id}\"",
                $"\"name\":\"{name}\"",
                $"\"icon\":\"{productNameToIcon(name)}\"",
                $"\"type\":\"{type}\"",
                $"\"icon_path\":\"{icon}\""
            };
            if (color.A > 0) props.Add($"\"color\":\"{color}\"");
            props.Add($"\"unit\":\"{format.UnitStr}\"");
            return WrapJsonObject(props);
        }

        public static string MakeTerrainMaterialJsonObject(
            string id, string name, string mined_product, string mining_hardness,
            string mined_quantity_per_tile_cubed, string disruption_recovery_time,
            string is_hardened_floor, string max_collapse_height_diff,
            string min_collapse_height_diff, string mined_quantity_mult,
            string vehicle_traversal_cost
        )
        {
            return WrapJsonObject(new List<string>
            {
                $"\"id\":\"{id}\"",
                $"\"name\":\"{name}\"",
                $"\"mined_product\":\"{mined_product}\"",
                $"\"mining_hardness\":\"{mining_hardness}\"",
                $"\"mined_quantity_per_tile_cubed\":{mined_quantity_per_tile_cubed}",
                $"\"disruption_recovery_time\":{disruption_recovery_time}",
                $"\"is_hardened_floor\":{is_hardened_floor}",
                $"\"max_collapse_height_diff\":{max_collapse_height_diff}",
                $"\"min_collapse_height_diff\":{min_collapse_height_diff}",
                $"\"mined_quantity_mult\":\"{mined_quantity_mult}\"",
                $"\"vehicle_traversal_cost\":{vehicle_traversal_cost}"
            });
        }

        public static string MakeContractJsonObject(
           string id, string product_to_buy_name, string product_to_buy_quantity,
           string product_to_pay_with_name, string product_to_pay_with_quantity,
           string unity_per_month, string unity_per_100_bought,
           string unity_to_establish, string min_reputation_required
       )
        {
            return WrapJsonObject(new List<string>
            {
                $"\"id\":\"{id}\"",
                $"\"product_to_buy_name\":\"{product_to_buy_name}\"",
                $"\"product_to_buy_quantity\":{product_to_buy_quantity}",
                $"\"product_to_pay_with_name\":\"{product_to_pay_with_name}\"",
                $"\"product_to_pay_with_quantity\":{product_to_pay_with_quantity}",
                $"\"unity_per_month\":{unity_per_month}",
                $"\"unity_per_100_bought\":{unity_per_100_bought}",
                $"\"unity_to_establish\":{unity_to_establish}",
                $"\"min_reputation_required\":{min_reputation_required}"
            });
        }

        public static string MakeResearchJsonObject(string id, string name, string difficulty, string total_steps)
        {
            return WrapJsonObject(new List<string>
            {
                $"\"id\":\"{id}\"",
                $"\"name\":\"{name}\"",
                $"\"difficulty\":{difficulty}",
                $"\"total_steps\":{total_steps}"
            });
        }

        public static string productNameToIcon(string n)
        {
            n = n.Replace("(", "").Replace(")", "");
            // Order matters: check longest suffixes first to avoid partial matches
            string[] romanNumerals = { " IV", " III", " II", " V", " I" };
            string[] arabicDigits = { "4", "3", "2", "5", "1" };
            for (int i = 0; i < romanNumerals.Length; i++)
            {
                if (n.EndsWith(romanNumerals[i]))
                {
                    n = n.Substring(0, n.Length - romanNumerals[i].Length) + arabicDigits[i];
                    break;
                }
            }
            return n.Replace(" ", "");
        }

        public static string MakeRecipeJsonObject(
            ProtosDb protosDb,
            IRecipeForUi recipe,
            string defaultId = "",
            string defaultName = ""
        )
        {
            try
            {
                var duration = (recipe.Duration.Ticks / 10.0f);

                string recipe_id = recipe.Id.ToString();
                string recipe_name = (recipe is RecipeProto recipeProto)
                    ? recipeProto.Strings.Name.ToString()
                    : recipe.Id.ToString();

                if (recipe_id.Equals("RecipeForUiData") && !defaultId.IsEmpty())
                    recipe_id = defaultId;
                if (recipe_name.Equals("RecipeForUiData") && !defaultName.IsEmpty())
                    recipe_name = defaultName;

                string recipe_duration = duration <= 0.1f ? "0" : duration.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                float power_mult = (recipe is RecipeProto rp) ? rp.PowerMultiplier.ToFloat() : Percent.Hundred.ToFloat();

                var inputItems = new List<string>();
                foreach (RecipeInput input in recipe.AllUserVisibleInputs)
                    inputItems.Add(MakeRecipeIOJsonObject(input.Product.Strings.Name.ToString(), input.Quantity.Value.ToString()));

                var outputItems = new List<string>();
                foreach (RecipeOutput output in recipe.AllUserVisibleOutputs)
                    outputItems.Add(MakeRecipeIOJsonObject(output.Product.Strings.Name.ToString(), output.Quantity.Value.ToString()));

                return MakeRecipeJsonObject(
                    recipe_id,
                    recipe_name,
                    recipe_duration,
                    inputItems.JoinStrings(","),
                    outputItems.JoinStrings(","),
                    power_mult
                );
            }
            catch (Exception e)
            {
                LogError("MakeRecipeJsonObject");
                throw;
            }
        }

        public static List<string> MakeRecipesJsonObject(
            ProtosDb protosDb,
            IEnumerable<IRecipeForUi> machineRecipes,
            string defaultId = "",
            string defaultName = ""
        )
        {
            List<string> recipeItems = new List<string> { };

            int i = 0;
            foreach (IRecipeForUi recipe in machineRecipes)
            {
                string defaultIdThis = defaultId.IsEmpty() ? defaultId : (defaultId + ((i != 0) ? i.ToString() : ""));
                string defaultNameThis = defaultName.IsEmpty() ? defaultName : (defaultName + ((i != 0) ? (" " + i.ToString()) : ""));

                string machineRecipeJson = MakeRecipeJsonObject(
                    protosDb,
                    recipe,
                    defaultIdThis,
                    defaultNameThis
                );
                recipeItems.Add(machineRecipeJson);
                i++;
            }
            return recipeItems;
        }

        public static void DumpObject(List<string> DUMP, string name, object element)
        {
            if (!DEBUG)
                return;
            var content = GenericToDataString.ObjectDumper.Dump(element);
            DUMP.Add(name);
            DUMP.Add("");
            DUMP.Add(content);
            DUMP.Add("");
            DUMP.Add("");
        }

        public void RegisterPrototypes(ProtoRegistrator registrator)
        {

            ProtosDb protosDb = registrator.PrototypesDb;

            SourceProductsAnalyzer sourceProdAnal = new SourceProductsAnalyzer(protosDb);

            List<string> DUMP = new List<string> { };

            string game_version = typeof(Mafi.Base.BaseMod).GetTypeInfo().Assembly.GetName().Version.ToString();


            /*
             * -------------------------------------
             * Part 1  - Ship Upgrades.
             * -------------------------------------
            */

            List<string> upgradeItems = new List<string> { };
            List<string> engineItems = new List<string> { };
            List<string> gunItems = new List<string> { };
            List<string> armorItems = new List<string> { };
            List<string> bridgeItems = new List<string> { };
            List<string> tankItems = new List<string> { };

            IEnumerable<FleetEnginePartProto> engines = protosDb.All<FleetEnginePartProto>();
            foreach (FleetEnginePartProto item in engines)
            {
                try
                {
                    string vehicleJson = MakeEngineJsonObject(
                        item.Strings.Name.ToString(),
                        item.FuelCapacity.ToString(),
                        item.ExtraCrew.BonusValue.ToString(),
                        FormatProductCosts(item.Value.Products)
                    );
                    engineItems.Add(vehicleJson);
                }
                catch { LogError(item.ToString()); }

            }
            upgradeItems.Add($"\"engines\":[{engineItems.JoinStrings(",")}]");

            IEnumerable<FleetWeaponProto> guns = protosDb.All<FleetWeaponProto>();
            foreach (FleetWeaponProto item in guns)
            {
                try
                {
                    string vehicleJson = MakeGunJsonObject(
                        item.Strings.Name.ToString(),
                        item.Range.ToString(),
                        item.Damage.ToString(),
                        item.ExtraCrew.BonusValue.ToString(),
                        FormatProductCosts(item.Value.Products)
                    );
                    gunItems.Add(vehicleJson);
                }
                catch { LogError(item.ToString()); }

            }
            upgradeItems.Add($"\"weapons\":[{gunItems.JoinStrings(",")}]");

            IEnumerable<UpgradeHullProto> armor = protosDb.All<UpgradeHullProto>();
            foreach (UpgradeHullProto item in armor)
            {
                try
                {
                    string vehicleJson = MakeArmorJsonObject(
                        item.Strings.Name.ToString(),
                        "0",
                        "0",
                        FormatProductCosts(item.Value.Products)
                    );
                    armorItems.Add(vehicleJson);
                }
                catch { LogError(item.ToString()); }

            }
            upgradeItems.Add($"\"armor\":[{armorItems.JoinStrings(",")}]");

            IEnumerable<FleetBridgePartProto> bridges = protosDb.All<FleetBridgePartProto>();
            foreach (FleetBridgePartProto item in bridges)
            {
                try
                {
                    string vehicleJson = MakeBridgeJsonObject(
                        item.Strings.Name.ToString(),
                        "0",
                        "0",
                        item.ExtraCrew.BonusValue.ToString(),
                        FormatProductCosts(item.Value.Products)
                    );
                    bridgeItems.Add(vehicleJson);
                }
                catch { LogError(item.ToString()); }

            }
            upgradeItems.Add($"\"bridges\":[{bridgeItems.JoinStrings(",")}]");

            IEnumerable<FleetFuelTankPartProto> tanks = protosDb.All<FleetFuelTankPartProto>();
            foreach (FleetFuelTankPartProto item in tanks)
            {
                try
                {
                    string vehicleJson = MakeTankJsonObject(
                        item.Strings.Name.ToString(),
                        item.AddedFuelCapacity.ToString(),
                        FormatProductCosts(item.Value.Products)
                    );
                    tankItems.Add(vehicleJson);
                }
                catch { LogError(item.ToString()); }

            }
            upgradeItems.Add($"\"fuel_tanks\":[{tankItems.JoinStrings(",")}]");

            this.WriteOutput("ship_upgrades.json", $"{{\"game_version\":\"{game_version}\",{upgradeItems.JoinStrings(",")}}}");

            /*
             * -------------------------------------
             * Part 2  - Vehicles.
             * -------------------------------------
            */

            List<string> vehicleItems = new List<string> { };

            var vehicles = new List<DrivingEntityProto>();
            vehicles.AddRange(protosDb.All<TruckProto>());
            vehicles.AddRange(protosDb.All<ExcavatorProto>());
            vehicles.AddRange(protosDb.All<TreeHarvesterProto>());

            foreach (DrivingEntityProto vehicle in vehicles)
            {
                try
                {
                    string vehicleJson = MakeVehicleJsonObject(
                        vehicle.Strings.Name.ToString(),
                        FormatProductCosts(vehicle.CostToBuild.Products)
                    );
                    vehicleItems.Add(vehicleJson);
                }
                catch { LogError(vehicle.ToString()); }

            }

            this.WriteOutput("vehicles.json", $"{{\"game_version\":\"{game_version}\",\"vehicles\":[{vehicleItems.JoinStrings(",")}]}}");

            /*
             * -------------------------------------
             * Part 3  - Power Generation Machines. (Behave Uniquely)
             * -------------------------------------
            */

            List<string> machineItems = new List<string> { };

            // -------------------------
            // Turbines
            // -------------------------

            IEnumerable<MechPowerGeneratorFromProductProto> turbines = protosDb.All<MechPowerGeneratorFromProductProto>();
            foreach (MechPowerGeneratorFromProductProto generator in turbines)
            {
                try
                {
                    string id = generator.Id.ToString();
                    string name = generator.Strings.Name.ToString();
                    string category = "";
                    string workers = generator.Costs.Workers.ToString();
                    string maintenance_cost_units = generator.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = generator.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string product_type = "";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";
                    string next_tier = "";
                    if (generator.Upgrade.NextTier.HasValue)
                    {
                        next_tier = generator.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in generator.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(generator.Costs.BaseConstructionCost.Products);

                    List<string> recipeItems = MakeRecipesJsonObject(protosDb, new IRecipeForUi[] { generator.Recipe });

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        generator.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        generator.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError(generator.ToString()); }
            }
            Log.Info("Completed Turbines");

            // -------------------------
            // Generators
            // -------------------------

            IEnumerable<ElectricityGeneratorFromMechPowerProto> generators = protosDb.All<ElectricityGeneratorFromMechPowerProto>();
            foreach (ElectricityGeneratorFromMechPowerProto generator in generators)
            {
                try
                {

                    string id = generator.Id.ToString();
                    string name = generator.Strings.Name.ToString();
                    string category = "";
                    string workers = generator.Costs.Workers.ToString();
                    string maintenance_cost_units = generator.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = generator.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in generator.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(generator.Costs.BaseConstructionCost.Products);

                    List<string> recipeItems = MakeRecipesJsonObject(protosDb, new IRecipeForUi[] { generator.Recipe });

                    var outputs = generator.Recipe.AllUserVisibleOutputs;
                    electricity_generated = outputs[0].Quantity.Value.ToString();

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        generator.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        generator.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError(generator.ToString()); }
            }
            Log.Info("Completed Generators");

            // -------------------------
            // Solar Panels
            // -------------------------

            IEnumerable<SolarElectricityGeneratorProto> solar = protosDb.All<SolarElectricityGeneratorProto>();
            foreach (SolarElectricityGeneratorProto generator in solar)
            {
                try
                {

                    string id = generator.Id.ToString();
                    string name = generator.Strings.Name.ToString();
                    string category = "";
                    string workers = generator.Costs.Workers.ToString();
                    string maintenance_cost_units = generator.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = generator.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = generator.OutputElectricity.Value.ToString();
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in generator.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(generator.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        generator.IconPath,
                        buildCosts,
                        "",
                        generator.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError(generator.ToString()); }
            }

            Log.Info("Completed Solar Panels");

            // -------------------------
            // Disel Generator
            // -------------------------

            IEnumerable<ElectricityGeneratorFromProductProto> powerMachines = protosDb.All<ElectricityGeneratorFromProductProto>();
            foreach (ElectricityGeneratorFromProductProto generator in powerMachines)
            {
                try
                {

                    string id = generator.Id.ToString();
                    string name = generator.Strings.Name.ToString();
                    string category = "";
                    string workers = generator.Costs.Workers.ToString();
                    string maintenance_cost_units = generator.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = generator.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = generator.OutputElectricity.Value.ToString();
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in generator.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(generator.Costs.BaseConstructionCost.Products);

                    List<string> recipeItems = MakeRecipesJsonObject(protosDb, new IRecipeForUi[] { generator.Recipe });

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        generator.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        generator.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError(generator.ToString()); }
            }

            Log.Info("Completed Diesel Generators");

            /*
             * -------------------------------------
             * Part 4  - General Machines.
             * -------------------------------------
            */

            //MaintenanceDepotProto is also MachineProto
            IEnumerable<MachineProto> machines = protosDb.All<MachineProto>();
            foreach (MachineProto machine in machines)
            {

                try
                {

                    List<IRecipeForUi> machineRecipes = machine.Recipes.AsEnumerable().ToList<IRecipeForUi>();

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = machine.ElectricityConsumed.Quantity.Value.ToString();
                    string electricity_generated = "0";
                    string computing_consumed = machine.ComputingConsumed.Value.ToString();
                    string computing_generated = "0";
                    string product_type = "";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";
                    string next_tier = "";
                    if (machine.Upgrade.NextTier.HasValue)
                    {
                        next_tier = machine.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    List<string> recipeItems = MakeRecipesJsonObject(protosDb, machineRecipes);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError(machine.ToString()); }
            }

            Log.Info("Completed General Machines");

            /*
             * -------------------------------------
             * Part 5  - Buildings. Uses Simpler Proto LookUp Method
             * -------------------------------------
            */

            IEnumerable<FarmProto> farms = protosDb.All<FarmProto>();
            IEnumerable<ProductProto> fertilizers = protosDb.Filter<ProductProto>(delegate (ProductProto product)
            {
                return product.GetParam<FertilizerProductParam>().Value != null;
            });
            ProductProto[] fertilizersArray = fertilizers.ToArray<ProductProto>();
            IEnumerable<CropProto> crops = protosDb.All<CropProto>();

            foreach (FarmProto item in farms)
            {

                try
                {

                    string id = item.Id.ToString();
                    string name = item.Strings.Name.ToString();
                    string category = "";
                    string workers = item.Costs.Workers.ToString();
                    string maintenance_cost_units = item.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = item.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string product_type = "";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";
                    string next_tier = "";
                    if (item.Upgrade.NextTier.HasValue)
                    {
                        next_tier = item.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in item.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(item.Costs.BaseConstructionCost.Products);

                    //recipes are build for max fertility
                    List<string> recipeItems = new List<string> { };
                    foreach (CropProto crop in crops)
                    {
                        if (crop.RequiresGreenhouse && !item.IsGreenhouse)
                            continue;
                        if ((crop.ProductProduced == null) || (crop.ProductProduced.Quantity.Value == 0))
                            continue;

                        var duration = (crop.DaysToGrow * 2);
                        List<string> inputItems = new List<string> { };
                        List<string> outputItems = new List<string> { };

                        string machineRecipeInputJson;
                        string machineRecipeOutputJson;

                        machineRecipeOutputJson = MakeRecipeIOJsonObject(crop.ProductProduced.Product.Strings.Name.ToString(), crop.ProductProduced.Quantity.ScaledBy(item.YieldMultiplier).ToString());
                        outputItems.Add(machineRecipeOutputJson);

                        if (item.HasIrrigationAndFertilizerSupport)
                        {
                            if (crop.ConsumedWaterPerDay.Value != null)
                            {
                                machineRecipeInputJson = MakeRecipeIOJsonObject("Water", (crop.ConsumedWaterPerDay.Value.ScaledBy(item.DemandsMultiplier) * crop.DaysToGrow).ToString());
                                inputItems.Add(machineRecipeInputJson);
                            }
                        }

                        if (item.HasIrrigationAndFertilizerSupport && (fertilizersArray.Length != 0))
                        {
                            foreach (ProductProto fertilizer in fertilizersArray)
                            {
                                List<string> inputItems2 = new List<string>(inputItems);
                                Option<FertilizerProductParam> fertilizerParam = fertilizer.GetParam<FertilizerProductParam>();
                                Fix64 fertilizerFerDay = (crop.ConsumedFertilityPerDay.ToFix64() * crop.DaysToGrow) / fertilizerParam.Value.FertilityPerQuantity.ToFix64();
                                machineRecipeInputJson = MakeRecipeIOJsonObject(fertilizer.Strings.Name.ToString(), fertilizerFerDay.ScaledBy(item.DemandsMultiplier).ToFix32().ToString());
                                inputItems2.Add(machineRecipeInputJson);

                                string machineRecipeJson = MakeRecipeJsonObject(
                                    crop.Id.ToString() + "_" + fertilizer.Id.ToString(),
                                    crop.Strings.Name.ToString() + " " + fertilizer.Strings.Name.ToString(),
                                    duration.ToString(),
                                    inputItems2.JoinStrings(","),
                                    outputItems.JoinStrings(",")
                                );
                                recipeItems.Add(machineRecipeJson);
                            }
                        }
                        else
                        {
                            string machineRecipeJson = MakeRecipeJsonObject(
                                crop.Id.ToString(),
                                crop.Strings.Name.ToString(),
                                duration.ToString(),
                                inputItems.JoinStrings(","),
                                outputItems.JoinStrings(",")
                            );
                            recipeItems.Add(machineRecipeJson);
                        }

                    }

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        item.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        item.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError(item.ToString()); }
            }
            Log.Info("Completed Farms");

            IEnumerable<AnimalFarmProto> animalFarms = protosDb.All<AnimalFarmProto>();
            foreach (AnimalFarmProto item in animalFarms)
            {

                try
                {

                    string id = item.Id.ToString();
                    string name = item.Strings.Name.ToString();
                    string category = "";
                    string workers = item.Costs.Workers.ToString();
                    string maintenance_cost_units = item.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = item.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string product_type = "";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";
                    string next_tier = "";
                    if (item.Upgrade.NextTier.HasValue)
                    {
                        next_tier = item.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in item.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(item.Costs.BaseConstructionCost.Products);

                    //recipe is built for max animals (500 chickens)
                    List<string> recipeItems = new List<string> { };
                    var duration = 60;

                    List<string> inputItems = new List<string> { };
                    List<string> outputItems = new List<string> { };

                    string machineRecipeInputJson;
                    machineRecipeInputJson = MakeRecipeIOJsonObject(item.FoodPerAnimalPerMonth.Product.Strings.Name.ToString(), (item.AnimalsCapacity * item.FoodPerAnimalPerMonth.Quantity.Value).ToString());
                    inputItems.Add(machineRecipeInputJson);
                    machineRecipeInputJson = MakeRecipeIOJsonObject(item.WaterPerAnimalPerMonth.Product.Strings.Name.ToString(), (item.AnimalsCapacity * item.WaterPerAnimalPerMonth.Quantity.Value).ToString());
                    inputItems.Add(machineRecipeInputJson);

                    string machineRecipeOutputJson;
                    var produced = item.ProducedPerAnimalPerMonth;
                    if (produced != null)
                    {
                        machineRecipeOutputJson = MakeRecipeIOJsonObject(produced.Value.Product.Strings.Name.ToString(), (item.AnimalsCapacity * produced.Value.Quantity.Value).ToString());
                        outputItems.Add(machineRecipeOutputJson);
                    }
                    //must be divided by 100, but according to wiki it produces 10 carcass instead of 20
                    machineRecipeOutputJson = MakeRecipeIOJsonObject(item.CarcassProto.Strings.Name.ToString(), ((item.AnimalsBornPer100AnimalsPerMonth * item.AnimalsCapacity) / 200).ToString());
                    outputItems.Add(machineRecipeOutputJson);

                    string machineRecipeJson = MakeRecipeJsonObject(
                        id,
                        name,
                        duration.ToString(),
                        inputItems.JoinStrings(","),
                        outputItems.JoinStrings(",")
                    );
                    recipeItems.Add(machineRecipeJson);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        item.IconPath,
                        buildCosts,
                        machineRecipeJson,
                        item.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError(item.ToString()); }
            }
            Log.Info("Completed Animal Farms");

            IEnumerable<CargoDepotProto> cargoDepots = protosDb.All<CargoDepotProto>();
            foreach (CargoDepotProto item in cargoDepots)
            {

                try
                {

                    string id = item.Id.ToString();
                    string name = item.Strings.Name.ToString();
                    string category = "";
                    string workers = item.Costs.Workers.ToString();
                    string maintenance_cost_units = item.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = item.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string product_type = "";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";
                    string next_tier = "";
                    if (item.Upgrade.NextTier.HasValue)
                    {
                        next_tier = item.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in item.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(item.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        item.IconPath,
                        buildCosts,
                        "",
                        item.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError(item.ToString()); }
            }
            Log.Info("Completed Cargo Depots");

            IEnumerable<CargoDepotModuleProto> cargoModules = protosDb.All<CargoDepotModuleProto>();
            foreach (CargoDepotModuleProto item in cargoModules)
            {

                try
                {

                    string id = item.Id.ToString();
                    string name = item.Strings.Name.ToString();
                    string category = "";
                    string product_type = "";
                    string capacity = item.Capacity.ToString();
                    string workers = item.Costs.Workers.ToString();
                    string maintenance_cost_units = item.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = item.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string unity_cost = "0";
                    string research_speed = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string next_tier = "";
                    if (item.Upgrade.NextTier.HasValue)
                    {
                        next_tier = item.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in item.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(item.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        item.IconPath,
                        buildCosts,
                        "",
                        item.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError(item.ToString()); }
            }
            Log.Info("Completed Cargo Modules");

            IEnumerable<ResearchLabProto> labs = protosDb.All<ResearchLabProto>();
            foreach (ResearchLabProto item in labs)
            {

                try
                {

                    string id = item.Id.ToString();
                    string name = item.Strings.Name.ToString();
                    string category = "";
                    string workers = item.Costs.Workers.ToString();
                    string maintenance_cost_units = item.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = item.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = item.ElectricityConsumed.Quantity.Value.ToString();
                    string electricity_generated = "0";
                    string computing_consumed = item.ComputingConsumed.ToString();
                    string unity_cost = item.UnityMonthlyCost.ToString();
                    string recipes = "";
                    Fix32 research_speed = (60 / item.DurationOfRecipe.Seconds) * item.SciencePerRecipe;
                    string product_type = "";
                    string capacity = "0";
                    string computing_generated = "0";
                    string next_tier = "";
                    if (item.Upgrade.NextTier.HasValue)
                    {
                        next_tier = item.Upgrade.NextTier.Value.Id.ToString();
                    }

                    if (item.Id.ToString() != "ResearchLab1")
                    {
                        Log.Info($"Processing {item.Id.ToString()}");
                        List<string> recipeItems = MakeRecipesJsonObject(protosDb, item.Recipes.AsEnumerable(), id, name);
                        recipes = recipeItems.JoinStrings(",");
                        foreach (var rec in item.Recipes)
                        {
                            Log.Info($"  Checking recipe {rec.ToString()}");
                            foreach (var outp in rec.AllUserVisibleOutputs)
                            {
                                var firstInput = rec.AllUserVisibleInputs[0];
                                Log.Info($"  Checking {outp.Product.Strings.Name.ToString()}");
                                if (outp.Product.Id.ToString() == "Product_Recyclables")
                                {

                                    firstInput.Product.TryGetParam<ApplyRecyclingRatioOnSourcesParam>(out var recParamInput);
                                    Log.Info($" Recyclable Product: {firstInput.Product.Strings.Name.ToString()}: {firstInput.Quantity.Value.ToString()} is Recyclable: {firstInput.Product.IsRecyclable} with ratios? {recParamInput.ToStringSafe()}");

                                    var result = new Lyst<ProductQuantity>();
                                    sourceProdAnal.GetSourceProductsFor(firstInput.Product, new Quantity(10000), result, true);

                                    foreach (var res in result)
                                    {
                                        res.Product.TryGetParam<ApplyRecyclingRatioOnSourcesParam>(out var recParam);
                                        Log.Info($" Source Product: {res.Product.Strings.Name.ToString()} qty: {res.Quantity.ToString()} is Recyclable: {res.Product.IsRecyclable} with ratios? {recParam.ToStringSafe()}");
                                    }
                                }
                            }
                        }
                    }

                    foreach (var cat in item.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(item.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed.ToString(),
                        item.IconPath,
                        buildCosts,
                        recipes,
                        item.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch
                {
                    LogError(item.ToString() + item.Id.ToString());
                }
            }
            Log.Info("Completed Research Labs");

            IEnumerable<VehicleDepotProto> depotsVehicles = protosDb.All<VehicleDepotProto>();
            foreach (VehicleDepotProto machine in depotsVehicles)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = machine.ElectricityConsumed.Quantity.Value.ToString();
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string product_type = "";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";
                    string next_tier = "";
                    if (machine.Upgrade.NextTier.HasValue)
                    {
                        next_tier = machine.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        "",
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }
            Log.Info("Completed Vehicle Depots");

            IEnumerable<FuelStationProto> depotsFuelds = protosDb.All<FuelStationProto>();
            foreach (FuelStationProto machine in depotsFuelds)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string product_type = "";
                    string capacity = machine.Capacity.ToString();
                    string unity_cost = "0";
                    string research_speed = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string next_tier = "";
                    if (machine.Upgrade.NextTier.HasValue)
                    {
                        next_tier = machine.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        "",
                        machine.Layout.LayoutSize
                    );

                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }
            Log.Info("Completed Fuel Stations");

            /*
             * -------------------------------------
             * Part - Products.
             * -------------------------------------
            */
            List<string> productsJson = new List<string> { };

            var prodNamesByType = new Dictionary<string, List<string>>
            {
                { "CountableProductProto", new List<string>() },
                { "LooseProductProto", new List<string>() },
                { "FluidProductProto", new List<string>() },
                { "MoltenProductProto", new List<string>() },
                { "VirtualProductProto", new List<string>() }
            };
            try
            {
                foreach (ProductProto product in protosDb.All<ProductProto>())
                {
                    string typeName = null;
                    if (product is CountableProductProto) typeName = "CountableProductProto";
                    else if (product is LooseProductProto) typeName = "LooseProductProto";
                    else if (product is FluidProductProto) typeName = "FluidProductProto";
                    else if (product is MoltenProductProto) typeName = "MoltenProductProto";
                    else if (product is VirtualProductProto) typeName = "VirtualProductProto";

                    if (typeName != null)
                    {
                        prodNamesByType[typeName].Add(product.Strings.Name.ToString());
                        var isSteam = product.Strings.Name.ToString().Contains("Steam");
                        productsJson.Add(MakeProductJsonObject(
                            product.Id.ToString(),
                            product.Strings.Name.ToString(),
                            product.Type.ToString(),
                            product.IconPath,
                            isSteam ? product.Graphics.TransportAccentColor : product.Graphics.TransportColor,
                            product.QuantityFormatter.GetFormatInfo(product, 1)
                           ));
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Products", ex);
            }
            Log.Info("Completed Products");

            this.WriteOutput("products.json", $"{{\"game_version\":\"{game_version}\",\"products\":[{productsJson.JoinStrings(",")}]}}");

            List<string> storageItems = new List<string> { };

            //NuclearWasteStorageProto is also instance of StorageProto
            IEnumerable<StorageProto> storages = protosDb.All<StorageProto>();
            foreach (StorageProto machine in storages)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string product_type = machine.ProductType.Value.ToString();
                    string capacity = machine.Capacity.ToString();
                    string unity_cost = "0";
                    string research_speed = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string next_tier = "";
                    if (machine.Upgrade.NextTier.HasValue)
                    {
                        next_tier = machine.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    string productTypeKey = machine.ProductType.Value.ToString();
                    List<string> StorageInputs;
                    if (!prodNamesByType.TryGetValue(productTypeKey, out StorageInputs))
                        StorageInputs = new List<string>();

                    List<string> recipeItems = new List<string> { };

                    foreach (string input in StorageInputs)
                    {
                        string recipe_name = input + " Storage";
                        string ioJson = MakeRecipeIOJsonObject(input, machine.Capacity.ToString());
                        recipeItems.Add(MakeRecipeJsonObject(recipe_name, recipe_name, "0", ioJson, ioJson));
                    }

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        "",
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                    string storageJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        machine.Layout.LayoutSize

                    );
                    storageItems.Add(storageJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            IEnumerable<SettlementHousingModuleProto> housing = protosDb.All<SettlementHousingModuleProto>();
            foreach (SettlementHousingModuleProto machine in housing)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string capacity = machine.Capacity.ToString();
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        "",
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            IEnumerable<MineTowerProto> mines = protosDb.All<MineTowerProto>();
            foreach (MineTowerProto machine in mines)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string capacity = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        "",
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            IEnumerable<SettlementWasteModuleProto> housingWaste = protosDb.All<SettlementWasteModuleProto>();
            foreach (SettlementWasteModuleProto machine in housingWaste)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string capacity = machine.Capacity.ToString();
                    string unity_cost = "0";
                    string research_speed = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        "",
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            IEnumerable<SettlementFoodModuleProto> housingFood = protosDb.All<SettlementFoodModuleProto>();
            foreach (SettlementFoodModuleProto machine in housingFood)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string capacity = machine.CapacityPerBuffer.ToString();
                    string unity_cost = "0";
                    string research_speed = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        "",
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            IEnumerable<SettlementModuleProto> housingNeed = protosDb.All<SettlementModuleProto>();
            foreach (SettlementModuleProto machine in housingNeed)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = machine.ElectricityConsumed.Quantity.Value.ToString();
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        "",
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            IEnumerable<NuclearReactorProto> reactors = protosDb.All<NuclearReactorProto>();
            foreach (NuclearReactorProto machine in reactors)
            {
                try
                {
                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = machine.ComputingConsumed.Value.ToString();
                    string computing_generated = "0";
                    string product_type = "";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";
                    string next_tier = "";
                    if (machine.Upgrade.NextTier.HasValue)
                    {
                        next_tier = machine.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    List<string> recipeItems = new List<string> { };

                    //default recipes are given at max power level
                    var recipeDurationAtMaxLevel = machine.Recipes.First().Duration;
                    var duration = (recipeDurationAtMaxLevel / 10);
                    var waterInPerDuration = (machine.WaterInPerPowerLevel.Quantity.Value * machine.MaxPowerLevel) * (recipeDurationAtMaxLevel.Seconds / machine.ProcessDuration.Seconds);
                    var steamOutPerDuration = (machine.SteamOutPerPowerLevel.Quantity.Value * machine.MaxPowerLevel) * (recipeDurationAtMaxLevel.Seconds / machine.ProcessDuration.Seconds);

                    //we keep recipe naming convention and order with prev version
                    int i = 0;
                    // Just make recipes for the max level. A reactor running at x0.5 at level 4 is the same as a reactor running at x1.0 at level 2
                    // And it's rare reactors aren't running with automatic management 
                    var level = machine.MaxPowerLevel;
                    foreach (var fuel in machine.FuelPairs)
                    {
                        var fuelPerDuration = (machine.MaxPowerLevel * recipeDurationAtMaxLevel.Seconds) / fuel.Duration.Seconds;

                        // If a machine has enrichment (FBR), the recipes should include it, one for each enrichment level
                        if (machine.Enrichment.HasValue)
                        {
                            var enrichment = machine.Enrichment.Value;
                            foreach (var step in enrichment.EnrichmentSteps)
                            {
                                i++;

                                string recipe_id = (id + "Enrichment" + ((i != 0) ? i.ToString() : ""));
                                string recipe_name = (name + " Enrichment " + step.BreedingRatio.ToString());

                                string machineRecipeJson;

                                List<string> inputItems = new List<string> { };
                                List<string> outputItems = new List<string> { };

                                machineRecipeJson = MakeRecipeIOJsonObject(machine.WaterInPerPowerLevel.Product.Strings.Name.ToString(), (waterInPerDuration / step.SteamReductionDiv).ToString());
                                inputItems.Add(machineRecipeJson);
                                machineRecipeJson = MakeRecipeIOJsonObject(machine.SteamOutPerPowerLevel.Product.Strings.Name.ToString(), (steamOutPerDuration / step.SteamReductionDiv).ToString());
                                outputItems.Add(machineRecipeJson);

                                machineRecipeJson = MakeRecipeIOJsonObject(fuel.FuelInProto.Strings.Name.ToString(), (step.FuelMultiplier.Apply(fuelPerDuration)).ToString());
                                inputItems.Add(machineRecipeJson);
                                machineRecipeJson = MakeRecipeIOJsonObject(fuel.SpentFuelOutProto.Strings.Name.ToString(), step.FuelMultiplier.Apply(fuelPerDuration).ToString());
                                outputItems.Add(machineRecipeJson);

                                var amount = (fuelPerDuration * step.BreedingRatio).ToStringRounded();
                                machineRecipeJson = MakeRecipeIOJsonObject(enrichment.InputProduct.Strings.Name.ToString(), amount.ToString(), false);
                                inputItems.Add(machineRecipeJson);
                                machineRecipeJson = MakeRecipeIOJsonObject(enrichment.OutputProduct.Strings.Name.ToString(), amount.ToString(), true);
                                outputItems.Add(machineRecipeJson);

                                machineRecipeJson = MakeRecipeJsonObject(
                                    recipe_id,
                                    recipe_name,
                                    "60",
                                    inputItems.JoinStrings(","),
                                    outputItems.JoinStrings(",")
                                );

                                recipeItems.Add(machineRecipeJson);
                            }
                        }
                        else
                        {
                            i++;

                            string recipe_id = (id + ((i != 0) ? i.ToString() : ""));
                            string recipe_name = (name + ((i != 0) ? (" " + i.ToString()) : ""));

                            List<string> inputItems = new List<string> { };
                            List<string> outputItems = new List<string> { };
                            string machineRecipeJson;

                            machineRecipeJson = MakeRecipeIOJsonObject(machine.WaterInPerPowerLevel.Product.Strings.Name.ToString(), waterInPerDuration.ToString());
                            inputItems.Add(machineRecipeJson);
                            machineRecipeJson = MakeRecipeIOJsonObject(machine.SteamOutPerPowerLevel.Product.Strings.Name.ToString(), steamOutPerDuration.ToString());
                            outputItems.Add(machineRecipeJson);

                            machineRecipeJson = MakeRecipeIOJsonObject(fuel.FuelInProto.Strings.Name.ToString(), fuelPerDuration.ToString());
                            inputItems.Add(machineRecipeJson);
                            machineRecipeJson = MakeRecipeIOJsonObject(fuel.SpentFuelOutProto.Strings.Name.ToString(), fuelPerDuration.ToString());
                            outputItems.Add(machineRecipeJson);
                            machineRecipeJson = MakeRecipeJsonObject(
                                recipe_id,
                                recipe_name,
                                duration.ToString(),
                                inputItems.JoinStrings(","),
                                outputItems.JoinStrings(",")
                            );
                            recipeItems.Add(machineRecipeJson);
                        }
                    }

                    MachineCoolant machineCoolant = new MachineCoolant
                    {
                        optional = true,
                        productIn = machine.CoolantIn,
                        productOut = machine.CoolantOut,
                        quantityIn = 30, // Not sure these are in the data anywhere
                        quantityOut = 30
                    };

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        machine.Layout.LayoutSize,
                        machineCoolant
                    );
                    machineItems.Add(machineJson);

                    DumpObject(DUMP, id, machine);
                    DumpObject(DUMP, id + "FuelPairs", machine.FuelPairs.AsEnumerable());
                    DumpObject(DUMP, id + "Enrichment", machine.Enrichment.HasValue ? machine.Enrichment.Value : null);
                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            IEnumerable<RocketAssemblyBuildingProto> rocketAssembly = protosDb.All<RocketAssemblyBuildingProto>();
            foreach (RocketAssemblyBuildingProto machine in rocketAssembly)
            {
                try
                {
                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = machine.ElectricityConsumed.Value.ToString();
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);
                    List<string> recipeItems = new List<string> { };
                    foreach (var ent in machine.BuildableEntities) {
                        if (ent == null || ent.Costs.BaseConstructionCost == null) continue;
                        List<string> inputItems = new List<string> { };
                        List<string> outputItems = new List<string> { };
                        string machineRecipeJson;

                        foreach (var prod in ent.Costs.BaseConstructionCost.Products)
                        {
                            if (prod.Product == null) continue;
                            inputItems.Add(MakeRecipeIOJsonObject(prod.Product.Strings.Name.ToString(), prod.Quantity.ToStringSafe()));
                        }

                        machineRecipeJson = MakeRecipeIOJsonObject(ent.Strings.Name.ToString(), "1");
                        outputItems.Add(machineRecipeJson);

                        machineRecipeJson = MakeRecipeJsonObject(
                            machine.Id.ToString() + "-" + ent.Id.ToString(),
                            ent.Id.ToString(),
                            ent.BuildDurationPerProduct.Seconds.ToString() + ent.BuildExtraDuration.Seconds.ToString(),
                            inputItems.JoinStrings(","),
                            outputItems.JoinStrings(",")
                        );
                        recipeItems.Add(machineRecipeJson);
                    }

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch(Exception e) { LogError("Depot " + machine.Id.ToString() + "\n" + e.ToString()); }
            }

            IEnumerable<RocketLaunchPadProto> rocketPad = protosDb.All<RocketLaunchPadProto>();
            foreach (RocketLaunchPadProto machine in rocketPad)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        "",
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            IEnumerable<WasteSortingPlantProto> wastePlant = protosDb.All<WasteSortingPlantProto>();
            foreach (WasteSortingPlantProto machine in wastePlant)
            {

                try
                {
                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string product_type = "";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";
                    string next_tier = "";
                    Log.Info("WasteSortingPlant Gathered basics");
                    if (machine.Upgrade.NextTier.HasValue)
                    {
                        next_tier = machine.Upgrade.NextTier.Value.Id.ToString();
                    }

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();
                    Log.Info("WasteSortingPlant Gathered categories");

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);
                    Log.Info("WasteSortingPlant Gathered products");
                    List<string> recipeItems = new List<string> { };
                    if (machine.Recipes != null)
                    {
                        recipeItems = MakeRecipesJsonObject(protosDb, machine.Recipes.AsEnumerable(), id, name);
                    }
                    Log.Info("WasteSortingPlant Gathered recipes");
                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);
                    Log.Info("WasteSortingPlant Created Json");
                    DumpObject(DUMP, id, machine);
                }
                catch (Exception e)
                {
                    LogError("WasteSortingPlant " + machine.Id.ToString(), e);
                    Log.Error("LineNum:" + (new StackTrace(e, true).GetFrame(0).GetFileLineNumber().ToString()));
                }
            }

            IEnumerable<ThermalStorageProto> thermalStorageProtos = protosDb.All<ThermalStorageProto>();
            foreach (ThermalStorageProto machine in thermalStorageProtos)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string product_type = "";
                    string capacity = machine.Capacity.ToString();
                    string unity_cost = "0";
                    string research_speed = "0";
                    string next_tier = "";
                    // No next tier checks needed

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    List<string> recipeItems = new List<string> { };
                    if (machine.Recipes != null)
                    {
                        recipeItems = MakeRecipesJsonObject(protosDb, machine.Recipes.AsEnumerable(), id, name);
                    }

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        next_tier,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        product_type,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch
                {
                    LogError("ThermalStorage " + machine.Id.ToString());
                }
            }
            Log.Info("Completed Thermal Storages");

            IEnumerable<RainwaterHarvesterProto> rainwaterHarvester = protosDb.All<RainwaterHarvesterProto>();
            foreach (RainwaterHarvesterProto machine in rainwaterHarvester)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                    string maintenance_cost_quantity = machine.Costs.Maintenance.MaintenancePerMonth.Value.ToString();
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    //machine.WaterCollectedPerDay.ToString() - wiki says 35-40 Units per year on average
                    //one day = 2 in game seconds, one month = 60 in game seconds
                    string machineRecipeOutputJson = MakeRecipeIOJsonObject(machine.WaterProto.Strings.Name.ToString(), "37");
                    string machineRecipeJson = MakeRecipeJsonObject(
                        id,
                        name,
                        "720",
                        "",
                        machineRecipeOutputJson
                    );

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        machineRecipeJson,
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                    DumpObject(DUMP, id, machine);
                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            IEnumerable<DataCenterProto> dataCenters = protosDb.All<DataCenterProto>();
            IEnumerable<ServerRackProto> dataRacks = protosDb.All<ServerRackProto>();
            foreach (DataCenterProto machine in dataCenters)
            {

                try
                {

                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "";
                    string workers = machine.Costs.Workers.ToString();
                    string maintenance_cost_units = "";
                    string maintenance_cost_quantity = "0";
                    string electricity_consumed = "0";
                    string electricity_generated = "0";
                    string computing_consumed = "0";
                    string computing_generated = "0";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    foreach (var cat in machine.Graphics.Categories) category = cat.CategoryProto.Strings.Name.ToString();

                    string buildCosts = FormatProductCosts(machine.Costs.BaseConstructionCost.Products);

                    //generate recipe on max server racks (48)
                    int racks_capacity = machine.RacksCapacity;
                    List<string> recipeItems = new List<string> { };
                    foreach (ServerRackProto dataRack in dataRacks)
                    {
                        string maintenance_cost_units1 = machine.Costs.Maintenance.Product.Strings.Name.ToString();
                        string maintenance_cost_quantity1 = (machine.Costs.Maintenance.MaintenancePerMonth.Value + (racks_capacity * dataRack.Maintenance.Value)).ToString();

                        string recipe_name = dataRack.Strings.Name.ToString();
                        string recipe_duration = "60";

                        List<string> inputItems = new List<string> { };
                        List<string> outputItems = new List<string> { };

                        string machineRecipeInputJson;
                        machineRecipeInputJson = MakeRecipeIOJsonObject(machine.CoolantIn.Strings.Name.ToString(), (racks_capacity * dataRack.CoolantInPerMonth.Value).ToString());
                        inputItems.Add(machineRecipeInputJson);
                        machineRecipeInputJson = MakeRecipeIOJsonObject(maintenance_cost_units1, maintenance_cost_quantity1);
                        inputItems.Add(machineRecipeInputJson);
                        machineRecipeInputJson = MakeRecipeIOJsonObject("Electricity", (racks_capacity * dataRack.ConsumedPowerPerTick.Value).ToString());
                        inputItems.Add(machineRecipeInputJson);

                        string machineRecipeOutputJson;
                        machineRecipeOutputJson = MakeRecipeIOJsonObject(machine.CoolantOut.Strings.Name.ToString(), (racks_capacity * dataRack.CoolantOutPerMonth.Value).ToString());
                        outputItems.Add(machineRecipeOutputJson);
                        machineRecipeOutputJson = MakeRecipeIOJsonObject("Computing", (racks_capacity * dataRack.CreatedComputingPerTick.Value).ToString());
                        outputItems.Add(machineRecipeOutputJson);

                        string machineRecipeJson = MakeRecipeJsonObject(
                            dataRack.Id.ToString(),
                            recipe_name,
                            recipe_duration,
                            inputItems.JoinStrings(","),
                            outputItems.JoinStrings(",")
                        );
                        recipeItems.Add(machineRecipeJson);
                    }

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        machine.IconPath,
                        buildCosts,
                        recipeItems.JoinStrings(","),
                        machine.Layout.LayoutSize
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            foreach (ServerRackProto machine in dataRacks)
            {
                try
                {
                    string id = machine.Id.ToString();
                    string name = machine.Strings.Name.ToString();
                    string category = "Data center";
                    string workers = "0";
                    string maintenance_cost_units = "Maintenance III";
                    string maintenance_cost_quantity = machine.Maintenance.Value.ToString();
                    string electricity_consumed = machine.ConsumedPowerPerTick.Value.ToString();
                    string electricity_generated = "0";
                    string computing_generated = machine.CreatedComputingPerTick.Value.ToString();
                    string computing_consumed = "0";
                    string capacity = "0";
                    string unity_cost = "0";
                    string research_speed = "0";

                    string buildCosts = MakeVehicleProductJsonObject(
                            machine.ProductToAddThis.Product.Strings.Name.ToString(),
                            machine.ProductToAddThis.Quantity.Value.ToString()
                    );

                    string machineJson = MakeMachineJsonObject(
                        id,
                        name,
                        category,
                        workers,
                        maintenance_cost_units,
                        maintenance_cost_quantity,
                        electricity_consumed,
                        electricity_generated,
                        computing_consumed,
                        computing_generated,
                        capacity,
                        unity_cost,
                        research_speed,
                        "",
                        buildCosts,
                        "",
                        new RelTile3i(0, 0, 0)
                    );
                    machineItems.Add(machineJson);

                }
                catch { LogError("Depot " + machine.Id.ToString()); }
            }

            /*
             * -------------------------------------
             * Part 6  - Terrain Materials. Uses Simpler Proto LookUp Method
             * -------------------------------------
            */

            List<string> materialItems = new List<string> { };

            IEnumerable<TerrainMaterialProto> materials = protosDb.All<TerrainMaterialProto>();
            foreach (TerrainMaterialProto material in materials)
            {

                try
                {
                    string id = material.Id.ToString();
                    string name = material.Strings.Name.ToString();
                    string mined_product = material.MinedProduct.Strings.Name.ToString();
                    string mining_hardness = "";
                    string mined_quantity_per_tile_cubed = material.MinedQuantityPerTileCubed.ToString();
                    string disruption_recovery_time = material.DisruptionRecoveryTime.ToString();
                    string is_hardened_floor = "false";
                    string max_collapse_height_diff = material.MaxCollapseHeightDiff.ToString();
                    string min_collapse_height_diff = material.MinCollapseHeightDiff.ToString();
                    string mined_quantity_mult = material.MinedQuantityMult.ToString();
                    string vehicle_traversal_cost = "0";

                    string materialJson = MakeTerrainMaterialJsonObject(
                        id,
                        name,
                        mined_product,
                        mining_hardness,
                        mined_quantity_per_tile_cubed,
                        disruption_recovery_time,
                        is_hardened_floor,
                        max_collapse_height_diff,
                        min_collapse_height_diff,
                        mined_quantity_mult,
                        vehicle_traversal_cost
                    );
                    materialItems.Add(materialJson);


                }
                catch (Exception ex)
                {
                    LogError("Material " + material.Id.ToString(), ex);
                }

            }

            this.WriteOutput("terrain_materials.json", $"{{\"game_version\":\"{game_version}\",\"terrain_materials\":[{materialItems.JoinStrings(",")}]}}");

            List<string> contractItems = new List<string> { };

            IEnumerable<ContractProto> contracts = protosDb.All<ContractProto>();

            foreach (ContractProto contract in contracts)
            {

                string contractJson = MakeContractJsonObject(
                    contract.Id.ToString(),
                    contract.ProductToBuy.Strings.Name.ToString(),
                    contract.GetQuantityToBuy(Percent.Hundred).ToString(),
                    contract.ProductToPayWith.Strings.Name.ToString(),
                    contract.QuantityToPayWith.ToString(),
                    contract.UpointsPerMonth.ToString(),
                    contract.UpointsPer100ProductsBought.ToString(),
                    contract.UpointsToEstablish.ToString(),
                    contract.MinReputationRequired.ToString()
                );
                contractItems.Add(contractJson);

            }

            this.WriteOutput("contracts.json", $"{{\"game_version\":\"{game_version}\",\"contracts\":[{contractItems.JoinStrings(",")}]}}");

            /*
                * -------------------------------------
                * Part - Transport
                * -------------------------------------
            */
            List<string> transportItems = new List<string> { };
            IEnumerable<TransportProto> transports = protosDb.All<TransportProto>();
            foreach (TransportProto transport in transports)
            {
                string category = "";
                string next_tier = "";
                if (transport.Upgrade.NextTier.HasValue)
                {
                    next_tier = transport.Upgrade.NextTier.Value.Id.ToString();
                }
                string maintenance_cost_units = "";
                string maintenance_cost_quantity = "0";

                string buildCosts = FormatProductCosts(transport.Costs.BaseConstructionCost.Products);

                string transportsJson = MakeTransportJsonObject(
                    transport.Id.ToString(),
                    transport.Strings.Name.ToString(),
                    category,
                    next_tier,
                    maintenance_cost_units,
                    maintenance_cost_quantity,
                    transport.BaseElectricityCost.Value.ToString(),
                    (transport.ThroughputPerTick.Value * 10).ToString(),
                    transport.LengthPerCost.Value.ToString(),
                    transport.IconPath,
                    buildCosts
                );
                transportItems.Add(transportsJson);
                this.sprites.Add(transport.Id.ToString(), new spriteToExport() { category = category, icon = transport.IconPath });
            }

            this.WriteOutput("transports.json", $"{{\"game_version\":\"{game_version}\",\"transports\":[{transportItems.JoinStrings(",")}]}}");

            /*
                * -------------------------------------
                * Part 7  - Final JSON Export
                * -------------------------------------
            */

            this.WriteOutput("machines_and_buildings.json", $"{{\"game_version\":\"{game_version}\",\"machines_and_buildings\":[{machineItems.JoinStrings(",")}]}}");
            this.WriteOutput("storages.json", $"{{\"game_version\":\"{game_version}\",\"storages\":[{storageItems.JoinStrings(",")}]}}");

            /*
                * -------------------------------------
                * TODO - retrieve Mafi.Unity.AssetsDb instance, and get UnityEngine.Texture2D by icon path.
                * Then use UnityEngine.ImageConversionModule.ImageConversion to convert texture to png, and export it to file.
                * -------------------------------------
            */
            if (DUMP.Count != 0)
            {
                this.WriteOutput("dump.txt", DUMP.JoinStrings());
            }
        }

        /*
         * -------------------------------------
         * Empty Implementation Of Required Mod Methods
         * -------------------------------------
        */


        public void Register(ImmutableArray<DataOnlyMod> mods, RegistrationContext context)
        {

        }

        public struct techCosts
        {
            public Dict<RecipeProto.ID, long> recipes;
            public Dict<ProductProto.ID, long> products;
        }

        public techCosts WalkResearchCosts(IEnumerable<ResearchNodeProto> allTechs)
        {
            Dict<RecipeProto.ID, long> recipeResearchCost = new Dict<RecipeProto.ID, long>();
            Dict<ProductProto.ID, long> productResearchCost = new Dict<ProductProto.ID, long>();

            var techCost = new Dict<ResearchNodeProto.ID, long>();

            var toVisit = new Queue<ResearchNodeProto>();

            var childrenOfNode = new Dict<ResearchNodeProto.ID, List<ResearchNodeProto>>();
            /**
             * For every tech in the tech tree, find ones without parents. 
             * Walk those roots and store the research cost for that node (sum all parents costs)
             * Store the current research cost for any product or recipe that tech unlocks
             */
            try
            {
                if (allTechs == null)
                    throw new Exception("allTechs is null");
                foreach (var tech in allTechs)
                {
                    if (!childrenOfNode.ContainsKey(tech.Id))
                        childrenOfNode.Add(tech.Id, new List<ResearchNodeProto>());

                    if (tech.Parents.IsEmpty)
                    {
                        toVisit.Enqueue(tech);
                        Log.Info("Found root tech " + tech.Id.ToString());
                    }
                    else
                    {
                        foreach (var parent in tech.Parents)
                        {
                            if (parent == null) continue;
                            if (!childrenOfNode.ContainsKey(parent.Id))
                                childrenOfNode.Add(parent.Id, new List<ResearchNodeProto>());
                            childrenOfNode[parent.Id].Add(tech);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error building research tree", e);
            }

            long getCost(ResearchNodeProto.ID id)
            {
                try
                {
                    var prop = typeof(ResearchCosts).GetProperty(id.ToString());
                    if (prop == null)
                        return (long)0;
                    foreach (var p in typeof(ResearchCosts).GetProperties())
                    {
                        Log.Info("Property " + p.Name);
                        if (p.Name == id.ToString())
                        {
                            prop = p;
                            break;
                        }
                    }
                    object val = prop.GetValue(null, null);
                    if (val is int)
                        return (long)val;
                    else
                        if (val is long) return (long)val;
                    else
                        throw new Exception("Unknown research cost type for " + id.ToString());
                }
                catch (Exception e)
                {
                    throw new Exception("Error getting research cost for " + id.ToString(), e);
                }
            }

            try
            {
                while (toVisit.Count > 0)
                {
                    var current = toVisit.Dequeue();

                    long currentCost = getCost(current.Id);
                    bool incomplete = false;
                    foreach (var parent in current.Parents)
                    {
                        if (techCost.ContainsKey(parent.Id))
                            currentCost += techCost[parent.Id];
                        else
                            incomplete = true;
                    }
                    //Log.Info("Total cost for tech " + current.Id.ToString() + " is " + currentCost.ToString() + " from " + current.Parents.Length.ToString() + " parents, incomplete=" + incomplete.ToString());
                    if (incomplete)
                        continue; // we're missing a parent cost, we'll pick it up later

                    techCost[current.Id] = currentCost;

                    foreach (var item in current.Units)
                    {
                        if (item is ProtoUnlock protoUnlock)
                        {
                            IEnumerable<IProto> items = protoUnlock.UnlockedProtos.Where((IProto x) => !(x is RecipeProto) && !(x is ProductProto));
                            // unlockedList.AddRange(items);
                            foreach (var unlocked in items)
                            {
                                if (unlocked is RecipeProto recipe)
                                {
                                    if (recipeResearchCost.ContainsKey(recipe.Id))
                                        throw new Exception("Recipe " + recipe.Id.ToString() + " unlocked by multiple techs, can't assign unique research cost");
                                    recipeResearchCost[recipe.Id] = currentCost;
                                }
                                else if (unlocked is ProductProto product)
                                {
                                    if (productResearchCost.ContainsKey(product.Id))
                                        throw new Exception("Product " + product.Id.ToString() + " unlocked by multiple techs, can't assign unique research cost");

                                    productResearchCost[product.Id] = currentCost;
                                }
                            }
                        }
                    }
                    foreach (var child in childrenOfNode[current.Id])
                        toVisit.Enqueue(child);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error walking research tree", e);
            }

            return new techCosts
            {
                recipes = recipeResearchCost,
                products = productResearchCost

            };
        }
        public void RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool gameWasLoaded)
        {
            Log.Info("Data Extractor Mod RegisterDependencies");

            // Research nodes aren't linked together until the Map loads so we run this part here
            var allTechs = WalkResearchCosts(protosDb.All<ResearchNodeProto>());


        }

        public void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
