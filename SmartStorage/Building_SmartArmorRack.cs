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

	public enum DangerEdge
	{
		Rising,
		Falling,
		Flat
	}

	public class Building_SmartArmorRack : Building_HeadAndTorsoStorage
	{
		static List<Season> coldSeasons = new List<Season>();

		// These are used to determine if a ThingDef is suitable armor
		static ArmorStats TorsoStats = new ArmorStats (0.30, 0.60);
		static ArmorStats HeadStats = new ArmorStats(0.1, 0.24);

		// Danger Rates
//		public StoryDanger currentDangerRate = StoryDanger.None;
		public StoryDanger previousDangerRate = StoryDanger.None;


		// JobDefs
		const String JobDef_WearArmor = "WearArmorInRack";

		public DangerEdge CurrentDangerState
		{
			get {
				var currentDanger = Find.StoryWatcher.watcherDanger.DangerRating;
				if (currentDanger != StoryDanger.None && previousDangerRate == StoryDanger.None)
				{
					previousDangerRate = currentDanger;
					return DangerEdge.Rising;
				} else if (currentDanger == StoryDanger.None && previousDangerRate != StoryDanger.None)
				{
					previousDangerRate = currentDanger;
					return DangerEdge.Falling;
				} else
				{
					return DangerEdge.Flat;
				}
			}
		}


		static Building_SmartArmorRack ()
		{
			// Build list of allowed apparel defs
			Building_SmartArmorRack.CreateApparelLists ();
		}


		public override void DrawGUIOverlay ()
		{
			if (Find.CameraMap.CurrentZoom == CameraZoomRange.Closest)
			{
				if (HaveHeadThing () || HaveTorsoThing ())
				{
					return;
				}

				string text;
				if (owner != null)
				{
					text = owner.Nickname;
				}
				else
				{
					text = "Unowned".Translate ();
				}
				GenWorldUI.DrawThingLabel (this, text, new Color (1, 1, 1, 1));
			}
		}


		public override string GetInspectString ()
		{
			var stringBuilder = new StringBuilder ();
			stringBuilder.Append (base.GetInspectString ());

			if (TESTING_MODE)
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("CurrentState: " + fsm_process.CurrentState);
				stringBuilder.AppendLine ();
				stringBuilder.Append ("CurrentDanger: " + Find.StoryWatcher.watcherDanger.DangerRating);
			}
			return stringBuilder.ToString ();
		}


		public override IEnumerable<Gizmo> GetGizmos()
		{
			var gizmoList = new List<Gizmo> ();
			const int groupKeyBase = 12091967;

			if (TESTING_MODE)
			{
				var createArmorButton = new Command_Action ();
				createArmorButton.defaultDesc = "Spawn armor in rack.";
				createArmorButton.defaultLabel = "Create Armor";
				createArmorButton.activateSound = SoundDef.Named ("Click");
				createArmorButton.action = new Action (Debug_SpawnArmor);
				createArmorButton.groupKey = groupKeyBase + 1;
				gizmoList.Add (createArmorButton);

				var destroyArmorButton = new Command_Action ();
				destroyArmorButton.defaultDesc = "Destroy armor in rack.";
				destroyArmorButton.defaultLabel = "Destroy Armor";
				destroyArmorButton.activateSound = SoundDef.Named ("Click");
				destroyArmorButton.action = new Action (Debug_DestroyStoredThings);
				destroyArmorButton.groupKey = groupKeyBase + 2;
				gizmoList.Add (destroyArmorButton);
			}

			IEnumerable<Gizmo> resultButtonList;
			IEnumerable<Gizmo> basebuttonList = base.GetGizmos();
			if (basebuttonList != null)
			{
				resultButtonList = gizmoList.AsEnumerable<Gizmo>().Concat(basebuttonList);
			}
			else
			{
				resultButtonList = gizmoList.AsEnumerable<Gizmo>();
			}
			return (resultButtonList);
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

				if (CurrentDangerState == DangerEdge.Rising)
				{
					InspectStateMachine (); // TODO what does this do for me?
					if (HaveTorsoThing () || HaveHeadThing ())
					{	
						if (owner != null)
						{
							Log.Message (string.Format ("[{0}] Issuing wear job.", owner.Nickname));
							IssueWearJob ();
						}
					}
				} else if (CurrentDangerState == DangerEdge.Falling)
				{
					// Put armor away, even if there is nothing in the rack to wear instead
					Log.Message (string.Format ("[{0}] Issuing put-away armor job.", owner));
				}
			}
		}


		/// <summary>
		/// Determines if issue wear job the specified pawn article.
		/// </summary>
		/// <returns><c>true</c> if issue wear job the specified pawn article; otherwise, <c>false</c>.</returns>
		public override void IssueWearJob()
		{
			var jobWear = new Job (DefDatabase<JobDef>.GetNamed (JobDef_WearArmor), this);

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
							Log.Message (String.Format ("Adding {0} to armorHead list.", thingDef.label));
							Building_HeadAndTorsoStorage.allowedHeadDefs.Add (thingDef);
						}
					} else if (IsTorsoShell(thingDef))
					{
						if (ArmorRating_Blunt >= TorsoStats.blunt && ArmorRating_Sharp >= TorsoStats.sharp)
						{
							Log.Message (String.Format ("Adding {0} to armorTorso list.", thingDef.label));
							Building_HeadAndTorsoStorage.allowedTorsoDefs.Add (thingDef);
						}
					}
					Building_HeadAndTorsoStorage.allowedAllDefs.AddRange (Building_HeadAndTorsoStorage.allowedHeadDefs);
					Building_HeadAndTorsoStorage.allowedAllDefs.AddRange (Building_HeadAndTorsoStorage.allowedTorsoDefs);
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