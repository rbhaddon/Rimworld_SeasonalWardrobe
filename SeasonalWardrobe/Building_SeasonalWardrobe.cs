// ----------------------------------------------------------------------
// These are basic usings. Always let them be here.
// ----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// ----------------------------------------------------------------------
// These are RimWorld-specific usings. Activate/Deactivate what you need:
// ----------------------------------------------------------------------
using UnityEngine;         // Always needed
//using VerseBase;         // Material/Graphics handling functions are found here
using Verse;               // RimWorld universal objects are here (like 'Building')
using Verse.AI;          // Needed when you do something with the AI
//using Verse.Sound;       // Needed when you do something with Sound
//using Verse.Noise;       // Needed when you do something with Noises
using RimWorld;            // RimWorld specific functions are found here (like 'Building_Battery')
//using RimWorld.Planet;   // RimWorld specific functions for world creation
//using RimWorld.SquadAI;  // RimWorld specific functions for squad brains 

namespace SeasonalWardrobe
{
	public class Building_SeasonalWardrobe : Building_Storage
	{
		// Lists of apparel to wear during cold seasons
		private static List<ThingDef> coldSeasonWraps = new List<ThingDef> ();
		private static List<ThingDef> coldSeasonHats = new List<ThingDef> ();
		private static List<ThingDef> coldSeasonAll = new List<ThingDef> ();

		// Lists of apparel to wear during cold seasons
		private static List<ThingDef> warmSeasonWraps = new List<ThingDef> ();
		private static List<ThingDef> warmSeasonHats = new List<ThingDef> ();
		private static List<ThingDef> warmSeasonAll = new List<ThingDef> ();

		// These records enable us to split our allowances into the above lists
		private static BodyPartRecord torsoParts = new BodyPartRecord ();
		private static BodyPartRecord headParts = new BodyPartRecord();

		const int COLD_SEASON = 0;
		const int WARM_SEASON = 1;

		const int HEAD_INSULATION_LIMIT = -10;
		const int TORSO_INSULATION_LIMIT = -30;

		// Owner is of this wardrobe is colonist who will receive the wear apparel jobs
		public Pawn owner = null;

		public bool ColdSeason = false;

		// The two items we can hold
		public Thing storedHat = null;
		public Thing storedWrap = null;

		// JobDefs
		private const String JobDef_wearClothes = "WearClothesInWardrobe";

		// Textures
		public static Texture2D assignOwnerIcon;
		public static Texture2D assignRoomOwnerIcon;

		private Process process;

		// Debug stuff
		public int dayTicks = 20000;
		public int counter = 0;

		static Building_SeasonalWardrobe ()
		{
			// Note: this type is marked as 'beforefieldinit'.

			Building_SeasonalWardrobe.assignOwnerIcon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner");
			Building_SeasonalWardrobe.assignRoomOwnerIcon = ContentFinder<Texture2D>.Get ("UI/Commands/AssignRoomOwner");

			// Add definition to our BodyPartsRecords so we can distinguish between parkas and tuques, for instance
			Building_SeasonalWardrobe.torsoParts.groups.Add (BodyPartGroupDefOf.Torso);
			Building_SeasonalWardrobe.headParts.groups.Add (BodyPartGroupDefOf.UpperHead);
			Building_SeasonalWardrobe.headParts.groups.Add (BodyPartGroupDefOf.FullHead);

			// Build list of allowed apparel defs
			Building_SeasonalWardrobe.CreateApparelLists ();
		}


		public override void ExposeData ()
		{
			base.ExposeData ();
			Scribe_References.LookReference<Pawn> (ref owner, "owner");
			Scribe_Values.LookValue<bool> (ref ColdSeason, "ColdSeason");
			Scribe_References.LookReference<Thing> (ref storedHat, "storedHat");
			Scribe_References.LookReference<Thing> (ref storedWrap, "storedWrap");
		}


		/// <summary>
		/// Setup a recently spawned Building_Wardrobe
		/// Assign button textures, set the wardrobe's owner, build apparel lists 
		/// </summary>
		public override void SpawnSetup ()
		{
			base.SpawnSetup ();

			process = new Process ();
			Log.Message ("Current state: " + process.CurrentState);

			// Restore state
			if (HaveHat ())
			{
				process.MoveNext (Command.AddHat);
			}
			if (HaveWrap ())
			{
				process.MoveNext (Command.AddWrap);
			}
			RunStateMachine ();

			// Assign this room's owner to the wardrobe
			if (owner == null)
				AssignRoomOwner ();

			// Set what is allowed to be stored herea
			RunStateMachine ();
		}


