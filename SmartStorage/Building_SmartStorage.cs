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
	public abstract class Building_HeadAndTorsoStorage : Building_Storage
	{
		// These records enable us to split our allowances into the above lists
		public static BodyPartRecord torsoParts = new BodyPartRecord ();
		public static BodyPartRecord headParts = new BodyPartRecord();

		// Lists of allowed Head and Torso ThingDefs
		public static List<ThingDef> allowedTorsoDefs = new List<ThingDef> ();
		public static List<ThingDef> allowedHeadDefs = new List<ThingDef> ();
		public static List<ThingDef> allowedAllDefs = new List<ThingDef> ();

		// Owner is of this wardrobe is colonist who will receive the wear apparel jobs
		public Pawn owner = null;

		// Wardrobe allowances are implemented as a finite state machine
		public FSM_Process fsm_process;

		// The two apparel Things we store in the wardrobe, one of each.
		public Thing storedHead = null;
		public Thing storedTorso = null;

		// Textures
		public static Texture2D assignOwnerIcon;
		public static Texture2D assignRoomOwnerIcon;
		public static Texture2D resetSmartStorageIcon;

		// Tick-related vars; used to fire ticker work once per 24 hr game period
		public int dayTicks = 24000;
		public int counter = 0;

		// Testing helper because seasons last too long for good testing efficiency
		public bool TESTING_MODE = true;

		// Number of storage slots in this building
		public int NUM_SLOTS
		{
			get {
				return AllSlotCellsList ().Count();
			}
		}


		static Building_HeadAndTorsoStorage ()
		{
			assignOwnerIcon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner");
			assignRoomOwnerIcon = ContentFinder<Texture2D>.Get ("UI/Commands/AssignRoomOwner");
			resetSmartStorageIcon = ContentFinder<Texture2D>.Get ("UI/Commands/ResetWardrobe");

			// Add definition to our BodyPartsRecords so we can distinguish between parkas and tuques, for instance
			torsoParts.groups.Add (BodyPartGroupDefOf.Torso);
			headParts.groups.Add (BodyPartGroupDefOf.FullHead);
			headParts.groups.Add (BodyPartGroupDefOf.UpperHead);
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
			Log.Warning ("In Building_SmartStorage.SpawnSetup()");
			base.SpawnSetup ();

			// Start wardrobe's state machine
			fsm_process = new FSM_Process ();

			// Restore previous state if needed
			if (HaveHeadThing ())
			{
				fsm_process.MoveNext (Command.AddHat);
			}
			if (HaveTorsoThing ())
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
			if (HaveHeadThing ())
				storedHead.SetForbidden (false);
			if (HaveTorsoThing ())
				storedTorso.SetForbidden (false);
		}


//		public override void DrawGUIOverlay ()
//		{
//			if (Find.CameraMap.CurrentZoom == CameraZoomRange.Closest)
//			{
//				if (HaveHeadThing () || HaveTorsoThing ())
//				{
//					return;
//				}
//
//				string text;
//				if (owner != null)
//				{
//					text = owner.Nickname;
//				}
//				else
//				{
//					text = "Unowned".Translate ();
//				}
//				GenWorldUI.DrawThingLabel (this, text, new Color (1, 1, 1, 1));
//			}
//		}


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
			if (HaveHeadThing ())
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("Head: " + storedHead.Label);
			}
			if (HaveTorsoThing ())
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("Torso: " + storedTorso.Label);
			}
			return stringBuilder.ToString ();
		}


		public override IEnumerable<Gizmo> GetGizmos()
		{
			IList<Gizmo> gizmoList = new List<Gizmo> ();
			int groupKeyBase = 12091967;

			Command_Action assignOwnerButton = new Command_Action();
			assignOwnerButton.icon = assignOwnerIcon;
			assignOwnerButton.defaultDesc = "Assign owner to this SmartStorage.";
			assignOwnerButton.defaultLabel = "Assign Owner";
			assignOwnerButton.activateSound = SoundDef.Named("Click");
			assignOwnerButton.action = new Action(PerformAssignSmartStorageAction);
			assignOwnerButton.groupKey = groupKeyBase + 3;
			gizmoList.Add(assignOwnerButton);

			Command_Action assignRoomOwnerButton = new Command_Action();
			assignRoomOwnerButton.icon = assignRoomOwnerIcon;
			assignRoomOwnerButton.defaultDesc = "Assign this room's owner to this SmartStorage.";
			assignRoomOwnerButton.defaultLabel = "Assign Owner from Room";
			assignRoomOwnerButton.activateSound = SoundDef.Named("Click");
			assignRoomOwnerButton.action = new Action(AssignRoomOwner);
			assignRoomOwnerButton.groupKey = groupKeyBase + 4;
			gizmoList.Add(assignRoomOwnerButton);

			Command_Action resetButton = new Command_Action();
			resetButton.icon = resetSmartStorageIcon;
			resetButton.defaultDesc = "Resets this SmartStorage's allowances in case of error.";
			resetButton.defaultLabel = "Reset SmartStorage";
			resetButton.activateSound = SoundDef.Named("Click");
			resetButton.action = new Action(PerformResetSmartStorageAction);
			resetButton.groupKey = groupKeyBase + 5;
			gizmoList.Add(resetButton);

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


		public override void Notify_ReceivedThing(Thing newItem)
		{
//			Log.Message (string.Format ("[{0}] Received in SmartStorage: {1}", owner, newItem.Label));

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
				Log.Error (string.Format ("[{0}] Invalid item stored in SmartStorage: {1}", owner, newItem.Label));
			}
			newItem.SetForbidden (true);
			InspectStateMachine ();
		}


		public override void Notify_LostThing(Thing newItem)
		{
//			Log.Message (string.Format ("[{0}] Removed from SmartStorage: {1}", owner, newItem.Label));

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
				Log.Error (string.Format ("[{0}] Invalid item removed from SmartStorage: {1}", owner, newItem.Label));
			}
			InspectStateMachine ();
		}
			

		/// <summary>
		/// This method is called on every tick when the XML ticker is set to 'Normal'.
		/// </summary>
		public override void Tick()
		{
			base.Tick ();
			DoTickerWork (1);
		}

		/// <summary>
		/// This method is called on every 250 normal ticks when the XML ticker is set to 'Rare'.
		/// </summary>
		public  override void TickRare()
		{
			//if (destroyedFlag) // Do nothing further, when destroyed (just a safety)
			//	return;

			base.TickRare();
			DoTickerWork (250);
		}


		/// <summary>
		/// Does the ticker work.  Must be implemented in derived class
		/// </summary>
		/// <param name="tickerAmount">Ticker amount.</param>
		public abstract void DoTickerWork (int tickerAmount);


		/// <summary>
		/// Issues wear job for stored items in this SmartStorage.  Must be implemented in derived class.
		/// </summary>
		public abstract void IssueWearJob ();


		/// <summary>
		/// Assigns the room owner.
		/// </summary>
		public void AssignRoomOwner()
		{
			Pawn newOwner;

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
		public bool HaveTorsoThing()
		{
			return (storedTorso != null);
		}


		/// <summary>
		/// Does the wardrobe hold a hat?
		/// </summary>
		/// <returns><c>true</c>, if hat was had, <c>false</c> otherwise.</returns>
		public bool HaveHeadThing()
		{
			return (storedHead != null);
		}


		/// <summary>
		/// Implements the state machine that determines what this wardrobe is allowed to store
		/// When the season is cold, we store warm clothes and vice-versa.
		/// When a hat has been added, we disallow additional hats and vice-versa with wraps.
		/// </summary>
		public virtual void InspectStateMachine()
		{
			Log.Message (String.Format ("[{0}] Current base state: {1}", owner, fsm_process.CurrentState));

			var allowedDefList = new List<ThingDef> ();

			switch (fsm_process.CurrentState)
			{
			case AllowanceState.AllowAll:
				allowedDefList.AddRange (Building_HeadAndTorsoStorage.allowedHeadDefs);
				allowedDefList.AddRange (Building_HeadAndTorsoStorage.allowedTorsoDefs);
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.AllowHat:
				allowedDefList.AddRange (Building_HeadAndTorsoStorage.allowedHeadDefs);
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.AllowWrap:
				allowedDefList.AddRange (Building_HeadAndTorsoStorage.allowedTorsoDefs);
				ChangeAllowances(allowedDefList);
				break;
			case AllowanceState.AllowNone:
				ChangeAllowances(allowedDefList);
				break;
			}
		}


		/// <summary>
		/// Changes the allowances.
		/// </summary>
		/// <param name="thingDefs">Thing defs.</param>
		public void ChangeAllowances(List<ThingDef> thingDefs)
		{
			// Update allowances
			settings.allowances.DisallowAll ();
			foreach (var td in thingDefs)
			{
//				Log.Message (String.Format ("[{0}] allowing in SmartStorage: {1}", owner.Nickname, td.label));
				settings.allowances.SetAllow (td, true);
			}
		}
			

		/// <summary>
		/// Conditionally unforbids stored apparel in the wardrobe 
		/// </summary>
		/// <param name="force">If set to <c>true</c> forces the unforbiding.</param>
		public virtual void UnforbidClothing(bool force=false)
		{
			if (force)
			{
				if (HaveHeadThing ())
				{
					storedHead.SetForbidden (false);
				}
				if (HaveTorsoThing ())
				{
					storedTorso.SetForbidden (false);
				}
				return;
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
					thingDef.apparel.CoversBodyPart (Building_HeadAndTorsoStorage.headParts);
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
					thingDef.apparel.CoversBodyPart (Building_HeadAndTorsoStorage.torsoParts);
			}
		}

		/// <summary>
		/// Performs the assign wardrobe action when assignOwnerButton is clicked
		/// </summary>
		public void PerformAssignSmartStorageAction()
		{
//			Log.Message ("Assign Owner Wardrobe");
			Find.LayerStack.Add (new Dialog_AssignSmartStorageOwner (this));
			return;
		}


		/// <summary>
		/// Reset the wardrobe: unforbid any stored clothing, assign owner, restart the state machine
		/// </summary>
		public void PerformResetSmartStorageAction()
		{
			UnforbidClothing (true);
			AssignRoomOwner ();
			fsm_process = new FSM_Process ();
		}
			

		/// <summary>
		/// Debugs the destroy stored things.
		/// </summary>
		public void Debug_DestroyStoredThings()
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
				Log.Warning (String.Format ("Destroying {0}", t));
				Notify_LostThing (t);
				t.Destroy ();
			}
		}


		/// <summary>
		/// Debugs the spawn stored things.
		/// </summary>
		/// <param name="thingDefs">Thing defs.</param>
		/// <param name="stuffDef">Stuff def.</param>
		public void Debug_SpawnStoredThings(List<ThingDef> thingDefs, ThingDef stuffDef)
		{
			List<IntVec3> cells = AllSlotCellsList ();
			fsm_process = new FSM_Process ();

			for (int i = 0; i < NUM_SLOTS; i++)
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


	} // class Building_HeadAndToroStorage
}