using UnityEngine;

using Verse;

namespace ReTend;

public class ReTendMod : Mod
{
	public ReTendSettings settings;
	public ReTendMod(ModContentPack content) : base(content)
	{
		settings = GetSettings<ReTendSettings>();
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		Rect contentRect = new Rect(0, 0, inRect.width, inRect.height - 10f).ContractedBy(10f);
		Listing_Standard listingStandard = new();
		listingStandard.Begin(inRect);
		listingStandard.CheckboxLabeled("Quality Prompt", ref ReTendSettings.qualityprompt, "Should a prompt for the desired minimum quality be given every time");
		listingStandard.Gap();
		listingStandard.CheckboxLabeled("Disease ReTending", ref ReTendSettings.diseases, "Can disease be ReTended?");
		listingStandard.Gap();
		listingStandard.Label("ReTending Order");
		if (listingStandard.RadioButton("Sort by lowest health", ReTendSettings.sortmethod))
			ReTendSettings.sortmethod = true;
		if (listingStandard.RadioButton("Sort by lowest quality", !ReTendSettings.sortmethod))
			ReTendSettings.sortmethod = false;
		listingStandard.Gap();
		//float offset = listingStandard.Label("Minium Quality: {0}%".Formatted((int)Mathf.Round(ReTendSettings.minquality * 100))).yMax;
		listingStandard.End();
		ReTendSettings.minquality = Widgets.HorizontalSlider(
				new Rect(0f, inRect.y + 182f, contentRect.width, 16f),
				Mathf.Round(ReTendSettings.minquality * 100),
				0f, 100f, false,
				"{0}%".Formatted(ReTendSettings.minquality * 100), "0", "100", 1f
			) / 100;
		Widgets.Label(new Rect(0f, inRect.y + 210f, contentRect.width, 24f), "Minium Disease Quality: {0}%".Formatted((int)Mathf.Round(ReTendSettings.diseasequality * 100)));
		ReTendSettings.diseasequality = Widgets.HorizontalSlider(
				new Rect(0f, inRect.y + 242f, contentRect.width, 20f),
				Mathf.Round(ReTendSettings.diseasequality * 100),
				0f, 100f, false,
				"{0}%".Formatted(ReTendSettings.diseasequality * 100), "0", "100", 1f
			) / 100;
		base.DoSettingsWindowContents(inRect);
	}

	public override string SettingsCategory()
	{
		return "ReTend";
	}
}
