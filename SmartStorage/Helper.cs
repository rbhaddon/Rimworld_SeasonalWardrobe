//
// Common utility functions for SmartStorage
//
using System;
using System.Collections.Generic;
using System.Linq;

using RimWorld;
using Verse;
using Verse.AI;

namespace SmartStorage
{
	static class Helper
	{
		/// <summary>
		/// Finds the wardrobe owned by pawn.
		/// </summary>
		/// <returns>The wardrobe owned by pawn.</returns>
		/// <param name="pawn">Pawn.</param>
		public static Building_SeasonalWardrobe FindWardrobeOwnedByPawn(Pawn pawn)
		{
//			IEnumerable<Thing> things = Find.ListerThings.AllThings.Where (t => t.Position == spot);
			IEnumerable<Building_SeasonalWardrobe> wardrobes = (IEnumerable<Building_SeasonalWardrobe>)Find.ListerBuildings.allBuildingsColonist.Where
					(b => b.def.defName == "Building_SeasonalWardrobe");
			foreach (Building_SeasonalWardrobe wardrobe in wardrobes)
			{
				if (wardrobe.owner == pawn)
					return wardrobe;
			}

			Log.Error (String.Format ("Failed to find wardrobe owned by {0}", pawn.Nickname));
			return null;
		}


		/// <summary>
		/// Gets the matching worn apparel.
		/// </summary>
		/// <returns>The matching worn apparel.</returns>
		/// <param name="owner">Owner.</param>
		/// <param name="Checker">Checker.</param>
		public static Apparel GetMatchingWornApparel(Pawn owner, Func<ThingDef, bool> Checker)
		{
			Apparel clothing = null;

			foreach (Apparel apparel in owner.apparel.WornApparel)
			{
				if (Checker (apparel.def))
				{	
//					Log.Message (owner.Nickname + " is wearing " + apparel.Label);
					// Pawn is currently wearing an item of same ThingDef
					clothing = apparel;
					break;
				}
			}
			return clothing;
		}


		/// <summary>
		/// Returns List of clothing from previousApparel that is not current being worn.
		/// </summary>
		/// <returns>The clothing.</returns>
		/// <param name="pawn">Pawn.</param>
		/// <param name="previousApparel">Previous apparel.</param>
		public static List<Thing> DroppedClothing(Pawn pawn, List<Thing> previousApparel)
		{
			IEnumerable<Thing> droppedClothing = previousApparel.Except (pawn.apparel.WornApparel);
			return droppedClothing.ToList();
		}


		/// <summary>
		/// Magically teleports the first building.NUM_SLOTS items from things into storage
		/// </summary>
		/// <param name="storage">Storage building.</param>
		/// <param name="things">Things to put into storage.</param>
		public static void AddThingsToStorage(Building_HeadAndTorsoStorage storage, List<Thing> things)
		{
			List<IntVec3> cells = storage.AllSlotCellsList ();
			for (int i = 0; i < storage.NUM_SLOTS; i++)
			{
				Thing thing = things [i];
				Log.Message (String.Format ("Adding {0} to {1}", thing, storage));
				thing.Position = cells[i];
				storage.Notify_ReceivedThing (things[i]);
			}
		}
	}
}