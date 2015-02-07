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
using VerseBase;         // Material/Graphics handling functions are found here
using Verse;               // RimWorld universal objects are here (like 'Building')
using Verse.AI;          // Needed when you do something with the AI
using Verse.Sound;       // Needed when you do something with Sound
using Verse.Noise;       // Needed when you do something with Noises
using RimWorld;            // RimWorld specific functions are found here (like 'Building_Battery')
using RimWorld.Planet;   // RimWorld specific functions for world creation
using RimWorld.SquadAI;  // RimWorld specific functions for squad brains 

namespace SeasonalWardrobe
{
	public class Building_Wardrobe : Building_Storage
	{
		// Owner is of this wardrobe is colonist who will receive the wear apparel jobs
		public Pawn owner = null;

		// The two items we can hold
		public Thing storedHat = null;
		public Thing storedWrap = null;

		// List of allowed Torso-Shells and Hats
		private static List<ThingDef> allowedTorsoShells = new List<ThingDef> ();
		private static List<ThingDef> allowedHats = new List<ThingDef> ();

		// These records enable us to split our allowances into the above lists
		private static BodyPartRecord torsoParts = new BodyPartRecord ();
		private static BodyPartRecord headParts = new BodyPartRecord();

		// Textures
		public Texture2D assignOwnerIcon;
		public Texture2D assignRoomOwnerIcon;

		// Debug stuff
		public int dayTicks = 1000;
		public int counter = 0;

		//
		// Properties -- TODO I don't think this is being used
		//
		public Pawn CurOccupant {
			get {
				List<Thing> list = Find.ThingGrid.ThingsListAt (base.Position);
				for (int i = 0; i < list.Count; i++) {
					Pawn pawn = list [i] as Pawn;
					if (pawn != null) {
						if (pawn.jobs.curJob != null) {
							if (pawn.jobs.curJob.def == JobDefOf.LayDown && pawn.jobs.curJob.targetA.Thing == this) {
								return pawn;
							}
						}
					}
				}
				return null;
			}
		}


		//
		// Constructors
		//
		static Building_Wardrobe ()
		{
			// Note: this type is marked as 'beforefieldinit'.
			// Add definition to our BodyPartsRecords so we can distinguish between parkas and tuques, for instance
			torsoParts.groups.Add (BodyPartGroupDefOf.Torso);
			headParts.groups.Add (BodyPartGroupDefOf.UpperHead);
			headParts.groups.Add (BodyPartGroupDefOf.FullHead);
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
			base.SpawnSetup ();
			assignOwnerIcon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner");
			assignRoomOwnerIcon = ContentFinder<Texture2D>.Get ("UI/Commands/AssignRoomOwner");

			// Assign this room's owner to the wardrobe
			AssignRoomOwner ();

			// Build list of allowed apparel defs
			ProvisionAllowedLists ();
			GenDate.Days
		}


		public override void DeSpawn ()
		{
			if (this.owner != null) {
				this.owner.ownership.UnclaimBed ();
			}
			Room room = base.Position.GetRoom ();
			base.DeSpawn ();
			if (room != null) {
				room.RoomChanged ();
			}
		}


