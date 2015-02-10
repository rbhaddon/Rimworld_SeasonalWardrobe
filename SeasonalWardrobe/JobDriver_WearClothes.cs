using System;
using System.Collections.Generic;
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
		private const Thing storedHat = null;
		private const Thing storedWrap = null;


		public JobDriver_WearClothes(Pawn pawn) : base(pawn)
		{ }

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// Set fail conditions
			this.FailOnBurningImmobile (WardrobeIdx);
//			this.FailOn (() => storedHat == null);
//			this.FailOn (() => storedWrap == null);

			// Toil: Goto Wardrobe
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

			Job wearClothing = new Job (JobDefOf.Wear, (Apparel)clothing);

			toil.initAction = () => {
				toil.actor.pather.StopDead ();
				toil.actor.jobs.StartJob (wearClothing);
			};

			toil.defaultCompleteMode = ToilCompleteMode.Instant;

			return toil;
		}

//		private bool HaveClothes(Thing hat, Thing wrap)
//		{
//			return (hat != null && wrap != null);
//		}
	}
}