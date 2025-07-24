using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Reflection;

namespace ReTend;

public class JobDriver_ReTendPatient : JobDriver_TendPatient
{
	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		if (Deliveree != pawn && !pawn.Reserve(Deliveree, job, 1, -1, null, errorOnFailed))
		{
			return false;
		}

		FieldInfo usesmedicine = GetType().BaseType.GetField("usesMedicine", BindingFlags.NonPublic | BindingFlags.Instance);
		if ((bool)usesmedicine.GetValue(this))
		{
			int num = pawn.Map.reservationManager.CanReserveStack(pawn, MedicineUsed, 10);
			if (num <= 0 || !pawn.Reserve(MedicineUsed, job, 10, Mathf.Min(num, ReTendFunctions.GetMedicineCountToReTend(Deliveree)), null, errorOnFailed))
			{
				return false;
			}
		}

		return true;
	}

#if V1_2 || V1_3 || V1_4 || V1_5
	protected override IEnumerable<Toil> MakeNewToils()
#else
    protected override IEnumerable<Toil> MakeNewToils()
#endif
	{
		this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
		this.FailOn(delegate
		{
			return (MedicineUsed != null && pawn.Faction == Faction.OfPlayer && Deliveree.playerSettings != null && !Deliveree.playerSettings.medCare.AllowsMedicine(MedicineUsed.def))
				|| (pawn == Deliveree && pawn.Faction == Faction.OfPlayer && pawn.playerSettings != null && !pawn.playerSettings.selfTend);
		});
		AddEndCondition(delegate
		{
			return pawn.Faction == Faction.OfPlayer && ReTendFunctions.ReTendAvailable(Deliveree)
				? JobCondition.Ongoing
				: ((job.playerForced || pawn.Faction != Faction.OfPlayer) && ReTendFunctions.ReTendAvailable(Deliveree)) ? JobCondition.Ongoing : JobCondition.Succeeded;
		});
		this.FailOnAggroMentalState(TargetIndex.A);
		Toil reserveMedicine = null;
		PathEndMode interactionCell = PathEndMode.None;
		if (Deliveree == pawn)
		{
			interactionCell = PathEndMode.OnCell;
		}
		else if (Deliveree.InBed())
		{
			interactionCell = PathEndMode.InteractionCell;
		}
		else if (Deliveree != pawn)
		{
			interactionCell = PathEndMode.ClosestTouch;
		}

		Toil gotoToil = Toils_Goto.GotoThing(TargetIndex.A, interactionCell);
		FieldInfo usesmedicine = GetType().BaseType.GetField("usesMedicine", BindingFlags.NonPublic | BindingFlags.Instance);
		if ((bool)usesmedicine.GetValue(this))
		{
#if V1_2
			reserveMedicine = ReTendFunctions.ReserveMedicine(TargetIndex.B, Deliveree).FailOnDespawnedNullOrForbidden(TargetIndex.B);
			yield return reserveMedicine;
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
			yield return ReTendFunctions.PickupMedicine(TargetIndex.B, Deliveree).FailOnDestroyedOrNull(TargetIndex.B);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveMedicine, TargetIndex.B, TargetIndex.None, takeFromValidStorage: true);
#else
			reserveMedicine = ReTendFunctions.ReserveMedicine(TargetIndex.B, Deliveree).FailOnDespawnedNullOrForbidden(TargetIndex.B);
			yield return Toils_Jump.JumpIf(gotoToil, () => IsMedicineInDoctorInventory);
			Toil goToMedicineHolder = Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch).FailOn(() => OtherPawnMedicineHolder != MedicineHolderInventory?.pawn || OtherPawnMedicineHolder.IsForbidden(pawn));
			yield return Toils_Haul.CheckItemCarriedByOtherPawn(MedicineUsed, TargetIndex.C, goToMedicineHolder);
			yield return reserveMedicine;
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
			yield return ReTendFunctions.PickupMedicine(TargetIndex.B, Deliveree).FailOnDestroyedOrNull(TargetIndex.B);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveMedicine, TargetIndex.B, TargetIndex.None, takeFromValidStorage: true);
			yield return Toils_Jump.Jump(gotoToil);
			yield return goToMedicineHolder;
			yield return Toils_General.Wait(25).WithProgressBarToilDelay(TargetIndex.C);
			yield return Toils_Haul.TakeFromOtherInventory(MedicineUsed, pawn.inventory.innerContainer, MedicineHolderInventory?.innerContainer, ReTendFunctions.GetMedicineCountToReTend(Deliveree), TargetIndex.B);
#endif
		}

		yield return gotoToil;
		int ticks = (int)(1f / pawn.GetStatValue(StatDefOf.MedicalTendSpeed) * 600f);
		Toil waitToil;
#if !V1_2
		if (!job.draftedTend)
		{
			waitToil = Toils_General.Wait(ticks);
		}
		else
#endif
		{
			waitToil = Toils_General.WaitWith(TargetIndex.A, ticks, useProgressBar: false, maintainPosture: true);
			waitToil.AddFinishAction(delegate
			{
				if (Deliveree != null && Deliveree != pawn && Deliveree.CurJob != null && (Deliveree.CurJob.def == JobDefOf.Wait || Deliveree.CurJob.def == JobDefOf.Wait_MaintainPosture))
				{
					Deliveree.jobs.EndCurrentJob(JobCondition.InterruptForced);
				}
			});
		}

		waitToil.FailOnCannotTouch(TargetIndex.A, interactionCell).WithProgressBarToilDelay(TargetIndex.A).PlaySustainerOrSound(SoundDefOf.Interact_Tend);
		waitToil.activeSkill = () => SkillDefOf.Medicine;
		waitToil.handlingFacing = true;
		waitToil.tickAction = delegate
		{
			if (pawn == Deliveree && pawn.Faction != Faction.OfPlayer && pawn.IsHashIntervalTick(100) && !pawn.Position.Fogged(pawn.Map))
			{
#if V1_2
				MoteMaker.ThrowMetaIcon(pawn.Position, pawn.Map, ThingDefOf.Mote_HealingCross);
#else
				FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.HealingCross);
#endif
			}

			if (pawn != Deliveree)
			{
				pawn.rotationTracker.FaceTarget(Deliveree);
			}
		};
#if !V1_2
		yield return Toils_Jump.JumpIf(waitToil, () => !(bool)usesmedicine.GetValue(this) || !IsMedicineInDoctorInventory);
		yield return ReTendFunctions.PickupMedicine(TargetIndex.B, Deliveree).FailOnDestroyedOrNull(TargetIndex.B);
#endif
		yield return waitToil;
		yield return ReTendFunctions.FinalizeReTend(Deliveree);
		if ((bool)usesmedicine.GetValue(this))
		{
#if V1_2 || V1_3
			Toil toil = new();
#else
			Toil toil = ToilMaker.MakeToil("MakeNewToils");
#endif
			toil.initAction = delegate
			{
				if (MedicineUsed.DestroyedOrNull())
				{
					Thing thing = ReTendFunctions.FindBestMedicineToReTend(pawn, Deliveree);
					if (thing != null)
					{
						job.targetB = thing;
						JumpToToil(reserveMedicine);
					}
				}
			};
			yield return toil;
		}

		yield return Toils_Jump.Jump(gotoToil);
	}
}
