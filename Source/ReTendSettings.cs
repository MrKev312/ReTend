using Verse;

namespace ReTend;

public class ReTendSettings : ModSettings
{
	public static bool qualityprompt = false;
	public static float minquality = 0.6f;
	public static bool sortmethod = true;
	public static bool diseases = true;
	public static float diseasequality = 0.6f;

	public override void ExposeData()
	{
		Scribe_Values.Look(ref qualityprompt, "ReTendQualityPrompt", false);
		Scribe_Values.Look(ref minquality, "ReTendMinQuality", 0.6f);
		Scribe_Values.Look(ref sortmethod, "ReTendSortMethod", true);
		Scribe_Values.Look(ref diseases, "ReTendDiseases", true);
		Scribe_Values.Look(ref diseasequality, "ReTendDiseaseQuality", 0.6f);
		base.ExposeData();
	}
}
