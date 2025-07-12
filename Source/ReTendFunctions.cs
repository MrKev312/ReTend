using RimWorld;

using UnityEngine;

using Verse;
using Verse.AI;
using Verse.Sound;

namespace ReTend;

public static class ReTendFunctions
{
	public static void DoReTend(Pawn doctor, Pawn patient, Medicine medicine)
	{
		if (medicine != null && medicine.Destroyed)
		{
			Log.Warning("Tried to use destroyed medicine.");
			medicine = null;
		}

		float quality = TendUtility.CalculateBaseTendQuality(doctor, patient, medicine?.def);
		List<Hediff> hediffs = [];
		GetOptimalHediffsToReTendWithSingleTreatment(patient, medicine != null, hediffs);
		float maxQuality = medicine?.def.GetStatValueAbstract(StatDefOf.MedicalQualityMax) ?? 0.7f;
		HediffComp_TendDuration hedifftotend;
		for (int i = 0; i < hediffs.Count; i++)
		{
			hedifftotend = hediffs[i].TryGetComp<HediffComp_TendDuration>();
			quality = Mathf.Clamp(quality + Rand.Range(-0.25f, 0.25f), 0f, maxQuality);
			if (hedifftotend.tendQuality < quality)
			{
				System.Reflection.FieldInfo totalTendQuality = hedifftotend.GetType().GetField("totalTendQuality", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				totalTendQuality.SetValue(hedifftotend, (float)totalTendQuality.GetValue(hedifftotend) + (quality - hedifftotend.tendQuality));
				hedifftotend.tendQuality = quality;
				hedifftotend.tendTicksLeft = hedifftotend.TProps.TendIsPermanent
					? 1 :
					Mathf.Max(0, hedifftotend.tendTicksLeft) + hedifftotend.TProps.TendTicksFull;

				if (hedifftotend.Pawn.Spawned)
				{
					string text = "TextMote_Tended".Translate(hedifftotend.parent.Label).CapitalizeFirst() + "\n" + "Quality".Translate() + " " + hedifftotend.tendQuality.ToStringPercent();
					MoteMaker.ThrowText(hedifftotend.Pawn.DrawPos, hedifftotend.Pawn.Map, text, Color.green, 3.65f);
				}

				hedifftotend.Pawn.health.Notify_HediffChanged(hedifftotend.parent);
			}
			else if (hedifftotend.Pawn.Spawned)
			{
				string text = "TextMote_Tended".Translate(hedifftotend.parent.Label).CapitalizeFirst() + "\n" + "Quality".Translate() + " " + quality.ToStringPercent();
				MoteMaker.ThrowText(hedifftotend.Pawn.DrawPos, hedifftotend.Pawn.Map, text, Color.red, 3.65f);
			}
		}

		if (doctor != null && doctor.Faction == Faction.OfPlayer && patient.Faction != doctor.Faction && !patient.IsPrisoner && patient.Faction != null)
		{
			patient.mindState.timesGuestTendedToByPlayer++;
		}

		if (doctor != null && doctor.RaceProps.Humanlike && patient.RaceProps.Animal
#if !V1_2
			&& patient.RaceProps.playerCanChangeMaster
#endif
			&& RelationsUtility.TryDevelopBondRelation(doctor, patient, 0.004f) && doctor.Faction != null && doctor.Faction != patient.Faction)
		{
#if V1_2
			InteractionWorker_RecruitAttempt.DoRecruit(doctor, patient, 1, useAudiovisualEffects: false);
#else
			InteractionWorker_RecruitAttempt.DoRecruit(doctor, patient, useAudiovisualEffects: false);
#endif
		}

		patient.records.Increment(RecordDefOf.TimesTendedTo);
		doctor?.records.Increment(RecordDefOf.TimesTendedOther);
		if (doctor == patient && !doctor.Dead)
		{
			doctor.mindState.Notify_SelfTended();
		}

		if (medicine != null)
		{
			if ((patient.Spawned || (doctor != null && doctor.Spawned)) && medicine != null && medicine.GetStatValue(StatDefOf.MedicalPotency) > ThingDefOf.MedicineIndustrial.GetStatValueAbstract(StatDefOf.MedicalPotency))
			{
				SoundDefOf.TechMedicineUsed.PlayOneShot(new TargetInfo(patient.Position, patient.Map));
			}

			if (medicine.stackCount > 1)
			{
				medicine.stackCount--;
			}
			else if (!medicine.Destroyed)
			{
				medicine.Destroy();
			}
		}

#if !V1_2
		if (ModsConfig.IdeologyActive && doctor != null && doctor.Ideo != null)
		{
			Precept_Role role = doctor.Ideo.GetRole(doctor);
			if (role != null && role.def.roleEffects != null)
			{
				foreach (RoleEffect roleEffect in role.def.roleEffects)
				{
					roleEffect.Notify_Tended(doctor, patient);
				}
			}
		}
#endif
	}

	public static bool ReTendAvailable(Pawn patient)
	{
		List<Hediff> hediffs = patient.health.hediffSet.hediffs;
		for (int i = 0; i < hediffs.Count; i++)
		{
			if (hediffs[i].IsTended() && ((ReTendSettings.diseases && hediffs[i].def.PossibleToDevelopImmunityNaturally()) || hediffs[i] is Hediff_Injury) && !hediffs[i].IsPermanent())
			{
				HediffComp_TendDuration tendDuration = hediffs[i].TryGetComp<HediffComp_TendDuration>();
				if (tendDuration != null)
				{
					if (hediffs[i] is Hediff_Injury)
					{
						if (tendDuration.tendQuality < ReTendSettings.minquality)
							return true;
					}
					else if (tendDuration.tendQuality < ReTendSettings.diseasequality)
						return true;
				}
			}
		}

		return false;
	}

	public static Toil FinalizeReTend(Pawn patient)
	{
#if V1_2 || V1_3
		Toil toil = new();
#else
			Toil toil = ToilMaker.MakeToil("FinalizeReTend");
#endif
		toil.initAction = delegate
		{
			Pawn actor = toil.actor;
			Medicine medicine = (Medicine)actor.CurJob.targetB.Thing;
			if (actor.skills != null && medicine != null)
			{
				float num = patient.RaceProps.Animal ? 175f : 500f;
				float num2 = medicine?.def.MedicineTendXpGainFactor ?? 0.5f;
				actor.skills.Learn(SkillDefOf.Medicine, num * num2);
			}

			DoReTend(actor, patient, medicine);
			if (medicine != null && medicine.Destroyed)
				actor.CurJob.SetTarget(TargetIndex.B, LocalTargetInfo.Invalid);

			if (toil.actor.CurJob.endAfterTendedOnce)
				actor.jobs.EndCurrentJob(JobCondition.Succeeded);
		};
		toil.defaultCompleteMode = ToilCompleteMode.Instant;
		return toil;
	}

	public static void GetOptimalHediffsToReTendWithSingleTreatment(Pawn patient, bool usingMedicine, List<Hediff> outHediffsToTend)
	{
		outHediffsToTend.Clear();
		List<Hediff> tmpHediffs = [];
		{
			List<Hediff> hediffs = patient.health.hediffSet.hediffs;
			for (int i = 0; i < hediffs.Count; i++)
			{
				if (hediffs[i].IsTended() && ((ReTendSettings.diseases && hediffs[i].def.PossibleToDevelopImmunityNaturally()) || hediffs[i] is Hediff_Injury) && !hediffs[i].IsPermanent())
				{
					HediffComp_TendDuration tendDuration = hediffs[i].TryGetComp<HediffComp_TendDuration>();
					if (tendDuration != null)
					{
						if (hediffs[i] is Hediff_Injury)
						{
							if (tendDuration.tendQuality < ReTendSettings.minquality)
								tmpHediffs.Add(hediffs[i]);
						}
						else if (tendDuration.tendQuality < ReTendSettings.diseasequality)
							tmpHediffs.Add(hediffs[i]);
					}
				}
			}

			tmpHediffs = ReTendSettings.sortmethod
				? [.. tmpHediffs.OrderBy(h => h.Severity).Reverse()]
				: [.. tmpHediffs.OrderBy(h => h.TryGetComp<HediffComp_TendDuration>().tendQuality)];
		}

		if (!tmpHediffs.Any())
		{
			return;
		}

		Hediff hediff = tmpHediffs[0];
		outHediffsToTend.Add(hediff);
		HediffCompProperties_TendDuration hediffCompProperties_TendDuration = hediff.def.CompProps<HediffCompProperties_TendDuration>();
		if (hediffCompProperties_TendDuration != null && hediffCompProperties_TendDuration.tendAllAtOnce)
		{
			for (int j = 0; j < tmpHediffs.Count; j++)
			{
				if (tmpHediffs[j] != hediff && tmpHediffs[j].def == hediff.def)
				{
					outHediffsToTend.Add(tmpHediffs[j]);
				}
			}
		}
		else if (hediff is Hediff_Injury && usingMedicine)
		{
			float num = hediff.Severity;
			for (int k = 0; k < tmpHediffs.Count; k++)
			{
				if (tmpHediffs[k] == hediff)
					continue;

				if (tmpHediffs[k] is Hediff_Injury hediff_Injury)
				{
					float severity = hediff_Injury.Severity;
					if (num + severity <= 20f)
					{
						num += severity;
						outHediffsToTend.Add(hediff_Injury);
					}
				}
			}
		}

		tmpHediffs.Clear();
	}

	public static int GetMedicineCountToReTend(Pawn pawn)
	{
		int num = 0;
		List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
		for (int i = 0; i < hediffs.Count; i++)
		{
			if (hediffs[i].IsTended() && ((ReTendSettings.diseases && hediffs[i].def.PossibleToDevelopImmunityNaturally()) || hediffs[i] is Hediff_Injury) && !hediffs[i].IsPermanent())
			{
				HediffComp_TendDuration tendDuration = hediffs[i].TryGetComp<HediffComp_TendDuration>();
				if (tendDuration != null)
				{
					if (hediffs[i] is Hediff_Injury)
					{
						if (tendDuration.tendQuality < ReTendSettings.minquality)
							num++;
					}
					else if (tendDuration.tendQuality < ReTendSettings.diseasequality)
						num++;
				}
			}
		}

		return num;
	}

	public static Toil ReserveMedicine(TargetIndex ind, Pawn injured)
	{
#if V1_2 || V1_3
		Toil toil = new();
#else
			Toil toil = ToilMaker.MakeToil("ReserveMedicineToReTend");
#endif
		toil.initAction = delegate
		{
			Pawn actor = toil.actor;
			Job curJob = actor.jobs.curJob;
			Thing thing = curJob.GetTarget(ind).Thing;
			int num = actor.Map.reservationManager.CanReserveStack(actor, thing, 10);
			if (num <= 0 || !actor.Reserve(thing, curJob, 10, Mathf.Min(num, GetMedicineCountToReTend(injured))))
			{
				toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
			}
		};
		toil.defaultCompleteMode = ToilCompleteMode.Instant;
		toil.atomicWithPrevious = true;
		return toil;
	}

	public static Toil PickupMedicine(TargetIndex ind, Pawn injured)
	{
#if V1_2 || V1_3
		Toil toil = new();
#else
			Toil toil = ToilMaker.MakeToil("ReTendPickupMedicine");
#endif
		toil.initAction = delegate
		{
			Pawn actor = toil.actor;
			Job curJob = actor.jobs.curJob;
			Thing thing = curJob.GetTarget(ind).Thing;
			int num = GetMedicineCountToReTend(injured);
			if (actor.carryTracker.CarriedThing != null)
			{
				num -= actor.carryTracker.CarriedThing.stackCount;
			}

			int num2 = Mathf.Min(actor.Map.reservationManager.CanReserveStack(actor, thing, 10), num);
			if (num2 > 0)
			{
				actor.carryTracker.TryStartCarry(thing, num2);
			}

			curJob.count = num - num2;
			if (thing.Spawned)
			{
				toil.actor.Map.reservationManager.Release(thing, actor, curJob);
			}

			curJob.SetTarget(ind, actor.carryTracker.CarriedThing);
		};
		toil.defaultCompleteMode = ToilCompleteMode.Instant;
		return toil;
	}
	public static Thing FindBestMedicineToReTend(Pawn healer, Pawn patient, bool onlyUseInventory = false)
	{
		if (patient.playerSettings != null && (int)patient.playerSettings.medCare <= 1)
			return null;

		if (GetMedicineCountToReTend(patient) <= 0)
			return null;

		bool validator(Thing m)
		{
			bool flag = ((patient.playerSettings == null) ? MedicalCareCategory.NoMeds : patient.playerSettings.medCare).AllowsMedicine(m.def);
			if (patient.playerSettings == null && onlyUseInventory)
			{
				flag = true;
			}

			return !m.IsForbidden(healer) && flag && healer.CanReserve(m, 10, 1);
		}

		Thing thing = GetBestMedInInventory(healer.inventory.innerContainer);
		if (onlyUseInventory)
		{
			return thing;
		}

		Thing thing2 = GenClosest.ClosestThing_Global_Reachable(patient.PositionHeld, patient.MapHeld, patient.MapHeld.listerThings.ThingsInGroup(ThingRequestGroup.Medicine), PathEndMode.ClosestTouch, TraverseParms.For(healer), 9999f, validator, PriorityOf);
		if (thing != null && thing2 != null)
		{
			return !(PriorityOf(thing) >= PriorityOf(thing2))
				? thing2
				: thing;
		}

		IEnumerable<Pawn> SpawnedColonyAnimals =
#if V1_2
			healer.Map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer && p.RaceProps.Animal);
#else
			healer.Map.mapPawns.SpawnedColonyAnimals;
#endif

		if (thing == null && thing2 == null && healer.IsColonist && healer.Map != null)
		{
			Thing thing3 = null;
			foreach (Pawn spawnedColonyAnimal in SpawnedColonyAnimals)
			{
				thing3 = GetBestMedInInventory(spawnedColonyAnimal.inventory.innerContainer);
				if (thing3 != null && (thing2 == null || PriorityOf(thing2) < PriorityOf(thing3)) && !spawnedColonyAnimal.IsForbidden(healer) && healer.CanReach(spawnedColonyAnimal, PathEndMode.OnCell, Danger.Some))
					thing2 = thing3;
			}
		}

		return thing ?? thing2;
		Thing GetBestMedInInventory(ThingOwner inventory)
		{
			return inventory.Count == 0
				? null
				: inventory.Where(t => t.def.IsMedicine && validator(t)).OrderByDescending(PriorityOf).FirstOrDefault();
		}

		float PriorityOf(Thing t) => t.def.GetStatValueAbstract(StatDefOf.MedicalPotency);
	}
}
