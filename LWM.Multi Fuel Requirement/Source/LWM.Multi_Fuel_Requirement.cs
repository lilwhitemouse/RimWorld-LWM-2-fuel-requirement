using RimWorld;
using Verse;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;

/***********************************
 * LWM's Multi Fuel Requirement Mod
 * 
 * This mod enables multiple fuel requirements for buildings
 * in RimWorld.  Instead of a building having a single
 * CompProperties_Refuelable, it may have multiple comp 
 * entries of LWM.Multi_Fuel_Requirement.Properties.
 * 
 * See About.xml for use, etc.
 * 
 * The Mod works by creating a new class CompRefuelable_Multi
 * that is derived from CompRefuelable.  The way the game is
 * set up, the engine checks for a CompRefuelable and if there
 * is one, does fueling stuff (check fuel levels, etc).  A
 * building could already have multiple CompRefuelable comps,
 * but only the first in the list is ever seen by the engine.
 * 
 * By changing the order of the CompRefuelable_Multi comps
 * as needed, the standard engine takes care of all the 
 * refueling, etc, and we don't have to care about that logic.
 * 
 * There are a few subtleties:
 *   1.  Adjust CompTick() so it only happens if all comps
 *       have fuel.
 *   2.  Adjust the Notify_Used to notify all fuel comps.
 *   3.  Create a controller to keep track of save/load order
 *   4.  Adjust HasComp so it can see the derived class
 *       (thanks, Harmony!)
 *   5.  Make the controller handle display text - it's
 *       just neater, that's all.
 *   6.  ???
 * 
 * Known limitations:
 * Does not patch Launchers or Explosions!
 * * Launchers:  (Pod) launchers need to check Fuel amounts
 *   at various times, and the logic is a bit fiddly,
 *   plus I'm not sure Harmony can patch the Fuel getter...
 *   Launchers need to know the minimum fuel available.
 *   ConsumeFuel() is also tricky.
 * * Explosions:  If a building with exposive fuel explodes,
 *   it also needs to check how much Fuel there is.  Again,
 *   I'm not sure Harmony can patch the Fuel getter.  Also
 *   both Fuel and ConsumeFuel() here would have to act 
 *   differently than for Launchers:  explosions need to 
 *   know the total explosive fuel available...
 * 
 *  License:
 *  GPL 3.0 with the following modification:
 *  If the mod is incorporated into the base game, the 
 *  game's owner may modify and distribute the code as
 *  if it were their own.
 * 
 */

/* Possible problems:
 * gizmo_refuelablefuelstatus - untested
 */


namespace LWM.Multi_Fuel_Requirement
{
    public static class Utils 
    {
        public static void ShowComps(ThingWithComps t, string s = ""){
            var l = t.AllComps;
            foreach (var c in l) {
                string o = "";
                if (s != "") {
                    o = s + ": ";
                }
                o += "Comp List: " + c.ToString();
                if (c is CompRefuelable_Multi) o += (": " + ((CompRefuelable_Multi)c).Props.fuelLabel);
                Log.Warning(o);
            }
        }

    }

    // Using Harmony, so we need to do this:
    internal class Multi_Fuel_Requirement_Mod : Mod
    {
        public Multi_Fuel_Requirement_Mod(ModContentPack content) : base(content)
        {
            try
            {
                Harmony.HarmonyInstance.Create("net.littlewhitemouse.rimworld.multi_fuel_requirement").
                              PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

                Log.Warning("LWM's Multi Fuel Requirement Mod: loaded Harmony patches");
            }
            catch (Exception e)
            {
                Log.Error("LWM's Multi Fuel Requirement: Oh no! Harmony failure:  caught exception: " + e);
            }
        }
    }

    // Potential approach #2:
    //   a CompRefuelable_Multi object thas has
    //   a list of compRefuelable objects as elements
    //   and calls them all as it gets called?
    // Easy enough to set up, but because we cannot 
    //   override so many of the important functions,
    //   it ends up being very diffuclt in practice...
    // If any one of the public (and non-virtual) "get" 
    //   functions gets inlined, the whole thing falls
    //   apart and it won't work.