		public override void DeSpawn ()
		{
			base.DeSpawn ();
			if (HaveHat ())
				storedHat.SetForbidden (false);
			if (HaveWrap ())
				storedWrap.SetForbidden (false);
		}


		public override void DrawGUIOverlay ()
		{
			if (Find.CameraMap.CurrentZoom == CameraZoomRange.Closest)
			{
				if (HaveHat () || HaveWrap ())
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
			if (HaveHat ())
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("\tHat: " + storedHat.Label);
			}
			if (HaveWrap ())
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("\tTorso: " + storedWrap.Label);
			}
			return stringBuilder.ToString ();
		}


//		///<summary>
//		///This creates the command buttons to control the wardrobe
//		///</summary>
//		///<returns>The list of command buttons to display.</returns>
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

			Command_Action debugColdButton = new Command_Action();
//			debugColdButton.icon = assignRoomOwnerIcon;
			debugColdButton.defaultDesc = "Spawn cold weather clothing in wardrobe.";
			debugColdButton.defaultLabel = "Debug Cold";
			debugColdButton.activateSound = SoundDef.Named("Click");
			debugColdButton.action = new Action(Debug_SpawnColdSeasonApparel);
			debugColdButton.groupKey = groupKeyBase + 4;
			gizmoList.Add(debugColdButton);

			Command_Action debugWarmButton = new Command_Action();
//			debugWarmButton.icon = assignRoomOwnerIcon;
			debugWarmButton.defaultDesc = "Spawn warm weather clothing in wardrobe.";
			debugWarmButton.defaultLabel = "Debug Warm";
			debugWarmButton.activateSound = SoundDef.Named("Click");
			debugWarmButton.action = new Action(Debug_SpawnWarmSeasonApparel);
			debugWarmButton.groupKey = groupKeyBase + 6;
			gizmoList.Add(debugWarmButton);

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
		/// Notifies the received thing.
		/// </summary>
		/// <param name="newItem">New item.</param>
		public override void Notify_ReceivedThing(Thing newItem)
		{
//			Log.Message (string.Format ("[{0}] Received in wardrobe: {1}", owner.Nickname, newItem.Label));

			if (IsTorsoShell(newItem.def))
			{
				process.MoveNext (Command.AddWrap);
				storedWrap = newItem;
			} else if (IsOverHead(newItem.def))
			{
				process.MoveNext (Command.AddHat);
				storedHat = newItem;
			} else 
			{
				Log.Error (string.Format ("[{0}] Invalid item stored in wardrobe: {1}", owner.Nickname, newItem.Label));
			}
			newItem.SetForbidden (true);
			RunStateMachine ();
		}

		/// <summary>
		/// Called when a Thing has been removed from storage
		/// I don't know how to queue jobs without them stepping on each other, so this is a work-around.
		/// If owner grabs a coat, then grab a hat also, and vice-versa.
		/// </summary>
		/// <param name="newItem">Lost item.</param>
		public override void Notify_LostThing(Thing newItem)
		{
//			Log.Message (string.Format ("[{0}] Removed from wardrobe: {1}", owner.Nickname, newItem.Label));

			if (IsTorsoShell(newItem.def))
			{
				process.MoveNext (Command.RemoveWrap);
				storedWrap = null;
			} else if (IsOverHead(newItem.def))
			{
				process.MoveNext (Command.RemoveHat);
				storedHat = null;
			} else 
			{
				Log.Error (string.Format ("[{0}] Invalid item removed from wardrobe: {1}", owner.Nickname, newItem.Label));
			}
			RunStateMachine ();
		}


		// ===================== Ticker =====================

