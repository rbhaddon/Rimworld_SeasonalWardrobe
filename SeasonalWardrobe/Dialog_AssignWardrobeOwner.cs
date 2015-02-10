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
	// Broken assign owner dialog stuff here
	public class Dialog_AssignWardrobeOwner : Layer
	{
		public Building_SeasonalWardrobe wardrobe;
		public Vector2 scrollPosition;
		//
		// Constructors
		//
		public Dialog_AssignWardrobeOwner (Building_SeasonalWardrobe wardrobe)
		{
			this.wardrobe = wardrobe;
			SetCentered (620, 500);
			category = LayerCategory.GameDialog;
			closeOnEscapeKey = true;
			doCloseButton = true;
			doCloseX = true;
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
			scrollPosition = Widgets.BeginScrollView (outRect, scrollPosition, viewRect);
			float num = 0;
			foreach (Pawn current in Find.ListerPawns.FreeColonists) {
				Rect rect = new Rect (0, num, (float)viewRect.width * (float)0.6, (float)32);
				Widgets.Label (rect, current.LabelCap);
				rect.x = rect.xMax;
				rect.width = (float)(viewRect.width * 0.4);
				if (Widgets.TextButton (rect, "WardrobeAssign".Translate ())) {
					//current.ownership.UnclaimBed ();
					//current.ownership.ClaimBed (this.wardrobe);
					wardrobe.owner = current;
					Close (true);
					return;
				}
				num += 35;
			}
			Widgets.EndScrollView ();
		}
	} // class Dialog_AssignWardrobeOwner
}