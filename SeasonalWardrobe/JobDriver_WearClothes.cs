using System;
using System.Collections.Generic;

using RimWorld;
using Verse;
using Verse.AI;

namespace SeasonalWardrobe
{
	/// <summary>
	/// Seasonal wardrobe job giver wear clothes.
	/// </summary>
	public class JobDriver_WearClothes: JobDriver
	{
		// Constants
		private const TargetIndex WardrobeIdx = TargetIndex.A;
		private Thing storedHat = null;
		private Thing storedWrap = null;
		private Building_SeasonalWardrobe wardrobe;

		public JobDriver_WearClothes(Pawn pawn) : base(pawn)
		{
			wardrobe = (Building_SeasonalWardrobe)TargetThingA;
			storedHat = wardrobe.storedHat;
			storedWrap = wardrobe.storedWrap;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			Log.Message (String.Format ("{0} received WearClothes job.", pawn));

			// Set fail conditions
			this.FailOnBurningImmobile (WardrobeIdx);

			// Toil: Goto Wardrobe
			Toil toilGoto = null;
			toilGoto = Toils_Goto.GotoThing (WardrobeIdx, PathMode.ClosestTouch);
			yield return toilGoto;

			// Toil: Wear clothes -- but only if they are correct for the current season
			var clothes = new List<Thing>();
			clothes.Add (storedHat);
			clothes.Add (storedWrap);

			foreach (Thing clothing in clothes)
			{
				if (clothing != null) // TODO this check is probably redundant with the next check
				{
					if (wardrobe.IsCorrectSeasonForApparel(clothing))
					{
						yield return Toils_WearThing (pawn, clothing);
					}
				}

			}
		}

		private Toil Toils_WearThing(Pawn owner, Thing clothing)
		{
			Toil toil = new Toil ();

			toil.initAction = () => {
				toil.actor.pather.StopDead ();
				toil.actor.apparel.Wear((Apparel)clothing, true);
			};

			toil.defaultCompleteMode = ToilCompleteMode.Instant;

			return toil;
		}
	}
}