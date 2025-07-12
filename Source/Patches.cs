#if v1_2 || v1_3 || v1_4 || v1_5
using HarmonyLib;

using RimWorld;

using UnityEngine;

using Verse;
using Verse.AI;

namespace ReTend;

[StaticConstructorOnStartup]
public static class Patches
{
	static Patches()
	{
		Harmony harmony = new("temmie3754.retend.1");
		harmony.Patch(AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders", null, null), null, new HarmonyMethod(patchType, "Patch_FloatMenuMakerMap", null), null, null);
	}

	public static void Patch_FloatMenuMakerMap(ref Vector3 clickPos, ref Pawn pawn, ref List<FloatMenuOption> opts)
	{
		TargetingParameters tendParams = new()
		{
			canTargetPawns = true,
			canTargetBuildings = false,
			mapObjectTargetsMustBeAutoAttackable = false,
			validator = t =>
			{
				if (!t.HasThing)
					return false;
				if (t.Thing is not Pawn patient)
					return false;
				if (patient.Downed)
					return true;

				return HealthAIUtility.ShouldBeTendedNowByPlayer(patient);
			}
		};

		foreach (LocalTargetInfo item in GenUI.TargetsAt(clickPos, tendParams, thingsOnly: true))
		{
			Pawn pawnRef = pawn;
			LocalTargetInfo target = item;
			Pawn targetPawn = (Pawn)target.Thing;
			if (!pawnRef.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && ReTendFunctions.ReTendAvailable(targetPawn))
			{
				if (!pawnRef.CanReach(targetPawn, PathEndMode.ClosestTouch, Danger.Deadly))
				{
					opts.Add(new FloatMenuOption("Cannot ReTend" + ": " + "NoPath".Translate().CapitalizeFirst(), null));
					continue;
				}

				if (targetPawn == pawnRef && pawn.playerSettings != null && !pawnRef.playerSettings.selfTend)
				{
					opts.Add(new FloatMenuOption("Cannot ReTend" + ": " + "SelfTendDisabled".Translate().CapitalizeFirst(), null));
					continue;
				}
				else if (targetPawn.InAggroMentalState && !targetPawn.health.hediffSet.HasHediff(HediffDefOf.Scaria))
				{
					opts.Add(new FloatMenuOption("Cannot ReTend" + ": " + "PawnIsInMentalState".Translate(targetPawn).CapitalizeFirst(), null));
					continue;
				}

				Thing medicine = ReTendFunctions.FindBestMedicineToReTend(pawnRef, targetPawn);
				opts.Add(new FloatMenuOption("ReTend {0}".Formatted(targetPawn) + (medicine != null ? "" : " (" + "WithoutMedicine".Translate().ToString() + ")"), Action, MenuOptionPriority.Default, null, targetPawn));
				void Action()
				{
					if (ReTendSettings.qualityprompt)
					{
						Dialog_Slider window = new(
							textGetter: x => "Minimum Quality: {0}%".Formatted(x),
							from: 0,
							to: 100,
							startingValue: Mathf.RoundToInt(100f * ReTendSettings.minquality),
							confirmAction: value =>
							{
								ReTendSettings.minquality = value / 100f;
								Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("ReTendPatient"), targetPawn, medicine);
								pawnRef.jobs.TryTakeOrderedJob(job, JobTag.Misc);
							}
						);
						Find.WindowStack.Add(window);
					}

					{
						Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("ReTendPatient"), targetPawn, medicine);
						pawnRef.jobs.TryTakeOrderedJob(job, JobTag.Misc);
					}
				}
			}
		}
	}

	private static readonly Type patchType = typeof(Patches);
}
#endif