// SmartStorage mod for RimWorld
// 
// Smart Armor Rack -- colonists don their armor when the thread level changes.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;         // Always needed
//using VerseBase;         // Material/Graphics handling functions are found here
using Verse;               // RimWorld universal objects are here (like 'Building')
using Verse.AI;          // Needed when you do something with the AI
//using Verse.Sound;       // Needed when you do something with Sound
//using Verse.Noise;       // Needed when you do something with Noises
using RimWorld;            // RimWorld specific functions are found here (like 'Building_Battery')
//using RimWorld.Planet;   // RimWorld specific functions for world creation
//using RimWorld.SquadAI;  // RimWorld specific functions for squad brains 

namespace SmartStorage
{
	public struct ArmorStats
	{
		public double blunt, sharp;

		public ArmorStats(double p1, double p2)
		{
			blunt = p1;
			sharp = p2;
		}
	}

	public class Building_SmartArmorRack : Building_HeadAndTorsoStorage
	{
		// Lists of apparel to wear during cold seasons
		private static List<ThingDef> allowedTorsoDefs = new List<ThingDef> ();
		private static List<ThingDef> allowedHeadDefs = new List<ThingDef> ();
		private static List<ThingDef> allowedAllDefs = new List<ThingDef> ();

		// These records enable us to split our allowances into the above lists
		private static BodyPartRecord torsoParts = new BodyPartRecord ();
		private static BodyPartRecord headParts = new BodyPartRecord();

		private static List<Season> coldSeasons = new List<Season>();

		// These are used to determine if a ThingDef is suitable armor
		private static ArmorStats TorsoStats = new ArmorStats (0.30, 0.60);
		private static ArmorStats HeadStats = new ArmorStats(0.1, 0.24);

		// Danger Rates
		public StoryDanger currentDangerRate = StoryDanger.None;
		public StoryDanger previousDangerRate = StoryDanger.None;

		// JobDefs
		private const String JobDef_wearArmor = "WearArmorInRack";


		static Building_SmartArmorRack ()
		{
			// Add definition to our BodyPartsRecords so we can distinguish between parkas and tuques, for instance
			Building_SmartArmorRack.torsoParts.groups.Add (BodyPartGroupDefOf.Torso);
			Building_SmartArmorRack.headParts.groups.Add (BodyPartGroupDefOf.FullHead);
			Building_SmartArmorRack.headParts.groups.Add (BodyPartGroupDefOf.UpperHead);

			// Build list of allowed apparel defs
			Building_SmartArmorRack.CreateApparelLists ();
		}

		public override void SpawnSetup()
		{
			base.SpawnSetup ();
		}

		public override string GetInspectString ()
		{
			StringBuilder stringBuilder = new StringBuilder ();
			stringBuilder.Append (base.GetInspectString ());

			if (TESTING_MODE)
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("CurrentState: " + fsm_process.CurrentState);
				stringBuilder.AppendLine ();
				stringBuilder.Append ("CurrentDanger: " + currentDangerRate);
			}
			return stringBuilder.ToString ();
		}


		public override IEnumerable<Gizmo> GetGizmos()
		{
			IList<Gizmo> gizmoList = new List<Gizmo> ();
			int groupKeyBase = 12091967;

			if (TESTING_MODE)
			{
				Command_Action createArmorButton = new Command_Action ();
				createArmorButton.defaultDesc = "Spawn armor in rack.";
				createArmorButton.defaultLabel = "Create Armor";
				createArmorButton.activateSound = SoundDef.Named ("Click");
				createArmorButton.action = new Action (Debug_SpawnArmor);
				createArmorButton.groupKey = groupKeyBase + 4;
				gizmoList.Add (createArmorButton);

				Command_Action destroyArmorButton = new Command_Action ();
				destroyArmorButton.defaultDesc = "Destroy armor in rack.";
				destroyArmorButton.defaultLabel = "Destroy Armor";
				destroyArmorButton.activateSound = SoundDef.Named ("Click");
				destroyArmorButton.action = new Action (Debug_DestroyStoredThings);
				destroyArmorButton.groupKey = groupKeyBase + 5;
				gizmoList.Add (destroyArmorButton);
			}

			IEnumerable<Gizmo> resultGizmoList;
			IEnumerable<Gizmo> baseGizmoList = base.GetGizmos();
			if (baseGizmoList != null)
			{
				resultGizmoList = gizmoList.AsEnumerable<Gizmo>().Concat(baseGizmoList);
			}
			else
			{
				resultGizmoList = gizmoList.AsEnumerable<Gizmo>();
			}
			return (resultGizmoList);
		}
			