		/// <summary>
		/// This is used, when the Ticker in the XML is set to 'Rare'
		/// This is a tick thats done once every 250 normal Ticks
		/// </summary>
		public override void TickRare()
		{
			//if (destroyedFlag) // Do nothing further, when destroyed (just a safety)
			//	return;

			base.TickRare();

			// Call work function
			DoTickerWork(250);
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

//			if (owner == null)
//				return;

			//Season currentSeason = GenDate.CurrentSeason;
			int dayOfMonth = GenDate.DayOfMonth;
			int hourOfDay = GenTime.HourInt;
			//if (currentSeason == Season.Fall && dayOfMonth == 1) // First day of fall
			//if (dayOfMonth % 2 == 0) {
			//Log.Message("Odd Day");

			if (counter >= dayTicks)
			{
				counter = 0;
				ColdSeason = !ColdSeason;
				if (owner != null)
					Log.Warning (String.Format ("[{0}] Is cold season? {1}", owner.Nickname, ColdSeason));
				process.MoveNext (Command.ChangeSeason);
				RunStateMachine ();

				if (HaveWrap () || HaveHat ())
				{	
					if (owner == null)
					{
						process.MoveNext (Command.ChangeSeason);
					} else
					{
						Log.Message (string.Format ("[{0}] Issuing wear job.", owner.Nickname));
						IssueWearJob ();
					}
				}
			}
		}

		// ======================== Private Methods ================

		/// <summary>
		/// Determines if issue wear job the specified pawn article.
		/// </summary>
		/// <returns><c>true</c> if issue wear job the specified pawn article; otherwise, <c>false</c>.</returns>
		void IssueWearJob()
		{
			var jobWear = new Job (DefDatabase<JobDef>.GetNamed (JobDef_wearClothes), this);
			owner.playerController.TakeOrderedJob (jobWear);
		}

		/// <summary>
		/// Assigns the room owner.
		/// </summary>
		void AssignRoomOwner()
		{
			// Assign the wardrobe
			Room room = Position.GetRoomOrAdjacent();
			if (room != null)
			{
				Pawn roomOwner = room.RoomOwner;
				// owner might be null, which is a valid owner
				owner = roomOwner;
			} else
			{
				owner = null;
			}
		}

		/// <summary>
		/// Does the wardrobe hold a wrap (torso shell-layer apparel)?
		/// </summary>
		/// <returns><c>true</c>, if wrap was had, <c>false</c> otherwise.</returns>
		bool HaveWrap()
		{
			return (storedWrap != null);
		}


		/// <summary>
		/// Does the wardrobe hold a hat?
		/// </summary>
		/// <returns><c>true</c>, if hat was had, <c>false</c> otherwise.</returns>
		bool HaveHat()
		{
			return (storedHat != null);
		}

		/// <summary>
		/// Shoulds the wear job be issued.
		/// </summary>
		/// <returns><c>true</c>, if wear job be issued was shoulded, <c>false</c> otherwise.</returns>
		/// <param name="storedApparel">Apparel thing.</param>
		public bool ShouldWearJobBeIssued(Thing storedApparel)
		{
			// Make additional check for owner.  If they are naked on head and torso shell, maybe issue job anyw
			bool alreadyWorn = false;

			foreach (Apparel wornApparel in owner.apparel.WornApparel)
			{
				if (IsOverHead(wornApparel.def) || IsTorsoShell(wornApparel.def))
				{
					Log.Message (String.Format("{0} is already wearing a similar item to {1}.", owner.Nickname, storedApparel.Label));
					alreadyWorn = true;
				}
			}

			return IsCorrectSeasonForApparel (storedApparel) && !alreadyWorn;
		}
			

		/// <summary>
		/// Determines whether the given apparel item is appropriate to wear in the current season
		/// </summary>
		/// <returns><c>true</c> if this instance is correct season for apparel the specified apparelThing; otherwise, <c>false</c>.</returns>
		/// <param name="apparelThing">Apparel thing.</param>
		public bool IsCorrectSeasonForApparel(Thing apparelThing)
		{
			bool retval;
			if (ColdSeason)
			{
				retval = coldSeasonAll.Contains (apparelThing.def);
			} else
			{
				retval = warmSeasonAll.Contains(apparelThing.def);
			}
			return retval;
		}

