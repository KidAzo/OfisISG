using UnityEngine;
using Woi.Localization;

public static class HazardScoreCalculator
{
	public static void GetGrade(int totalScore, Language language, out string letter, out string description)
	{
		if(language == Language.English)
		{
			 if (totalScore >= 90)
			{
				letter = "A";
				description = "Excellent";
			}
			else if (totalScore >= 75)
			{
				letter = "B";
				description = "Good";
			}
			else if (totalScore >= 60)
			{
				letter = "C";
				description = "Average";
			}
			else
			{
				letter = "D";
				description = "Needs Improvement";
			}
			
			return;
		}

		if (totalScore >= 90)
		{
			letter = "A";
			description = "Cok iyi";
		}
		else if (totalScore >= 75)
		{
			letter = "B";
			description = "Iyi";
		}
		else if (totalScore >= 60)
		{
			letter = "C";
			description = "Orta";
		}
		else
		{
			letter = "D";
			description = "Gelistirilmeli";
		}
	}
}
