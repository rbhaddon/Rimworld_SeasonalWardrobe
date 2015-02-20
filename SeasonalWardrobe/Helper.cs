using System;
using System.Collections.Generic;
using System.Linq;

using Verse;

namespace SeasonalWardrobe
{
	class Helper
	{
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

	}
}