		/// <summary>
		/// Dos the ticker work.
		/// Remove the AllSlotCells iteration.  Instead, use HaveHat() and HaveWrap() to directly wear items
		/// Tell owner to wear wrap.  Add to LostItem() that if Wrap is lost and HaveHat() then put on hat as well
		/// Keep wardrobe disallowed until spring time, tell colonists to put their shit away.
		/// When seasons change, items left in wardrobes from past season that were never worn need to be unforbidden.
		/// </summary>
		/// <param name="tickerAmount">Ticker amount.</param>
		public override void DoTickerWork (int tickerAmount)
		{
			counter += tickerAmount;

			// Check once per day if season has changed and issue jobs if so
			if (counter % 60 == 0)
			{
				counter = 0;

				currentDangerRate = Find.StoryWatcher.watcherDanger.DangerRating;
				if (currentDangerRate != previousDangerRate)
				{
					previousDangerRate = currentDangerRate;
					if (currentDangerRate != StoryDanger.None)
					{
						InspectStateMachine ();
						if (HaveTorsoThing () || HaveHeadThing ())
						{	
							if (owner != null)
							{
								Log.Message (string.Format ("[{0}] Issuing wear job.", owner.Nickname));
								IssueWearJob ();
							}
						}
					}
				}
			}
		}


		/// <summary>
		/// Determines if issue wear job the specified pawn article.
		/// </summary>
		/// <returns><c>true</c> if issue wear job the specified pawn article; otherwise, <c>false</c>.</returns>
		public override void IssueWearJob()
		{
			var jobWear = new Job (DefDatabase<JobDef>.GetNamed (JobDef_wearArmor), this);

			owner.playerController.TakeOrderedJob (jobWear);
		}


		/// <summary>
		/// Creates the apparel lists, separated into what to warm and cold season and head vs. torso categories
 		/// </summary>
		static void CreateApparelLists()
		{
			IEnumerable<ThingDef> allThingDefs = DefDatabase<ThingDef>.AllDefs;
			foreach (ThingDef thingDef in allThingDefs)
			{
				if (thingDef.IsApparel)
				{
					double ArmorRating_Blunt = (double)thingDef.statBases.GetStatValueFromList (StatDefOf.ArmorRating_Blunt, (float)0.0);
					double ArmorRating_Sharp = (double)thingDef.statBases.GetStatValueFromList (StatDefOf.ArmorRating_Sharp, (float)0.0);
//					Log.Message(String.Format("{0} stat: ArmorRating_Blunt = {1}", thingDef.label, ArmorRating_Blunt));
//					Log.Message(String.Format("{0} stat: ArmorRating_Sharp = {1}", thingDef.label, ArmorRating_Sharp));

					if (IsOverHead(thingDef))
					{
						if (ArmorRating_Blunt >= HeadStats.blunt && ArmorRating_Sharp >= HeadStats.sharp)
						{
//							Log.Message (String.Format ("Adding {0} to armorHead list.", thingDef.label));
							Building_SmartArmorRack.allowedHeadDefs.Add (thingDef);
						}
					} else if (IsTorsoShell(thingDef))
					{
						if (ArmorRating_Blunt >= TorsoStats.blunt && ArmorRating_Sharp >= TorsoStats.sharp)
						{
//							Log.Message (String.Format ("Adding {0} to armorTorso list.", thingDef.label));
							Building_SmartArmorRack.allowedTorsoDefs.Add (thingDef);
						}
					}
					Building_SmartArmorRack.allowedAllDefs.AddRange (Building_SmartArmorRack.allowedHeadDefs);
					Building_SmartArmorRack.allowedAllDefs.AddRange (Building_SmartArmorRack.allowedTorsoDefs);
				}
			}
		}

	
		/// <summary>
		/// Spawns the cold season apparel.
		/// </summary>
		void Debug_SpawnArmor()
		{
			var thingDefs = new List<ThingDef> ();
			thingDefs.Add(ThingDef.Named("Apparel_PowerArmorHelmet"));
			thingDefs.Add(ThingDef.Named("Apparel_PowerArmor"));
			ThingDef stuffDef = null;

			Debug_SpawnStoredThings (thingDefs, stuffDef);
		}


	} // class Building_SmartArmorRack
}