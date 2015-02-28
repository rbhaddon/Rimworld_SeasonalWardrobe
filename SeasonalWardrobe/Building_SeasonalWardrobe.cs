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
	public class Building_SeasonalWardrobe : Building_HeadAndTorsoStorage
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

		private static List<Season> coldSeasons = new List<Season>();

		// These are used to determine if a ThingDef is suitable cold weather apparel
		const int HEAD_INSULATION_LIMIT = -10;
		const int TORSO_INSULATION_LIMIT = -30;

		// Wardrobe allowances are implemented as a finite state machine
		private FSM_Process fsm_process;

		// TODO
		private Season previousSeason = Season.Undefined;

		// JobDefs
		private const String JobDef_wearClothes = "WearClothesInWardrobe";

		// Testing helper because seasons last too long for good testing efficiency
		public Func<bool> SeasonHasChanged;
		bool TESTING_MODE = false;

		// Indicates if current season is cold.  Used to determine correct storage allowances and wear jobs.
		bool _seasonIsCold;
		public bool SeasonIsCold
		{
			get {
				if (TESTING_MODE)
				{
					_seasonIsCold = (GenDate.DayOfMonth % 2 == 0);
				} else
				{
					_seasonIsCold = coldSeasons.Contains (GenDate.CurrentSeason);
				}
				return _seasonIsCold;
			}
			set {
				_seasonIsCold = value;
			}
		}

		static Building_SeasonalWardrobe ()
		{
			// Build list of allowed apparel defs
			Building_SeasonalWardrobe.CreateApparelLists ();

			// Pick which seasons are "cold" seasons
			coldSeasons.Add (Season.Fall);
			coldSeasons.Add (Season.Winter);

//			Log.Message(String.Format("SeasonalTemp is {0}", GenTemperature.SeasonalTemp));
//			GenTemperature.AverageTemperatureAtWorldCoordsForMonth (worldCoords, Month);
		}


		public override void ExposeData ()
		{
			base.ExposeData ();
			Scribe_Values.LookValue<bool> (ref _seasonIsCold, "_seasonIsCold");
			Scribe_References.LookReference<Thing> (ref storedHead, "storedHat");
			Scribe_References.LookReference<Thing> (ref storedTorso, "storedWrap");
			Scribe_Values.LookValue<Season> (ref previousSeason, "previousSeason");
		}


		public override void SpawnSetup ()
		{
			// Set the SeasonHasChanged func to normal mode or testing
			if (TESTING_MODE)
			{
				SeasonHasChanged = Test_SeasonHasChanged;
			} else
			{
				SeasonHasChanged = Normal_SeasonHasChanged;
			}

			// Set the current season
			SeasonHasChanged ();

			base.SpawnSetup ();
		}


		public override void DeSpawn ()
		{
			base.DeSpawn ();
			if (HaveHat ())
				storedHead.SetForbidden (false);
			if (HaveWrap ())
				storedTorso.SetForbidden (false);
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
			if (SeasonIsCold)
			{
				stringBuilder.Append ("Requesting: Warm weather apparel");
			} else
			{
				stringBuilder.Append ("Requesting: Cold weather apparel");
			}

			return stringBuilder.ToString ();
		}


		public override IEnumerable<Gizmo> GetGizmos()
		{
			IList<Gizmo> gizmoList = new List<Gizmo> ();
			const int groupKeyBase = 12091967;

			if (TESTING_MODE)
			{
				Command_Action debugColdButton = new Command_Action ();
				debugColdButton.defaultDesc = "Spawn cold weather clothing in wardrobe.";
				debugColdButton.defaultLabel = "Debug Cold";
				debugColdButton.activateSound = SoundDef.Named ("Click");
				debugColdButton.action = new Action (Debug_SpawnColdSeasonApparel);
				debugColdButton.groupKey = groupKeyBase + 4;
				gizmoList.Add (debugColdButton);

				Command_Action debugWarmButton = new Command_Action ();
				debugWarmButton.defaultDesc = "Spawn warm weather clothing in wardrobe.";
				debugWarmButton.defaultLabel = "Debug Warm";
				debugWarmButton.activateSound = SoundDef.Named ("Click");
				debugWarmButton.action = new Action (Debug_SpawnWarmSeasonApparel);
				debugWarmButton.groupKey = groupKeyBase + 5;
				gizmoList.Add (debugWarmButton);
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
		public override void DoTickerWork (int tickerAmount)
		{
			counter += tickerAmount;

			// Check once per day if season has changed and issue jobs if so
			if (counter >= dayTicks)
			{
				counter = 0;
				if (SeasonHasChanged ())
				{
//					Log.Warning ("Seasons changed; it is now cold: " + SeasonIsCold);
					InspectStateMachine ();

					if (HaveWrap () || HaveHat ())
					{	
						if (owner != null)
						{
//							Log.Message (string.Format ("[{0}] Issuing wear job.", owner.Nickname));
							IssueWearJob ();
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
			var jobWear = new Job (DefDatabase<JobDef>.GetNamed (JobDef_wearClothes), this);
			owner.playerController.TakeOrderedJob (jobWear);
		}


		/// <summary>
		/// Checks if the current season has changed since the last check.
		/// </summary>
		/// <returns><c>true</c>, if has changed was seasoned, <c>false</c> otherwise.</returns>
		bool Normal_SeasonHasChanged()
		{
			// TODO this method is poorly named.  It's not so much a season change indicator as it is a
			// bi-annual cold to warm weather season checkinator.

			bool retval;
			var currentSeason = GenDate.CurrentSeason;

			retval = coldSeasons.Contains (previousSeason) && !coldSeasons.Contains (currentSeason);

			if (currentSeason != previousSeason)
			{
				previousSeason = currentSeason;
			}
				
			return retval;
		}


		/// <summary>
		/// Checks if the current season has changed since the last check.  Testing version.
		/// </summary>
		/// <returns><c>true</c>, on even numbered days <c>false</c> otherwise.</returns>
		bool Test_SeasonHasChanged()
		{
			Season currentSeason = GenDate.CurrentSeason;

			if (currentSeason != previousSeason)
			{
				// this is some hacky shit, just for testing
				if (SeasonIsCold)
				{
					if (currentSeason == Season.Winter)
						previousSeason = Season.Fall;
					else
						previousSeason = Season.Summer;
				} else
				{
					if (currentSeason == Season.Summer)
						previousSeason = Season.Spring;
					else
						previousSeason = Season.Winter;
				}
				return true;
			}
			return false;
		}
			

		/// <summary>
		/// Shoulds the wear job be issued.
		/// </summary>
		/// <returns><c>true</c>, if wear job be issued was shoulded, <c>false</c> otherwise.</returns>
		/// <param name="storedApparel">Apparel thing.</param>
		public bool ShouldWearJobBeIssued(Thing storedApparel)
		{
			if (IsCorrectSeasonForApparel (storedApparel))
			{
				return true;
			}

			// Make additional check for owner.  If they are naked on head and torso shell, maybe issue job anyw
			bool alreadyWorn = false;

			foreach (Apparel wornApparel in owner.apparel.WornApparel)
			{
				if (IsOverHead(wornApparel.def) || IsTorsoShell(wornApparel.def))
				{
//					Log.Message (String.Format("{0} is already wearing a similar item to {1}.", owner.Nickname, storedApparel.Label));
					alreadyWorn = true;
				}
			}
			return !alreadyWorn;
		}


		public bool ShouldWornApparelBeRemoved(Apparel clothing)
		{
			return (coldSeasonAll.Contains (clothing.def) && !SeasonIsCold);
		}
			

		/// <summary>
		/// Determines whether the given apparel item is appropriate to wear in the current season
		/// </summary>
		/// <returns><c>true</c> if this instance is correct season for apparel the specified apparelThing; otherwise, <c>false</c>.</returns>
		/// <param name="apparelThing">Apparel thing.</param>
		public bool IsCorrectSeasonForApparel(Thing apparelThing)
		{
			bool retval;
			if (SeasonIsCold)
			{
				retval = coldSeasonAll.Contains (apparelThing.def);
			} else
			{
				retval = warmSeasonAll.Contains (apparelThing.def);
			}
			return retval;
		}

		/// <summary>
		/// Implements the state machine that determines what this wardrobe is allowed to store
		/// When the season is cold, we store warm clothes and vice-versa.
		/// When a hat has been added, we disallow additional hats and vice-versa with wraps.
		/// </summary>
		public override void InspectStateMachine()
		{
//			if (owner != null)
//			{
//				Log.Message (String.Format ("[{0}] Current state: {1}", owner.Nickname, fsm_process.CurrentState));
//			} else
//			{
//				Log.Message ("[Unowned] Current state: " + fsm_process.CurrentState);
//			}
			
			var allowedDefList = new List<ThingDef> ();
			List<ThingDef> hats;
			List<ThingDef> wraps;

			if (SeasonIsCold)
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

			switch (fsm_process.CurrentState)
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
			default:
				Log.Error ("Invalid AllowanceState in Building_SeasonalWardrobe.process.");
				break;
			}
		}


		/// <summary>
		/// Conditionally unforbids stored apparel in the wardrobe 
		/// </summary>
		/// <param name="force">If set to <c>true</c> forces the unforbiding.</param>
		public override void UnforbidClothing(bool force=false)
		{
			if (force)
			{
				if (HaveHat ())
				{
					storedHead.SetForbidden (false);
				}
				if (HaveWrap ())
				{
					storedTorso.SetForbidden (false);
				}
				return;
			}

			// Conditionally unforbid clothing as long as the stored clothing should not be stored in the current season
			List<ThingDef> currentSeasonApparel;

			if (SeasonIsCold)
			{
				currentSeasonApparel = Building_SeasonalWardrobe.coldSeasonAll;
			} else
			{
				currentSeasonApparel = Building_SeasonalWardrobe.warmSeasonAll;
			}

			if (HaveHat())
			{
				if (currentSeasonApparel.Contains (storedHead.def))
				{
					storedHead.SetForbidden (false);
					fsm_process.MoveNext (Command.AddHat);
				}
			}
			if (HaveWrap())
			{
				if (currentSeasonApparel.Contains (storedTorso.def))
				{
					storedTorso.SetForbidden (false);
					fsm_process.MoveNext (Command.AddWrap);
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
//			Log.Message (String.Format ("coldSeasonHats contains {0} ThingDefs", Building_SeasonalWardrobe.coldSeasonHats.Count));
//			Log.Message (String.Format ("coldSeasonWraps contains {0} ThingDefs", Building_SeasonalWardrobe.coldSeasonWraps.Count));
//			Log.Message (String.Format ("warmSeasonHats contains {0} ThingDefs", Building_SeasonalWardrobe.warmSeasonHats.Count));
//			Log.Message (String.Format ("warmSeasonWraps contains {0} ThingDefs", Building_SeasonalWardrobe.warmSeasonWraps.Count));
		}
			

		/// <summary>
		/// Spawns the cold season apparel.
		/// </summary>
		void Debug_SpawnColdSeasonApparel()
		{
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
			var thingDefs = new List<ThingDef> ();
			thingDefs.Add(ThingDef.Named("Apparel_Jacket"));
			thingDefs.Add(ThingDef.Named("Apparel_CowboyHat"));
			ThingDef stuffDef = ThingDef.Named("Cloth");

			Debug_SpawnApparel (thingDefs, stuffDef);
		}


	} // class Building_Wardrobe
}