		/// <summary>
		/// Implements the state machine that determines what this wardrobe is allowed to store
		/// When the season is cold, we store warm clothes and vice-versa.
		/// When a hat has been added, we disallow additional hats and vice-versa with wraps.
		/// </summary>
		void RunStateMachine()
		{
			if (owner != null)
				Log.Message (String.Format ("[{0}] Current state is {1}", owner.Nickname, process.CurrentState));
			var allowedDefList = new List<ThingDef> ();
			var hats = new List<ThingDef> ();
			var wraps = new List<ThingDef> ();

			if (ColdSeason)
			{
				// Season is currently cold, so allow storage of warm stuff
				hats = Building_SeasonalWardrobe.warmSeasonHats;
				wraps = Building_SeasonalWardrobe.warmSeasonWraps;
			} else
			{
				// Season is currently warm, so allow storage of cold stuff
				hats = Building_SeasonalWardrobe.coldSeasonHats;
				wraps = Building_SeasonalWardrobe.coldSeasonWraps;
			}

			switch (process.CurrentState)
			{
			case AllowanceState.AllowAll:
				allowedDefList.AddRange (hats);
				allowedDefList.AddRange (wraps);
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.AllowHat:
				allowedDefList.AddRange (hats);
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.AllowWrap:
				allowedDefList.AddRange (wraps);
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.AllowNone:
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.WearAny:
				break;
			case AllowanceState.WearHat:
				if (!HaveHat ())
				{
					process.MoveNext (Command.ChangeSeason);
					UnforbidClothing ();
					RunStateMachine ();
				}
				break;
			case AllowanceState.WearWrap:
				if (!HaveWrap ())
				{
					process.MoveNext (Command.ChangeSeason);
					UnforbidClothing ();
					RunStateMachine ();
				}
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
		/// Unforbids the clothing if any is present.
		/// </summary>
		public void UnforbidClothing()
		{
			List<ThingDef> currentSeasonApparel;
	
			if (ColdSeason)
			{
				currentSeasonApparel = Building_SeasonalWardrobe.coldSeasonAll;
			} else
			{
				currentSeasonApparel = Building_SeasonalWardrobe.warmSeasonAll;
			}

			if (HaveHat())
			{
				if (currentSeasonApparel.Contains (storedHat.def))
				{
					storedHat.SetForbidden (false);
					process.MoveNext (Command.AddHat);
				}
			}
			if (HaveWrap())
			{
				if (currentSeasonApparel.Contains (storedWrap.def))
				{
					storedWrap.SetForbidden (false);
					process.MoveNext (Command.AddWrap);
				}
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
					int statInsulationCold = (int)thingDef.statBases.GetStatValueFromList (StatDefOf.Insulation_Cold, 500);
//					Log.Message(String.Format("{0} stat: Insulation_Cold = {1}", thingDef.label, statInsulationCold));

					if (IsOverHead(thingDef))
					{
						if (statInsulationCold <= HEAD_INSULATION_LIMIT)
						{
//							Log.Message (String.Format ("Adding {0} to cold weather hats.", thingDef.label));
							Building_SeasonalWardrobe.coldSeasonHats.Add (thingDef);
						} else
						{
//							Log.Message (String.Format ("Adding {0} to warm weather hats.", thingDef.label));
							Building_SeasonalWardrobe.warmSeasonHats.Add (thingDef);
						}
					} else if (IsTorsoShell(thingDef))
					{
						if (statInsulationCold <= TORSO_INSULATION_LIMIT)
						{
//							Log.Message (String.Format ("Adding {0} to cold weather wraps.", thingDef.label));
							Building_SeasonalWardrobe.coldSeasonWraps.Add (thingDef);
						} else
						{
//							Log.Message (String.Format ("Adding {0} to warm weather wraps.", thingDef.label));
							Building_SeasonalWardrobe.warmSeasonWraps.Add (thingDef);
						}
					}
					Building_SeasonalWardrobe.coldSeasonAll.AddRange (Building_SeasonalWardrobe.coldSeasonHats);
					Building_SeasonalWardrobe.coldSeasonAll.AddRange (Building_SeasonalWardrobe.coldSeasonWraps);
					Building_SeasonalWardrobe.warmSeasonAll.AddRange (Building_SeasonalWardrobe.warmSeasonHats);
					Building_SeasonalWardrobe.warmSeasonAll.AddRange (Building_SeasonalWardrobe.warmSeasonWraps);
				}
			}
			Log.Message (String.Format ("coldSeasonHats contains {0} ThingDefs", Building_SeasonalWardrobe.coldSeasonHats.Count));
			Log.Message (String.Format ("coldSeasonWraps contains {0} ThingDefs", Building_SeasonalWardrobe.coldSeasonWraps.Count));
			Log.Message (String.Format ("warmSeasonHats contains {0} ThingDefs", Building_SeasonalWardrobe.warmSeasonHats.Count));
			Log.Message (String.Format ("warmSeasonWraps contains {0} ThingDefs", Building_SeasonalWardrobe.warmSeasonWraps.Count));
		}

		/// <summary>
		/// Determines if thingDef is an apparel that goes over the head
		/// </summary>
		/// <returns><c>true</c> if is over head the specified thingDef; otherwise, <c>false</c>.</returns>
		/// <param name="thingDef">Thing def.</param>
		static bool IsOverHead(ThingDef thingDef)
		{
			if (!thingDef.IsApparel)
			{
				return false;
			}
			else
			{
				return thingDef.apparel.layers.Contains (ApparelLayer.Overhead) &&
					thingDef.apparel.CoversBodyPart (Building_SeasonalWardrobe.headParts);
			}
		}

		/// <summary>
		/// Determines if thingDef is an apparel that covers the torso at the shell layer
		/// </summary>
		/// <returns><c>true</c> if is torso shell the specified thingDef; otherwise, <c>false</c>.</returns>
		/// <param name="thingDef">Thing def.</param>
		static bool IsTorsoShell(ThingDef thingDef)
		{
			if (!thingDef.IsApparel)
			{
				return false;
			}
			else
			{
				return thingDef.apparel.layers.Contains (ApparelLayer.Shell) &&
					thingDef.apparel.CoversBodyPart (Building_SeasonalWardrobe.torsoParts);
			}
		}

		/// <summary>
		/// Performs the assign wardrobe action when assignOwnerButton is clicked
		/// </summary>
		void PerformAssignWardrobeAction()
		{
//			Log.Message ("Assign Owner Wardrobe");
			Find.LayerStack.Add (new Dialog_AssignWardrobeOwner (this));
			return;
		}

		/// <summary>
		/// Spawns the cold season apparel.
		/// </summary>
		void Debug_SpawnColdSeasonApparel()
		{
			Log.Message ("Debug_SpawnCold");

			var thingDefs = new List<ThingDef> ();
			thingDefs.Add(ThingDef.Named("Apparel_Parka"));
			thingDefs.Add(ThingDef.Named("Apparel_Tuque"));
			ThingDef stuffDef = ThingDef.Named("DevilstrandCloth");

			Debug_SpawnApparel (thingDefs, stuffDef);
		}

		/// <summary>
		/// Spawns the warm season apparel.
		/// </summary>
		void Debug_SpawnWarmSeasonApparel()
		{
			Log.Message ("Debug_SpawnCold");

			var thingDefs = new List<ThingDef> ();
			thingDefs.Add(ThingDef.Named("Apparel_Jacket"));
			thingDefs.Add(ThingDef.Named("Apparel_CowboyHat"));
			ThingDef stuffDef = ThingDef.Named("Cloth");

			Debug_SpawnApparel (thingDefs, stuffDef);
		}

		void Debug_SpawnApparel(List<ThingDef> thingDefs, ThingDef stuffDef)
		{
			List<IntVec3> cells = AllSlotCells ().ToList ();
			var thingsToDestroy = new List<Thing> ();

			process = new Process ();

			for (int i = 0; i < 2; i++)
			{
				// Destroy any existing items first -- this is a hack: assumes there is only one item as positon
				IEnumerable<Thing> oldThings = Find.ListerThings.AllThings.Where (t => t.Position == cells [i]);
				foreach (Thing t in oldThings)
				{
					if (t != this)
					{
						thingsToDestroy.Add(t);
					}
				}

				foreach (Thing t in thingsToDestroy)
				{
					t.Destroy ();
				}

				// Spawn the new thing
				Thing newThing = ThingMaker.MakeThing (thingDefs [i], stuffDef);
				GenSpawn.Spawn (newThing, cells [i]).stackCount = 1;
				Notify_ReceivedThing (newThing);
			}
		}
	} // class Building_Wardrobe
}