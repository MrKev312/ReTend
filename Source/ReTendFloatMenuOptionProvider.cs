#if !V1_2 && !V1_3 && !V1_4 && !V1_5
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ReTend;

public class ReTendFloatMenuOptionProvider : FloatMenuOptionProvider
{
	// These properties determine when this provider is active.
	// "ReTend" should be available for both drafted and undrafted pawns.
	protected override bool Drafted => true;
    protected override bool Undrafted => true;
    protected override bool Multiselect => false;

	/// <summary>
	/// This method is called for each pawn under the cursor to see what float menu options are available.
	/// It returns a collection of FloatMenuOption objects.
	/// </summary>
	public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
	{
		// Correctly get the pawn performing the action.
		Pawn doctor = context.FirstSelectedPawn;

		// If for some reason no pawn is selected, no options can be provided.
		if (doctor == null)
		{
			yield break;
		}

		Pawn patient = clickedPawn; // The pawn being targeted by the action.

		// --- Validation Checks ---
		// Ported from the original Harmony patch logic.

		// 1. Check if the acting pawn can perform doctor work.
		if (doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
			yield break; // Exit without providing an option.

		// 2. Check if re-tending is available.
		if (!ReTendFunctions.ReTendAvailable(patient))
			yield break; // Exit without providing an option.

		// 3. Handle self-tending restrictions.
		if (doctor == patient && (doctor.playerSettings == null || !doctor.playerSettings.selfTend))
		{
			yield return new FloatMenuOption("Cannot ReTend" + ": " + "SelfTendDisabled".Translate().CapitalizeFirst(), null);
			yield break;
		}

		// 4. Check if the target is in a hostile mental state.
		if (patient.InAggroMentalState && !patient.health.hediffSet.HasHediff(HediffDefOf.Scaria))
		{
			yield return new FloatMenuOption("Cannot ReTend" + ": " + "PawnIsInMentalState".Translate(patient).CapitalizeFirst(), null);
			yield break;
		}

		// 5. Check for a valid path.
		if (!doctor.CanReach(patient, PathEndMode.ClosestTouch, Danger.Deadly))
		{
			yield return new FloatMenuOption("Cannot ReTend" + ": " + "NoPath".Translate().CapitalizeFirst(), null);
			yield break;
		}

		// --- Option Creation ---
		// If all checks pass, create the clickable "ReTend" option.
		Thing medicine = ReTendFunctions.FindBestMedicineToReTend(doctor, patient);

		void Action()
		{
			// If the setting for a quality prompt is enabled, show the slider dialog.
			if (ReTendSettings.qualityprompt)
			{
				var window = new Dialog_Slider(
					textGetter: x => "Minimum Quality: {0}%".Formatted(x),
					from: 0,
					to: 100,
					startingValue: Mathf.RoundToInt(100f * ReTendSettings.minquality),
					confirmAction: value =>
					{
						// This code runs when the player confirms the dialog.
						ReTendSettings.minquality = value / 100f;
						Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("ReTendPatient"), patient, medicine);
						doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
					}
				);
				Find.WindowStack.Add(window);
			}
			else // Otherwise, assign the job immediately.
			{
				Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("ReTendPatient"), patient, medicine);
				doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}
		}

		string label = $"{"ReTend {0}".Formatted(patient.LabelCap)}{(medicine != null ? "" : $" ({"WithoutMedicine".Translate()})")}";

		// Return the final, clickable float menu option using the correct constructor.
		yield return new FloatMenuOption(
			label: label,
			action: Action,
			priority: MenuOptionPriority.Default,
			mouseoverGuiAction: null,
			revalidateClickTarget: patient
		);
	}
}
#endif