    public class Properties : CompProperties_Refuelable
    {
        public Properties()
        {
            this.compClass = typeof(CompRefuelable_Multi);
        }
    }

    /****
     * The controller is here to handle load/save; it's added by the first
     * CompRefuelable_Multi comp and kept at the end of the parent's comp
     * list.  This allows it to handle load/save of the order of the multi-
     * comps without screwing things up...
     *****/
    public class Controller : ThingComp {
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            fuels = new List<CompRefuelable_Multi>();
        }
        public override void PostExposeData() // where save/load is handled
        {
            base.PostExposeData();
            // saving data: the controller handles the order:
            CompRefuelable_Multi crm = parent.GetComp<CompRefuelable_Multi>();
            string orderString = "" + crm.id;
            while (crm.next != null)
            {
                crm = crm.next;
                orderString += ":" + crm.id;
            }
            //Log.Warning("Saving order- " + orderString);
            Scribe_Values.Look<string>(ref orderString, "LWM_CRM_Order");
            // On load, put things in the right order!
            //   We can get away with this because the controller is 
            //   guaranteed to be *after* the CompRefuelable_Multis 
            //   and rearranging them is okay
            var allCrms = parent.GetComps<CompRefuelable_Multi>().ToList();
            var allComps = parent.AllComps;
            //Log.Warning("Loading order- " + orderString);
            var orders = orderString.Split(':').Reverse();// reverse order so can put at beginning easily
            // Go thru what the order should be
            foreach (string s in orders)
            {
                int which = Int32.Parse(s); // let's be honest.  Won't need Int64 :p
                                            // Find the right ones and put them in:
                crm = allCrms.Find(x => x.id == which);
                if (crm == null)
                {
                    Log.Error("LWM.Multi_Fuel_Requirement: Tried to load fuel " + which +
                              " but could not find it in def. Destroying " +
                              parent.ToString() + " and aborting.");
                    parent.Destroy();
                    return;
                }
                allComps.Remove(crm);
                allComps.Insert(0,crm); // put at beginning
            }
            this.first = parent.GetComp<CompRefuelable_Multi>();
            // Each CompRefuelable_Multi handles its own fuel amounts, etc.
        }

        public override void CompTick()
        {
            // Do the CompTick() for each fuel:
            if (this.first.HasFuel) {
                for (int i = 0; i < fuels.Count; i++) {
                    fuels[i].DoTheCompTick();
                }
            }
            // Rarely check to see which fuel needs attention:
            //    ...CompTickRare() does not actually get called >_<
            //    so we have to do it in CompTick():
            if (Find.TickManager.TicksGame % 2000 == 0)
            {
                // If it's reserved, don't change anything:
                //   (a pawn might be bringing fuel A to refuel when fuel B gets bumped to
                //    the top.  Hilarity would ensue, etc.)
                if (this.parent.Map.pawnDestinationReservationManager.
                    IsReserved(parent.InteractionCell)) return;
                CheckFuelList();
            }
        }

        // Easiset way to get the rest of the code to pick up the multiple
        //   fuels is to put the one that needs attention most right on top (First in List<comps>).
        // This sorts that list.
        // (Probably should have been in Controller, but whatever)
        public void CheckFuelList()
        {
            var refuelables = parent.GetComps<CompRefuelable_Multi>().OrderBy(cr => cr.FuelPercentOfMax).ToArray();
            //if (refuelables.Length <= 1) { return; } // if you make something with one fuel, you get the performace hit
            var bigList = parent.AllComps;
            bigList.Remove(refuelables[0]); // start re-ordering the parent's comp list
            bigList.Insert(0, refuelables[0]); // put at beginning
            for (int i = 1; i < refuelables.Length; i++)
            {
                // re-order the parent's comp list:
                bigList.Remove(refuelables[i]); // remove from whereever
                bigList.Insert(i, refuelables[i]);    // add to beginning in order, after other ones
                // re-order linked list so we can quickly call all the CompRefuelable_Multis:
                refuelables[i - 1].next = refuelables[i];
            }
            refuelables[refuelables.Length - 1].next = null;
            this.first = refuelables[0];
        }