		public override void DrawGUIOverlay ()
		{
			if (Find.CameraMap.CurrentZoom == CameraZoomRange.Closest) {
				if (this.owner != null && this.owner.InBed () && this.owner.CurrentBed ().owner == this.owner) {
					return;
				}
				string text;
				if (this.owner != null) {
					text = this.owner.Nickname;
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
			if (this.owner == null) {
				stringBuilder.Append ("Nobody".Translate ());
			}
			else {
				stringBuilder.Append (this.owner.LabelCap);
			}
			if (this.HaveHat ())
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("\tHat: " + storedHat.Label);
			}
			if (this.HaveWrap ())
			{
				stringBuilder.AppendLine ();
				stringBuilder.Append ("\tTorso: " + storedWrap.Label);
			}
			return stringBuilder.ToString ();
		}


		public override void ExposeData ()
		{
			base.ExposeData ();
			//Scribe_Values.LookValue<Pawn> (ref owner, "owner", null);
			Scribe_References.LookReference<Pawn> (ref owner, "owner");
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


		public override void Notify_ReceivedThing(Thing newItem)
		{
			Log.Message (newItem.Label + " was added to wardrobe");
			Log.Message ("Allowd to accept: " + this.settings.AllowedToAccept (newItem));

			if (allowedTorsoShells.Contains(newItem.def))
			{
				// Save the item
				storedWrap = newItem;
			}

			if (allowedHats.Contains (newItem.def))
			{
				storedHat = newItem;
			}
				
			newItem.SetForbidden (true);

			settings.allowances.DisallowAll ();
//			settings.allowances.thingDefs.Clear ();
			if (!HaveHat())
			{
				Log.Message ("Allowing hats");
//				settings.allowances.thingDefs.AddAllInList (allowedHats);
				foreach (ThingDef hat in allowedHats)
					settings.allowances.SetAllow (hat, true);
			}

			if (!HaveWrap ())
			{
				Log.Message ("Allowing wraps");
//				settings.allowances.thingDefs.AddAllInList (allowedTorsoShells);
				foreach (ThingDef torsoShell in allowedTorsoShells)
					settings.allowances.SetAllow (torsoShell, true);
			}
		}


		public override void Notify_LostThing(Thing lostItem)
		{
			Log.Message (lostItem.Label + " was taken from wardrobe");



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
		/// </summary>
		/// <param name="tickerAmount">Ticker amount.</param>
		private void DoTickerWork (int tickerAmount)
		{
			counter += tickerAmount;
			// do this once a day only
			if (counter % dayTicks == 0) {
				// reset counter
				counter = 0;

				if (owner == null)
					return;

				//Season currentSeason = GenDate.CurrentSeason;
				int dayOfMonth = GenDate.DayOfMonth;
				//if (dayOfMonth % 2 == 0) {
				//Log.Message("Odd Day");

				IEnumerable<IntVec3> spots = this.AllSlotCells ();
				foreach (IntVec3 spot in spots)
				{
//					IEnumerable<Thing> things = Find.ListerThings.AllThings.Where (t => t.Position == this.Position);
					IEnumerable<Thing> things = Find.ListerThings.AllThings.Where (t => t.Position == spot);
					//Thing thing = null;
					foreach (Thing t in things)
					{
						Log.Message ("Processing " + t.Label);
//						if (t.def.category == EntityCategory.Item && t != this) {
//						if (t.def == ThingDef.Named ("Apparel_Parka") || t.def == ThingDef.Named("Apparel_Tuque"))
						if (allowedTorsoShells.Contains(t.def) || allowedHats.Contains(t.def)) 
						{
							foreach (Apparel apparel in owner.apparel.WornApparel)
							{
								if (apparel.def == ThingDef.Named ("Apparel_Jacket"))
								{
									Log.Message (owner.Nickname + " is wearing a jacket.");
								}
							}
							owner.jobs.StopAll ();
							owner.jobs.StartJob (new Job (JobDefOf.Wear, (Apparel)t));
//							owner.apparel.Wear ((Apparel)t);  //this method just makes it happen instantly
							break;
						} else
						{
							Log.Message ("Skipping " + t.Label);
						}
					}
				}
			}
		}

		// ======================== Private Methods ================

		private void AssignRoomOwner()
		{
			// Assign the wardrobe
			Room room = this.Position.GetRoomOrAdjacent();
			if (room != null)
			{
				Pawn roomOwner = room.RoomOwner;
				// owner might be null, which is a valid owner
				this.owner = roomOwner;
			} else
			{
				this.owner = null;
			}
		}

		/// <summary>
		/// Does the wardrobe hold a wrap (torso shell-layer apparel)?
		/// </summary>
		/// <returns><c>true</c>, if wrap was had, <c>false</c> otherwise.</returns>
		private bool HaveWrap()
		{
			return (storedWrap != null);
		}


		/// <summary>
		/// Does the wardrobe hold a hat?
		/// </summary>
		/// <returns><c>true</c>, if hat was had, <c>false</c> otherwise.</returns>
		private bool HaveHat()
		{
			return (storedHat != null);
		}


		/// <summary>
		/// Provisions the list of allowed apparel items into their respective lists
		/// based on our storage settings allowances (defined in the ThingDef XML)
		/// </summary>
		private void ProvisionAllowedLists()
		{
			StorageSettings mySettings = this.GetStoreSettings ();
			foreach (ThingDef thingDef in mySettings.allowances.thingDefs)
			{
				if (thingDef.apparel.CoversBodyPart (torsoParts))
				{
					allowedTorsoShells.Add (thingDef);
				} else if (thingDef.apparel.CoversBodyPart (headParts))
				{
					allowedHats.Add (thingDef);
				} else
				{
					Log.Warning ("Storage allowance has non-torso/hat thing: " + thingDef.label);
				}
			}
		}

		/// <summary>
		/// Performs the assign wardrobe action when assignOwnerButton is clicked
		/// </summary>
		private void PerformAssignWardrobeAction()
		{
			//Dialog_AssignWardrobeOwner (this);
			Log.Message ("Assign Owner Wardrobe");
		}
	} // class Building_Wardrobe


	// ============================= Assign Owner Dialog ======================

	// Broken assign owner dialog stuff here
	public class Dialog_AssignWardrobeOwner : Layer
	{
		public Building_Wardrobe wardrobe;
		public Vector2 scrollPosition;
		//
		// Constructors
		//
		public Dialog_AssignWardrobeOwner (Building_Wardrobe wardrobe)
		{
			this.wardrobe = wardrobe;
			base.SetCentered (620, 500);
			this.category = LayerCategory.GameDialog;
			this.closeOnEscapeKey = true;
			this.doCloseButton = true;
			this.doCloseX = true;
		}

		//
		// Methods
		//
		protected override void FillWindow (Rect inRect)
		{
			Text.Font = GameFont.Small;
			Rect outRect = new Rect (inRect);
			outRect.yMin += 20;
			outRect.yMax -= 40;
			Rect viewRect = new Rect (0, 0, inRect.width - 16, (float)Find.ListerPawns.FreeColonistsCount * 35 + 100);
			this.scrollPosition = Widgets.BeginScrollView (outRect, this.scrollPosition, viewRect);
			float num = 0;
			foreach (Pawn current in Find.ListerPawns.FreeColonists) {
				Rect rect = new Rect (0, num, (float)viewRect.width * (float)0.6, (float)32);
				Widgets.Label (rect, current.LabelCap);
				rect.x = rect.xMax;
				rect.width = (float)(viewRect.width * 0.4);
				if (Widgets.TextButton (rect, "WardrobeAssign".Translate ())) {
					//current.ownership.UnclaimBed ();
					//current.ownership.ClaimBed (this.wardrobe);
					this.wardrobe.owner = current;
					base.Close (true);
					return;
				}
				num += 35;
			}
			Widgets.EndScrollView ();
		}
	} // class Dialog_AssignWardrobeOwner
}
