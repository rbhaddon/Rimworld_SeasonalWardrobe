using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
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
			Log.Warning (String.Format ("TargetThingA is {0}", TargetThingA.Label));
			wardrobe = (Building_SeasonalWardrobe)TargetThingA;
			storedHat = wardrobe.storedHat;
			storedWrap = wardrobe.storedWrap;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// Set fail conditions
			this.FailOnBurningImmobile (WardrobeIdx);
//			this.FailOn (() => storedHat == null);
//			this.FailOn (() => storedWrap == null);

			// Toil: Goto Wardrobe
			Log.Message (String.Format ("{0} is going to wardrobe.", pawn));
			Toil toilGoto = null;
			toilGoto = Toils_Goto.GotoThing (WardrobeIdx, PathMode.ClosestTouch);
			yield return toilGoto;

			// Toil: Wear wrap
			if (storedWrap != null)
				yield return Toils_WearThing (pawn, storedWrap);

			// Toil: Wear hat
			if (storedHat != null)
				yield return Toils_WearThing (pawn, storedHat);

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