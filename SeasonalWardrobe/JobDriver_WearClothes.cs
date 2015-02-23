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
//			Log.Message (String.Format ("{0} received WearClothes job.", pawn));

			// Set fail conditions
			this.FailOnBurningImmobile (WardrobeIdx);

			// Toil: Goto Wardrobe
			Toil toilGoto = null;
			toilGoto = Toils_Goto.GotoThing (WardrobeIdx, PathMode.ClosestTouch);
			yield return toilGoto;

			// Toil: Wear clothes -- but only if they are correct for the current season

			// key can't be null; makes dictionary poor choice
//			Dictionary<Thing, Func<ThingDef, bool>> dictionary = new Dictionary<Thing, Func<ThingDef, bool>> ()
//			{
//				{storedHat, Building_SeasonalWardrobe.IsOverHead},
//				{storedWrap, Building_SeasonalWardrobe.IsTorsoShell}
//			};

			var clothing = new List<Thing> ();
			var checkers = new List<Func<ThingDef, bool>> ();
			clothing.Add (storedHat);
			checkers.Add (Building_SeasonalWardrobe.IsOverHead);
			clothing.Add (storedWrap);
			checkers.Add (Building_SeasonalWardrobe.IsTorsoShell);

			for (int idx = 0; idx < clothing.Count; idx++)
			{
				if (clothing[idx] != null)
				{
					// Put on stored wrap if the season is right for it
					if (wardrobe.ShouldWearJobBeIssued (clothing[idx]))
					{
						yield return Toils_WearApparel (pawn, clothing[idx]);
					}
				} else
				{
					// Nothing to put on, but does pawn have something to take off (i.e. warm clothing in warm season)?
					Apparel wornApparel = Helper.GetMatchingWornApparel (pawn, checkers[idx]);
					if (wornApparel != null)
					{
						if (wardrobe.ShouldWornApparelBeRemoved (wornApparel))
						{
//							Log.Message (String.Format ("{0} should remove {1}", pawn.Nickname, wornApparel.Label));
							yield return Toils_RemoveApparel (pawn, wornApparel);
						}
					}
				}
//				if (storedHat != null)
//				{
//					// Put on stored wrap if the season is right for it
//					if (wardrobe.ShouldWearJobBeIssued (storedHat))
//					{
//						yield return Toils_WearApparel (pawn, storedHat);
//					}
//				} else
//				{
//					// Nothing to put on, but does pawn have something to take off (i.e. warm clothing in warm season)?
//					Apparel wornHat = Helper.GetMatchingWornApparel (pawn, Building_SeasonalWardrobe.IsOverHead);
//					if (wornHat != null)
//					{
//						if (wardrobe.ShouldWornApparelBeRemoved (wornHat))
//						{
//							Log.Message (String.Format ("{0} should remove {1}", pawn.Nickname, wornHat.Label));
//							yield return Toils_RemoveApparel (pawn, wornHat);
//						}
//					}
//				}
			}
		}


//			foreach (Thing clothing in clothes)
//			{
//				if (clothing != null)
//				{
//					if (wardrobe.ShouldWearJobBeIssued (clothing))
//					{
//						yield return Toils_WearApparel (pawn, clothing);
//					}
//				}
//				else
//				{
//					if (wardrobe.ShouldWornApparelBeRemoved (clothing.def))
//					{
//						yield return Toils_RemoveApparel (pawn, Building_SeasonalWardrobe.IsTorsoShell());
//					}
//				}
//			}


		private Toil Toils_WearApparel(Pawn owner, Thing clothing)
		{
			Toil toil = new Toil ();

			toil.initAction = () => {
				toil.actor.pather.StopDead ();
				toil.actor.apparel.Wear((Apparel)clothing, true);
			};

			toil.defaultCompleteMode = ToilCompleteMode.Instant;

			return toil;
		}


		private Toil Toils_RemoveApparel(Pawn owner, Apparel clothing)
		{
			Toil toil = new Toil ();
	
			Apparel droppedClothing;

			toil.initAction = () => {
				toil.actor.pather.StopDead ();
				toil.actor.apparel.TryDrop(clothing, out(droppedClothing), wardrobe.Position, false);
			};

			toil.defaultCompleteMode = ToilCompleteMode.Instant;
			return toil;
		}
	}
}