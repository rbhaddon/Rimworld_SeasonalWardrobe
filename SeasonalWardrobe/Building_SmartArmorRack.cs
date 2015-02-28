// Seasonal Wardrobe mod for RimWorld
// 
// In Building_SeasonalWardrobe.SpawnSetup(), set func SeasonHasChanged() to either normal or test
// Test mode fakes seasons changes every 24 game hours
// Normal mode detects actual season changes from Summer --> Fall and Winter --> Spring.
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
		public const int NUM_SLOTS = 2;

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

		// Owner is of this wardrobe is colonist who will receive the wear apparel jobs
		public Pawn owner = null;

		// Wardrobe allowances are implemented as a finite state machine
		private FSM_Process fsm_process;



		// Danger Rates
		public StoryDanger currentDangerRate = StoryDanger.None;
		public StoryDanger previousDangerRate = StoryDanger.None;

		// JobDefs
		private const String JobDef_wearArmor = "WearArmorInRack";

		// Textures
		public static Texture2D assignOwnerIcon;
		public static Texture2D assignRoomOwnerIcon;
		public static Texture2D resetWardrobeIcon;

		// Tick-related vars; used to fire ticker work once per 24 hr game period
		public int dayTicks = 24000;
		public int counter = 0;

		// Testing
		private bool TESTING_MODE = true;

		static Building_SmartArmorRack ()
		{
			// Note: this type is marked as 'beforefieldinit'.

			Building_SmartArmorRack.assignOwnerIcon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner");
			Building_SmartArmorRack.assignRoomOwnerIcon = ContentFinder<Texture2D>.Get ("UI/Commands/AssignRoomOwner");
			Building_SmartArmorRack.resetWardrobeIcon = ContentFinder<Texture2D>.Get ("UI/Commands/ResetWardrobe");

			// Add definition to our BodyPartsRecords so we can distinguish between parkas and tuques, for instance
			Building_SmartArmorRack.torsoParts.groups.Add (BodyPartGroupDefOf.Torso);
			Building_SmartArmorRack.headParts.groups.Add (BodyPartGroupDefOf.FullHead);
			Building_SmartArmorRack.headParts.groups.Add (BodyPartGroupDefOf.UpperHead);

			// Build list of allowed apparel defs
			Building_SmartArmorRack.CreateApparelLists ();
		}


		public override void ExposeData ()
		{
			base.ExposeData ();
			Scribe_References.LookReference<Pawn> (ref owner, "owner");
			Scribe_References.LookReference<Thing> (ref storedHead, "storedHead");
			Scribe_References.LookReference<Thing> (ref storedTorso, "storedTorso");
		}


		public override void SpawnSetup ()
		{
			base.SpawnSetup ();

			// Start wardrobe's state machine
			fsm_process = new FSM_Process ();

			// Restore previous state if needed
			if (HaveHeadArmor ())
			{
				fsm_process.MoveNext (Command.AddHat);
			}
			if (HaveTorsoArmor ())
			{
				fsm_process.MoveNext (Command.AddWrap);
			}

			// Assign this room's owner to the wardrobe
			if (owner == null)
			{
				AssignRoomOwner ();
			}

			// Check current state and perform allowance adjustments
			InspectStateMachine ();
		}


		public override void DeSpawn ()
		{
			base.DeSpawn ();
			if (HaveHeadArmor ())
				storedHead.SetForbidden (false);
			if (HaveTorsoArmor ())
				storedTorso.SetForbidden (false);
		}


		public override void DrawGUIOverlay ()
		{
			if (Find.CameraMap.CurrentZoom == CameraZoomRange.Closest)
			{
				if (HaveHeadArmor () || HaveTorsoArmor ())
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
			StringBuilder stringBuilder = new StringBuilder ();
			stringBuilder.Append (base.GetInspectString ());

			stringBuilder.AppendLine ();
			stringBuilder.Append ("Owner".Translate () + ": ");
			if (owner == null) {
				stringBuilder.Append ("Nobody".Translate ());
			}
			else {
				stringBuilder.Append (owner.LabelCap);
			}
			if (HaveHeadArmor ())
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("Helmut: " + storedHead.Label);
			}
			if (HaveTorsoArmor ())
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("Armor: " + storedTorso.Label);
			}
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

			Command_Action assignOwnerButton = new Command_Action();
			assignOwnerButton.icon = assignOwnerIcon;
			assignOwnerButton.defaultDesc = "Assign owner to wardrobe.";
			assignOwnerButton.defaultLabel = "Assign Owner";
			assignOwnerButton.activateSound = SoundDef.Named("Click");
			assignOwnerButton.action = new Action(PerformAssignWardrobeAction);
			assignOwnerButton.groupKey = groupKeyBase + 1;
			gizmoList.Add(assignOwnerButton);

			Command_Action assignRoomOwnerButton = new Command_Action();
			assignRoomOwnerButton.icon = assignRoomOwnerIcon;
			assignRoomOwnerButton.defaultDesc = "Assign owner of room to wardrobe.";
			assignRoomOwnerButton.defaultLabel = "Assign Owner of Room";
			assignRoomOwnerButton.activateSound = SoundDef.Named("Click");
			assignRoomOwnerButton.action = new Action(AssignRoomOwner);
			assignRoomOwnerButton.groupKey = groupKeyBase + 2;
			gizmoList.Add(assignRoomOwnerButton);

			Command_Action resetButton = new Command_Action();
			resetButton.icon = resetWardrobeIcon;
			resetButton.defaultDesc = "Resets the wardrobe allowances in case of error.";
			resetButton.defaultLabel = "Reset Wardrobe";
			resetButton.activateSound = SoundDef.Named("Click");
			resetButton.action = new Action(PerformResetWardrobeAction);
			resetButton.groupKey = groupKeyBase + 3;
			gizmoList.Add(resetButton);

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
				destroyArmorButton.action = new Action (Debug_DestroyArmor);
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


		public override void Notify_ReceivedThing(Thing newItem)
		{
//			Log.Message (string.Format ("[{0}] Received in wardrobe: {1}", owner.Nickname, newItem.Label));

			if (IsTorsoShell(newItem.def))
			{
				fsm_process.MoveNext (Command.AddWrap);
				storedTorso = newItem;
			} else if (IsOverHead(newItem.def))
			{
				fsm_process.MoveNext (Command.AddHat);
				storedHead = newItem;
			} else 
			{
				Log.Error (string.Format ("[{0}] Invalid item stored in wardrobe: {1}", owner.Nickname, newItem.Label));
			}
			newItem.SetForbidden (true);
			InspectStateMachine ();
		}


		public override void Notify_LostThing(Thing newItem)
		{
//			Log.Message (string.Format ("[{0}] Removed from wardrobe: {1}", owner.Nickname, newItem.Label));

			if (IsTorsoShell(newItem.def))
			{
				fsm_process.MoveNext (Command.RemoveWrap);
				storedTorso = null;
			} else if (IsOverHead(newItem.def))
			{
				fsm_process.MoveNext (Command.RemoveHat);
				storedHead = null;
			} else 
			{
				Log.Error (string.Format ("[{0}] Invalid item removed from wardrobe: {1}", owner.Nickname, newItem.Label));
			}
			InspectStateMachine ();
		}


		// ===================== Ticker =====================

		/// <summary>
		/// This is the normal ticker
		/// </summary>
		public override void Tick()
		{
			//if (destroyedFlag) // Do nothing further, when destroyed (just a safety)
			//	return;

			base.Tick();

			// Call work function
			DoTickerWork(1);
		}

		/// <summary>
		/// Dos the ticker work.
		/// Remove the AllSlotCells iteration.  Instead, use HaveHat() and HaveWrap() to directly wear items
		/// Tell owner to wear wrap.  Add to LostItem() that if Wrap is lost and HaveHat() then put on hat as well
		/// Keep wardrobe disallowed until spring time, tell colonists to put their shit away.
		/// When seasons change, items left in wardrobes from past season that were never worn need to be unforbidden.
		/// </summary>
		/// <param name="tickerAmount">Ticker amount.</param>
		private void DoTickerWork (int tickerAmount)
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
						if (HaveTorsoArmor () || HaveHeadArmor ())
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
		void IssueWearJob()
		{
			var jobWear = new Job (DefDatabase<JobDef>.GetNamed (JobDef_wearArmor), this);

			owner.playerController.TakeOrderedJob (jobWear);
		}


		/// <summary>
		/// Assigns the room owner.
		/// </summary>
		void AssignRoomOwner()
		{
			Pawn newOwner = null;

			// Assign the wardrobe
			Room room = Position.GetRoomOrAdjacent();
			if (room != null)
			{
				Pawn roomOwner = room.RoomOwner;
				newOwner = roomOwner;  // roomOwner may be null, which is valid
			} else
			{
				newOwner = null;
			}
			owner = newOwner;
		}


		/// <summary>
		/// Does the wardrobe hold a wrap (torso shell-layer apparel)?
		/// </summary>
		/// <returns><c>true</c>, if wrap was had, <c>false</c> otherwise.</returns>
		bool HaveTorsoArmor()
		{
			return (storedTorso != null);
		}


		/// <summary>
		/// Does the wardrobe hold a hat?
		/// </summary>
		/// <returns><c>true</c>, if hat was had, <c>false</c> otherwise.</returns>
		bool HaveHeadArmor()
		{
			return (storedHead != null);
		}


		/// <summary>
		/// Implements the state machine that determines what this wardrobe is allowed to store
		/// When the season is cold, we store warm clothes and vice-versa.
		/// When a hat has been added, we disallow additional hats and vice-versa with wraps.
		/// </summary>
		void InspectStateMachine()
		{
//			if (owner != null)
//			{
//				Log.Message (String.Format ("[{0}] Current state: {1}", owner.Nickname, fsm_process.CurrentState));
//			} else
//			{
//				Log.Message ("[Unowned] Current state: " + fsm_process.CurrentState);
//			}
			
			var allowedDefList = new List<ThingDef> ();

			switch (fsm_process.CurrentState)
			{
			case AllowanceState.AllowAll:
				allowedDefList.AddRange (allowedHeadDefs);
				allowedDefList.AddRange (allowedTorsoDefs);
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.AllowHat:
				allowedDefList.AddRange (allowedHeadDefs);
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.AllowWrap:
				allowedDefList.AddRange (allowedTorsoDefs);
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.AllowNone:
				ChangeAllowances(allowedDefList);
				break;
			default:
				Log.Error ("Invalid AllowanceState in Building_SeasonalWardrobe.process.");
				break;
			}
		}


		/// <summary>
		/// Changes the allowances.
		/// </summary>
		/// <param name="thingDefs">Thing defs.</param>
		void ChangeAllowances(List<ThingDef> thingDefs)
		{
			// Update allowances
			settings.allowances.DisallowAll ();
			foreach (var td in thingDefs)
			{
//				Log.Message (String.Format ("[{0}] allowing in wardrobe: {1}", owner.Nickname, td.label));
				settings.allowances.SetAllow (td, true);
			}
		}


		/// <summary>
		/// Conditionally unforbids stored apparel in the wardrobe 
		/// </summary>
		/// <param name="force">If set to <c>true</c> forces the unforbiding.</param>
		public void UnforbidClothing(bool force=false)
		{
			if (force)
			{
				if (HaveHeadArmor ())
				{
					storedHead.SetForbidden (false);
				}
				if (HaveTorsoArmor ())
				{
					storedTorso.SetForbidden (false);
				}
				return;
			}
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
		/// Determines if thingDef is an apparel that goes over the head
		/// </summary>
		/// <returns><c>true</c> if is over head the specified thingDef; otherwise, <c>false</c>.</returns>
		/// <param name="thingDef">Thing def.</param>
		public static bool IsOverHead(ThingDef thingDef)
		{
			if (!thingDef.IsApparel)
			{
				return false;
			}
			else
			{
				return thingDef.apparel.layers.Contains (ApparelLayer.Overhead) &&
					thingDef.apparel.CoversBodyPart (Building_SmartArmorRack.headParts);
			}
		}

		/// <summary>
		/// Determines if thingDef is an apparel that covers the torso at the shell layer
		/// </summary>
		/// <returns><c>true</c> if is torso shell the specified thingDef; otherwise, <c>false</c>.</returns>
		/// <param name="thingDef">Thing def.</param>
		public static bool IsTorsoShell(ThingDef thingDef)
		{
			if (!thingDef.IsApparel)
			{
				return false;
			}
			else
			{
				return thingDef.apparel.layers.Contains (ApparelLayer.Shell) &&
					thingDef.apparel.CoversBodyPart (Building_SmartArmorRack.torsoParts);
			}
		}

		/// <summary>
		/// Performs the assign wardrobe action when assignOwnerButton is clicked
		/// </summary>
		void PerformAssignWardrobeAction()
		{
			Log.Message ("Assign Owner Wardrobe");
//			Find.LayerStack.Add (new Dialog_AssignWardrobeOwner (this));
			return;
		}


		/// <summary>
		/// Reset the wardrobe: unforbid any stored clothing, assign owner, restart the state machine
		/// </summary>
		void PerformResetWardrobeAction()
		{
			UnforbidClothing (true);
			AssignRoomOwner ();
			fsm_process = new FSM_Process ();
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

			Debug_SpawnApparel (thingDefs, stuffDef);
		}

		/// <summary>
		/// Spawns the warm season apparel.
		/// </summary>
		void Debug_DestroyArmor()
		{
			var thingsToDestroy = new List<Thing> ();

			foreach (IntVec3 cell in AllSlotCellsList())
			{
				IEnumerable<Thing> oldThings = Find.ListerThings.AllThings.Where (t => t.Position == cell);
				foreach (Thing t in oldThings)
				{
					if (t != this)
					{
						thingsToDestroy.Add (t);
					}
				}
			}

			foreach (Thing t in thingsToDestroy)
			{
				Log.Message (String.Format ("Destroying {0}", t));
				Notify_LostThing (t);
				t.Destroy ();
			}
		}

		void Debug_SpawnApparel(List<ThingDef> thingDefs, ThingDef stuffDef)
		{
			List<IntVec3> cells = AllSlotCellsList ();
			fsm_process = new FSM_Process ();
		
			for (int i = 0; i < Building_SmartArmorRack.NUM_SLOTS; i++)
			{
				// Spawn the new thing
				Thing newThing;
				if (stuffDef == null)
					newThing = ThingMaker.MakeThing (thingDefs [i]);
				else
					newThing = ThingMaker.MakeThing (thingDefs [i], stuffDef);

				GenSpawn.Spawn (newThing, cells [i]).stackCount = 1;
				Notify_ReceivedThing (newThing);
			}
		}
	} // class Building_Wardrobe
}