        // get a nice tidy display for the parent object:
        public override string CompInspectStringExtra()
        {
            string s = fuels[0].MyInspectString();
            for (int i = 1; i < fuels.Count; i++) {
                s += "\n" + fuels[i].MyInspectString();
            }
            return s;
        }

        public List<CompRefuelable_Multi> fuels;
        public CompRefuelable_Multi first; // First in terms of linked list/priority/parent's comps.
    }

    public class PlaceHolder : CompProperties {
        // Just to be on the safe side...make sure there's a compProperty for controller
    }

    public class CompRefuelable_Multi : CompRefuelable
    {
        // use id as a label for save/load
        public int id;
        // A quick way to zip through all the CompRefuelable_Multi:
        public CompRefuelable_Multi next = null;
        public Controller controller;

        public override void Initialize(Verse.CompProperties props)
        {
            base.Initialize(props);
            // Do 4 things
            //   1.  Create Controller for saving/loading in proper order (do once)
            //   2.  most important: give each compRefuelable from the def a unique id for save/load
            //   3.  make linked list
            //   4.  make sure the fuel has a decent label
            CompRefuelable_Multi crm = this.parent.GetComp<CompRefuelable_Multi>();

            if (crm == this) // "first post!"
            {
                id = 0;
                this.controller = (LWM.Multi_Fuel_Requirement.Controller)Activator.
                                      CreateInstance(typeof(LWM.Multi_Fuel_Requirement.Controller));
                controller.parent = parent;
                controller.Initialize(new PlaceHolder());
                parent.AllComps.Add(controller);
                controller.fuels.Add(this);
            }
            else
            {
                this.controller = crm.controller;
                controller.fuels.Add(this);
                // Build a linked list of the CompRefuelable_Multis this parent has:
                //   This will give us a faster way to check all of the than searching the whole
                //   List<> by way of GetComps<CompRefuelable_Multi>
                int count = 1;
                while (crm.next != null)
                {
                    crm = crm.next;
                    count++;
                }
                crm.next = this;
                id = count; // unique label for save/load
                // Move the controller to the end of the list: (slightly inefficient, but necessary)
                var c = parent.GetComp<LWM.Multi_Fuel_Requirement.Controller>();
                parent.AllComps.Remove(c);
                parent.AllComps.Add(c);
            }

            // It would be nice to tell the fuels apart:
            //   (we do this here because we can override Initialize.
            //    we can't override for CompProperties_Refuelable?)
            if (this.Props.fuelLabel.NullOrEmpty()) {
                this.Props.fuelLabel = this.Props.fuelFilter.Summary;
            }
        } // initialize

        // Save fuel level and target fuel levels
        //   We have to override this to give a unique label for Scribe_Values.Look
        public override void PostExposeData()
        {
            base.PostExposeData();
            // Want to do:
            // Scribe_Values.Look<float>(this.fuel, "LWM"+id+"fuel", 0f, false);
            // But this.fuel is a private field.  Oops.  But we can grab it anyway, thanks
            //   to the miracles of reflection: #DeepMagic
            float f = Fuel;

            Scribe_Values.Look<float>(ref f, "LWM" + id + "fuel", 0f, false);

            var fuelFI = typeof(CompRefuelable).GetField("fuel",
                                                               System.Reflection.BindingFlags.NonPublic |
                                                               System.Reflection.BindingFlags.Instance |
                                                               System.Reflection.BindingFlags.SetField | 
                                                               System.Reflection.BindingFlags.SetProperty);
            // I could write fuelFI?.SetValue(this, f);
            //   with that slick '?.' operator but I would rather it throw an exception
            fuelFI.SetValue(this, f);
        }

        public override void CompTick()
        {
            // override using CompTick() - will call from controller!
            return;
        }
        public void DoTheCompTick() // thanks, C#, for making this complicated?
        { // C++ would be easier, as I understand it?
            base.CompTick();
        }

        // Don't give a string here - the controller will figure out what labels are needed:
        public override string CompInspectStringExtra()
        {
            return null;
        }
        public string MyInspectString() { // but the controller does need the base string...
            return base.CompInspectStringExtra();
        }

        //public override void Notify_UsedThisTick() {
        //Son. Of. A. B.  It's not marked virtual...  Use Harmony instead ><
        //See below.
        //}
    } // end CompRefuelable_Multi, rest is patches

    /***************************** HARMONY PATCHES **************************/

    // HasComp - only non-CompRefuelable patch
    // The way refueling happens, the refueling workgiver checks a list of refuel-able Things,
    //   that is, things that HasComp(CompRefuelable).  Alas, HasComp checks explicitely against 
    //   class type (type == CompRefuelable), so derived classes are out of luck.
    // We fix that:
    [HarmonyPatch(typeof(Verse.ThingDef),"HasComp")]
    class Patch_ThingDef_HasComp
    {
        static bool Prefix(ref bool __result, ThingDef __instance, Type compType) {
            if (compType != typeof(CompRefuelable)) return true; // we only care about ourselves
            for (int i = 0; i < __instance.comps.Count; i++)
            {
                if (__instance.comps[i].compClass == compType||
                    __instance.comps[i].compClass.IsSubclassOf(compType))
                {
                    __result = true;
                    return false;
                }
            }
            __result = false;
            return false;
        }
    }

    // public override void Notify_UsedThisTick()
    //   Have to use Harmony b/c it's not "virtual"
    // Let other CompRefuelable_Multis know the object got used this tick, too
    // Only the first gets called by whatever is using it
    //   We go through the linked list to call the rest:
    [HarmonyPatch(typeof(RimWorld.CompRefuelable), "Notify_UsedThisTick")]
    class Patch_Notify_UsedThisTick
    {
        protected static void Postfix(CompRefuelable __instance)
        {
            if (!(__instance is CompRefuelable_Multi)) return;
            if ((__instance as CompRefuelable_Multi).next != null)
            {
                //when we're out of one fuel, stop burning others
                //  this is not exact, but is probably close enough:
                //  whatever might Use this building probably checks if it HasFuel
                if (!(__instance as CompRefuelable_Multi).HasFuel) { return; }
                (__instance as CompRefuelable_Multi).next.Notify_UsedThisTick();
            }
        }
    }

    // The parent object only has fuel if ALL CompRefuelables have fuel.
    //   Luckily, we can patch the HasFuel getter:
    [HarmonyPatch(typeof(RimWorld.CompRefuelable), "get_HasFuel")]
    class Patch_HasFuel_Getter
    {
        protected static void Postfix(CompRefuelable __instance, ref bool __result)
        {
            if (__result==false) return; // first CompRefuelable is out of fuel
            if (!(__instance is CompRefuelable_Multi)) return; // this cannot use multi fuels, so whatever
            if ((__instance as CompRefuelable_Multi).next == null) return; // Checked all the fuels
            __result = (__instance as CompRefuelable_Multi).next.HasFuel;
            return;
        }
    }

    // Final patch: Refueling.  After refueling, check to see which fuel is lowest:
    [HarmonyPatch(typeof(RimWorld.CompRefuelable), "Refuel", new Type[] { typeof(List<Thing>) })]
    class Patch_Refuel_Complex  // public void Refuel(List<Thing> fuelThings)
    {   // This is for refueling parent objects that can run on more than one thing?
        // It calls the regular Refuel several times - we don't want things changing mid-refuel!
        public static bool Refueling = false; // So we keep track of whether we're already refueling...
        static void Prefix()
        {
            Refueling = true;
        }
        static void Postfix(CompRefuelable __instance)
        {
            Refueling = false;
            if (__instance is CompRefuelable_Multi) {
                (__instance as CompRefuelable_Multi).controller.CheckFuelList();
            }
        }
    }
    [HarmonyPatch(typeof(RimWorld.CompRefuelable), "Refuel", new Type[] { typeof(float) })]
    class Patch_Refuel_Simple // public void Refuel(float amount)
    {
        static void Postfix(CompRefuelable __instance)
        {
            // If we re-order the list while the complex refueling above is going on,
            //   who knows what will happen!
            if (Patch_Refuel_Complex.Refueling) { return; }
            if (__instance is CompRefuelable_Multi) {
                (__instance as CompRefuelable_Multi).controller.CheckFuelList();
            }
        }
    }
}
