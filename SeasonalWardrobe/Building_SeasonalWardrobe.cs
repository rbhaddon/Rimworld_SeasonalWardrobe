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
		const int COLD_SEASON = 0;
		const int WARM_SEASON = 1;

		// Owner is of this wardrobe is colonist who will receive the wear apparel jobs
		public Pawn owner = null;

		public bool ColdSeason = false;
		public int LastSeason = COLD_SEASON;

		// The two items we can hold
		public Thing storedHat = null;
		public Thing storedWrap = null;

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

		// JobDefs
		private const String JobDef_wearClothes = "WearClothesInWardrobe";

		// Textures
		public Texture2D assignOwnerIcon;
		public Texture2D assignRoomOwnerIcon;

		// Debug stuff
		public int dayTicks = 10000;
		public int counter = 0;


		public override void ExposeData ()
		{
			base.ExposeData ();
			Scribe_References.LookReference<Pawn> (ref this.owner, "owner");
			Scribe_Values.LookValue<int> (ref LastSeason, "LastSeason");
			Scribe_Values.LookValue<bool> (ref ColdSeason, "ColdSeason");
			Scribe_References.LookReference<Thing> (ref this.storedHat, "storedHat");
			Scribe_References.LookReference<Thing> (ref this.storedWrap, "storedWrap");

		}


		//
		// Constructors
		//
		static Building_SeasonalWardrobe ()
		{
			// Note: this type is marked as 'beforefieldinit'.
			// Add definition to our BodyPartsRecords so we can distinguish between parkas and tuques, for instance


		}

		//
		// Methods
		//

		/// <summary>
		/// Setup a recently spawned Building_Wardrobe
		/// Assign button textures, set the wardrobe's owner, build apparel lists 
		/// </summary>
		public override void SpawnSetup ()
		{
			Log.Message (String.Format("Current season is cold: {0}", ColdSeason));
			base.SpawnSetup ();
			assignOwnerIcon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner");
			assignRoomOwnerIcon = ContentFinder<Texture2D>.Get ("UI/Commands/AssignRoomOwner");

			torsoParts.groups.Add (BodyPartGroupDefOf.Torso);
			headParts.groups.Add (BodyPartGroupDefOf.UpperHead);
			headParts.groups.Add (BodyPartGroupDefOf.FullHead);

			// Assign this room's owner to the wardrobe
			if (owner == null)
				AssignRoomOwner ();

			// Build list of allowed apparel defs
			CreateApparelLists ();

			// Set what is allowed to be stored herea
			ChangeAllowances ();
		}


		public override void DeSpawn ()
		{
			owner = null;
			storedHat.SetForbidden (false);
			storedWrap.SetForbidden (false);
			storedHat = null;
			storedWrap = null;
			base.DeSpawn ();
		}


		public override void DrawGUIOverlay ()
		{
			if (Find.CameraMap.CurrentZoom == CameraZoomRange.Closest) {
				if (owner != null && owner.InBed () && owner.CurrentBed ().owner == owner) {
					return;
				}
				string text;
				if (owner != null) {
					text = owner.Nickname;
				}
				else {
					text = "Unowned".Translate ();
				}
				GenWorldUI.DrawThingLabelFor (this, text, new Color (1, 1, 1, 1));
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


		///<summary>
		///This creates the command buttons to control the wardrobe
		///</summary>
		///<returns>The list of command buttons to display.</returns>
		public override IEnumerable<Command> GetCommands()
		{
			IList<Command> buttonList = new List<Command>();
			int groupKeyBase = 12091967;

			Command_Action assignOwnerButton = new Command_Action();
			assignOwnerButton.icon = assignOwnerIcon;
			assignOwnerButton.defaultDesc = "Assign owner to wardrobe.";
			assignOwnerButton.defaultLabel = "Assign Owner";
			assignOwnerButton.activateSound = SoundDef.Named("Click");
			assignOwnerButton.action = new Action(PerformAssignWardrobeAction);
			assignOwnerButton.groupKey = groupKeyBase + 1;
			buttonList.Add(assignOwnerButton);

			Command_Action assignRoomOwnerButton = new Command_Action();
			assignRoomOwnerButton.icon = assignRoomOwnerIcon;
			assignRoomOwnerButton.defaultDesc = "Assign owner of room to wardrobe.";
			assignRoomOwnerButton.defaultLabel = "Assign Owner of Room";
			assignRoomOwnerButton.activateSound = SoundDef.Named("Click");
			assignRoomOwnerButton.action = new Action(AssignRoomOwner);
			assignRoomOwnerButton.groupKey = groupKeyBase + 2;
			buttonList.Add(assignRoomOwnerButton);

			IEnumerable<Command> resultButtonList;
			IEnumerable<Command> basebuttonList = base.GetCommands();
			if (basebuttonList != null)
			{
				resultButtonList = buttonList.AsEnumerable<Command>().Concat(basebuttonList);
			}
			else
			{
				resultButtonList = buttonList.AsEnumerable<Command>();
			}
			return (resultButtonList);
		}

		/// <summary>
		/// Notifies the received thing.
		/// </summary>
		/// <param name="newItem">New item.</param>
		public override void Notify_ReceivedThing(Thing newItem)
		{
//			Log.Message (string.Format ("Received in wardrobe: {0}", newItem.Label));

			if (newItem.def.apparel.CoversBodyPart (torsoParts))
			{
				storedWrap = newItem;
			} else if (newItem.def.apparel.CoversBodyPart (headParts))
			{
				storedHat = newItem;
			} else 
			{
				Log.Error (string.Format ("Invalid item stored in wardrobe: {0}", newItem.Label));
			}
			newItem.SetForbidden (true);
			ChangeAllowances ();
		}

		/// <summary>
		/// Called when a Thing has been removed from storage
		/// I don't know how to queue jobs without them stepping on each other, so this is a work-around.
		/// If owner grabs a coat, then grab a hat also, and vice-versa.
		/// </summary>
		/// <param name="newItem">Lost item.</param>
		public override void Notify_LostThing(Thing newItem)
		{
//			Log.Message (String.Format("Removed from wardrobe: {0}", newItem.Label));

			if (newItem.def.apparel.CoversBodyPart (torsoParts))
			{
				storedWrap = null;
			} else if (newItem.def.apparel.CoversBodyPart (headParts))
			{
				storedHat = null;
			} else 
			{
				Log.Error (string.Format ("Invalid item removed from wardrobe: {0}", newItem.Label));
			}
			ChangeAllowances ();
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

			// Don't forget the base work
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
			if (owner == null)
				return;

			//Season currentSeason = GenDate.CurrentSeason;
			int dayOfMonth = GenDate.DayOfMonth;
			int hourOfDay = GenTime.HourInt;
			//if (currentSeason == Season.Fall && dayOfMonth == 1) // First day of fall
			//if (dayOfMonth % 2 == 0) {
			//Log.Message("Odd Day");

			if (dayOfMonth % 2 == 0 && hourOfDay == 10)
			{
				ColdSeason = true;
			} else
			{
				ColdSeason = false;
			}

			if (ColdSeason)
			{
				if (LastSeason != COLD_SEASON)
				{
					// Seasons have changed, so update allowances
					Log.Error (string.Format ("{0}: Season is cold: {1}", owner.Nickname, ColdSeason));
					UnforbidClothing ();
					ChangeAllowances ();
					LastSeason = COLD_SEASON;
					if (HaveWrap () || HaveHat ())
					{	
						Log.Message (string.Format ("Issuing Cold Season wear job for {0}", owner));
						IssueWearJob ();
					}
				}
			}
			if (!ColdSeason)
			{
				if (LastSeason != WARM_SEASON)
				{
					// Seasons have changed, so update allowances
					Log.Error (string.Format ("{0}: Season is cold: {1}", owner.Nickname, ColdSeason));
					UnforbidClothing ();
					ChangeAllowances ();
					LastSeason = WARM_SEASON;
					if (HaveWrap () || HaveHat ())
					{	
						Log.Message (string.Format ("Issuing Warm Season wear job for {0}", owner));
						IssueWearJob ();
					}

//					foreach (Apparel apparel in owner.apparel.WornApparel)
//					{
//						if (coldSeasonAll.Contains (apparel.def))
//						{	
//							// Pawn is wearing our stuff
//							Log.Message (owner.Nickname + " is wearing " + apparel.Label);
//						}
//					}
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
		void ChangeAllowances()
		{
			var allowedDefList = new List<ThingDef> ();

			if (ColdSeason)
			{
				// Autumn & Winter
				if (!HaveHat ())
					Log.Message (String.Format("{0}: Currently cold season and allowing warm weather hats", owner.Nickname));
					allowedDefList.AddRange (warmSeasonHats);
				if (!HaveWrap())
					Log.Message (String.Format("{0}: Currently cold season and allowing warm weather wraps", owner.Nickname));
					allowedDefList.AddRange (warmSeasonWraps);
			} else
			{
				// Spring & Summer
				if (!HaveHat ())
					Log.Message (String.Format("{0}: Currently warm season and allowing cold weather hats", owner.Nickname));
					allowedDefList.AddRange (coldSeasonHats);
				if (!HaveWrap ())
					Log.Message (String.Format("{0}: Currently warm season and allowing cold weather wraps", owner.Nickname));
					allowedDefList.AddRange (coldSeasonWraps);
			}

			// Update allowances
			settings.allowances.DisallowAll ();
			foreach (var td in allowedDefList)
			{
				settings.allowances.SetAllow (td, true);
			}
		}

		/// <summary>
		/// Unforbids the clothing if any is present.
		/// This will be more pawn job efficient if clothing that fits the current storage rule is left forbidden
		/// </summary>
		public void UnforbidClothing()
		{
			List<ThingDef> currentSeasonApparel;
	
			if (ColdSeason)
			{
				currentSeasonApparel = coldSeasonAll;
			} else
			{
				currentSeasonApparel = warmSeasonAll;
			}

			if (storedHat != null)
			{
				if (currentSeasonApparel.Contains(storedHat.def))
					storedHat.SetForbidden (false);
			}
			if (storedWrap != null)
			{
				if (currentSeasonApparel.Contains(storedWrap.def))
					storedWrap.SetForbidden (false);
			}
		}

		/// <summary>
		/// Creates the apparel lists, separated into what to warm and cold season and head vs. torso categories
 		/// </summary>
		void CreateApparelLists()
		{
			IEnumerable<ThingDef> allThingDefs = DefDatabase<ThingDef>.AllDefs;
			foreach (ThingDef thingDef in allThingDefs)
			{
				if (thingDef.IsApparel)
				{
					int comfyMaxTemp = (int)thingDef.equippedStatOffsets.GetStatOffsetFromList (StatDefOf.ComfyTemperatureMax);
//					Log.Message(String.Format("{0} ComfyTempMax: {1}", thingDef.label, comfyMaxTemp));

					if (comfyMaxTemp < 0)
					{
						// Cold Season clothing
						coldSeasonAll.Add (thingDef);
						if (thingDef.apparel.CoversBodyPart (headParts))
						{
							coldSeasonHats.Add (thingDef);
						} else if (thingDef.apparel.CoversBodyPart (torsoParts))
						{
							coldSeasonWraps.Add (thingDef);
						}
					} else
					{
						// Warm Season clothing
						warmSeasonAll.Add (thingDef);
						if (thingDef.apparel.CoversBodyPart(headParts))
						{
							warmSeasonHats.Add (thingDef);
						} else if (thingDef.apparel.CoversBodyPart(torsoParts))
						{
							warmSeasonWraps.Add(thingDef);
						}
					}
				}
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
	} // class Building_Wardrobe
}
